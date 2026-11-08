<script setup lang="ts">
import { computed } from 'vue'

/** Marca: etiqueta de inventario con su perforación. */
const props = withDefaults(
  defineProps<{
    // Clases de tamaño (prop y no class: en Vue class se suma y h-8 chocaría con h-7).
    size?: string
    // Sobre fondo navy (panel del login) el contraste se invierte.
    inverted?: boolean
  }>(),
  { size: 'h-8 w-8', inverted: false },
)

const fill = computed(() => (props.inverted ? 'var(--color-card)' : 'var(--color-primary)'))
const ink = computed(() => (props.inverted ? 'var(--color-primary)' : 'var(--color-card)'))
</script>

<template>
  <svg viewBox="0 0 32 32" :class="size" role="img" aria-label="TecNM Inventario">
    <rect width="32" height="32" rx="7" :fill="fill" />
    <circle cx="23.5" cy="8.5" r="2.25" :fill="ink" />
    <path d="M8 15h12M8 19.5h16M8 24h9" :stroke="ink" stroke-width="2.25" stroke-linecap="round" />
  </svg>
</template>
