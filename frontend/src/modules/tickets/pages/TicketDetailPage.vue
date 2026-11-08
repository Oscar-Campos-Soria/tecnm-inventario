<script setup lang="ts">
import { computed, onMounted, ref, useId, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import DocumentList from '../../../shared/components/DocumentList.vue'
import NavIcon from '../../../shared/components/NavIcon.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import UiField from '../../../shared/components/UiField.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate } from '../../../shared/utils/format'
import { ticketsApi } from '../api'
import PriorityBadge from '../components/PriorityBadge.vue'
import TicketStaffPanel from '../components/TicketStaffPanel.vue'
import TicketThread from '../components/TicketThread.vue'
import { OPEN_STATUSES, TERMINAL_STATUSES, categoryIcon, statusClass, statusLabel, ticketNumber } from '../labels'
import type { TicketCatalogs, TicketDetail, TicketStatus } from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

const route = useRoute()
const { session, hasPermission } = useAuth()
const reopenFieldId = useId()

const numericId = computed(() => Number(route.params.ticketId))
const isValidId = computed(() => Number.isInteger(numericId.value))

const state = ref<LoadState>('loading')
const ticket = ref<TicketDetail | null>(null)
const catalogs = ref<TicketCatalogs | null>(null)
const notice = ref(route.query.aviso === 'foto' ? 'El ticket se creó, pero la foto no se pudo subir. Adjúntala de nuevo abajo.' : '')
const actionError = ref('')
const acting = ref(false)
const reopening = ref(false)
const reopenReason = ref('')

const currentUserId = computed(() => session.value?.userId ?? 0)
const isRequester = computed(() => ticket.value?.requesterId === currentUserId.value)
const isOpen = computed(() => (ticket.value ? OPEN_STATUSES.includes(ticket.value.status) : false))
const isTerminal = computed(() => (ticket.value ? TERMINAL_STATUSES.includes(ticket.value.status) : false))

async function load() {
  const detail = await ticketsApi.getById(numericId.value)
  ticket.value = detail
}

watch(
  numericId,
  () => {
    if (!isValidId.value) return
    state.value = 'loading'
    load()
      .then(() => (state.value = 'ready'))
      .catch((err) => {
        // Un ticket ajeno también responde 404: no se distingue de uno inexistente.
        state.value = err instanceof ApiError && err.status === 404 ? 'not-found' : 'error'
      })
  },
  { immediate: true },
)

onMounted(() => {
  ticketsApi.catalogs().then((c) => (catalogs.value = c)).catch(() => (catalogs.value = null))
})

function reload() {
  load().catch(() => (actionError.value = 'No se pudo recargar el ticket. Recarga la página.'))
}

function conflict() {
  actionError.value = 'Alguien más cambió este ticket. Recargamos los datos; revísalos e intenta de nuevo.'
  reload()
}

// Acciones del solicitante: confirmar, reabrir o cancelar.
async function changeStatus(status: TicketStatus, reason?: string) {
  if (!ticket.value) return
  actionError.value = ''
  acting.value = true
  try {
    await ticketsApi.setStatus(ticket.value.ticketId, ticket.value.version, status, reason)
    reopening.value = false
    reopenReason.value = ''
    await load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) conflict()
    else actionError.value = err instanceof ApiError ? err.message : 'No se pudo actualizar el ticket.'
  } finally {
    acting.value = false
  }
}

function submitReopen() {
  if (!reopenReason.value.trim()) {
    actionError.value = 'Cuéntale al técnico qué sigue fallando.'
    return
  }
  changeStatus('EN_ATENCION', reopenReason.value.trim())
}

function cancelTicket() {
  if (window.confirm('¿Cancelar este ticket? Ya no recibirá atención.')) changeStatus('CANCELADO')
}
</script>

<template>
  <p v-if="isValidId && state === 'loading'" class="text-sm text-muted-foreground">Cargando ticket…</p>

  <div v-else-if="!isValidId || state === 'not-found'" class="space-y-3">
    <p class="font-medium text-foreground">Ticket no encontrado</p>
    <RouterLink to="/tickets" class="text-sm text-primary underline-offset-2 hover:underline">Volver a tickets</RouterLink>
  </div>

  <p v-else-if="state === 'error' || !ticket" role="alert" class="text-sm text-destructive">No se pudo cargar el ticket.</p>

  <div v-else class="space-y-5">
    <header class="space-y-2">
      <RouterLink to="/tickets" class="text-sm text-secondary underline-offset-2 hover:underline">
        {{ ticket.canManage ? 'Bandeja de tickets' : 'Mis tickets' }}
      </RouterLink>
      <h1 class="font-display text-2xl font-semibold text-foreground">{{ ticket.title }}</h1>
      <div class="flex flex-wrap items-center gap-2 text-sm">
        <span class="font-medium text-muted-foreground">{{ ticketNumber(ticket.ticketId) }}</span>
        <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', statusClass(ticket.status)]">
          {{ statusLabel(ticket.status, isRequester) }}
        </span>
        <PriorityBadge :priority="ticket.priorityCode" :name="ticket.priorityName" />
        <span class="inline-flex items-center gap-1.5 text-secondary">
          <NavIcon :paths="categoryIcon(ticket.categoryCode)" />
          {{ ticket.categoryName }}
        </span>
      </div>
    </header>

    <p v-if="notice" role="status" class="rounded-md bg-priority-high/10 px-3 py-2 text-sm text-priority-high">{{ notice }}</p>
    <p v-if="actionError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ actionError }}</p>

    <!-- Resuelto: el solicitante confirma o reabre; si no contesta, se cierra solo. -->
    <section v-if="ticket.status === 'RESUELTO' && isRequester" class="rounded-lg border border-success/40 bg-success/5 p-5" aria-labelledby="confirmar">
      <h2 id="confirmar" class="font-display text-base font-semibold text-foreground">El técnico marcó esto como resuelto. ¿Quedó bien?</h2>
      <p v-if="ticket.resolution" class="mt-2 whitespace-pre-wrap text-sm text-foreground">{{ ticket.resolution }}</p>
      <p v-if="ticket.autoCloseAt" class="mt-2 text-sm text-muted-foreground">
        Si no respondes, se cerrará solo el {{ formatDate(ticket.autoCloseAt) }}.
      </p>

      <div v-if="!reopening" class="mt-4 flex flex-wrap gap-2">
        <UiButton variant="primary" :disabled="acting" @click="changeStatus('CERRADO')">Sí, quedó bien</UiButton>
        <UiButton :disabled="acting" @click="reopening = true">No, sigue fallando</UiButton>
      </div>
      <form v-else class="mt-4 space-y-2" novalidate @submit.prevent="submitReopen">
        <label :for="reopenFieldId" class="block text-sm font-medium text-foreground">¿Qué sigue fallando?</label>
        <textarea :id="reopenFieldId" v-model="reopenReason" rows="2" maxlength="500" class="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground" />
        <div class="flex gap-2">
          <UiButton type="submit" variant="primary" :disabled="acting">Reabrir ticket</UiButton>
          <UiButton :disabled="acting" @click="reopening = false">Volver</UiButton>
        </div>
      </form>
    </section>

    <!-- Resuelto visto por el técnico: solo informa la resolución y el plazo. -->
    <section v-else-if="ticket.status === 'RESUELTO'" class="rounded-lg border border-border bg-card p-5" aria-labelledby="resolucion">
      <h2 id="resolucion" class="font-display text-base font-semibold text-foreground">Resolución</h2>
      <p class="mt-2 whitespace-pre-wrap text-sm text-foreground">{{ ticket.resolution }}</p>
      <p v-if="ticket.autoCloseAt" class="mt-2 text-sm text-muted-foreground">
        Esperando confirmación del usuario; se cierra solo el {{ formatDate(ticket.autoCloseAt) }}.
      </p>
    </section>

    <p
      v-else-if="ticket.status === 'ESPERANDO_USUARIO' && isRequester"
      class="rounded-lg border border-priority-high/40 bg-priority-high/5 px-4 py-3 text-sm font-medium text-priority-high"
    >
      El técnico necesita tu respuesta. Escríbele abajo en la conversación.
    </p>

    <div class="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div class="min-w-0 space-y-5">
        <TicketThread :ticket="ticket" :current-user-id="currentUserId" @changed="reload" />
        <DocumentList
          owner-type="TICKET"
          :owner-id="ticket.ticketId"
          title="Fotos y archivos"
          empty-text="Sin archivos adjuntos."
          :can-upload="!isTerminal"
        />
      </div>

      <aside class="space-y-5">
        <section aria-labelledby="datos" class="rounded-lg border border-border bg-card p-5">
          <h2 id="datos" class="mb-3 font-display text-base font-semibold text-foreground">Datos</h2>
          <dl class="space-y-3">
            <UiField label="Levantado por" :value="ticket.requesterName" />
            <UiField label="Fecha" :value="formatDate(ticket.createdAt)" />
            <UiField label="Atiende" :value="ticket.assignedToName ?? 'Aún sin técnico'" />
            <UiField label="Equipo">
              <RouterLink
                v-if="ticket.assetId !== null && hasPermission('assets.view')"
                :to="`/assets/${ticket.assetId}`"
                class="text-primary underline-offset-2 hover:underline"
              >
                {{ ticket.assetInventoryCode }}
              </RouterLink>
              <template v-else>{{ ticket.assetInventoryCode ?? '—' }}</template>
            </UiField>
            <UiField v-if="ticket.locationName" label="Ubicación" :value="ticket.locationName" />
            <UiField v-if="ticket.closedAt" label="Cerrado" :value="formatDate(ticket.closedAt)" />
          </dl>
          <UiButton v-if="isOpen && (isRequester || ticket.canManage)" class="mt-4 w-full" :disabled="acting" @click="cancelTicket">
            Cancelar ticket
          </UiButton>
        </section>

        <TicketStaffPanel v-if="ticket.canManage" :ticket="ticket" :catalogs="catalogs" @changed="reload" @conflict="conflict" />
      </aside>
    </div>
  </div>
</template>
