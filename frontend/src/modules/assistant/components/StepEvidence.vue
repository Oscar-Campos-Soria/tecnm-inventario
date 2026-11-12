<script setup lang="ts">
import type { AssistantStep } from '../types'

// "Consulté": cada consulta del asistente en palabras y, al abrirla, los datos reales que obtuvo.
// Si el modelo redacta mal un número, aquí está el dato tal como salió del sistema.
defineProps<{ steps: AssistantStep[] }>()

type Row = Record<string, unknown>
const MAX_COLUMNS = 6

function rows(data: AssistantStep['data']): Row[] {
  if (!data) return []
  const list = Object.values(data).find((v) => Array.isArray(v) && v.length > 0 && typeof v[0] === 'object')
  return (list as Row[] | undefined) ?? []
}

function columns(list: Row[]): string[] {
  const keys = Object.keys(list[0] ?? {})
  // Solo columnas con algún valor: el modelo recibe muchas vacías (hostname, notas).
  return keys.filter((k) => list.some((r) => r[k] !== null && r[k] !== '' && !Array.isArray(r[k]))).slice(0, MAX_COLUMNS)
}

function scalars(data: AssistantStep['data']): [string, unknown][] {
  if (!data) return []
  return Object.entries(data).filter(([, v]) => v !== null && typeof v !== 'object')
}

function label(key: string): string {
  const text = key.replace(/_/g, ' ')
  return text.charAt(0).toUpperCase() + text.slice(1)
}

function cell(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'boolean') return value ? 'Sí' : 'No'
  return String(value)
}
</script>

<template>
  <div v-if="steps.length" class="mt-2 space-y-1.5">
    <p class="text-xs font-medium text-muted-foreground">Consulté</p>
    <details
      v-for="(step, i) in steps"
      :key="i"
      class="group rounded-md border border-border bg-background text-xs open:bg-card"
    >
      <summary class="flex min-h-[36px] cursor-pointer list-none items-center gap-2 px-2.5 py-1.5 text-secondary hover:text-primary">
        <svg aria-hidden="true" viewBox="0 0 24 24" class="h-3.5 w-3.5 shrink-0 transition-transform group-open:rotate-90 motion-reduce:transition-none" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 6l6 6-6 6" /></svg>
        <span v-if="step.error" class="text-destructive">{{ step.error }}</span>
        <span v-else>{{ step.summary }}</span>
      </summary>
      <div v-if="!step.error && step.data" class="border-t border-border px-2.5 py-2">
        <p v-if="scalars(step.data).length" class="mb-1.5 text-muted-foreground">
          <span v-for="[key, value] in scalars(step.data)" :key="key" class="mr-3">{{ label(key) }}: <strong class="font-semibold text-foreground">{{ cell(value) }}</strong></span>
        </p>
        <div v-if="step.tool !== 'buscar_manual' && rows(step.data).length" class="overflow-x-auto">
          <table class="w-full text-left">
            <thead>
              <tr class="text-muted-foreground">
                <th v-for="column in columns(rows(step.data))" :key="column" scope="col" class="whitespace-nowrap py-1 pr-3 font-medium">{{ label(column) }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, r) in rows(step.data)" :key="r" class="border-t border-border/70">
                <td v-for="column in columns(rows(step.data))" :key="column" class="whitespace-nowrap py-1 pr-3 text-foreground">{{ cell(row[column]) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <ul v-if="step.tool === 'buscar_manual'" class="space-y-1">
          <li v-for="(fragment, f) in (step.data.fragmentos as { seccion: string }[] | undefined) ?? []" :key="f">
            Manual: <strong class="font-semibold text-foreground">{{ fragment.seccion }}</strong>
          </li>
        </ul>
      </div>
    </details>
  </div>
</template>
