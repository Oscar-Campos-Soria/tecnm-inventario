<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useId, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../../shared/utils/format'
import { inventoryApi } from '../api'
import CameraScanner from '../components/CameraScanner.vue'
import { RECORD_CLASSES, RECORD_LABELS, SESSION_LABELS } from '../labels'
import type { InventoryCatalogs, InventoryRecord, LocationOption, RecordStatus, SessionDetail } from '../types'

const route = useRoute()
const { hasPermission } = useAuth()
const codeId = useId()
const foundAtId = useId()

const sessionId = computed(() => Number(route.params.sessionId))
const session = ref<SessionDetail | null>(null)
const state = ref<'loading' | 'ready' | 'not-found' | 'error'>('loading')
const locations = ref<LocationOption[]>([])
const catalogs = ref<InventoryCatalogs | null>(null)
const code = ref('')
const foundAt = ref('')
const scanning = ref(false)
const feedback = ref<{ kind: 'ok' | 'warn' | 'error'; text: string } | null>(null)
const busyRecord = ref<number | null>(null)
const pageError = ref('')
const filter = ref<RecordStatus | 'TODOS'>('TODOS')
const codeInput = ref<HTMLInputElement | null>(null)

const isOpen = computed(() => session.value?.status === 'abierta')
const ORDER: RecordStatus[] = ['PENDIENTE', 'EN_OTRO_LUGAR', 'SOBRANTE', 'NO_ENCONTRADO', 'ENCONTRADO']

async function load() {
  session.value = await inventoryApi.get(sessionId.value)
}

watch(
  sessionId,
  () => {
    state.value = 'loading'
    load()
      .then(() => {
        state.value = 'ready'
        if (!foundAt.value && session.value?.locationId) foundAt.value = String(session.value.locationId)
      })
      .catch((err) => (state.value = err instanceof ApiError && err.status === 404 ? 'not-found' : 'error'))
  },
  { immediate: true },
)

onMounted(() => {
  inventoryApi.locations().then((l) => (locations.value = l)).catch(() => (locations.value = []))
  inventoryApi.catalogs().then((c) => (catalogs.value = c)).catch(() => (catalogs.value = null))
})

const records = computed(() => {
  const list = session.value?.records ?? []
  const filtered = filter.value === 'TODOS' ? list : list.filter((r) => r.status === filter.value)
  return [...filtered].sort((a, b) => ORDER.indexOf(a.status) - ORDER.indexOf(b.status))
})
const total = computed(() => session.value?.records.length ?? 0)
const checked = computed(() => session.value?.records.filter((r) => r.status !== 'PENDIENTE').length ?? 0)
const toReconcile = computed(
  () => session.value?.records.filter((r) => ['EN_OTRO_LUGAR', 'SOBRANTE', 'NO_ENCONTRADO'].includes(r.status) && !r.adjustment).length ?? 0,
)

// ---------- Conteo abierto ----------

async function scan(value?: string) {
  const inventoryCode = (value ?? code.value).trim()
  if (!inventoryCode || !session.value) return
  scanning.value = true
  feedback.value = null
  try {
    const r = await inventoryApi.scan(session.value.inventorySessionId, inventoryCode, foundAt.value ? Number(foundAt.value) : undefined)
    if (r.alreadyChecked) feedback.value = { kind: 'warn', text: `${r.inventoryCode} ya estaba registrado.` }
    else if (r.assetStatusCode === 'DADO_DE_BAJA') feedback.value = { kind: 'warn', text: `${r.inventoryCode} está dado de baja y apareció aquí: debió retirarse.` }
    else if (r.status === 'SOBRANTE') feedback.value = { kind: 'warn', text: `${r.inventoryCode} no estaba en la lista de esta área (sobrante).` }
    else if (r.status === 'EN_OTRO_LUGAR') feedback.value = { kind: 'warn', text: `${r.inventoryCode} encontrado en otro lugar.` }
    else feedback.value = { kind: 'ok', text: `${r.inventoryCode} en su lugar.` }
    code.value = ''
    await load()
  } catch (err) {
    feedback.value = { kind: 'error', text: err instanceof ApiError ? err.message : 'No se pudo registrar el código.' }
  } finally {
    scanning.value = false
    // Listo para el siguiente escaneo (el lector USB escribe en el campo con foco).
    await nextTick()
    codeInput.value?.focus()
  }
}

async function markNotFound(r: InventoryRecord) {
  if (!session.value) return
  busyRecord.value = r.inventoryRecordId
  try {
    await inventoryApi.markNotFound(session.value.inventorySessionId, r.inventoryRecordId, r.version)
    await load()
  } catch (err) {
    pageError.value = err instanceof ApiError ? err.message : 'No se pudo marcar.'
    await load()
  } finally {
    busyRecord.value = null
  }
}

async function close() {
  if (!session.value) return
  const pending = total.value - checked.value
  const msg = pending > 0
    ? `Quedan ${pending} equipo(s) sin revisar; contarán como no encontrados. ¿Cerrar el conteo?`
    : '¿Cerrar el conteo? Ya no se podrán escanear equipos.'
  if (!window.confirm(msg)) return
  pageError.value = ''
  try {
    await inventoryApi.close(session.value.inventorySessionId, session.value.version)
    filter.value = 'TODOS'
    await load()
  } catch (err) {
    pageError.value = err instanceof ApiError && err.status === 409
      ? 'Otra persona modificó el conteo. Recargamos los datos.'
      : err instanceof ApiError ? err.message : 'No se pudo cerrar el conteo.'
    await load()
  }
}

async function cancel() {
  if (!session.value) return
  const reason = window.prompt('¿Por qué se cancela el conteo? No se cambia ningún equipo y la ubicación queda libre para un conteo nuevo.')
  if (reason === null) return
  if (reason.trim().length < 5) {
    pageError.value = 'Para cancelar el conteo escribe el motivo (al menos 5 caracteres).'
    return
  }
  pageError.value = ''
  try {
    await inventoryApi.cancel(session.value.inventorySessionId, session.value.version, reason.trim())
    filter.value = 'TODOS'
  } catch (err) {
    pageError.value = err instanceof ApiError && err.status === 409
      ? 'Otra persona modificó el conteo. Recargamos los datos.'
      : err instanceof ApiError ? err.message : 'No se pudo cancelar el conteo.'
  }
  await load()
}

// ---------- Conciliación (conteo cerrado) ----------

const ajusteReasonId = computed(() => catalogs.value?.movementReasons.find((m) => m.code === 'AJUSTE_INVENTARIO')?.id)
const missingTypeId = computed(() => catalogs.value?.incidentTypes.find((t) => t.code === 'ROBO_EXTRAVIO')?.id)
const highPriorityId = computed(() => catalogs.value?.incidentPriorities.find((p) => p.code === 'ALTA')?.id)

function canMove(r: InventoryRecord): boolean {
  return hasPermission('assets.move')
    && r.locationFoundId !== null
    && (r.assetStatusCode === 'DISPONIBLE' || r.assetStatusCode === 'ASIGNADO')
}

async function reconcile(r: InventoryRecord, action: 'MOVER' | 'REPORTAR' | 'SIN_CAMBIO') {
  if (!session.value) return
  const s = session.value
  pageError.value = ''
  busyRecord.value = r.inventoryRecordId
  try {
    if (action === 'MOVER') {
      // Ya movido (p. ej. reintento tras un fallo al registrar): no se repite.
      if (r.assetCurrentLocationId !== r.locationFoundId) {
        await inventoryApi.moveAsset(r.assetId, r.assetVersion, r.locationFoundId!, ajusteReasonId.value!, `Ajuste por ${s.name}`)
      }
      await inventoryApi.adjust(s.inventorySessionId, r.inventoryRecordId, r.version, 'MOVIMIENTO')
    } else if (action === 'REPORTAR') {
      const incident = await inventoryApi.reportMissing({
        assetId: r.assetId,
        incidentTypeId: missingTypeId.value!,
        incidentPriorityId: highPriorityId.value!,
        title: `No localizado en ${s.name}`,
        description: `No se encontró durante el levantamiento físico "${s.name}". Ubicación esperada: ${r.expectedLocationName ?? '—'}.`,
      })
      await inventoryApi.adjust(s.inventorySessionId, r.inventoryRecordId, r.version, 'INCIDENTE', incident.incidentId)
    } else {
      await inventoryApi.adjust(s.inventorySessionId, r.inventoryRecordId, r.version, 'SIN_CAMBIO')
    }
    await load()
  } catch (err) {
    pageError.value = err instanceof ApiError && err.status === 409
      ? 'El equipo o el renglón cambió mientras tanto. Recargamos los datos; revisa e intenta de nuevo.'
      : err instanceof ApiError ? err.message : 'No se pudo aplicar el ajuste.'
    await load()
  } finally {
    busyRecord.value = null
  }
}

const ADJUSTMENT_LABELS: Record<string, string> = {
  MOVIMIENTO: 'Ubicación corregida',
  INCIDENTE: 'Reportado como extraviado',
  SIN_CAMBIO: 'Sin cambio',
}

const FILTERS: (RecordStatus | 'TODOS')[] = ['TODOS', 'PENDIENTE', 'ENCONTRADO', 'EN_OTRO_LUGAR', 'SOBRANTE', 'NO_ENCONTRADO']
</script>

<template>
  <p v-if="state === 'loading'" class="text-sm text-muted-foreground">Cargando conteo…</p>
  <div v-else-if="state === 'not-found'" class="space-y-3">
    <p class="font-medium text-foreground">Conteo no encontrado</p>
    <RouterLink to="/inventory" class="text-sm text-primary underline-offset-2 hover:underline">Volver a levantamiento</RouterLink>
  </div>
  <p v-else-if="state === 'error' || !session" role="alert" class="text-sm text-destructive">No se pudo cargar el conteo.</p>

  <div v-else class="space-y-5">
    <header class="space-y-2">
      <RouterLink to="/inventory" class="text-sm text-secondary underline-offset-2 hover:underline">Levantamiento físico</RouterLink>
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="font-display text-2xl font-semibold text-foreground">{{ session.name }}</h1>
        <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', isOpen ? 'bg-accent/10 text-accent' : 'bg-muted text-muted-foreground']">
          {{ SESSION_LABELS[session.status] }}
        </span>
      </div>
      <p class="text-sm text-muted-foreground">
        {{ session.locationName }}, abierto el {{ formatDate(session.startedAt) }}<template v-if="session.endedAt">, cerrado el {{ formatDate(session.endedAt) }}</template>.
        {{ checked }} de {{ total }} revisados.
      </p>
      <!-- Avance del recorrido: la única señal visual fuerte de la pantalla. -->
      <div class="h-2 overflow-hidden rounded-full bg-muted" role="progressbar" :aria-valuenow="checked" :aria-valuemin="0" :aria-valuemax="total" aria-label="Equipos revisados">
        <div class="h-full rounded-full bg-accent transition-[width] duration-300" :style="{ width: total ? `${(checked / total) * 100}%` : '0%' }" />
      </div>
    </header>

    <p v-if="pageError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ pageError }}</p>

    <!-- Escaneo -->
    <section v-if="isOpen" aria-labelledby="escanear" class="space-y-3 rounded-lg border border-border bg-card p-5">
      <h2 id="escanear" class="font-display text-base font-semibold text-foreground">Registrar equipo</h2>
      <form class="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] sm:items-end" novalidate @submit.prevent="scan()">
        <div>
          <label :for="codeId" class="mb-1 block text-sm font-medium text-foreground">Código de inventario</label>
          <input
            :id="codeId"
            ref="codeInput"
            v-model="code"
            type="text"
            autocomplete="off"
            autofocus
            placeholder="Escanea con el lector o escribe el código"
            class="min-h-[48px] w-full rounded-md border border-border bg-background px-3 font-medium tracking-wide text-foreground"
          />
        </div>
        <div>
          <label :for="foundAtId" class="mb-1 block text-sm font-medium text-foreground">¿Dónde estás?</label>
          <select :id="foundAtId" v-model="foundAt" class="min-h-[48px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground">
            <option v-for="l in locations" :key="l.locationId" :value="String(l.locationId)">{{ l.name }} ({{ l.code }})</option>
          </select>
        </div>
        <UiButton type="submit" variant="primary" :disabled="scanning || !code.trim()" class="min-h-[48px]">Registrar</UiButton>
      </form>
      <CameraScanner @detected="scan" />
      <p
        v-if="feedback"
        role="status"
        :class="[
          'rounded-md px-3 py-2 text-sm font-medium',
          feedback.kind === 'ok' ? 'bg-success/10 text-success' : feedback.kind === 'warn' ? 'bg-priority-high/10 text-priority-high' : 'bg-destructive/10 text-destructive',
        ]"
      >
        {{ feedback.text }}
      </p>
    </section>

    <!-- Conciliación -->
    <section v-else aria-labelledby="conciliar" class="rounded-lg border border-border bg-card p-5">
      <h2 id="conciliar" class="font-display text-base font-semibold text-foreground">Conciliación</h2>
      <p class="mt-1 text-sm text-muted-foreground">
        <template v-if="toReconcile > 0">Quedan {{ toReconcile }} diferencia(s) por resolver. Cada ajuste se confirma por separado; nada cambia solo.</template>
        <template v-else>Todas las diferencias están resueltas.</template>
      </p>
    </section>

    <div class="flex flex-wrap gap-2" role="group" aria-label="Filtrar por resultado">
      <button
        v-for="f in FILTERS"
        :key="f"
        type="button"
        :aria-pressed="filter === f"
        :class="[
          'min-h-[44px] cursor-pointer rounded-full border px-3 text-sm transition-colors duration-150',
          filter === f ? 'border-accent bg-accent/10 font-semibold text-primary' : 'border-border text-secondary hover:bg-muted',
        ]"
        @click="filter = f"
      >
        {{ f === 'TODOS' ? 'Todos' : RECORD_LABELS[f] }} ({{ f === 'TODOS' ? total : (session.summary[f] ?? 0) }})
      </button>
    </div>

    <ul class="divide-y divide-border overflow-hidden rounded-lg border border-border bg-card">
      <li v-if="records.length === 0" class="px-4 py-6 text-center text-sm text-muted-foreground">Nada en esta vista.</li>
      <li v-for="r in records" :key="r.inventoryRecordId" class="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 text-sm">
        <div class="min-w-0 flex-1">
          <RouterLink :to="`/assets/${r.assetId}`" class="font-medium text-primary underline-offset-2 hover:underline">{{ r.inventoryCode }}</RouterLink>
          <span class="ml-2 text-secondary">{{ r.assetTypeName }}</span>
          <p class="text-xs text-muted-foreground">
            <template v-if="r.expectedLocationName">Debía estar en {{ r.expectedLocationName }}</template>
            <template v-else>No estaba en la lista</template>
            <template v-if="r.locationFoundName">; se encontró en {{ r.locationFoundName }}</template>
            <template v-if="r.checkedAt">, {{ formatDateTime(r.checkedAt) }}</template>
            <template v-if="r.assetStatusCode === 'DADO_DE_BAJA'">. Dado de baja: debió retirarse.</template>
          </p>
        </div>
        <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', RECORD_CLASSES[r.status]]">{{ RECORD_LABELS[r.status] }}</span>

        <!-- Acciones mientras el conteo está abierto -->
        <UiButton
          v-if="isOpen && r.expectedLocationId !== null && r.status !== 'NO_ENCONTRADO'"
          :disabled="busyRecord === r.inventoryRecordId"
          @click="markNotFound(r)"
        >
          No está
        </UiButton>

        <!-- Conciliación -->
        <template v-if="!isOpen && ['EN_OTRO_LUGAR', 'SOBRANTE', 'NO_ENCONTRADO'].includes(r.status)">
          <span v-if="r.adjustment" class="text-xs font-medium text-success">
            {{ ADJUSTMENT_LABELS[r.adjustment] }}<template v-if="r.incidentId"> (incidente #{{ r.incidentId }})</template>
          </span>
          <template v-else>
            <UiButton
              v-if="r.status !== 'NO_ENCONTRADO' && canMove(r)"
              variant="primary"
              :disabled="busyRecord === r.inventoryRecordId || !ajusteReasonId"
              @click="reconcile(r, 'MOVER')"
            >
              Mover a {{ r.locationFoundName }}
            </UiButton>
            <UiButton
              v-if="r.status === 'NO_ENCONTRADO' && hasPermission('incidents.create')"
              variant="danger"
              :disabled="busyRecord === r.inventoryRecordId || !missingTypeId || !highPriorityId"
              @click="reconcile(r, 'REPORTAR')"
            >
              Reportar extraviado
            </UiButton>
            <UiButton :disabled="busyRecord === r.inventoryRecordId" @click="reconcile(r, 'SIN_CAMBIO')">Sin cambio</UiButton>
          </template>
        </template>
      </li>
    </ul>

    <div v-if="isOpen" class="flex flex-wrap justify-end gap-2">
      <UiButton @click="cancel">Cancelar conteo</UiButton>
      <UiButton variant="danger" @click="close">Cerrar conteo</UiButton>
    </div>
  </div>
</template>
