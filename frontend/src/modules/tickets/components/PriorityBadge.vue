<script setup lang="ts">
import type { PriorityCode } from '../types'

// Prioridad legible sin depender solo del color: Crítica dice "Urgente".
defineProps<{ priority: PriorityCode; name: string }>()
</script>

<template>
  <span
    v-if="priority === 'CRITICA'"
    class="inline-flex items-center gap-1 rounded-full bg-destructive px-2.5 py-0.5 text-xs font-semibold text-on-destructive"
  >
    <svg viewBox="0 0 24 24" class="h-3.5 w-3.5" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" aria-hidden="true">
      <path d="M12 7v6M12 17v.5" />
    </svg>
    Urgente
  </span>
  <span
    v-else-if="priority === 'ALTA'"
    class="rounded-full bg-priority-high/10 px-2.5 py-0.5 text-xs font-semibold text-priority-high"
  >
    Prioridad alta
  </span>
  <span v-else class="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
    Prioridad {{ name.toLowerCase() }}
  </span>
</template>
