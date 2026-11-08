<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDateTime } from '../../../shared/utils/format'
import { ticketsApi } from '../api'
import { TERMINAL_STATUSES } from '../labels'
import type { TicketDetail } from '../types'

// Conversación del ticket: la descripción abre el hilo y cada comentario
// sigue en orden. Lo mío a la derecha, lo demás a la izquierda; las notas
// internas (solo técnicos) en ámbar con candado; el sistema al centro.
const props = defineProps<{
  ticket: TicketDetail
  currentUserId: number
}>()

const emit = defineEmits<{ changed: [] }>()

const composerId = useId()
const body = ref('')
const isInternal = ref(false)
const sending = ref(false)
const error = ref('')

const readOnly = computed(() => TERMINAL_STATUSES.includes(props.ticket.status))

const placeholder = computed(() => {
  if (isInternal.value) return 'Nota para el equipo de soporte (el solicitante no la ve)'
  return props.ticket.requesterId === props.currentUserId
    ? 'Escribe una respuesta para el técnico'
    : 'Escribe una respuesta para el solicitante'
})

async function send() {
  error.value = ''
  if (!body.value.trim()) return
  sending.value = true
  try {
    await ticketsApi.comment(props.ticket.ticketId, body.value.trim(), isInternal.value)
    body.value = ''
    isInternal.value = false
    emit('changed')
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo enviar el mensaje.'
  } finally {
    sending.value = false
  }
}

function isSystem(username: string) {
  return username === 'system'
}
</script>

<template>
  <section aria-labelledby="conversacion" class="rounded-lg border border-border bg-card">
    <h2 id="conversacion" class="border-b border-border px-5 py-3 font-display text-base font-semibold text-foreground">Conversación</h2>

    <ol class="space-y-4 px-4 py-5 sm:px-5">
      <li :class="['flex flex-col', ticket.requesterId === currentUserId ? 'items-end' : 'items-start']">
        <p class="mb-1 text-xs text-muted-foreground">
          {{ ticket.requesterId === currentUserId ? 'Tú' : ticket.requesterName }}, {{ formatDateTime(ticket.createdAt) }}
        </p>
        <div
          :class="[
            'max-w-[85%] rounded-lg px-3.5 py-2.5 text-sm',
            ticket.requesterId === currentUserId
              ? 'rounded-tr-sm bg-primary text-on-primary'
              : 'rounded-tl-sm border border-border bg-background text-foreground',
          ]"
        >
          <p class="font-medium">{{ ticket.title }}</p>
          <p v-if="ticket.description" class="mt-1 whitespace-pre-wrap">{{ ticket.description }}</p>
        </div>
      </li>

      <li
        v-for="comment in ticket.comments"
        :key="comment.commentId"
        :class="[
          'flex flex-col',
          isSystem(comment.authorUsername) ? 'items-center' : comment.authorId === currentUserId ? 'items-end' : 'items-start',
        ]"
      >
        <p v-if="isSystem(comment.authorUsername)" class="max-w-[90%] rounded-full bg-muted px-3 py-1 text-center text-xs text-muted-foreground">
          {{ comment.body }}, {{ formatDateTime(comment.createdAt) }}
        </p>
        <template v-else>
          <p class="mb-1 text-xs text-muted-foreground">
            {{ comment.authorId === currentUserId ? 'Tú' : comment.authorName }}, {{ formatDateTime(comment.createdAt) }}
          </p>
          <div
            :class="[
              'max-w-[85%] rounded-lg px-3.5 py-2.5 text-sm',
              comment.isInternal
                ? 'border border-amber-300 bg-amber-50 text-amber-950'
                : comment.authorId === currentUserId
                  ? 'rounded-tr-sm bg-primary text-on-primary'
                  : 'rounded-tl-sm border border-border bg-background text-foreground',
            ]"
          >
            <p v-if="comment.isInternal" class="mb-1 flex items-center gap-1 text-xs font-semibold">
              <svg viewBox="0 0 24 24" class="h-3.5 w-3.5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <rect x="5" y="11" width="14" height="10" rx="2" />
                <path d="M8 11V8a4 4 0 018 0v3" />
              </svg>
              Nota interna
            </p>
            <p class="whitespace-pre-wrap">{{ comment.body }}</p>
          </div>
        </template>
      </li>
    </ol>

    <p v-if="readOnly" class="border-t border-border px-5 py-3 text-sm text-muted-foreground">
      Este ticket está {{ ticket.status === 'CERRADO' ? 'cerrado' : 'cancelado' }}; ya no recibe mensajes. Si el problema vuelve, levanta un ticket nuevo.
    </p>

    <form v-else class="space-y-2 border-t border-border p-4 sm:px-5" novalidate @submit.prevent="send">
      <label :for="composerId" class="sr-only">Mensaje</label>
      <textarea
        :id="composerId"
        v-model="body"
        rows="3"
        maxlength="4000"
        :placeholder="placeholder"
        :class="[
          'w-full rounded-md border px-3 py-2 text-sm text-foreground',
          isInternal ? 'border-amber-300 bg-amber-50' : 'border-border bg-background',
        ]"
      />
      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
      <div class="flex flex-wrap items-center justify-between gap-2">
        <label v-if="ticket.canManage" class="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm text-foreground">
          <input v-model="isInternal" type="checkbox" class="h-4 w-4 cursor-pointer" />
          Nota interna (no la ve el solicitante)
        </label>
        <span v-else />
        <UiButton type="submit" variant="primary" :disabled="sending || !body.trim()">
          {{ sending ? 'Enviando…' : isInternal ? 'Guardar nota' : 'Enviar' }}
        </UiButton>
      </div>
    </form>
  </section>
</template>
