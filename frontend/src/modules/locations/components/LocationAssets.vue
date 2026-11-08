<script setup lang="ts">
import { ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { locationsApi } from '../api'
import type { LocationAssetItem, PagedResult } from '../types'

// Activos en la ubicación y sus sub-ubicaciones (GET /api/assets?locationId=, assets.view).
const props = defineProps<{ locationId: number }>()

const result = ref<PagedResult<LocationAssetItem> | null>(null)
const page = ref(1)
const error = ref('')

watch(() => props.locationId, () => (page.value = 1))
watch(
  [() => props.locationId, page],
  () => {
    locationsApi
      .assets(props.locationId, page.value)
      .then((r) => {
        error.value = ''
        result.value = r
      })
      .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los activos.'))
  },
  { immediate: true },
)
</script>

<template>
  <section aria-labelledby="activos-ubicacion" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex items-baseline justify-between gap-2">
      <h2 id="activos-ubicacion" class="font-display text-base font-semibold text-foreground">Activos aquí</h2>
      <span v-if="result" class="text-sm text-muted-foreground">{{ result.totalCount }} (incluye sub-ubicaciones)</span>
    </div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-else-if="!result" class="text-sm text-muted-foreground">Cargando…</p>
    <p v-else-if="result.items.length === 0" class="text-sm text-muted-foreground">No hay activos registrados en esta ubicación.</p>
    <ul v-else class="divide-y divide-border">
      <li v-for="a in result.items" :key="a.assetId" class="flex flex-wrap items-center gap-x-4 gap-y-1 py-2 text-sm">
        <RouterLink :to="`/assets/${a.assetId}`" class="font-medium text-primary underline-offset-2 hover:underline">{{ a.inventoryCode }}</RouterLink>
        <span class="text-secondary">{{ a.assetTypeName }}</span>
        <span class="text-secondary">{{ a.statusName }}</span>
        <span class="text-muted-foreground">{{ a.locationName }}</span>
      </li>
    </ul>
    <nav v-if="result && result.totalPages > 1" class="mt-3 flex items-center gap-2" aria-label="Paginación de activos de la ubicación">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <span class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</span>
      <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
    </nav>
  </section>
</template>
