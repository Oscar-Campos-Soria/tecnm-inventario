<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { softwareApi } from '../api'
import { EXPIRING_DAYS, expiry } from '../status'
import type { CatalogItem, LicenseDetail, LicenseSummary, PagedResult, SoftwareItem, SupplierOption } from '../types'
import LicenseDetailModal from './LicenseDetailModal.vue'
import UsageBar from './UsageBar.vue'

const { hasPermission } = useAuth()
const canPickSupplier = computed(() => hasPermission('procurement.manage'))

const result = ref<PagedResult<LicenseSummary> | null>(null)
const page = ref(1)
const search = ref('')
// /software?vencen=1 (enlace del panel) abre con el filtro de vencimiento.
const onlyExpiring = ref(useRoute().query.vencen === '1')
const error = ref('')
const detailId = ref<number | null>(null)

const programs = ref<SoftwareItem[]>([])
const licenseTypes = ref<CatalogItem[]>([])
const suppliers = ref<SupplierOption[]>([])

const open = ref(false)
const editing = ref<LicenseDetail | null>(null)
const saving = ref(false)
const formError = ref('')
const blank = () => ({ softwareId: '', typeId: '', key: '', removeKey: false, quantity: '', from: '', until: '', supplierId: '', notes: '' })
const form = ref(blank())
const ids = { search: useId(), expiring: useId(), software: useId(), type: useId(), key: useId(), removeKey: useId(), quantity: useId(), quantityHelp: useId(), from: useId(), until: useId(), supplier: useId(), notes: useId() }

function load() {
  softwareApi
    .licenses(page.value, search.value.trim(), onlyExpiring.value ? EXPIRING_DAYS : null)
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar las licencias.'))
}
watch(page, load, { immediate: true })

let timer: ReturnType<typeof setTimeout> | undefined
function reload() {
  if (page.value === 1) load()
  else page.value = 1
}
watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(reload, 250)
})
watch(onlyExpiring, reload)
onBeforeUnmount(() => clearTimeout(timer))

async function loadOptions() {
  // shortcut: el selector trae hasta 100 programas (tope de PagedRequest); con más, cambiar a buscador.
  const [p, c] = await Promise.all([softwareApi.software(1, '', 100), softwareApi.catalogs()])
  programs.value = p.items
  licenseTypes.value = c.licenseTypes
  if (canPickSupplier.value) suppliers.value = await softwareApi.suppliers().catch(() => [])
}

async function openForm(id: number | null) {
  formError.value = ''
  try {
    await loadOptions()
    editing.value = id === null ? null : await softwareApi.license(id)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo abrir el formulario.'
    return
  }
  const l = editing.value
  form.value = l
    ? {
        softwareId: String(l.softwareId),
        typeId: String(l.licenseTypeId),
        key: '',
        removeKey: false,
        quantity: l.quantityPurchased === null ? '' : String(l.quantityPurchased),
        from: l.validFrom ?? '',
        until: l.validUntil ?? '',
        supplierId: l.supplierId ? String(l.supplierId) : '',
        notes: l.notes ?? '',
      }
    : blank()
  open.value = true
}

async function submit() {
  const f = form.value
  if (!editing.value && !f.softwareId) return void (formError.value = 'Elige el programa de la licencia.')
  if (!f.typeId) return void (formError.value = 'Elige el tipo de licencia.')
  const quantity = f.quantity.trim() === '' ? null : Number(f.quantity)
  if (quantity !== null && (!Number.isInteger(quantity) || quantity < 0)) {
    return void (formError.value = 'La cantidad debe ser un número entero, o déjala vacía si no tiene límite.')
  }
  if (f.from && f.until && f.until < f.from) return void (formError.value = 'El vencimiento no puede ser anterior al inicio.')

  // Al editar, la clave vacía conserva la guardada; "Quitar clave" manda '' para borrarla.
  const key = f.removeKey ? '' : f.key.trim() || null
  const input = {
    licenseTypeId: Number(f.typeId),
    licenseKey: key,
    quantityPurchased: quantity,
    validFrom: f.from || null,
    validUntil: f.until || null,
    // Sin permiso de proveedores se respeta el que ya tenía la licencia.
    supplierId: canPickSupplier.value ? (f.supplierId ? Number(f.supplierId) : null) : (editing.value?.supplierId ?? null),
    notes: f.notes.trim() || null,
  }
  saving.value = true
  formError.value = ''
  try {
    if (editing.value) await softwareApi.updateLicense(editing.value, input)
    else await softwareApi.createLicense({ ...input, softwareId: Number(f.softwareId) })
    open.value = false
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      open.value = false
      error.value = 'Otra persona modificó esa licencia. Recargamos la lista; intenta de nuevo.'
      load()
    } else formError.value = err instanceof ApiError ? err.message : 'No se pudo guardar la licencia.'
  } finally {
    saving.value = false
  }
}

async function remove(l: LicenseSummary) {
  if (!window.confirm(`¿Eliminar la licencia de ${l.softwareName}?`)) return
  try {
    await softwareApi.deleteLicense(l)
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar la licencia.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const label = 'mb-1 block text-sm font-medium text-foreground'
</script>

<template>
  <div class="space-y-3">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-wrap items-center gap-4">
        <div class="w-full sm:w-72">
          <label :for="ids.search" class="sr-only">Buscar licencia por programa</label>
          <input :id="ids.search" v-model="search" type="search" placeholder="Buscar por programa" :class="field" />
        </div>
        <label :for="ids.expiring" class="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm text-foreground">
          <input :id="ids.expiring" v-model="onlyExpiring" type="checkbox" class="h-4 w-4 accent-accent" />
          Solo las que vencen en {{ EXPIRING_DAYS }} días
        </label>
      </div>
      <UiButton variant="primary" @click="openForm(null)">Nueva licencia</UiButton>
    </div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando licencias…</p>
    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Programa</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Tipo</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Equipos</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Vigencia</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="result.items.length === 0">
            <td colspan="5" class="px-4 py-6 text-center text-muted-foreground">
              <template v-if="onlyExpiring">Ninguna licencia vence en los próximos {{ EXPIRING_DAYS }} días.</template>
              <template v-else-if="search.trim()">Ninguna licencia coincide con la búsqueda.</template>
              <template v-else>Registra una licencia para controlar cuántos equipos la usan.</template>
            </td>
          </tr>
          <tr v-for="l in result.items" :key="l.licenseId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <p class="font-medium text-foreground">{{ l.softwareName }}</p>
              <p v-if="l.softwareVendor || l.supplierName" class="text-xs text-muted-foreground">
                {{ [l.softwareVendor, l.supplierName ? `Proveedor: ${l.supplierName}` : null].filter(Boolean).join(', ') }}
              </p>
            </td>
            <td class="px-4 py-3 text-secondary">{{ l.licenseTypeName }}</td>
            <td class="px-4 py-3"><UsageBar :license="l" /></td>
            <td class="px-4 py-3">
              <span :class="['rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap', expiry(l).cls]">{{ expiry(l).text }}</span>
            </td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-2">
                <UiButton @click="detailId = l.licenseId">Equipos</UiButton>
                <UiButton @click="openForm(l.licenseId)">Editar</UiButton>
                <UiButton variant="danger" @click="remove(l)">Eliminar</UiButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <nav v-if="result && result.totalPages > 1" class="flex justify-end gap-2" aria-label="Paginación de licencias">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
    </nav>

    <LicenseDetailModal :license-id="detailId" @close="detailId = null" @changed="load" />

    <UiModal :open="open" :title="editing ? `Editar licencia de ${editing.softwareName}` : 'Nueva licencia'" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div v-if="!editing">
          <label :for="ids.software" :class="label">Programa</label>
          <select :id="ids.software" v-model="form.softwareId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="" disabled>Elige un programa</option>
            <option v-for="p in programs" :key="p.softwareId" :value="String(p.softwareId)">{{ p.vendor ? `${p.name} (${p.vendor})` : p.name }}</option>
          </select>
          <p v-if="programs.length === 0" class="mt-1 text-xs text-muted-foreground">Primero registra el programa en la pestaña Programas.</p>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.type" :class="label">Tipo de licencia</label>
            <select :id="ids.type" v-model="form.typeId" :class="[field, 'cursor-pointer px-2.5']">
              <option value="" disabled>Elige el tipo</option>
              <option v-for="t in licenseTypes" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </div>
          <div>
            <label :for="ids.quantity" :class="label">Equipos permitidos</label>
            <input :id="ids.quantity" v-model="form.quantity" inputmode="numeric" placeholder="Sin límite" :aria-describedby="ids.quantityHelp" :class="field" />
            <p :id="ids.quantityHelp" class="mt-1 text-xs text-muted-foreground">Vacío si no tiene límite (gratuita o de sitio).</p>
          </div>
          <div>
            <label :for="ids.from" :class="label">Vigente desde</label>
            <input :id="ids.from" v-model="form.from" type="date" :class="field" />
          </div>
          <div>
            <label :for="ids.until" :class="label">Vence</label>
            <input :id="ids.until" v-model="form.until" type="date" :class="field" />
          </div>
        </div>
        <div>
          <label :for="ids.key" :class="label">Clave de licencia (opcional)</label>
          <input
            :id="ids.key"
            v-model="form.key"
            type="password"
            autocomplete="off"
            :disabled="form.removeKey"
            :placeholder="editing?.maskedLicenseKey ? `Guardada: ${editing.maskedLicenseKey}. Escribe una nueva para cambiarla` : 'XXXXX-XXXXX-XXXXX'"
            :class="field"
          />
          <label v-if="editing?.maskedLicenseKey" :for="ids.removeKey" class="mt-1.5 flex cursor-pointer items-center gap-2 text-sm text-secondary">
            <input :id="ids.removeKey" v-model="form.removeKey" type="checkbox" class="h-4 w-4 accent-accent" />
            Quitar la clave guardada
          </label>
        </div>
        <div v-if="canPickSupplier">
          <label :for="ids.supplier" :class="label">Proveedor (opcional)</label>
          <select :id="ids.supplier" v-model="form.supplierId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="">Sin proveedor</option>
            <option v-for="s in suppliers" :key="s.supplierId" :value="String(s.supplierId)">{{ s.name }}</option>
          </select>
        </div>
        <div>
          <label :for="ids.notes" :class="label">Notas</label>
          <textarea :id="ids.notes" v-model="form.notes" rows="2" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Guardar licencia' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
