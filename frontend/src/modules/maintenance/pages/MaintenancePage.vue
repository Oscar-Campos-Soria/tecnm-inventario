<script setup lang="ts">
import { ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDate } from '../../../shared/utils/format'
import { maintenanceApi } from '../api'
import type { MaintenanceItem, PagedResult } from '../types'

const days = ref(30)
const upcoming = ref<MaintenanceItem[] | null>(null)
const history = ref<PagedResult<MaintenanceItem> | null>(null)
const page = ref(1)
const typeFilter = ref('')
const error = ref('')

watch(
  days,
  (value) => {
    maintenanceApi
      .upcoming(value)
      .then((list) => (upcoming.value = list))
      .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudo cargar el calendario.'))
  },
  { immediate: true },
)

watch(typeFilter, () => (page.value = 1))
watch(
  [page, typeFilter],
  () => {
    maintenanceApi
      .history(page.value, typeFilter.value || undefined)
      .then((h) => (history.value = h))
      .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudo cargar el historial.'))
  },
  { immediate: true },
)

// Días que faltan (negativo = vencido), contando por fecha local.
function daysUntil(iso: string | null): number {
  if (!iso) return 0
  const target = new Date(iso)
  const today = new Date()
  const a = Date.UTC(target.getFullYear(), target.getMonth(), target.getDate())
  const b = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate())
  return Math.round((a - b) / 86_400_000)
}

function dueLabel(iso: string | null): string {
  const d = daysUntil(iso)
  if (d < 0) return `Vencido hace ${-d} ${-d === 1 ? 'día' : 'días'}`
  if (d === 0) return 'Vence hoy'
  return `En ${d} ${d === 1 ? 'día' : 'días'}`
}
</script>

<template>
  <div class="space-y-6">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Mantenimiento</h1>
      <p class="mt-1 text-sm text-muted-foreground">Preventivos por vencer y bitácora de intervenciones. Se registran desde la ficha de cada activo.</p>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <section aria-labelledby="proximos" class="rounded-lg border border-border bg-card p-5">
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 id="proximos" class="font-display text-base font-semibold text-foreground">Preventivos por vencer</h2>
        <label class="flex items-center gap-2 text-sm text-foreground">
          Próximos
          <select v-model.number="days" class="min-h-[44px] cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm">
            <option :value="30">30 días</option>
            <option :value="60">60 días</option>
            <option :value="90">90 días</option>
          </select>
        </label>
      </div>
      <p v-if="upcoming === null" class="text-sm text-muted-foreground">Cargando…</p>
      <p v-else-if="upcoming.length === 0" class="text-sm text-muted-foreground">Ningún preventivo vence en este periodo.</p>
      <ul v-else class="divide-y divide-border">
        <li v-for="item in upcoming" :key="item.maintenanceRecordId" class="flex flex-wrap items-center gap-x-4 gap-y-1 py-2.5 text-sm">
          <RouterLink :to="`/assets/${item.assetId}`" class="font-medium text-primary underline-offset-2 hover:underline">{{ item.assetInventoryCode }}</RouterLink>
          <span class="text-secondary">{{ formatDate(item.nextMaintenanceAt) }}</span>
          <span :class="['rounded-full px-2.5 py-0.5 text-xs font-semibold', daysUntil(item.nextMaintenanceAt) < 0 ? 'bg-destructive text-on-destructive' : daysUntil(item.nextMaintenanceAt) <= 7 ? 'bg-priority-high/10 text-priority-high' : 'bg-muted text-secondary']">
            {{ dueLabel(item.nextMaintenanceAt) }}
          </span>
          <span class="text-muted-foreground">último: {{ formatDate(item.performedAt) }}</span>
        </li>
      </ul>
    </section>

    <section aria-labelledby="bitacora" class="space-y-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 id="bitacora" class="font-display text-base font-semibold text-foreground">Bitácora</h2>
        <label for="type-filter" class="sr-only">Tipo</label>
        <select id="type-filter" v-model="typeFilter" class="min-h-[44px] cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm">
          <option value="">Todos los tipos</option>
          <option value="PREVENTIVO">Preventivo</option>
          <option value="CORRECTIVO">Correctivo</option>
        </select>
      </div>
      <p v-if="history && history.items.length === 0" class="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">Sin registros.</p>
      <div v-else-if="history" class="overflow-x-auto rounded-lg border border-border bg-card">
        <table class="w-full text-left text-sm">
          <thead>
            <tr class="border-b border-border bg-muted">
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Fecha</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Activo</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Tipo</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Qué se hizo</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Técnico</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in history.items" :key="item.maintenanceRecordId" class="border-b border-border last:border-0">
              <td class="px-4 py-3 text-secondary">{{ formatDate(item.performedAt) }}</td>
              <td class="px-4 py-3"><RouterLink :to="`/assets/${item.assetId}`" class="text-primary underline-offset-2 hover:underline">{{ item.assetInventoryCode }}</RouterLink></td>
              <td class="px-4 py-3 text-secondary">{{ item.typeName }}</td>
              <td class="max-w-md px-4 py-3 text-foreground">{{ item.description }}</td>
              <td class="px-4 py-3 text-secondary">{{ item.performedByName ?? '—' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <nav v-if="history && history.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de la bitácora">
        <p class="text-sm text-muted-foreground">Página {{ history.pageNumber }} de {{ history.totalPages }}</p>
        <div class="flex gap-2">
          <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
          <UiButton :disabled="page >= history.totalPages" @click="page++">Siguiente</UiButton>
        </div>
      </nav>
    </section>
  </div>
</template>
