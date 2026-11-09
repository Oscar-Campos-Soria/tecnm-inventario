<script setup lang="ts">
import { computed } from 'vue'

// Antes/después de una fila de audit_log: solo los campos que cambiaron.
const props = defineProps<{ oldData: string | null; newData: string | null }>()

function parse(json: string | null): Record<string, unknown> {
  if (!json) return {}
  try {
    const value = JSON.parse(json)
    return value && typeof value === 'object' && !Array.isArray(value) ? value : { valor: value }
  } catch {
    return { valor: json }
  }
}

function show(value: unknown): string {
  if (value === undefined) return '—'
  if (value === null) return 'vacío'
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}

const rows = computed(() => {
  const before = parse(props.oldData)
  const after = parse(props.newData)
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])]
  return keys
    .filter((k) => show(before[k]) !== show(after[k]))
    .map((k) => ({ key: k, before: k in before ? show(before[k]) : null, after: k in after ? show(after[k]) : null }))
})
</script>

<template>
  <dl v-if="rows.length" class="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-xs">
    <template v-for="r in rows" :key="r.key">
      <dt class="font-medium text-secondary">{{ r.key }}</dt>
      <dd class="break-all text-foreground">
        <span v-if="r.before !== null" class="text-muted-foreground line-through">{{ r.before }}</span>
        <span v-if="r.before !== null && r.after !== null" aria-hidden="true"> → </span>
        <span v-if="r.after !== null">{{ r.after }}</span>
      </dd>
    </template>
  </dl>
  <p v-else class="text-xs text-muted-foreground">Sin detalle de campos.</p>
</template>
