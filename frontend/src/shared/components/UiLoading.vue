<script setup lang="ts">
// Carga: renglones que anticipan la forma del contenido; el texto del llamador queda para lectores de pantalla.
const WIDTHS = ['w-2/5', 'w-full', 'w-11/12', 'w-4/5']
</script>

<template>
  <div role="status" class="space-y-3 py-2">
    <span class="sr-only"><slot>Cargando…</slot></span>
    <div v-for="width in WIDTHS" :key="width" :class="['skeleton-line h-4 rounded', width]" aria-hidden="true" />
  </div>
</template>
