<script setup lang="ts">
import { computed, ref } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiLoading from '../../../shared/components/UiLoading.vue'
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

// En el orden del ciclo de vida, con el mismo color y símbolo que en el resto de la app. ink: texto sobre el relleno.
const STATES = [
  { code: 'DISPONIBLE', name: 'Disponible', chip: 'chip-success', fill: 'bg-state-available', ink: 'text-foreground', help: 'Listos para asignarse' },
  { code: 'ASIGNADO', name: 'Asignado', chip: 'chip-info', fill: 'bg-state-assigned', ink: 'text-white', help: 'A cargo de alguien' },
  { code: 'EN_MANTENIMIENTO', name: 'En mantenimiento', chip: 'chip-warning', fill: 'bg-state-maintenance', ink: 'text-foreground', help: 'En reparación' },
  { code: 'CON_FALLA', name: 'Con falla', chip: 'chip-danger', fill: 'bg-state-fault', ink: 'text-white', help: 'Esperan revisión' },
]

const states = computed(() =>
  STATES.map((state) => {
    const item = summary.value?.assetsByStatus.find((s) => s.code === state.code)
    return { ...state, count: item?.count ?? 0 }
  }),
)
const disposed = computed(() => summary.value?.assetsByStatus.find((s) => s.code === 'DADO_DE_BAJA')?.count ?? 0)
const canOpenAssets = computed(() => hasPermission('assets.view'))
const highlighted = ref<string | null>(null)

// Lo que dice la banda, en una frase.
const headline = computed(() => {
  const total = summary.value?.totalAssets ?? 0
  const down = states.value.filter((s) => s.code === 'CON_FALLA' || s.code === 'EN_MANTENIMIENTO').reduce((n, s) => n + s.count, 0)
  if (total === 0) return 'Aún no hay equipos en servicio.'
  const inService = `${total} ${total === 1 ? 'equipo' : 'equipos'} en servicio`
  return down === 0 ? `${inService}, ninguno detenido.` : `${inService}, ${down} ${down === 1 ? 'detenido' : 'detenidos'} por falla o reparación.`
})

interface Attention {
  label: string
  detail: string
  count: number
  to: string | null
  level: 'urgent' | 'pending' | 'clear'
}

// Lo que pide acción; el enlace solo aparece si el rol puede abrir esa pantalla.
const attention = computed<Attention[]>(() => {
  const s = summary.value
  if (!s) return []
  const open = s.openIncidentsByPriority.filter((p) => p.count > 0)
  const openCount = open.reduce((n, p) => n + p.count, 0)
  const level = (urgent: boolean, count: number): Attention['level'] => (urgent ? 'urgent' : count > 0 ? 'pending' : 'clear')
  return [
    {
      label: 'Incidentes abiertos',
      detail: open.length ? `Prioridad: ${open.map((p) => `${p.count} ${p.name.toLowerCase()}`).join(', ')}` : 'Ninguno pendiente',
      count: openCount,
      to: hasPermission('incidents.view') ? '/incidents' : null,
      level: level(open.some((p) => p.code === 'ALTA' || p.code === 'CRITICA'), openCount),
    },
    {
      label: 'Preventivos vencidos',
      detail: `${s.preventiveDueSoon} más vencen en ${s.windowDays} días`,
      count: s.preventiveOverdue,
      to: hasPermission('maintenance.manage') ? '/maintenance' : null,
      level: level(s.preventiveOverdue > 0, s.preventiveDueSoon),
    },
    {
      label: 'Licencias por vencer',
      detail: s.licensesExpired ? `${s.licensesExpired} ya vencidas` : `En los próximos ${s.windowDays} días`,
      count: s.licensesExpiringSoon,
      to: hasPermission('software.manage') ? '/software?vencen=1' : null,
      level: level(s.licensesExpired > 0, s.licensesExpiringSoon),
    },
  ]
})

const LEVEL = {
  urgent: { chip: 'chip-danger', text: 'Atender hoy' },
  pending: { chip: 'chip-warning', text: 'Pendiente' },
  clear: { chip: 'chip-success', text: 'Al día' },
}

const breakdowns = computed(() => [
  { title: 'Por tipo de equipo', items: summary.value?.assetsByType ?? [], empty: 'Sin equipos en servicio.' },
  { title: 'Ubicaciones con más equipos', items: summary.value?.assetsByLocation ?? [], empty: 'Ningún equipo tiene ubicación asignada.' },
])

function widthOf(item: CountItem, list: CountItem[]) {
  const max = Math.max(...list.map((x) => x.count), 1)
  return `${Math.max(2, Math.round((item.count / max) * 100))}%`
}
</script>

<template>
  <div class="space-y-10">
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <UiLoading v-else-if="!summary">Cargando métricas…</UiLoading>

    <section v-else aria-labelledby="estado-parque">
      <div class="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
        <h2 id="estado-parque" class="font-display text-xl font-semibold text-foreground">{{ headline }}</h2>
        <p class="text-xs text-muted-foreground">Actualizado {{ formatDateTime(summary.generatedAt) }}</p>
      </div>

      <!-- Banda: proporción por estado. El dato completo está en la lista de abajo. -->
      <div v-if="summary.totalAssets" class="band-reveal mt-4 flex h-16 gap-0.5" aria-hidden="true">
        <div
          v-for="s in states.filter((x) => x.count > 0)"
          :key="s.code"
          :class="[
            'flex min-w-9 items-center justify-center font-display text-2xl font-bold tabular-nums transition-opacity duration-150 first:rounded-l-lg last:rounded-r-lg',
            s.fill,
            s.ink,
            highlighted && highlighted !== s.code ? 'opacity-35' : '',
          ]"
          :style="{ flexGrow: s.count, flexBasis: 0 }"
          @pointerenter="highlighted = s.code"
          @pointerleave="highlighted = null"
        >
          {{ s.count }}
        </div>
      </div>

      <ul class="mt-3 grid gap-x-4 gap-y-1 sm:grid-cols-2 lg:grid-cols-4">
        <li v-for="s in states" :key="s.code">
          <component
            :is="canOpenAssets ? 'RouterLink' : 'div'"
            :to="canOpenAssets ? `/assets?status=${s.code}` : undefined"
            :class="[
              'flex items-start gap-3 rounded-lg p-2.5 transition-colors duration-150',
              highlighted === s.code ? 'bg-muted' : '',
              canOpenAssets ? 'hover:bg-muted' : '',
            ]"
            @pointerenter="highlighted = s.code"
            @pointerleave="highlighted = null"
            @focus="highlighted = s.code"
            @blur="highlighted = null"
          >
            <span class="min-w-8 font-display text-2xl font-bold leading-none text-foreground tabular-nums">{{ s.count }}</span>
            <span class="min-w-0">
              <span :class="['chip', s.chip]">{{ s.name }}</span>
              <span class="mt-1 block text-xs text-muted-foreground">{{ s.help }}</span>
            </span>
          </component>
        </li>
      </ul>
      <p v-if="disposed" class="mt-1 px-2.5 text-xs text-muted-foreground">
        Además, {{ disposed }} {{ disposed === 1 ? 'equipo dado de baja' : 'equipos dados de baja' }}, fuera de esta cuenta.
      </p>
    </section>

    <div :class="['grid gap-x-10 gap-y-8', summary ? 'lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]' : '']">
      <section v-if="summary" aria-labelledby="atencion">
        <h2 id="atencion" class="font-display text-lg font-semibold text-foreground">Requiere atención</h2>
        <ul class="mt-3 divide-y divide-border border-y border-border">
          <li v-for="a in attention" :key="a.label">
            <component
              :is="a.to ? 'RouterLink' : 'div'"
              :to="a.to ?? undefined"
              :class="['flex min-h-[72px] items-center gap-4 px-2 py-3', a.to ? 'transition-colors duration-150 hover:bg-muted' : '']"
            >
              <span class="w-10 text-right font-display text-3xl font-bold leading-none text-foreground tabular-nums">{{ a.count }}</span>
              <span class="min-w-0 flex-1">
                <span class="block font-medium text-foreground">{{ a.label }}</span>
                <span class="block text-sm text-muted-foreground">{{ a.detail }}</span>
              </span>
              <span :class="['chip shrink-0', LEVEL[a.level].chip]">{{ LEVEL[a.level].text }}</span>
            </component>
          </li>
        </ul>
      </section>

      <slot />
    </div>

    <div v-if="summary" class="grid gap-x-10 gap-y-8 lg:grid-cols-2">
      <section v-for="block in breakdowns" :key="block.title">
        <h2 class="mb-3 font-display text-lg font-semibold text-foreground">{{ block.title }}</h2>
        <p v-if="block.items.length === 0" class="text-sm text-muted-foreground">{{ block.empty }}</p>
        <ul v-else class="space-y-2 text-sm">
          <li v-for="(item, i) in block.items" :key="item.code" class="group grid grid-cols-[minmax(0,10rem)_minmax(0,1fr)] items-center gap-3">
            <span class="truncate text-foreground" :title="item.name">{{ item.name }}</span>
            <span class="flex items-center gap-2 pr-8">
              <span
                class="bar-grow h-5 rounded-r bg-primary transition-colors duration-150 group-hover:bg-secondary"
                :style="{ width: widthOf(item, block.items), '--i': i }"
                aria-hidden="true"
              />
              <span class="font-semibold text-foreground tabular-nums">{{ item.count }}</span>
            </span>
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>
