<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { assistantApi } from '../api'
import type { TicketDraft } from '../types'

// Borrador que armó el asistente. Solo este clic crea el ticket: el modelo nunca escribe.
const props = defineProps<{ draft: TicketDraft }>()

const state = ref<'draft' | 'sending' | 'created' | 'discarded'>('draft')
const ticketId = ref<number | null>(null)
const error = ref('')

async function create() {
  if (state.value !== 'draft') return
  state.value = 'sending'
  error.value = ''
  try {
    ticketId.value = (await assistantApi.createTicket(props.draft)).ticketId
    state.value = 'created'
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'No se pudo crear el ticket. Intenta de nuevo.'
    state.value = 'draft'
  }
}
</script>

<template>
  <div class="mt-3 rounded-lg border border-border bg-background p-3" role="group" aria-label="Borrador de ticket">
    <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Borrador de ticket</p>
    <dl class="mt-2 space-y-1 text-sm">
      <div><dt class="inline text-muted-foreground">Tipo: </dt><dd class="inline">{{ draft.categoria }}</dd></div>
      <div><dt class="inline text-muted-foreground">Qué pasa: </dt><dd class="inline font-medium">{{ draft.titulo }}</dd></div>
      <div v-if="draft.descripcion"><dt class="inline text-muted-foreground">Detalles: </dt><dd class="inline whitespace-pre-wrap">{{ draft.descripcion }}</dd></div>
      <div v-if="draft.equipo"><dt class="inline text-muted-foreground">Equipo: </dt><dd class="inline">{{ draft.equipo }}</dd></div>
    </dl>

    <p v-if="error" role="alert" class="mt-2 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="state === 'created'" role="status" class="mt-3 text-sm text-foreground">
      Ticket creado.
      <RouterLink :to="{ name: 'ticket-detail', params: { ticketId: ticketId } }" class="font-semibold text-primary underline">
        Ver ticket #{{ ticketId }}
      </RouterLink>
    </p>
    <p v-else-if="state === 'discarded'" class="mt-3 text-sm text-muted-foreground">Borrador descartado. No se creó nada.</p>
    <div v-else class="mt-3 flex flex-wrap gap-2">
      <UiButton variant="primary" :disabled="state === 'sending'" @click="create">
        {{ state === 'sending' ? 'Creando…' : 'Crear ticket' }}
      </UiButton>
      <UiButton :disabled="state === 'sending'" @click="state = 'discarded'">Descartar</UiButton>
    </div>
    <p v-if="state === 'draft'" class="mt-2 text-xs text-muted-foreground">¿Algo no cuadra? Pídele al asistente que lo cambie.</p>
  </div>
</template>
