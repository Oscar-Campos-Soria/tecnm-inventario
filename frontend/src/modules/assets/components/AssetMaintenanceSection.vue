<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { AssetDetail, AssetIncidentOption, GetCatalogsResponse, MaintenanceItem, PagedResult } from '../types'

// Bitácora de mantenimiento del activo (maintenance.manage). No cambia el
// estado del activo; un correctivo puede ligarse a su incidente.
const props = defineProps<{
  asset: AssetDetail
  catalogs: GetCatalogsResponse | null
}>()

const { hasPermission } = useAuth()
const canManage = computed(() => hasPermission('maintenance.manage'))
const isDisposed = computed(() => props.asset.statusCode === 'DADO_DE_BAJA')

const typeFieldId = useId()
const dateFieldId = useId()
const nextFieldId = useId()
const descriptionFieldId = useId()
const partsFieldId = useId()
const costFieldId = useId()
const incidentFieldId = useId()

const history = ref<PagedResult<MaintenanceItem> | null>(null)
const page = ref(1)
const incidents = ref<AssetIncidentOption[]>([])
const open = ref(false)
const saving = ref(false)
const error = ref('')
const listError = ref('')
const form = ref({ typeId: '', performedAt: '', nextAt: '', description: '', parts: '', cost: '', incidentId: '' })

const isCorrective = computed(
  () => props.catalogs?.maintenanceTypes.find((t) => String(t.id) === form.value.typeId)?.code === 'CORRECTIVO',
)

function load() {
  assetsApi
    .maintenance(props.asset.assetId, page.value)
    .then((h) => {
      listError.value = ''
      history.value = h
    })
    .catch((err) => (listError.value = err instanceof ApiError ? err.message : 'No se pudo cargar el historial.'))
}

watch([() => props.asset.assetId, page], () => canManage.value && load(), { immediate: true })

// "2026-11-08" del input -> mediodía local en ISO (evita que cambie de día al pasar a UTC).
function toIso(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(y, m - 1, d, 12).toISOString()
}

function openForm() {
  form.value = { typeId: '', performedAt: todayIso(), nextAt: '', description: '', parts: '', cost: '', incidentId: '' }
  error.value = ''
  open.value = true
  if (hasPermission('incidents.view')) {
    assetsApi.incidents(props.asset.assetId).then((list) => (incidents.value = list)).catch(() => (incidents.value = []))
  }
}

async function submit() {
  error.value = ''
  const f = form.value
  if (!f.typeId || !f.performedAt || !f.description.trim()) {
    error.value = 'Tipo, fecha y descripción son obligatorios.'
    return
  }
  saving.value = true
  try {
    await assetsApi.addMaintenance({
      assetId: props.asset.assetId,
      maintenanceTypeId: Number(f.typeId),
      performedAt: toIso(f.performedAt),
      nextMaintenanceAt: f.nextAt ? toIso(f.nextAt) : undefined,
      description: f.description.trim(),
      partsUsed: f.parts.trim() || undefined,
      cost: f.cost ? Number(f.cost) : undefined,
      incidentId: isCorrective.value && f.incidentId ? Number(f.incidentId) : undefined,
    })
    open.value = false
    page.value = 1
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo registrar el mantenimiento.'
  } finally {
    saving.value = false
  }
}

async function remove(item: MaintenanceItem) {
  if (!window.confirm('¿Eliminar este registro? Úsalo solo si se capturó por error.')) return
  try {
    await assetsApi.removeMaintenance(item.maintenanceRecordId, item.version)
    load()
  } catch (err) {
    listError.value = err instanceof ApiError ? err.message : 'No se pudo eliminar el registro.'
    load()
  }
}

const money = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' })
const fieldClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <section v-if="canManage" aria-labelledby="mantenimiento" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="mantenimiento" class="font-display text-base font-semibold text-foreground">Mantenimiento</h2>
      <UiButton v-if="!isDisposed" @click="openForm">Registrar mantenimiento</UiButton>
    </div>

    <p v-if="listError" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ listError }}</p>
    <p v-if="!history && !listError" class="text-sm text-muted-foreground">Cargando historial…</p>
    <p v-else-if="history && history.items.length === 0" class="text-sm text-muted-foreground">Sin mantenimientos registrados.</p>

    <ul v-else-if="history" class="space-y-2">
      <li v-for="item in history.items" :key="item.maintenanceRecordId" class="rounded-md border border-border px-3 py-2.5 text-sm">
        <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
          <span class="font-medium text-foreground">{{ formatDate(item.performedAt) }}</span>
          <span :class="['rounded-full px-2 py-0.5 text-xs font-medium', item.typeCode === 'CORRECTIVO' ? 'bg-destructive/10 text-destructive' : 'bg-accent/10 text-accent']">
            {{ item.typeName }}
          </span>
          <span class="text-secondary">{{ item.performedByName ?? '—' }}</span>
          <span v-if="item.cost !== null" class="text-secondary">{{ money.format(item.cost) }}</span>
          <RouterLink v-if="item.incidentId && hasPermission('incidents.view')" :to="`/incidents/${item.incidentId}`" class="text-primary underline-offset-2 hover:underline">
            Incidente #{{ item.incidentId }}
          </RouterLink>
          <button
            type="button"
            class="ml-auto min-h-[44px] cursor-pointer rounded-md px-2 text-xs text-muted-foreground underline-offset-2 hover:text-destructive hover:underline"
            @click="remove(item)"
          >
            Eliminar
          </button>
        </div>
        <p class="mt-1 whitespace-pre-wrap text-foreground">{{ item.description }}</p>
        <p v-if="item.partsUsed" class="text-xs text-muted-foreground">Piezas: {{ item.partsUsed }}</p>
        <p v-if="item.nextMaintenanceAt" class="text-xs text-muted-foreground">Próximo: {{ formatDate(item.nextMaintenanceAt) }}</p>
      </li>
    </ul>

    <nav v-if="history && history.totalPages > 1" class="mt-3 flex items-center gap-2" aria-label="Paginación de mantenimientos">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <span class="text-sm text-muted-foreground">Página {{ history.pageNumber }} de {{ history.totalPages }}</span>
      <UiButton :disabled="page >= history.totalPages" @click="page++">Siguiente</UiButton>
    </nav>

    <UiModal :open="open" title="Registrar mantenimiento" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="typeFieldId" class="mb-1 block text-sm font-medium text-foreground">Tipo</label>
            <select :id="typeFieldId" v-model="form.typeId" :class="[fieldClass, 'cursor-pointer px-2.5']">
              <option value="">Selecciona</option>
              <option v-for="t in catalogs?.maintenanceTypes ?? []" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </div>
          <div>
            <label :for="dateFieldId" class="mb-1 block text-sm font-medium text-foreground">Fecha en que se hizo</label>
            <input :id="dateFieldId" v-model="form.performedAt" type="date" :max="todayIso()" :class="fieldClass" />
          </div>
        </div>
        <div>
          <label :for="descriptionFieldId" class="mb-1 block text-sm font-medium text-foreground">Qué se hizo</label>
          <textarea :id="descriptionFieldId" v-model="form.description" rows="3" maxlength="4000" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="partsFieldId" class="mb-1 block text-sm font-medium text-foreground">Piezas usadas <span class="font-normal text-muted-foreground">(opcional)</span></label>
            <input :id="partsFieldId" v-model="form.parts" type="text" maxlength="2000" :class="fieldClass" />
          </div>
          <div>
            <label :for="costFieldId" class="mb-1 block text-sm font-medium text-foreground">Costo MXN <span class="font-normal text-muted-foreground">(opcional)</span></label>
            <input :id="costFieldId" v-model="form.cost" type="number" min="0" step="0.01" inputmode="decimal" :class="fieldClass" />
          </div>
        </div>
        <div>
          <label :for="nextFieldId" class="mb-1 block text-sm font-medium text-foreground">Próximo mantenimiento <span class="font-normal text-muted-foreground">(opcional)</span></label>
          <input :id="nextFieldId" v-model="form.nextAt" type="date" :min="form.performedAt" :class="fieldClass" />
        </div>
        <div v-if="isCorrective && incidents.length > 0">
          <label :for="incidentFieldId" class="mb-1 block text-sm font-medium text-foreground">Incidente que lo originó <span class="font-normal text-muted-foreground">(opcional)</span></label>
          <select :id="incidentFieldId" v-model="form.incidentId" :class="[fieldClass, 'cursor-pointer px-2.5']">
            <option value="">Ninguno</option>
            <option v-for="i in incidents" :key="i.incidentId" :value="String(i.incidentId)">#{{ i.incidentId }} {{ i.title }} ({{ i.statusName }})</option>
          </select>
          <p class="mt-1 text-xs text-muted-foreground">Registrar el correctivo no resuelve el incidente; resuélvelo desde su ficha.</p>
        </div>
        <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Guardar' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
