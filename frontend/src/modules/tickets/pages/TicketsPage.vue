<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import NavIcon from '../../../shared/components/NavIcon.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatRelative } from '../../../shared/utils/format'
import { ticketsApi } from '../api'
import NewTicketModal from '../components/NewTicketModal.vue'
import PriorityBadge from '../components/PriorityBadge.vue'
import { categoryIcon, priorityStripe, statusClass, statusLabel, ticketNumber } from '../labels'
import type { PagedResult, TicketCatalogs, TicketFilters, TicketSummary } from '../types'

type Tab = { id: string; label: string; filters: TicketFilters }

const route = useRoute()
const router = useRouter()
const { session, hasPermission } = useAuth()

// Solo UX: el servidor ya devuelve únicamente los tickets propios a quien no atiende.
const isStaff = computed(() => hasPermission('tickets.manage'))

const tabs = computed<Tab[]>(() =>
  isStaff.value
    ? [
        { id: 'pendientes', label: 'Pendientes', filters: { onlyOpen: true } },
        { id: 'sin-asignar', label: 'Sin asignar', filters: { onlyOpen: true, unassigned: true } },
        { id: 'mios', label: 'Asignados a mí', filters: { onlyOpen: true, assignedTo: session.value?.userId } },
        { id: 'resueltos', label: 'Resueltos', filters: { status: 'RESUELTO' } },
        { id: 'todos', label: 'Todos', filters: {} },
      ]
    : [
        { id: 'abiertos', label: 'Abiertos', filters: { onlyOpen: true } },
        { id: 'resueltos', label: 'Por confirmar', filters: { status: 'RESUELTO' } },
        { id: 'todos', label: 'Todos', filters: {} },
      ],
)

const activeTab = ref(tabs.value[0].id)
const search = ref('')
const debouncedSearch = ref('')
const categoryFilter = ref('')
const page = ref(1)
const result = ref<PagedResult<TicketSummary> | null>(null)
const catalogs = ref<TicketCatalogs | null>(null)
const loading = ref(true)
const error = ref('')
const newOpen = ref(false)

// "Reportar problema" desde la ficha del activo llega con ?nuevo=1&assetId=N.
const presetAssetId = computed(() => {
  const value = Number(route.query.assetId)
  return Number.isInteger(value) && value > 0 ? value : null
})

onMounted(() => {
  ticketsApi.catalogs().then((c) => (catalogs.value = c)).catch(() => (catalogs.value = null))
  if (route.query.nuevo === '1') newOpen.value = true
})

function closeNew() {
  // El <dialog> nativo también avisa "close" al cerrarse por código: si ya se
  // cerró (p. ej. tras crear y navegar al detalle) no se toca la ruta.
  if (!newOpen.value) return
  newOpen.value = false
  // Quita ?nuevo y ?assetId para que recargar la página no reabra el formulario.
  if (route.query.nuevo) router.replace({ path: '/tickets' })
}

function onCreated(ticketId: number, photoFailed: boolean) {
  newOpen.value = false
  router.push({ path: `/tickets/${ticketId}`, query: photoFailed ? { aviso: 'foto' } : undefined })
}

let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (debouncedSearch.value = value.trim()), 200)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

watch([activeTab, search, categoryFilter], () => (page.value = 1))

const filters = computed<TicketFilters>(() => ({
  ...(tabs.value.find((tab) => tab.id === activeTab.value)?.filters ?? {}),
  categoryId: categoryFilter.value ? Number(categoryFilter.value) : undefined,
  search: debouncedSearch.value || undefined,
}))

// Solo cuenta la última petición (cambiar de pestaña rápido no mezcla resultados).
let requestId = 0
watch(
  [filters, page],
  () => {
    const current = ++requestId
    loading.value = true
    ticketsApi
      .list(filters.value, page.value)
      .then((list) => {
        if (current !== requestId) return
        error.value = ''
        result.value = list
      })
      .catch((err) => {
        if (current !== requestId) return
        error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los tickets.'
      })
      .finally(() => {
        if (current === requestId) loading.value = false
      })
  },
  { immediate: true },
)

const emptyText = computed(() => {
  if (debouncedSearch.value || categoryFilter.value) return 'Ningún ticket coincide con la búsqueda.'
  if (!isStaff.value) {
    return activeTab.value === 'abiertos'
      ? 'No tienes tickets abiertos. Si algo falla o necesitas ayuda, levanta uno.'
      : 'Aquí aparecen los tickets que levantes.'
  }
  return activeTab.value === 'sin-asignar' ? 'Todos los tickets pendientes ya tienen técnico.' : 'No hay tickets en esta vista.'
})
</script>

<template>
  <div class="space-y-5">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-semibold text-foreground">{{ isStaff ? 'Bandeja de tickets' : 'Mis tickets' }}</h1>
        <p class="mt-1 text-sm text-muted-foreground">
          {{ isStaff ? 'Lo urgente arriba; a igual prioridad, lo más antiguo primero.' : 'Pide ayuda a soporte técnico y sigue tu solicitud.' }}
        </p>
      </div>
      <UiButton variant="primary" @click="newOpen = true">Nuevo ticket</UiButton>
    </header>

    <div class="flex flex-wrap items-center gap-x-6 gap-y-3 border-b border-border">
      <nav class="-mb-px flex gap-1 overflow-x-auto" aria-label="Vistas de tickets">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          :aria-current="activeTab === tab.id ? 'page' : undefined"
          :class="[
            'min-h-[44px] shrink-0 cursor-pointer border-b-2 px-3 text-sm transition-colors duration-150',
            activeTab === tab.id ? 'border-accent font-semibold text-primary' : 'border-transparent text-muted-foreground hover:text-primary',
          ]"
          @click="activeTab = tab.id"
        >
          {{ tab.label }}
        </button>
      </nav>
      <div class="ml-auto flex w-full gap-2 pb-2 sm:w-auto">
        <label for="ticket-search" class="sr-only">Buscar por título</label>
        <input
          id="ticket-search"
          v-model="search"
          type="search"
          placeholder="Buscar por título"
          class="min-h-[44px] w-full min-w-0 rounded-md border border-border bg-background px-3 text-sm text-foreground sm:w-56"
        />
        <label for="ticket-category" class="sr-only">Tipo de ticket</label>
        <select
          id="ticket-category"
          v-model="categoryFilter"
          class="min-h-[44px] cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground"
        >
          <option value="">Todos los tipos</option>
          <option v-for="category in catalogs?.ticketCategories ?? []" :key="category.id" :value="String(category.id)">{{ category.name }}</option>
        </select>
      </div>
    </div>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="loading && !result" class="text-sm text-muted-foreground">Cargando tickets…</p>

    <div v-else-if="result && result.items.length === 0" class="rounded-lg border border-dashed border-border px-6 py-10 text-center">
      <p class="text-sm text-muted-foreground">{{ emptyText }}</p>
      <UiButton v-if="!isStaff && !debouncedSearch && !categoryFilter" variant="primary" class="mt-4" @click="newOpen = true">
        Levantar un ticket
      </UiButton>
    </div>

    <ul v-else-if="result" :class="['divide-y divide-border overflow-hidden rounded-lg border border-border bg-card', loading ? 'opacity-60' : '']">
      <li v-for="ticket in result.items" :key="ticket.ticketId" :class="['relative', priorityStripe(ticket.priorityCode)]">
        <RouterLink
          :to="`/tickets/${ticket.ticketId}`"
          :class="[
            'grid gap-x-4 gap-y-1.5 py-3.5 pl-5 pr-4 transition-colors duration-150 hover:bg-muted sm:grid-cols-[1fr_auto]',
            !isStaff && ticket.status === 'ESPERANDO_USUARIO' ? 'bg-priority-high/5' : '',
          ]"
        >
          <div class="flex min-w-0 items-start gap-3">
            <span class="mt-0.5 text-secondary"><NavIcon :paths="categoryIcon(ticket.categoryCode)" /></span>
            <div class="min-w-0">
              <p class="truncate font-medium text-foreground">{{ ticket.title }}</p>
              <p class="mt-0.5 text-xs text-muted-foreground">
                {{ ticketNumber(ticket.ticketId) }}, {{ ticket.categoryName }}<template v-if="ticket.assetInventoryCode">, equipo {{ ticket.assetInventoryCode }}</template>
              </p>
              <p v-if="!isStaff && ticket.status === 'ESPERANDO_USUARIO'" class="mt-1 text-sm font-medium text-priority-high">
                El técnico necesita tu respuesta
              </p>
            </div>
          </div>
          <div class="flex flex-wrap items-center gap-2 pl-8 sm:justify-end sm:pl-0">
            <PriorityBadge v-if="ticket.priorityCode === 'CRITICA' || ticket.priorityCode === 'ALTA'" :priority="ticket.priorityCode" :name="ticket.priorityName" />
            <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', statusClass(ticket.status)]">
              {{ statusLabel(ticket.status, !isStaff) }}
            </span>
          </div>
          <p class="pl-8 text-xs text-muted-foreground sm:col-span-2">
            <template v-if="isStaff">{{ ticket.requesterName }}, </template>levantado {{ formatRelative(ticket.createdAt) }}<template v-if="isStaff">, {{ ticket.assignedToUsername ? `atiende ${ticket.assignedToUsername}` : 'sin asignar' }}</template>
          </p>
        </RouterLink>
      </li>
    </ul>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de tickets">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>

    <NewTicketModal :open="newOpen" :catalogs="catalogs" :preset-asset-id="presetAssetId" @close="closeNew" @created="onCreated" />
  </div>
</template>
