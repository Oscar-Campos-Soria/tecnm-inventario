<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import DocumentList from '../../../shared/components/DocumentList.vue'
import IncidentActions from '../components/IncidentActions.vue'
import UiField from '../../../shared/components/UiField.vue'
import { formatDateTime } from '../../../shared/utils/format'
import { incidentsApi } from '../api'
import StatusChip from '../components/StatusChip.vue'
import type { IncidentDetail } from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

// Incidentes "vivos": mientras uno de estos esté abierto el activo no se
// libera (IncidentStatusCodes.Open).
const OPEN_STATUSES = ['ABIERTO', 'EN_DIAGNOSTICO', 'EN_REPARACION']

const ASSET_STATUS_LABELS: Record<string, string> = {
  DISPONIBLE: 'Disponible',
  ASIGNADO: 'Asignado',
  EN_MANTENIMIENTO: 'En mantenimiento',
  CON_FALLA: 'Con falla',
  DADO_DE_BAJA: 'Dado de baja',
}

function assetStatusClass(statusCode: string): string {
  switch (statusCode) {
    case 'CON_FALLA':
      return 'bg-destructive/10 text-destructive'
    case 'DISPONIBLE':
      return 'bg-success/10 text-success'
    case 'EN_MANTENIMIENTO':
    case 'ASIGNADO':
      return 'bg-muted text-secondary'
    default:
      return 'bg-muted text-muted-foreground'
  }
}

const route = useRoute()
const numericId = computed(() => Number(route.params.incidentId))
const isValidId = computed(() => Number.isInteger(numericId.value))

const state = ref<LoadState>('loading')
const detail = ref<IncidentDetail | null>(null)
const notice = ref('')
const pageError = ref('')

// Recarga tras una acción del técnico; con 409 avisa que otra persona lo cambió.
function reload(message?: string) {
  pageError.value = ''
  notice.value = message ?? ''
  incidentsApi
    .getById(numericId.value)
    .then((incident) => (detail.value = incident))
    .catch(() => (pageError.value = 'No se pudo recargar el incidente. Recarga la página.'))
}

function onConflict() {
  reload()
  pageError.value = 'Otra persona modificó este incidente. Recargamos los datos; revísalos e intenta de nuevo.'
}

watch(
  numericId,
  () => {
    if (!isValidId.value) return
    state.value = 'loading'
    incidentsApi
      .getById(numericId.value)
      .then((incident) => {
        detail.value = incident
        state.value = 'ready'
      })
      .catch((err) => {
        state.value = err instanceof ApiError && err.status === 404 ? 'not-found' : 'error'
      })
  },
  { immediate: true },
)

// Efecto del incidente sobre el estado del activo: mientras haya un incidente
// abierto el activo no se libera; al resolver el último vuelve a circulación.
const assetNote = computed(() => {
  if (!detail.value) return ''
  const open = OPEN_STATUSES.includes(detail.value.statusCode)
  const assetOut = detail.value.assetStatusCode === 'CON_FALLA' || detail.value.assetStatusCode === 'EN_MANTENIMIENTO'
  if (open && assetOut) return 'El activo está fuera de circulación mientras este incidente esté abierto.'
  if (!open && assetOut) return 'El activo sigue fuera de circulación: hay otros incidentes abiertos sobre él.'
  if (!open && !assetOut) return 'El activo volvió a circulación tras la resolución de este incidente.'
  return ''
})
</script>

<template>
  <p v-if="isValidId && state === 'loading'" class="text-sm text-muted-foreground">Cargando incidente…</p>

  <div v-else-if="!isValidId || state === 'not-found'" class="space-y-3">
    <p class="font-medium text-foreground">Incidente no encontrado</p>
    <RouterLink to="/incidents" class="text-sm text-primary underline-offset-2 hover:underline">Volver a incidentes</RouterLink>
  </div>

  <p v-else-if="state === 'error' || !detail" role="alert" class="text-sm text-destructive">No se pudo cargar el incidente.</p>

  <div v-else class="space-y-6">
    <header class="space-y-1">
      <RouterLink to="/incidents" class="text-sm text-secondary underline-offset-2 hover:underline">Incidentes</RouterLink>
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="font-display text-2xl font-semibold text-foreground">{{ detail.title }}</h1>
        <StatusChip :status-code="detail.statusCode" :status-name="detail.statusName" />
        <span class="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">{{ detail.priorityName }}</span>
        <span class="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">{{ detail.typeName }}</span>
      </div>
    </header>

    <p v-if="notice" role="status" class="rounded-md bg-success/10 px-3 py-2 text-sm text-success">{{ notice }}</p>
    <p v-if="pageError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ pageError }}</p>

    <section aria-labelledby="activo-afectado" class="rounded-lg border border-border bg-card p-5">
      <h2 id="activo-afectado" class="mb-3 font-display text-base font-semibold text-foreground">Activo afectado</h2>
      <div class="flex flex-wrap items-center gap-3">
        <RouterLink :to="`/assets/${detail.assetId}`" class="font-medium text-primary underline-offset-2 hover:underline">
          {{ detail.assetInventoryCode }}
        </RouterLink>
        <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', assetStatusClass(detail.assetStatusCode)]">
          {{ ASSET_STATUS_LABELS[detail.assetStatusCode] ?? detail.assetStatusCode }}
        </span>
      </div>
      <p v-if="assetNote" class="mt-2 text-sm text-muted-foreground">{{ assetNote }}</p>
    </section>

    <section aria-labelledby="detalles" class="rounded-lg border border-border bg-card p-5">
      <h2 id="detalles" class="mb-3 font-display text-base font-semibold text-foreground">Detalles</h2>
      <dl class="grid gap-x-8 gap-y-3 sm:grid-cols-2">
        <UiField label="Descripción" :value="detail.description" multiline />
      </dl>
    </section>

    <section aria-labelledby="seguimiento" class="rounded-lg border border-border bg-card p-5">
      <h2 id="seguimiento" class="mb-3 font-display text-base font-semibold text-foreground">Seguimiento</h2>
      <dl class="grid gap-x-8 gap-y-3 sm:grid-cols-2">
        <UiField label="Reportado por" :value="`${detail.reportedByName} (${detail.reportedByUsername})`" multiline />
        <UiField label="Fecha de reporte" :value="formatDateTime(detail.reportedAt)" multiline />
        <UiField
          label="Asignado a"
          :value="detail.assignedToName ? `${detail.assignedToName} (${detail.assignedToUsername})` : 'Sin asignar'"
          multiline
        />
        <UiField label="Fecha de resolución" :value="formatDateTime(detail.resolvedAt)" multiline />
        <UiField label="Diagnóstico" :value="detail.diagnosis" multiline />
        <UiField label="Solución" :value="detail.solution" multiline />
      </dl>
    </section>

    <IncidentActions :incident="detail" @changed="reload" @conflict="onConflict" />

    <DocumentList owner-type="INCIDENT" :owner-id="detail.incidentId" can-delete />
  </div>
</template>
