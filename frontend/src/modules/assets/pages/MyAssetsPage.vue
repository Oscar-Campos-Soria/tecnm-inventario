<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDate, formatRelative } from '../../../shared/utils/format'
import { statusClass, statusLabel } from '../../tickets/labels'
import { assetsApi } from '../api'
import type { MyAsset } from '../types'

// "Mis equipos": lo que el usuario tiene a su cargo (asignaciones vigentes). El servidor
// devuelve solo lo suyo; desde aquí reporta fallas de su equipo sin ver el inventario completo.
const STATUS_CHIP: Record<string, string> = {
  DISPONIBLE: 'bg-success/10 text-success',
  ASIGNADO: 'bg-accent/10 text-accent',
  CON_FALLA: 'bg-destructive/10 text-destructive',
  EN_MANTENIMIENTO: 'bg-priority-high/10 text-priority-high',
}

const assets = ref<MyAsset[]>([])
const loading = ref(true)
const error = ref('')

onMounted(async () => {
  try {
    assets.value = await assetsApi.mine()
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'No se pudieron cargar tus equipos. Recarga la página.'
  } finally {
    loading.value = false
  }
})

function title(asset: MyAsset): string {
  return [asset.assetTypeName, asset.manufacturerName, asset.modelName].filter(Boolean).join(' ')
}
</script>

<template>
  <div class="space-y-5">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Mis equipos</h1>
      <p class="mt-1 text-sm text-muted-foreground">Los equipos del instituto que tienes a tu cargo. Si alguno falla, repórtalo desde aquí.</p>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="loading" class="text-sm text-muted-foreground">Cargando tus equipos…</p>

    <div v-else-if="!error && assets.length === 0" class="rounded-lg border border-dashed border-border px-6 py-10 text-center">
      <p class="font-medium text-foreground">No tienes equipos asignados.</p>
      <p class="mx-auto mt-1 max-w-prose text-sm text-muted-foreground">
        Si usas un equipo del instituto, pide al área de cómputo que te lo asigne. Para cualquier otro problema, levanta un ticket.
      </p>
      <UiButton variant="primary" class="mt-4" @click="$router.push('/tickets?nuevo=1')">Levantar un ticket</UiButton>
    </div>

    <ul v-else class="grid gap-4 lg:grid-cols-2">
      <li v-for="asset in assets" :key="asset.assetId">
        <article
          :aria-labelledby="`asset-${asset.assetId}`"
          class="flex h-full flex-col rounded-lg border border-border bg-card p-5"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0">
              <h2 :id="`asset-${asset.assetId}`" class="text-lg font-semibold text-foreground">{{ title(asset) }}</h2>
              <p class="mt-0.5 text-sm text-muted-foreground">
                {{ asset.inventoryCode }}<template v-if="asset.serialNumber">, serie {{ asset.serialNumber }}</template>
              </p>
            </div>
            <span :class="['shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium', STATUS_CHIP[asset.statusCode] ?? 'bg-muted text-secondary']">
              {{ asset.statusName }}
            </span>
          </div>

          <dl class="mt-4 grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
            <div>
              <dt class="text-muted-foreground">Ubicación</dt>
              <dd class="text-foreground">{{ asset.locationName ?? 'Sin ubicación registrada' }}</dd>
            </div>
            <div>
              <dt class="text-muted-foreground">A tu cargo desde</dt>
              <dd class="text-foreground">{{ formatDate(asset.assignedAt) }} <span class="text-muted-foreground">({{ asset.assignmentTypeName.toLowerCase() }})</span></dd>
            </div>
          </dl>

          <div class="mt-4 flex-1 border-t border-border pt-3">
            <h3 class="text-sm font-medium text-foreground">Tus reportes de este equipo</h3>
            <p v-if="asset.tickets.length === 0" class="mt-1 text-sm text-muted-foreground">Aún no has reportado problemas de este equipo.</p>
            <ul v-else class="mt-1.5 space-y-1">
              <li v-for="ticket in asset.tickets" :key="ticket.ticketId">
                <RouterLink
                  :to="`/tickets/${ticket.ticketId}`"
                  class="flex min-h-[44px] items-center justify-between gap-3 rounded-md px-2 text-sm text-foreground hover:bg-muted"
                >
                  <span class="min-w-0 truncate">{{ ticket.title }}</span>
                  <span class="flex shrink-0 items-center gap-2">
                    <span class="text-xs text-muted-foreground">{{ formatRelative(ticket.createdAt) }}</span>
                    <span :class="['rounded-full px-2 py-0.5 text-xs font-medium', statusClass(ticket.status)]">{{ statusLabel(ticket.status, true) }}</span>
                  </span>
                </RouterLink>
              </li>
            </ul>
          </div>

          <UiButton variant="primary" class="mt-4 self-start" @click="$router.push(`/tickets?nuevo=1&assetId=${asset.assetId}`)">
            Reportar problema
          </UiButton>
        </article>
      </li>
    </ul>
  </div>
</template>
