<script setup lang="ts">
import { computed, onMounted, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { ticketsApi } from '../api'
import { OPEN_STATUSES } from '../labels'
import type { TicketCatalogs, TicketDetail, UserItem } from '../types'
import CreateIncidentModal from './CreateIncidentModal.vue'

// Acciones del técnico (solo se monta con tickets.manage; el servidor valida).
const props = defineProps<{
  ticket: TicketDetail
  catalogs: TicketCatalogs | null
}>()

// changed: recargar el ticket; conflict: 409, otra persona lo cambió.
const emit = defineEmits<{ changed: []; conflict: [] }>()

const { session, hasPermission } = useAuth()
const assignFieldId = useId()
const priorityFieldId = useId()
const resolutionFieldId = useId()

const technicians = ref<UserItem[]>([])
const assignTo = ref('')
const priorityId = ref('')
const resolution = ref('')
const busy = ref<string | null>(null)
const error = ref('')
const incidentOpen = ref(false)

const isOpen = computed(() => OPEN_STATUSES.includes(props.ticket.status))
const canCreateIncident = computed(() => hasPermission('incidents.create'))
const myId = computed(() => session.value?.userId ?? 0)

onMounted(() => {
  ticketsApi.technicians().then((list) => (technicians.value = list)).catch(() => (technicians.value = []))
})

// Los selectores muestran lo que el ticket tiene ahora (también tras recargar).
watch(
  () => props.ticket,
  (ticket) => {
    assignTo.value = ticket.assignedTo !== null ? String(ticket.assignedTo) : ''
    priorityId.value = String(ticket.priorityId)
  },
  { immediate: true },
)

async function run(key: string, action: () => Promise<unknown>) {
  error.value = ''
  busy.value = key
  try {
    await action()
    emit('changed')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      emit('conflict')
      return
    }
    error.value = err instanceof ApiError ? err.message : 'No se pudo completar la acción.'
  } finally {
    busy.value = null
  }
}

function assign(userId: number) {
  return run('assign', () => ticketsApi.assign(props.ticket.ticketId, props.ticket.version, userId))
}

function changePriority() {
  if (Number(priorityId.value) === props.ticket.priorityId) return
  return run('priority', () => ticketsApi.setPriority(props.ticket.ticketId, props.ticket.version, Number(priorityId.value)))
}

function resolve() {
  if (!resolution.value.trim()) {
    error.value = 'Describe qué se hizo para resolverlo; el usuario lo verá.'
    return
  }
  return run('resolve', async () => {
    await ticketsApi.resolve(props.ticket.ticketId, props.ticket.version, resolution.value.trim())
    resolution.value = ''
  })
}

const selectClass = 'min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground'
</script>

<template>
  <section aria-labelledby="atencion" class="space-y-5 rounded-lg border border-border bg-card p-5">
    <h2 id="atencion" class="font-display text-base font-semibold text-foreground">Atención</h2>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <div v-if="isOpen">
      <label :for="assignFieldId" class="mb-1 block text-sm font-medium text-foreground">Técnico</label>
      <div class="flex gap-2">
        <select :id="assignFieldId" v-model="assignTo" :class="selectClass" :disabled="busy !== null">
          <option value="" disabled>Sin asignar</option>
          <option v-for="tech in technicians" :key="tech.userId" :value="String(tech.userId)">
            {{ tech.firstName }} {{ tech.lastName }}
          </option>
        </select>
        <UiButton :disabled="busy !== null || !assignTo || Number(assignTo) === ticket.assignedTo" @click="assign(Number(assignTo))">
          Asignar
        </UiButton>
      </div>
      <button
        v-if="ticket.assignedTo !== myId"
        type="button"
        class="mt-1 min-h-[44px] cursor-pointer text-sm font-medium text-accent underline-offset-2 hover:underline disabled:opacity-50"
        :disabled="busy !== null"
        @click="assign(myId)"
      >
        Tomarlo yo
      </button>
    </div>

    <div v-if="isOpen || ticket.status === 'RESUELTO'">
      <label :for="priorityFieldId" class="mb-1 block text-sm font-medium text-foreground">Prioridad</label>
      <select :id="priorityFieldId" v-model="priorityId" :class="selectClass" :disabled="busy !== null" @change="changePriority">
        <option v-for="p in catalogs?.incidentPriorities ?? []" :key="p.id" :value="String(p.id)">{{ p.name }}</option>
      </select>
    </div>

    <div v-if="ticket.status === 'EN_ATENCION' || ticket.status === 'ESPERANDO_USUARIO'" class="flex flex-wrap gap-2">
      <UiButton
        v-if="ticket.status === 'EN_ATENCION'"
        :disabled="busy !== null"
        @click="run('wait', () => ticketsApi.setStatus(ticket.ticketId, ticket.version, 'ESPERANDO_USUARIO'))"
      >
        Esperar respuesta del usuario
      </UiButton>
      <UiButton
        v-else
        :disabled="busy !== null"
        @click="run('resume', () => ticketsApi.setStatus(ticket.ticketId, ticket.version, 'EN_ATENCION'))"
      >
        Retomar sin respuesta
      </UiButton>
    </div>

    <form v-if="isOpen" novalidate class="space-y-2 border-t border-border pt-4" @submit.prevent="resolve">
      <label :for="resolutionFieldId" class="block text-sm font-medium text-foreground">Resolver</label>
      <textarea
        :id="resolutionFieldId"
        v-model="resolution"
        rows="3"
        maxlength="4000"
        placeholder="Qué se hizo. El usuario lo verá y confirmará si quedó."
        class="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
      />
      <UiButton type="submit" variant="primary" :disabled="busy !== null">
        {{ busy === 'resolve' ? 'Guardando…' : 'Marcar como resuelto' }}
      </UiButton>
    </form>

    <div class="border-t border-border pt-4">
      <p class="mb-1 text-sm font-medium text-foreground">Incidente técnico</p>
      <p v-if="ticket.incidentId !== null" class="text-sm">
        <RouterLink v-if="hasPermission('incidents.view')" :to="`/incidents/${ticket.incidentId}`" class="font-medium text-primary underline-offset-2 hover:underline">
          Incidente #{{ ticket.incidentId }}
        </RouterLink>
        <span v-else class="font-medium text-foreground">Incidente #{{ ticket.incidentId }}</span>
        <span class="text-muted-foreground">, {{ ticket.incidentStatusName ?? ticket.incidentStatusCode }}</span>
      </p>
      <template v-else-if="isOpen && canCreateIncident">
        <p class="mb-2 text-sm text-muted-foreground">Si es una falla de hardware, regístrala para que el equipo quede fuera de circulación.</p>
        <UiButton :disabled="busy !== null" @click="incidentOpen = true">Crear incidente</UiButton>
      </template>
      <p v-else class="text-sm text-muted-foreground">Sin incidente ligado.</p>
    </div>

    <CreateIncidentModal
      :open="incidentOpen"
      :ticket="ticket"
      :catalogs="catalogs"
      @close="incidentOpen = false"
      @linked="emit('changed')"
      @conflict="emit('conflict')"
    />
  </section>
</template>
