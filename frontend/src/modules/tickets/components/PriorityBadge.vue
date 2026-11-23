<script setup lang="ts">
import type { PriorityCode } from '../types'

// Prioridad legible sin depender solo del color: Crítica dice "Urgente".
defineProps<{ priority: PriorityCode; name: string }>()
</script>

<template>
  <span
    v-if="priority === 'CRITICA'"
    class="chip bg-destructive text-on-destructive"
  >
    <svg viewBox="0 0 24 24" class="h-3.5 w-3.5" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" aria-hidden="true">
      <path d="M12 7v6M12 17v.5" />
    </svg>
    Urgente
  </span>
  <span
    v-else-if="priority === 'ALTA'"
    class="chip chip-alert"
  >
    Prioridad alta
  </span>
  <span v-else class="chip chip-plain">
    Prioridad {{ name.toLowerCase() }}
  </span>
</template>
