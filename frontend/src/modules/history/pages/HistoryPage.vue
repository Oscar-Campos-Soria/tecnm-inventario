<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import AuditChanges from '../../../shared/components/AuditChanges.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDateTime } from '../../../shared/utils/format'
import { historyApi } from '../api'
import type { AuditFilters, AuditLogItem, PagedResult } from '../types'

const result = ref<PagedResult<AuditLogItem> | null>(null)
const tables = ref<string[]>([])
const page = ref(1)
const table = ref('')
const recordId = ref('')
const userId = ref('')
const from = ref('')
const to = ref('')
const error = ref('')

onMounted(() => {
  historyApi.tables().then((t) => (tables.value = t)).catch(() => (tables.value = []))
})

// Fechas locales del input -> ISO; "hasta" incluye el día completo.
const filters = computed<AuditFilters>(() => ({
  table: table.value || undefined,
  recordId: /^\d+$/.test(recordId.value) ? Number(recordId.value) : undefined,
  userId: /^\d+$/.test(userId.value) ? Number(userId.value) : undefined,
  from: from.value ? new Date(`${from.value}T00:00:00`).toISOString() : undefined,
  to: to.value ? new Date(`${to.value}T23:59:59.999`).toISOString() : undefined,
}))
watch(filters, () => (page.value = 1))

function load() {
  historyApi
    .list(filters.value, page.value)
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudo cargar el historial.'))
}
watch([filters, page], load, { immediate: true })

const ACTION_CHIP: Record<string, string> = {
  INSERT: 'bg-success/10 text-success',
  UPDATE: 'bg-muted text-accent',
  DELETE: 'bg-destructive/10 text-destructive',
}
const ACTION_LABEL: Record<string, string> = { INSERT: 'Alta', UPDATE: 'Cambio', DELETE: 'Baja' }
const input = 'min-h-[44px] rounded-md border border-border bg-background px-2.5 text-sm text-foreground'
</script>

<template>
  <div class="space-y-4">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Historial</h1>
      <p class="mt-1 text-sm text-muted-foreground">Bitácora de todos los cambios: quién, cuándo y qué cambió.</p>
    </header>

    <div class="flex flex-wrap gap-2">
      <label class="sr-only" for="h-table">Tabla</label>
      <select id="h-table" v-model="table" :class="[input, 'cursor-pointer']">
        <option value="">Todas las tablas</option>
        <option v-for="t in tables" :key="t" :value="t">{{ t }}</option>
      </select>
      <label class="sr-only" for="h-record">Id de registro</label>
      <input id="h-record" v-model="recordId" inputmode="numeric" placeholder="Id de registro" :class="[input, 'w-36']" />
      <label class="sr-only" for="h-user">Id de usuario</label>
      <input id="h-user" v-model="userId" inputmode="numeric" placeholder="Id de usuario" :class="[input, 'w-36']" />
      <label class="flex items-center gap-1.5 text-sm text-secondary" for="h-from">Desde <input id="h-from" v-model="from" type="date" :class="input" /></label>
      <label class="flex items-center gap-1.5 text-sm text-secondary" for="h-to">Hasta <input id="h-to" v-model="to" type="date" :class="input" /></label>
    </div>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando historial…</p>
    <p v-else-if="result && result.items.length === 0" class="text-sm text-muted-foreground">Sin movimientos con esos filtros.</p>

    <ol v-else-if="result" class="space-y-2">
      <li v-for="item in result.items" :key="item.auditLogId" class="rounded-lg border border-border bg-card px-4 py-3">
        <div class="mb-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', ACTION_CHIP[item.action]]">{{ ACTION_LABEL[item.action] ?? item.action }}</span>
          <span class="font-medium text-foreground">{{ item.tableName }} #{{ item.recordId }}</span>
          <span class="text-muted-foreground">{{ item.performedByUsername ?? 'sistema' }}, {{ formatDateTime(item.performedAt) }}</span>
        </div>
        <AuditChanges :old-data="item.oldData" :new-data="item.newData" />
      </li>
    </ol>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación del historial">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }} ({{ result.totalCount }} eventos)</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>
  </div>
</template>
