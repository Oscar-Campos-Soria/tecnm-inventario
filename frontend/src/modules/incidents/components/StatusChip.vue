<script setup lang="ts">
import { computed } from 'vue'

// Chip de estado del incidente: rojo = falla activa (ABIERTO), verde =
// resuelto, azul tenue = en proceso (diagnóstico/reparación) o cerrado.
const props = defineProps<{
  statusCode: string
  statusName?: string
}>()

const chipClass = computed(() => {
  switch (props.statusCode) {
    case 'ABIERTO':
      return 'bg-destructive/10 text-destructive'
    case 'RESUELTO':
      return 'bg-success/10 text-success'
    case 'CERRADO':
      return 'bg-muted text-muted-foreground'
    default:
      return 'bg-muted text-secondary'
  }
})
</script>

<template>
  <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', chipClass]">
    {{ statusName ?? statusCode }}
  </span>
</template>
