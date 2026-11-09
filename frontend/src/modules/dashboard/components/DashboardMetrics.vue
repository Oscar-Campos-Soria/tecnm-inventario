<script setup lang="ts">
import { computed, ref } from 'vue'
import { ApiError } from '../../../shared/api/http'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDateTime } from '../../../shared/utils/format'
import { dashboardApi } from '../api'
import type { CountItem, DashboardSummary } from '../types'

const { hasPermission } = useAuth()
const summary = ref<DashboardSummary | null>(null)
const error = ref('')

dashboardApi
  .summary()
  .then((s) => (summary.value = s))
  .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar las métricas.'))

// Color por estado del activo; el texto de la leyenda lleva el dato (no solo el color).
const STATUS_COLOR: Record<string, string> = {
  DISPONIBLE: 'bg-success',
  ASIGNADO: 'bg-accent',
  EN_MANTENIMIENTO: 'bg-secondary',
  CON_FALLA: 'bg-destructive',
  DADO_DE_BAJA: 'bg-muted-foreground/50',
}
const STATUS_ORDER = ['DISPONIBLE', 'ASIGNADO', 'EN_MANTENIMIENTO', 'CON_FALLA', 'DADO_DE_BAJA']

const statuses = computed(() =>
  [...(summary.value?.assetsByStatus ?? [])].sort((a, b) => STATUS_ORDER.indexOf(a.code) - STATUS_ORDER.indexOf(b.code)),
)
const statusTotal = computed(() => statuses.value.reduce((n, s) => n + s.count, 0))
const openIncidents = computed(() => summary.value?.openIncidentsByPriority.reduce((n, p) => n + p.count, 0) ?? 0)

interface Attention {
  label: string
  detail: string
  count: number
  to: string | null
  urgent: boolean
}

// Lo que pide acción; el enlace solo aparece si el rol puede abrir esa pantalla.
const attention = computed<Attention[]>(() => {
  const s = summary.value
  if (!s) return []
  const priorities = s.openIncidentsByPriority.filter((p) => p.count > 0).map((p) => `${p.count} ${p.name.toLowerCase()}`)
  return [
    {
      label: 'Incidentes abiertos',
      detail: priorities.length ? `Prioridad: ${priorities.join(', ')}` : 'Ninguno pendiente',
      count: openIncidents.value,
      to: hasPermission('incidents.view') ? '/incidents' : null,
      urgent: s.openIncidentsByPriority.some((p) => (p.code === 'ALTA' || p.code === 'CRITICA') && p.count > 0),
    },
    {
      label: 'Preventivos vencidos',
      detail: `${s.preventiveDueSoon} más vencen en ${s.windowDays} días`,
      count: s.preventiveOverdue,
      to: hasPermission('maintenance.manage') ? '/maintenance' : null,
      urgent: s.preventiveOverdue > 0,
    },
    {
      label: 'Licencias por vencer',
      detail: s.licensesExpired ? `${s.licensesExpired} ya vencidas` : `En los próximos ${s.windowDays} días`,
      count: s.licensesExpiringSoon,
      to: hasPermission('software.manage') ? '/software?vencen=1' : null,
      urgent: s.licensesExpired > 0,
    },
  ]
})

const breakdowns = computed(() => [
  { title: 'Por tipo de equipo', items: summary.value?.assetsByType ?? [], empty: 'Sin equipos en servicio.' },
  { title: 'Ubicaciones con más equipos', items: summary.value?.assetsByLocation ?? [], empty: 'Ningún equipo tiene ubicación asignada.' },
])

function widthOf(item: CountItem, list: CountItem[]) {
  const max = Math.max(...list.map((x) => x.count), 1)
  return `${Math.max(4, Math.round((item.count / max) * 100))}%`
}
</script>

<template>
  <section aria-labelledby="metricas" class="space-y-4">
    <div class="flex flex-wrap items-baseline justify-between gap-2">
      <h2 id="metricas" class="font-display text-lg font-semibold text-foreground">Inventario</h2>
      <p v-if="summary" class="text-xs text-muted-foreground">Actualizado {{ formatDateTime(summary.generatedAt) }}</p>
    </div>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-else-if="!summary" class="text-sm text-muted-foreground">Cargando métricas…</p>

    <template v-else>
      <div class="rounded-xl border border-border bg-card p-5">
        <p class="font-display text-5xl font-semibold leading-none text-foreground tabular-nums">{{ summary.totalAssets }}</p>
        <p class="mt-1 text-sm text-muted-foreground">equipos en servicio</p>
        <div v-if="statusTotal" class="mt-5 flex h-3 overflow-hidden rounded-full bg-muted" aria-hidden="true">
          <div
            v-for="s in statuses"
            :key="s.code"
            :class="STATUS_COLOR[s.code] ?? 'bg-primary'"
            :style="{ width: `${(s.count / statusTotal) * 100}%` }"
          />
        </div>
        <ul class="mt-3 flex flex-wrap gap-x-5 gap-y-1.5 text-sm">
          <li v-for="s in statuses" :key="s.code" class="flex items-center gap-2">
            <span :class="['h-2.5 w-2.5 rounded-full', STATUS_COLOR[s.code] ?? 'bg-primary']" aria-hidden="true" />
            <span class="text-secondary">{{ s.name }}</span>
            <span class="font-semibold text-foreground tabular-nums">{{ s.count }}</span>
          </li>
        </ul>
      </div>

      <ul class="grid gap-3 sm:grid-cols-3">
        <li v-for="a in attention" :key="a.label">
          <component
            :is="a.to ? 'RouterLink' : 'div'"
            :to="a.to ?? undefined"
            :class="['flex h-full flex-col rounded-xl border p-4', a.urgent ? 'border-priority-high/50 bg-priority-high/5' : 'border-border bg-card', a.to ? 'transition-colors hover:border-accent' : '']"
          >
            <span class="text-sm font-medium text-secondary">{{ a.label }}</span>
            <span :class="['mt-1 font-display text-3xl font-semibold tabular-nums', a.urgent ? 'text-priority-high' : 'text-foreground']">{{ a.count }}</span>
            <span class="mt-1 text-xs text-muted-foreground">{{ a.detail }}</span>
          </component>
        </li>
      </ul>

      <div class="grid gap-3 lg:grid-cols-2">
        <div v-for="block in breakdowns" :key="block.title" class="rounded-xl border border-border bg-card p-5">
          <h3 class="mb-3 font-display text-base font-semibold text-foreground">{{ block.title }}</h3>
          <p v-if="block.items.length === 0" class="text-sm text-muted-foreground">{{ block.empty }}</p>
          <ul v-else class="space-y-2.5">
            <li v-for="item in block.items" :key="item.code" class="text-sm">
              <div class="flex justify-between gap-3">
                <span class="truncate text-foreground">{{ item.name }}</span>
                <span class="font-semibold text-foreground tabular-nums">{{ item.count }}</span>
              </div>
              <div class="mt-1 h-1.5 rounded-full bg-muted" aria-hidden="true">
                <div class="h-full rounded-full bg-primary" :style="{ width: widthOf(item, block.items) }" />
              </div>
            </li>
          </ul>
        </div>
      </div>
    </template>
  </section>
</template>
