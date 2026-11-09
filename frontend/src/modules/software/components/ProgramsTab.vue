<script setup lang="ts">
import { onBeforeUnmount, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { softwareApi } from '../api'
import type { CatalogItem, PagedResult, SoftwareItem } from '../types'

const result = ref<PagedResult<SoftwareItem> | null>(null)
const page = ref(1)
const search = ref('')
const error = ref('')
const types = ref<CatalogItem[]>([])

const open = ref(false)
const editing = ref<SoftwareItem | null>(null)
const saving = ref(false)
const formError = ref('')
const form = ref({ name: '', vendor: '', typeId: '' })
const ids = { search: useId(), name: useId(), vendor: useId(), type: useId() }

function load() {
  softwareApi
    .software(page.value, search.value.trim())
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los programas.'))
}
watch(page, load, { immediate: true })

let timer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(() => (page.value === 1 ? load() : (page.value = 1)), 250)
})
onBeforeUnmount(() => clearTimeout(timer))

softwareApi.catalogs().then((c) => (types.value = c.softwareTypes)).catch(() => {})

function openForm(s: SoftwareItem | null) {
  editing.value = s
  form.value = s ? { name: s.name, vendor: s.vendor ?? '', typeId: s.softwareTypeId ? String(s.softwareTypeId) : '' } : { name: '', vendor: '', typeId: '' }
  formError.value = ''
  open.value = true
}

async function submit() {
  const f = form.value
  if (!f.name.trim()) return void (formError.value = 'Escribe el nombre del programa.')
  const input = { name: f.name.trim(), vendor: f.vendor.trim() || null, softwareTypeId: f.typeId ? Number(f.typeId) : null }
  saving.value = true
  formError.value = ''
  try {
    if (editing.value) await softwareApi.updateSoftware(editing.value, input)
    else await softwareApi.createSoftware(input)
    open.value = false
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      open.value = false
      error.value = 'Otra persona modificó ese programa. Recargamos la lista; intenta de nuevo.'
      load()
    } else formError.value = err instanceof ApiError ? err.message : 'No se pudo guardar el programa.'
  } finally {
    saving.value = false
  }
}

async function remove(s: SoftwareItem) {
  if (!window.confirm(`¿Eliminar ${s.name}?`)) return
  try {
    await softwareApi.deleteSoftware(s)
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar el programa.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const label = 'mb-1 block text-sm font-medium text-foreground'
</script>

<template>
  <div class="space-y-3">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="w-full sm:w-72">
        <label :for="ids.search" class="sr-only">Buscar programa</label>
        <input :id="ids.search" v-model="search" type="search" placeholder="Buscar por nombre o fabricante" :class="field" />
      </div>
      <UiButton variant="primary" @click="openForm(null)">Nuevo programa</UiButton>
    </div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando programas…</p>
    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Programa</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Tipo</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Licencias</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Instalado en</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="result.items.length === 0">
            <td colspan="5" class="px-4 py-6 text-center text-muted-foreground">
              {{ search.trim() ? 'Ningún programa coincide con la búsqueda.' : 'Registra el primer programa para poder cargarle licencias.' }}
            </td>
          </tr>
          <tr v-for="s in result.items" :key="s.softwareId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <p class="font-medium text-foreground">{{ s.name }}</p>
              <p v-if="s.vendor" class="text-xs text-muted-foreground">{{ s.vendor }}</p>
            </td>
            <td class="px-4 py-3 text-secondary">{{ s.softwareTypeName ?? '—' }}</td>
            <td class="px-4 py-3 text-right text-secondary">{{ s.licenseCount }}</td>
            <td class="px-4 py-3 text-right text-secondary">{{ s.installCount === 1 ? '1 equipo' : `${s.installCount} equipos` }}</td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-2">
                <UiButton @click="openForm(s)">Editar</UiButton>
                <UiButton variant="danger" @click="remove(s)">Eliminar</UiButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <nav v-if="result && result.totalPages > 1" class="flex justify-end gap-2" aria-label="Paginación de programas">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
    </nav>

    <UiModal :open="open" :title="editing ? `Editar ${editing.name}` : 'Nuevo programa'" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="ids.name" :class="label">Nombre</label>
          <input :id="ids.name" v-model="form.name" type="text" maxlength="150" placeholder="Office 2021" :class="field" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.vendor" :class="label">Fabricante (opcional)</label>
            <input :id="ids.vendor" v-model="form.vendor" type="text" maxlength="150" placeholder="Microsoft" :class="field" />
          </div>
          <div>
            <label :for="ids.type" :class="label">Tipo (opcional)</label>
            <select :id="ids.type" v-model="form.typeId" :class="[field, 'cursor-pointer px-2.5']">
              <option value="">Sin tipo</option>
              <option v-for="t in types" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </div>
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Guardar programa' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
