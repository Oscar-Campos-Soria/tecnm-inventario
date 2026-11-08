<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDate } from '../../../shared/utils/format'
import { disposalApi } from '../api'
import type { CatalogItem, DisposalFilters, DisposalSummary, PagedResult } from '../types'

const result = ref<PagedResult<DisposalSummary> | null>(null)
const reasons = ref<CatalogItem[]>([])
const page = ref(1)
const search = ref('')
const debouncedSearch = ref('')
const reasonFilter = ref('')
const error = ref('')

onMounted(() => {
  disposalApi.reasons().then((r) => (reasons.value = r)).catch(() => (reasons.value = []))
})

let timer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(timer)
  timer = setTimeout(() => (debouncedSearch.value = value.trim()), 200)
})
onBeforeUnmount(() => clearTimeout(timer))
watch([search, reasonFilter], () => (page.value = 1))

const filters = computed<DisposalFilters>(() => ({
  disposalReasonId: reasonFilter.value ? Number(reasonFilter.value) : undefined,
  search: debouncedSearch.value || undefined,
}))

let requestId = 0
watch(
  [filters, page],
  () => {
    const current = ++requestId
    disposalApi
      .list(filters.value, page.value)
      .then((list) => {
        if (current !== requestId) return
        error.value = ''
        result.value = list
      })
      .catch((err) => {
        if (current === requestId) error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar las bajas.'
      })
  },
  { immediate: true },
)
</script>

<template>
  <div class="space-y-4">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-semibold text-foreground">Bajas</h1>
        <p class="mt-1 text-sm text-muted-foreground">Para dar de baja un equipo, ábrelo desde Activos.</p>
      </div>
      <span v-if="result" class="text-sm text-muted-foreground">{{ result.totalCount }} {{ result.totalCount === 1 ? 'baja' : 'bajas' }}</span>
    </header>

    <div class="flex flex-wrap gap-2">
      <label for="disposal-search" class="sr-only">Buscar por folio o código</label>
      <input
        id="disposal-search"
        v-model="search"
        type="search"
        placeholder="Folio o código de inventario"
        class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-sm text-foreground sm:w-72"
      />
      <label for="disposal-reason" class="sr-only">Motivo</label>
      <select id="disposal-reason" v-model="reasonFilter" class="min-h-[44px] cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground">
        <option value="">Todos los motivos</option>
        <option v-for="r in reasons" :key="r.id" :value="String(r.id)">{{ r.name }}</option>
      </select>
    </div>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando bajas…</p>

    <div v-else-if="result && result.items.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="text-sm text-muted-foreground">{{ debouncedSearch || reasonFilter ? 'Ninguna baja coincide.' : 'Aún no hay activos dados de baja.' }}</p>
    </div>

    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Folio</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Activo</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Motivo</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Fecha</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Registró</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="d in result.items" :key="d.assetDisposalId" class="border-b border-border last:border-0">
            <td class="px-4 py-3 font-medium text-foreground">{{ d.folio }}</td>
            <td class="px-4 py-3">
              <RouterLink :to="`/assets/${d.assetId}`" class="text-primary underline-offset-2 hover:underline">{{ d.assetInventoryCode }}</RouterLink>
              <p class="text-xs text-muted-foreground">{{ d.assetTypeName }}</p>
            </td>
            <td class="px-4 py-3 text-secondary">{{ d.reasonName }}</td>
            <td class="px-4 py-3 text-secondary">{{ formatDate(d.disposedAt) }}</td>
            <td class="px-4 py-3 text-secondary">{{ d.createdByUsername ?? '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de bajas">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>
  </div>
</template>
