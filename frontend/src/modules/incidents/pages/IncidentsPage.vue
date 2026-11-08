<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { can } from '../../../shared/utils/permissions'
import { formatDate } from '../../../shared/utils/format'
import { incidentsApi } from '../api'
import StatusChip from '../components/StatusChip.vue'
import type { IncidentCatalogs, IncidentFilters, IncidentSummary, PagedResult, UserItem } from '../types'

const { session } = useAuth()
const viewGranted = computed(() => can(session.value, 'view'))

const result = ref<PagedResult<IncidentSummary> | null>(null)
const catalogs = ref<IncidentCatalogs | null>(null)
const technicians = ref<UserItem[]>([])
const page = ref(1)
const loading = ref(true)
const error = ref('')

const search = ref('')
const debouncedSearch = ref('')
const statusFilter = ref('')
const priorityFilter = ref('')
const typeFilter = ref('')
const technicianFilter = ref('')
const onlyOpen = ref(false)

// Búsqueda con debounce: no disparar por cada tecla.
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (debouncedSearch.value = value.trim()), 200)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

// Cualquier filtro nuevo vuelve a la primera página.
watch([search, statusFilter, priorityFilter, typeFilter, technicianFilter, onlyOpen], () => (page.value = 1))

const filters = computed<IncidentFilters>(() => ({
  status: statusFilter.value || undefined,
  priority: priorityFilter.value || undefined,
  incidentTypeId: typeFilter.value ? Number(typeFilter.value) : undefined,
  assignedTo: technicianFilter.value ? Number(technicianFilter.value) : undefined,
  onlyOpen: onlyOpen.value || undefined,
  search: debouncedSearch.value || undefined,
}))

const hasFilters = computed(() =>
  Boolean(statusFilter.value || priorityFilter.value || typeFilter.value || technicianFilter.value || onlyOpen.value || debouncedSearch.value),
)

// Catálogos (desplegables de los filtros) y técnicos (filtro "asignado a"): una sola vez.
onMounted(() => {
  if (!viewGranted.value) return
  incidentsApi.catalogs().then((c) => (catalogs.value = c)).catch(() => (catalogs.value = null))
  incidentsApi.technicians().then((t) => (technicians.value = t)).catch(() => (technicians.value = []))
})

// Respuestas viejas se descartan: solo cuenta la última petición.
let requestId = 0
watch(
  [filters, page],
  () => {
    if (!viewGranted.value) return
    const current = ++requestId
    incidentsApi
      .list(filters.value, page.value)
      .then((listResult) => {
        if (current !== requestId) return
        error.value = ''
        result.value = listResult
      })
      .catch((err) => {
        if (current !== requestId) return
        error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los incidentes.'
      })
      .finally(() => {
        if (current === requestId) loading.value = false
      })
  },
  { immediate: true },
)

function clearFilters() {
  search.value = ''
  debouncedSearch.value = ''
  statusFilter.value = ''
  priorityFilter.value = ''
  typeFilter.value = ''
  technicianFilter.value = ''
  onlyOpen.value = false
  page.value = 1
}

const selectClass = 'min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground'
</script>

<template>
  <div v-if="!viewGranted" class="space-y-3">
    <h1 class="font-display text-2xl font-semibold text-foreground">Incidentes</h1>
    <p role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
      No tienes permiso para ver el módulo de incidentes.
    </p>
  </div>

  <div v-else class="space-y-4">
    <header class="flex items-center justify-between">
      <h1 class="font-display text-2xl font-semibold text-foreground">Incidentes</h1>
      <span v-if="result" role="status" class="text-sm text-muted-foreground">
        {{ result.totalCount }} incidente{{ result.totalCount === 1 ? '' : 's' }}
      </span>
    </header>

    <form role="search" aria-label="Filtros de incidentes" class="rounded-lg border border-border bg-card p-4" @submit.prevent>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <label for="incident-search" class="mb-1 block text-sm font-medium text-foreground">Buscar</label>
          <input
            id="incident-search"
            v-model="search"
            type="search"
            placeholder="Título del incidente"
            class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
          />
        </div>
        <div>
          <label for="status-filter" class="mb-1 block text-sm font-medium text-foreground">Estado</label>
          <select id="status-filter" v-model="statusFilter" :class="selectClass">
            <option value="">Todos</option>
            <option v-for="status in catalogs?.incidentStatuses ?? []" :key="status.id" :value="status.code">{{ status.name }}</option>
          </select>
        </div>
        <div>
          <label for="priority-filter" class="mb-1 block text-sm font-medium text-foreground">Prioridad</label>
          <select id="priority-filter" v-model="priorityFilter" :class="selectClass">
            <option value="">Todas</option>
            <option v-for="priority in catalogs?.incidentPriorities ?? []" :key="priority.id" :value="priority.code">{{ priority.name }}</option>
          </select>
        </div>
        <div>
          <label for="type-filter" class="mb-1 block text-sm font-medium text-foreground">Tipo de incidente</label>
          <select id="type-filter" v-model="typeFilter" :class="selectClass">
            <option value="">Todos</option>
            <option v-for="type in catalogs?.incidentTypes ?? []" :key="type.id" :value="String(type.id)">{{ type.name }}</option>
          </select>
        </div>
        <div>
          <label for="technician-filter" class="mb-1 block text-sm font-medium text-foreground">Técnico asignado</label>
          <select id="technician-filter" v-model="technicianFilter" :class="selectClass">
            <option value="">Todos</option>
            <option v-for="technician in technicians" :key="technician.userId" :value="String(technician.userId)">
              {{ technician.firstName }} {{ technician.lastName }} ({{ technician.username }})
            </option>
          </select>
        </div>
        <div class="flex items-end">
          <label class="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm font-medium text-foreground">
            <input v-model="onlyOpen" type="checkbox" class="h-4 w-4 cursor-pointer" />
            Solo abiertos
          </label>
        </div>
      </div>
      <UiButton v-if="hasFilters" class="mt-3" @click="clearFilters">Limpiar filtros</UiButton>
    </form>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="loading && !result" class="text-sm text-muted-foreground">Cargando incidentes…</p>

    <div v-if="!loading && !error && result && result.items.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="font-medium text-foreground">{{ hasFilters ? 'Sin resultados' : 'Sin incidentes' }}</p>
      <p class="mt-1 text-sm text-muted-foreground">
        {{ hasFilters ? 'Prueba con otros filtros.' : 'Los incidentes reportados sobre activos aparecen aquí.' }}
      </p>
    </div>

    <div v-if="result && result.items.length > 0" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Incidente</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Activo</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Prioridad</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Estado</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Reportado por</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Asignado a</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Fecha</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="incident in result.items" :key="incident.incidentId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <RouterLink :to="`/incidents/${incident.incidentId}`" class="font-medium text-primary underline-offset-2 hover:underline">
                {{ incident.title }}
              </RouterLink>
              <p class="text-xs text-muted-foreground">{{ incident.typeName }}</p>
            </td>
            <td class="px-4 py-3">
              <RouterLink :to="`/assets/${incident.assetId}`" class="text-primary underline-offset-2 hover:underline">
                {{ incident.assetInventoryCode }}
              </RouterLink>
            </td>
            <td class="px-4 py-3 text-secondary">{{ incident.priorityName }}</td>
            <td class="px-4 py-3"><StatusChip :status-code="incident.statusCode" :status-name="incident.statusName" /></td>
            <td class="px-4 py-3 text-secondary">{{ incident.reportedByUsername }}</td>
            <td class="px-4 py-3 text-secondary">{{ incident.assignedToUsername ?? '—' }}</td>
            <td class="px-4 py-3 text-secondary">{{ formatDate(incident.reportedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de incidentes">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>
  </div>
</template>
