<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useId } from 'vue'
import { ApiError } from '../../../shared/api/http'
import { assistantApi } from '../api'
import type { AssistantStatus, ChatMessage } from '../types'
import AnswerText from './AnswerText.vue'
import StepEvidence from './StepEvidence.vue'

// Botón flotante "Pregúntale" + panel de conversación. Solo aparece si el modelo responde
// (GET /api/assistant/status); el resto de la app no depende de él.
const MAX_QUESTION = 500

const status = ref<AssistantStatus | null>(null)
const open = ref(false)
const messages = ref<ChatMessage[]>([])
const conversationId = ref<string | null>(null)
const question = ref('')
const sending = ref(false)
let nextId = 1

const panelId = useId()
const titleId = useId()
const inputId = useId()
const launcher = ref<HTMLButtonElement | null>(null)
const input = ref<HTMLTextAreaElement | null>(null)
const scroller = ref<HTMLElement | null>(null)

onMounted(() => {
  assistantApi.status().then((s) => (status.value = s)).catch(() => (status.value = null))
})

// Sugerencias según lo que el rol puede consultar (las herramientas que el servidor le permite).
const suggestions = computed(() => {
  const tools = new Set(status.value?.tools ?? [])
  const list: string[] = []
  if (tools.has('buscar_activos')) list.push('¿Qué equipos tienen falla?')
  else if (tools.has('mis_equipos')) list.push('¿Qué equipo tengo asignado?')
  if (tools.has('buscar_incidentes')) list.push('¿Qué hay de nuevo hoy?')
  if (tools.has('buscar_tickets')) list.push(tools.has('buscar_incidentes') ? '¿Cuántos tickets sin asignar hay?' : '¿Cuáles son mis tickets?')
  if (tools.has('buscar_manual')) list.push('¿Cómo doy de baja un equipo?')
  return list.slice(0, 4)
})

async function scrollToEnd() {
  await nextTick()
  scroller.value?.scrollTo({ top: scroller.value.scrollHeight })
}

async function openPanel() {
  open.value = true
  await nextTick()
  input.value?.focus()
}

function closePanel() {
  open.value = false
  nextTick(() => launcher.value?.focus())
}

async function send(text = question.value) {
  const asked = text.trim()
  if (!asked || sending.value) return
  messages.value.push({ id: nextId++, role: 'user', text: asked, steps: [] })
  question.value = ''
  sending.value = true
  scrollToEnd()
  try {
    const reply = await assistantApi.ask(asked, conversationId.value)
    conversationId.value = reply.conversationId
    messages.value.push({ id: nextId++, role: 'assistant', text: reply.answer, steps: reply.steps })
  } catch (e) {
    const detail = e instanceof ApiError ? e.message : 'No se pudo consultar al asistente. Intenta de nuevo.'
    messages.value.push({ id: nextId++, role: 'assistant', text: detail, steps: [], failed: true })
  } finally {
    sending.value = false
    scrollToEnd()
    nextTick(() => input.value?.focus())
  }
}

async function newConversation() {
  const id = conversationId.value
  messages.value = []
  conversationId.value = null
  input.value?.focus()
  // Borra la memoria en el servidor; si ya expiró, no importa.
  if (id) await assistantApi.end(id).catch(() => undefined)
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    send()
  }
}
</script>

<template>
  <template v-if="status?.enabled">
    <button
      v-show="!open"
      ref="launcher"
      type="button"
      :aria-expanded="open"
      :aria-controls="panelId"
      aria-label="Pregúntale al asistente del inventario"
      class="fixed bottom-4 right-4 z-40 flex min-h-[52px] cursor-pointer items-center gap-2 rounded-full bg-primary px-4 font-semibold text-on-primary shadow-lg transition-colors duration-150 hover:bg-secondary sm:bottom-6 sm:right-6"
      @click="openPanel"
    >
      <svg aria-hidden="true" viewBox="0 0 24 24" class="h-6 w-6" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
        <path d="M4 5h16v11H9l-5 4z" />
        <path d="M9 10h.01M12 10h.01M15 10h.01" />
      </svg>
      <span class="hidden sm:inline">Pregúntale</span>
    </button>

    <section
      v-if="open"
      :id="panelId"
      role="dialog"
      :aria-labelledby="titleId"
      class="assistant-panel fixed inset-0 z-50 flex flex-col bg-card shadow-2xl sm:inset-auto sm:bottom-6 sm:right-6 sm:h-[min(640px,calc(100vh-3rem))] sm:w-[420px] sm:rounded-xl sm:border sm:border-border"
      @keydown.esc="closePanel"
    >
      <header class="flex items-center gap-2 border-b border-border px-4 py-3">
        <div class="min-w-0 flex-1">
          <h2 :id="titleId" class="font-display text-base font-semibold text-foreground">Asistente del inventario</h2>
          <p class="text-xs text-muted-foreground">Responde con los datos del sistema. Solo consulta, no cambia nada.</p>
        </div>
        <button
          v-if="messages.length"
          type="button"
          class="min-h-[40px] cursor-pointer rounded-md px-2.5 text-sm text-primary hover:bg-muted"
          @click="newConversation"
        >
          Nueva conversación
        </button>
        <button type="button" aria-label="Cerrar el asistente" class="flex h-10 w-10 cursor-pointer items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground" @click="closePanel">
          <svg aria-hidden="true" viewBox="0 0 24 24" class="h-5 w-5" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 6l12 12M18 6L6 18" /></svg>
        </button>
      </header>

      <div ref="scroller" class="flex-1 space-y-4 overflow-y-auto px-4 py-4">
        <div v-if="messages.length === 0" class="space-y-3">
          <p class="text-sm text-foreground">
            Pregunta por equipos, tickets o cómo se hace algo en el sistema. Puedes seguir la plática: "¿y cuáles de esas…?".
          </p>
          <ul class="space-y-2">
            <li v-for="suggestion in suggestions" :key="suggestion">
              <button
                type="button"
                class="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-3 text-left text-sm text-primary hover:border-accent hover:bg-muted"
                @click="send(suggestion)"
              >
                {{ suggestion }}
              </button>
            </li>
          </ul>
        </div>

        <article v-for="message in messages" :key="message.id" :class="message.role === 'user' ? 'flex justify-end' : ''">
          <p v-if="message.role === 'user'" class="max-w-[85%] whitespace-pre-wrap rounded-lg bg-primary px-3 py-2 text-sm text-on-primary">
            {{ message.text }}
          </p>
          <div v-else class="text-sm text-foreground">
            <div :class="message.failed ? 'rounded-md bg-destructive/10 px-3 py-2 text-destructive' : ''">
              <AnswerText :text="message.text" />
            </div>
            <StepEvidence :steps="message.steps" />
          </div>
        </article>

        <p v-if="sending" class="flex items-center gap-2 text-sm text-muted-foreground">
          <span class="assistant-dot h-2 w-2 rounded-full bg-accent" aria-hidden="true" />
          Consultando el inventario…
        </p>
      </div>
      <p class="sr-only" aria-live="polite">{{ sending ? 'Consultando el inventario' : (messages.at(-1)?.role === 'assistant' ? 'Respuesta lista' : '') }}</p>

      <form class="border-t border-border p-3" @submit.prevent="send()">
        <label :for="inputId" class="sr-only">Tu pregunta</label>
        <div class="flex items-end gap-2">
          <textarea
            :id="inputId"
            ref="input"
            v-model="question"
            rows="2"
            :maxlength="MAX_QUESTION"
            placeholder="Escribe tu pregunta"
            class="max-h-32 min-h-[44px] flex-1 resize-none rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
            @keydown="onKeydown"
          />
          <button
            type="submit"
            :disabled="sending || !question.trim()"
            class="min-h-[44px] cursor-pointer rounded-md bg-accent px-4 text-sm font-semibold text-on-accent hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
          >
            Preguntar
          </button>
        </div>
        <p class="mt-1 text-xs text-muted-foreground">Enter para preguntar, Mayús + Enter para otra línea.</p>
      </form>
    </section>
  </template>
</template>

<style scoped>
@media (prefers-reduced-motion: no-preference) {
  .assistant-panel {
    animation: assistant-open 160ms ease-out;
  }
  .assistant-dot {
    animation: assistant-pulse 1s ease-in-out infinite;
  }
}

@keyframes assistant-open {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
}

@keyframes assistant-pulse {
  50% {
    opacity: 0.3;
  }
}
</style>
