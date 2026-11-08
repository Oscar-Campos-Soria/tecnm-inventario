<script setup lang="ts">
import { onMounted, ref, useId, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { formatDate } from '../../../shared/utils/format'
import { inventoryApi } from '../api'
import { SESSION_LABELS } from '../labels'
import type { LocationOption, PagedResult, SessionSummary } from '../types'

const router = useRouter()
const nameId = useId()
const locationId = useId()

const result = ref<PagedResult<SessionSummary> | null>(null)
const page = ref(1)
const error = ref('')
const open = ref(false)
const locations = ref<LocationOption[]>([])
const form = ref({ name: '', locationId: '' })
const saving = ref(false)
const formError = ref('')

watch(
  page,
  (p) => {
    inventoryApi
      .list(p)
      .then((r) => (result.value = r))
      .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los conteos.'))
  },
  { immediate: true },
)

onMounted(() => {
  inventoryApi.locations().then((l) => (locations.value = l)).catch(() => (locations.value = []))
})

function openForm() {
  const month = new Intl.DateTimeFormat('es-MX', { month: 'long', year: 'numeric' }).format(new Date())
  form.value = { name: `Conteo ${month}`, locationId: '' }
  formError.value = ''
  open.value = true
}

async function create() {
  formError.value = ''
  if (!form.value.name.trim() || !form.value.locationId) {
    formError.value = 'Ponle nombre y elige qué área se va a contar.'
    return
  }
  saving.value = true
  try {
    const created = await inventoryApi.create(form.value.name.trim(), Number(form.value.locationId))
    open.value = false
    await router.push(`/inventory/${created.inventorySessionId}`)
  } catch (err) {
    formError.value = err instanceof ApiError ? err.message : 'No se pudo abrir el conteo.'
  } finally {
    saving.value = false
  }
}

const fieldClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <div class="space-y-4">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-semibold text-foreground">Levantamiento físico</h1>
        <p class="mt-1 text-sm text-muted-foreground">Recorre un área, escanea cada equipo y concilia las diferencias al cerrar.</p>
      </div>
      <UiButton variant="primary" @click="openForm">Nuevo conteo</UiButton>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando…</p>
    <div v-else-if="result && result.items.length === 0" class="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted-foreground">
      Aún no hay conteos. Abre uno para empezar a revisar un edificio o laboratorio.
    </div>

    <ul v-else-if="result" class="divide-y divide-border overflow-hidden rounded-lg border border-border bg-card">
      <li v-for="s in result.items" :key="s.inventorySessionId">
        <RouterLink :to="`/inventory/${s.inventorySessionId}`" class="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3.5 transition-colors duration-150 hover:bg-muted">
          <div class="min-w-0 flex-1">
            <p class="font-medium text-foreground">{{ s.name }}</p>
            <p class="text-xs text-muted-foreground">{{ s.locationName ?? '—' }}, {{ formatDate(s.startedAt) }}<template v-if="s.createdByUsername">, {{ s.createdByUsername }}</template></p>
          </div>
          <span class="text-sm text-secondary">{{ s.checkedRecords }} de {{ s.totalRecords }} revisados</span>
          <span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', s.status === 'abierta' ? 'bg-accent/10 text-accent' : 'bg-muted text-muted-foreground']">
            {{ SESSION_LABELS[s.status] }}
          </span>
        </RouterLink>
      </li>
    </ul>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de conteos">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>

    <UiModal :open="open" title="Nuevo conteo" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="create">
        <div>
          <label :for="nameId" class="mb-1 block text-sm font-medium text-foreground">Nombre</label>
          <input :id="nameId" v-model="form.name" type="text" maxlength="150" :class="fieldClass" />
        </div>
        <div>
          <label :for="locationId" class="mb-1 block text-sm font-medium text-foreground">Área a contar</label>
          <select :id="locationId" v-model="form.locationId" :class="[fieldClass, 'cursor-pointer px-2.5']">
            <option value="">Selecciona un edificio o área</option>
            <option v-for="l in locations" :key="l.locationId" :value="String(l.locationId)">{{ l.name }} ({{ l.code }})</option>
          </select>
          <p class="mt-1 text-xs text-muted-foreground">Incluye todas sus sub-ubicaciones. La lista esperada son los equipos registrados ahí.</p>
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Abriendo…' : 'Abrir conteo' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
