<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiField from '../../../shared/components/UiField.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { fetchLocationTypes, locationsApi } from '../api'
import ConfirmDeleteModal from '../components/ConfirmDeleteModal.vue'
import LocationFormModal from '../components/LocationFormModal.vue'
import LocationAssets from '../components/LocationAssets.vue'
import type { LocationDetail, LocationItem, LocationTypeItem } from '../types'

type LoadState = 'loading' | 'not-found' | 'error' | 'ready'

const route = useRoute()
const router = useRouter()
const { hasPermission } = useAuth()
const numericId = computed(() => Number(route.params.locationId))
const isValidId = computed(() => Number.isInteger(numericId.value))

// Las acciones de escritura solo las ve ADMIN (locations.manage está asignado a ADMIN).
const canManage = computed(() => hasPermission('locations.manage'))

const state = ref<LoadState>('loading')
const detail = ref<LocationDetail | null>(null)
const locations = ref<LocationItem[]>([])
const locationTypes = ref<LocationTypeItem[]>([])
const detailLoading = ref(false)

const formOpen = ref(false)
const deleteOpen = ref(false)
const deleting = ref(false)
const deleteError = ref('')
// Avisos de página (p. ej. recarga por 409).
const pageError = ref('')

async function fetchAll() {
  const [d, all, types] = await Promise.all([
    locationsApi.getById(numericId.value),
    locationsApi.list(),
    fetchLocationTypes(),
  ])
  detail.value = d
  locations.value = all
  locationTypes.value = types
}

// Recarga de la ficha (tras guardar, o automática ante un 409).
async function loadDetail() {
  detailLoading.value = true
  try {
    await fetchAll()
  } catch {
    // Recarga automática: si falla, el usuario recarga a mano.
  } finally {
    detailLoading.value = false
  }
}

// Un 409 significa que otra persona modificó la ubicación: se recargan los datos.
function concurrencyConflict(): string {
  loadDetail()
  return 'Otra persona modificó esta ubicación. Recargamos los datos; revísalos e intenta de nuevo.'
}

// Carga inicial (y al navegar a otra ubicación desde el breadcrumb).
watch(
  numericId,
  () => {
    if (!isValidId.value) return
    state.value = 'loading'
    fetchAll()
      .then(() => (state.value = 'ready'))
      .catch((err) => {
        state.value = err instanceof ApiError && err.status === 404 ? 'not-found' : 'error'
      })
  },
  { immediate: true },
)

// Breadcrumb con enlaces: el backend devuelve solo nombres, así que los ids
// se reconstruyen subiendo por parentLocationId desde la lista completa.
const breadcrumb = computed(() => {
  if (!detail.value) return []
  const byId = new Map(locations.value.map((item) => [item.locationId, item]))
  const trail: LocationItem[] = []
  let current = byId.get(detail.value.locationId)
  let guard = 0
  while (current && guard++ < 20) {
    trail.unshift(current)
    current = current.parentLocationId !== null ? byId.get(current.parentLocationId) : undefined
  }
  return trail
})

const typeName = computed(
  () =>
    locationTypes.value.find((type) => type.code === detail.value?.locationTypeCode)?.name ??
    detail.value?.locationTypeCode ??
    '',
)

const parentLocation = computed(() =>
  detail.value && detail.value.parentLocationId !== null
    ? locations.value.find((item) => item.locationId === detail.value?.parentLocationId) ?? null
    : null,
)

// Bloquea las acciones mientras hay una operación o una recarga en curso.
const busy = computed(() => deleting.value || detailLoading.value)

function openDelete() {
  deleteError.value = ''
  deleteOpen.value = true
}

async function handleDelete() {
  if (!detail.value) return
  deleting.value = true
  deleteError.value = ''
  try {
    await locationsApi.remove(detail.value.locationId, detail.value.version)
    deleteOpen.value = false
    const parentId = detail.value.parentLocationId
    await router.push(parentId !== null ? `/locations/${parentId}` : '/locations')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      deleteOpen.value = false
      pageError.value = concurrencyConflict()
      return
    }
    // 400 del backend: tiene sub-ubicaciones o activos asignados; se muestra tal cual.
    deleteError.value = err instanceof ApiError ? err.message : 'No se pudo eliminar la ubicación.'
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <p v-if="isValidId && state === 'loading'" class="text-sm text-muted-foreground">Cargando ubicación…</p>

  <div v-else-if="!isValidId || state === 'not-found'" class="space-y-3">
    <p class="font-medium text-foreground">Ubicación no encontrada</p>
    <RouterLink to="/locations" class="text-sm text-primary underline-offset-2 hover:underline">Volver a ubicaciones</RouterLink>
  </div>

  <p v-else-if="state === 'error' || !detail" role="alert" class="text-sm text-destructive">No se pudo cargar la ubicación.</p>

  <div v-else class="space-y-6">
    <header class="space-y-1">
      <nav aria-label="Ruta de la ubicación" class="text-sm">
        <RouterLink to="/locations" class="text-secondary underline-offset-2 hover:underline">Ubicaciones</RouterLink>
        <span v-for="(item, index) in breadcrumb" :key="item.locationId">
          <span class="mx-1.5 text-muted-foreground" aria-hidden="true">›</span>
          <RouterLink
            v-if="index < breadcrumb.length - 1"
            :to="`/locations/${item.locationId}`"
            class="text-secondary underline-offset-2 hover:underline"
          >
            {{ item.name }}
          </RouterLink>
          <span v-else class="font-medium text-foreground">{{ item.name }}</span>
        </span>
      </nav>
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="font-display text-2xl font-semibold text-foreground">{{ detail.name }}</h1>
        <span class="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-secondary">{{ typeName }}</span>
        <span class="text-sm text-muted-foreground">{{ detail.code }}</span>
        <span v-if="canManage" class="flex gap-2">
          <UiButton :disabled="busy" @click="formOpen = true">Editar</UiButton>
          <UiButton variant="danger" :disabled="busy" @click="openDelete">Eliminar</UiButton>
        </span>
      </div>
    </header>

    <p v-if="pageError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ pageError }}</p>

    <section aria-labelledby="datos-generales" class="rounded-lg border border-border bg-card p-5">
      <h2 id="datos-generales" class="mb-3 font-display text-base font-semibold text-foreground">Datos generales</h2>
      <dl class="grid gap-x-8 gap-y-3 sm:grid-cols-2">
        <UiField label="Código" :value="detail.code" />
        <UiField label="Tipo" :value="typeName" />
        <UiField label="Ubicación padre">
          <RouterLink
            v-if="parentLocation"
            :to="`/locations/${parentLocation.locationId}`"
            class="text-primary underline-offset-2 hover:underline"
          >
            {{ parentLocation.name }} ({{ parentLocation.code }})
          </RouterLink>
          <template v-else>— (raíz)</template>
        </UiField>
        <UiField label="Versión" :value="String(detail.version)" />
      </dl>
    </section>

    <LocationAssets v-if="hasPermission('assets.view')" :location-id="detail.locationId" />

    <LocationFormModal
      :open="formOpen"
      mode="edit"
      :parent-location-id="detail.parentLocationId"
      :initial="detail"
      :locations="locations"
      @close="formOpen = false"
      @saved="loadDetail"
      @concurrency-conflict="pageError = concurrencyConflict()"
    />

    <ConfirmDeleteModal
      :open="deleteOpen"
      :location="detail"
      :deleting="deleting"
      :error="deleteError"
      @cancel="deleteOpen = false"
      @confirm="handleDelete"
    />
  </div>
</template>
