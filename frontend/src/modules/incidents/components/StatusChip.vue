<script setup lang="ts">
import { computed } from 'vue'

// Chip de estado del incidente: rojo = falla activa (ABIERTO), verde =
// resuelto, ámbar = en proceso (diagnóstico/reparación), gris = cerrado.
const props = defineProps<{
  statusCode: string
  statusName?: string
}>()

const chipClass = computed(() => {
  switch (props.statusCode) {
    case 'ABIERTO':
      return 'chip-danger'
    case 'RESUELTO':
      return 'chip-success'
    case 'CERRADO':
      return 'chip-neutral'
    default:
      return 'chip-warning'
  }
})
</script>

<template>
  <span :class="['chip', chipClass]">
    {{ statusName ?? statusCode }}
  </span>
</template>
