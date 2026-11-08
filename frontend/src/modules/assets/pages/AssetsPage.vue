<script setup lang="ts">
import { ref, watch } from 'vue'
import { assetsApi } from '../api'
import type { AssetSummary, PagedResult } from '../types'
import UiButton from '../../../shared/components/UiButton.vue'

const result = ref<PagedResult<AssetSummary> | null>(null)
const page = ref(1)
const error = ref('')

watch(
  page,
  (current) => {
    assetsApi
      .list(current)
      .then((list) => {
        error.value = ''
        result.value = list
      })
      .catch(() => (error.value = 'No se pudieron cargar los activos.'))
  },
  { immediate: true },
)
</script>

<template>
  <div class="space-y-4">
    <header class="flex items-center justify-between">
      <h1 class="font-display text-2xl font-semibold text-foreground">Activos</h1>
      <span v-if="result" class="text-sm text-muted-foreground">
        {{ result.totalCount }} activo{{ result.totalCount === 1 ? '' : 's' }}
      </span>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
      {{ error }}
    </p>

    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando activos…</p>

    <div v-if="result && result.items.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="font-medium text-foreground">Sin activos</p>
      <p class="mt-1 text-sm text-muted-foreground">Registra el primer activo del inventario.</p>
    </div>

    <div v-if="result && result.items.length > 0" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Código de inventario</th>
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
