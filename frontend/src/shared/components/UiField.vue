<script setup lang="ts">
// Par etiqueta/valor de las fichas de detalle (va dentro de un <dl>).
// Antes cada página tenía su propio Field idéntico.
defineProps<{
  label: string
  value?: string | number | null
  // Textos largos (descripción, solución) conservan sus saltos de línea.
  multiline?: boolean
}>()
</script>

<template>
  <div>
    <dt class="text-xs font-medium text-muted-foreground">{{ label }}</dt>
    <dd :class="['mt-0.5 text-sm text-foreground', multiline ? 'whitespace-pre-wrap' : '']">
      <slot>{{ value || '—' }}</slot>
    </dd>
  </div>
</template>
