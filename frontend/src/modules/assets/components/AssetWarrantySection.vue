<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { SupplierOption, WarrantyItem } from '../types'

// Garantías del activo (procurement.manage, solo ADMIN).
const props = defineProps<{ assetId: number }>()

const { hasPermission } = useAuth()
const canManage = computed(() => hasPermission('procurement.manage'))
const ids = { supplier: useId(), number: useId(), starts: useId(), ends: useId(), coverage: useId() }

const items = ref<WarrantyItem[] | null>(null)
const suppliers = ref<SupplierOption[]>([])
const listError = ref('')
const open = ref(false)
const saving = ref(false)
const error = ref('')
const form = ref({ supplierId: '', number: '', startsOn: '', endsOn: '', coverage: '' })

function load() {
  assetsApi
    .warranties(props.assetId)
    .then((w) => {
      listError.value = ''
      items.value = w
    })
    .catch((err) => (listError.value = err instanceof ApiError ? err.message : 'No se pudieron cargar las garantías.'))
}
watch(() => [props.assetId, canManage.value], () => canManage.value && load(), { immediate: true })

const today = todayIso()
function isActive(w: WarrantyItem) {
  return !w.endsOn || w.endsOn >= today
}
function supplierName(id: number | null) {
  return suppliers.value.find((s) => s.supplierId === id)?.name ?? null
}
watch(canManage, (can) => can && assetsApi.suppliers().then((s) => (suppliers.value = s)).catch(() => {}), { immediate: true })

function openForm() {
  form.value = { supplierId: '', number: '', startsOn: today, endsOn: '', coverage: '' }
  error.value = ''
  open.value = true
}

async function submit() {
  const f = form.value
  if (f.startsOn && f.endsOn && f.endsOn < f.startsOn) {
    error.value = 'La fecha de fin no puede ser anterior a la de inicio.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    await assetsApi.addWarranty({
      assetId: props.assetId,
      supplierId: f.supplierId ? Number(f.supplierId) : null,
      warrantyNumber: f.number.trim() || null,
      startsOn: f.startsOn || null,
      endsOn: f.endsOn || null,
      coverage: f.coverage.trim() || null,
      terms: null,
    })
    open.value = false
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo registrar la garantía.'
  } finally {
    saving.value = false
  }
}

async function remove(w: WarrantyItem) {
  if (!window.confirm('¿Eliminar esta garantía?')) return
  try {
    await assetsApi.removeWarranty(w.warrantyId, w.version)
    load()
  } catch (err) {
    listError.value = err instanceof ApiError ? err.message : 'No se pudo eliminar la garantía.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <section v-if="canManage" aria-labelledby="garantias" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="garantias" class="font-display text-base font-semibold text-foreground">Garantías</h2>
      <UiButton @click="openForm">Registrar garantía</UiButton>
    </div>
    <p v-if="listError" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ listError }}</p>
    <p v-if="!items && !listError" class="text-sm text-muted-foreground">Cargando garantías…</p>
    <p v-else-if="items && items.length === 0" class="text-sm text-muted-foreground">Sin garantías registradas.</p>
    <ul v-else-if="items" class="space-y-2">
      <li v-for="w in items" :key="w.warrantyId" class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2.5 text-sm">
        <div>
          <p class="font-medium text-foreground">
            {{ w.warrantyNumber ?? 'Sin número' }}<template v-if="supplierName(w.supplierId)">, {{ supplierName(w.supplierId) }}</template>
            <span :class="['ml-2 rounded-full px-2 py-0.5 text-xs font-medium', isActive(w) ? 'bg-success/10 text-success' : 'bg-muted text-muted-foreground']">{{ isActive(w) ? 'Vigente' : 'Vencida' }}</span>
          </p>
          <p class="text-xs text-muted-foreground">{{ formatDate(w.startsOn) }} a {{ w.endsOn ? formatDate(w.endsOn) : 'sin fecha de fin' }}<template v-if="w.coverage">, {{ w.coverage }}</template></p>
        </div>
        <UiButton variant="danger" @click="remove(w)">Eliminar</UiButton>
      </li>
    </ul>

    <UiModal :open="open" title="Registrar garantía" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="ids.supplier" class="mb-1 block text-sm font-medium text-foreground">Proveedor (opcional)</label>
          <select :id="ids.supplier" v-model="form.supplierId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="">Sin proveedor</option>
            <option v-for="s in suppliers" :key="s.supplierId" :value="String(s.supplierId)">{{ s.name }}</option>
          </select>
        </div>
        <div>
          <label :for="ids.number" class="mb-1 block text-sm font-medium text-foreground">Número de garantía (opcional)</label>
          <input :id="ids.number" v-model="form.number" type="text" maxlength="100" :class="field" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.starts" class="mb-1 block text-sm font-medium text-foreground">Inicio</label>
            <input :id="ids.starts" v-model="form.startsOn" type="date" :class="field" />
          </div>
          <div>
            <label :for="ids.ends" class="mb-1 block text-sm font-medium text-foreground">Fin</label>
            <input :id="ids.ends" v-model="form.endsOn" type="date" :class="field" />
          </div>
        </div>
        <div>
          <label :for="ids.coverage" class="mb-1 block text-sm font-medium text-foreground">Cobertura (opcional)</label>
          <input :id="ids.coverage" v-model="form.coverage" type="text" maxlength="255" :class="field" placeholder="Partes y mano de obra" />
        </div>
        <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Registrar' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
