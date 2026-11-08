<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { assetsApi } from '../api'
import type { AssetFilters, AssetSummary, GetCatalogsResponse, LocationItem, PagedResult } from '../types'

const result = ref<PagedResult<AssetSummary> | null>(null)
const catalogs = ref<GetCatalogsResponse | null>(null)
const locations = ref<LocationItem[]>([])
const page = ref(1)
const error = ref('')

const search = ref('')
const debouncedSearch = ref('')
const status = ref('')
const typeId = ref('')
const locationId = ref('')
const includeDisposed = ref(false)

onMounted(() => {
  assetsApi.catalogs().then((c) => (catalogs.value = c)).catch(() => (catalogs.value = null))
  assetsApi.locations().then((l) => (locations.value = l)).catch(() => (locations.value = []))
})

let timer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(timer)
  timer = setTimeout(() => (debouncedSearch.value = value.trim()), 250)
})
onBeforeUnmount(() => clearTimeout(timer))
watch([search, status, typeId, locationId, includeDisposed], () => (page.value = 1))

const filters = computed<AssetFilters>(() => ({
  search: debouncedSearch.value || undefined,
  status: status.value || undefined,
  assetTypeId: typeId.value ? Number(typeId.value) : undefined,
  locationId: locationId.value ? Number(locationId.value) : undefined,
  includeDisposed: includeDisposed.value || undefined,
}))
const hasFilters = computed(() => Object.values(filters.value).some((v) => v !== undefined))

let requestId = 0
watch(
  [filters, page],
  () => {
    const current = ++requestId
    assetsApi
      .list(page.value, 20, filters.value)
      .then((list) => {
        if (current !== requestId) return
        error.value = ''
        result.value = list
      })
      .catch(() => {
        if (current === requestId) error.value = 'No se pudieron cargar los activos.'
      })
  },
  { immediate: true },
)

function clearFilters() {
  search.value = ''
  debouncedSearch.value = ''
  status.value = ''
  typeId.value = ''
  locationId.value = ''
  includeDisposed.value = false
}

const STATUS_CHIP: Record<string, string> = {
  DISPONIBLE: 'bg-success/10 text-success',
  CON_FALLA: 'bg-destructive/10 text-destructive',
  DADO_DE_BAJA: 'bg-foreground/10 text-foreground',
}

const selectClass = 'min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground'
</script>

<template>
  <div class="space-y-4">
    <header class="flex items-center justify-between">
      <h1 class="font-display text-2xl font-semibold text-foreground">Activos</h1>
      <span v-if="result" class="text-sm text-muted-foreground">
        {{ result.totalCount }} activo{{ result.totalCount === 1 ? '' : 's' }}
      </span>
    </header>

    <form role="search" aria-label="Filtros de activos" class="rounded-lg border border-border bg-card p-4" @submit.prevent>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div class="sm:col-span-2 lg:col-span-1">
          <label for="asset-search" class="mb-1 block text-sm font-medium text-foreground">Buscar</label>
          <input
            id="asset-search"
            v-model="search"
            type="search"
            placeholder="Código, serie o hostname"
            class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-sm text-foreground"
          />
        </div>
        <div>
          <label for="asset-status" class="mb-1 block text-sm font-medium text-foreground">Estado</label>
          <select id="asset-status" v-model="status" :class="selectClass">
            <option value="">Todos</option>
            <option v-for="s in catalogs?.assetStatuses ?? []" :key="s.id" :value="s.code">{{ s.name }}</option>
          </select>
        </div>
        <div>
          <label for="asset-type" class="mb-1 block text-sm font-medium text-foreground">Tipo</label>
          <select id="asset-type" v-model="typeId" :class="selectClass">
            <option value="">Todos</option>
            <option v-for="t in catalogs?.assetTypes ?? []" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
          </select>
        </div>
        <div>
          <label for="asset-location" class="mb-1 block text-sm font-medium text-foreground">Ubicación</label>
          <select id="asset-location" v-model="locationId" :class="selectClass">
            <option value="">Todas</option>
            <option v-for="l in locations" :key="l.locationId" :value="String(l.locationId)">{{ l.name }} ({{ l.code }})</option>
          </select>
        </div>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <label class="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm text-foreground">
          <input v-model="includeDisposed" type="checkbox" class="h-4 w-4 cursor-pointer" :disabled="status !== ''" />
          Incluir dados de baja
        </label>
        <UiButton v-if="hasFilters" @click="clearFilters">Limpiar filtros</UiButton>
      </div>
    </form>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando activos…</p>

    <div v-if="result && result.items.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="font-medium text-foreground">{{ hasFilters ? 'Sin resultados' : 'Sin activos' }}</p>
      <p class="mt-1 text-sm text-muted-foreground">
        {{ hasFilters ? 'Prueba con otros filtros.' : 'Registra el primer activo del inventario.' }}
      </p>
    </div>

    <div v-if="result && result.items.length > 0" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Código de inventario</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Tipo</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Estado</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Ubicación</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Serie</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Hostname</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="asset in result.items" :key="asset.assetId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <RouterLink :to="`/assets/${asset.assetId}`" class="font-medium text-primary underline-offset-2 hover:underline">
                {{ asset.inventoryCode }}
              </RouterLink>
            </td>
            <td class="px-4 py-3 text-secondary">{{ asset.assetTypeName }}</td>
            <td class="px-4 py-3">
              <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', STATUS_CHIP[asset.statusCode] ?? 'bg-muted text-secondary']">
                {{ asset.statusName }}
              </span>
            </td>
            <td class="px-4 py-3 text-secondary">{{ asset.locationName ?? '—' }}</td>
            <td class="px-4 py-3 text-secondary">{{ asset.serialNumber ?? '—' }}</td>
            <td class="px-4 py-3 text-secondary">{{ asset.hostname ?? '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de activos">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>
  </div>
</template>
