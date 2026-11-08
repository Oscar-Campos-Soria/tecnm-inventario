<script setup lang="ts">
// Ícono de línea 24x24 a partir de sus trazos (menú y módulos).
defineProps<{ paths: string[] }>()
</script>

<template>
  <svg
    viewBox="0 0 24 24"
    class="h-5 w-5 shrink-0"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="d in paths" :key="d" :d="d" />
  </svg>
</template>
