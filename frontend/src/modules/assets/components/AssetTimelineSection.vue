<script setup lang="ts">
import { ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import AuditChanges from '../../../shared/components/AuditChanges.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDateTime } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { AuditLogItem } from '../types'

// Línea de tiempo del activo desde audit_log (history.view, solo ADMIN).
// Se carga al pedirla: puede traer hasta 200 eventos.
const props = defineProps<{ assetId: number }>()

const { hasPermission } = useAuth()
const events = ref<AuditLogItem[] | null>(null)
const error = ref('')
const loading = ref(false)

async function load() {
  loading.value = true
  error.value = ''
  try {
    events.value = await assetsApi.timeline(props.assetId)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo cargar la línea de tiempo.'
  } finally {
    loading.value = false
  }
}
watch(() => props.assetId, () => (events.value = null))

const TABLE_LABEL: Record<string, string> = {
  assets: 'Activo',
  asset_movements: 'Movimiento',
  asset_assignments: 'Asignación',
  incidents: 'Incidente',
  maintenance_records: 'Mantenimiento',
  asset_disposals: 'Baja',
  inventory_records: 'Levantamiento',
  asset_warranties: 'Garantía',
  tickets: 'Ticket',
}
const ACTION_LABEL: Record<string, string> = { INSERT: 'alta', UPDATE: 'cambio', DELETE: 'eliminación' }
</script>

<template>
  <section v-if="hasPermission('history.view')" aria-labelledby="linea-tiempo" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="linea-tiempo" class="font-display text-base font-semibold text-foreground">Línea de tiempo</h2>
      <UiButton :disabled="loading" @click="load">{{ loading ? 'Cargando…' : events ? 'Actualizar' : 'Ver línea de tiempo' }}</UiButton>
    </div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-else-if="events && events.length === 0" class="text-sm text-muted-foreground">Sin eventos registrados.</p>
    <ol v-else-if="events" class="relative space-y-3 border-l-2 border-border pl-4">
      <li v-for="e in events" :key="e.auditLogId" class="relative">
        <span class="absolute -left-[1.3rem] top-1.5 size-2.5 rounded-full bg-accent" aria-hidden="true" />
        <p class="text-sm">
          <span class="font-medium text-foreground">{{ TABLE_LABEL[e.tableName] ?? e.tableName }}: {{ ACTION_LABEL[e.action] ?? e.action }}</span>
          <span class="text-muted-foreground">, {{ e.performedByUsername ?? 'sistema' }}, {{ formatDateTime(e.performedAt) }}</span>
        </p>
        <AuditChanges class="mt-1" :old-data="e.oldData" :new-data="e.newData" />
      </li>
    </ol>
  </section>
</template>
