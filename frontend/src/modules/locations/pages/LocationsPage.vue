<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import UiButton from '../../../shared/components/UiButton.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import LocationFormModal from '../components/LocationFormModal.vue'
import LocationTreeNode from '../components/LocationTreeNode.vue'
import { fetchLocationTypes, locationsApi } from '../api'
import type { LocationItem, LocationTypeItem } from '../types'

type LoadState = 'loading' | 'error' | 'ready'

const state = ref<LoadState>('loading')
const locations = ref<LocationItem[]>([])
const locationTypes = ref<LocationTypeItem[]>([])
const search = ref('')
const debouncedSearch = ref('')
// Se reemplaza el Set completo al cambiar, para que Vue detecte el cambio.
const expanded = ref<Set<number>>(new Set())
// Avisos no fatales (p. ej. fallo al recargar la lista tras guardar).
const error = ref('')
const createForm = ref<{ open: boolean; parentLocationId: number | null }>({ open: false, parentLocationId: null })

// Las acciones de escritura solo las ve ADMIN (locations.manage está asignado a ese rol).
const { hasPermission } = useAuth()
const canManage = computed(() => hasPermission('locations.manage'))

onMounted(() => {
  locationsApi
    .list()
    .then((items) => {
      locations.value = items
      // Raíces expandidas por defecto para ver la estructura del plantel de inmediato.
      expanded.value = new Set(items.filter((item) => item.parentLocationId === null).map((item) => item.locationId))
      state.value = 'ready'
    })
    .catch(() => (state.value = 'error'))

  fetchLocationTypes()
    .then((types) => (locationTypes.value = types))
    .catch(() => (locationTypes.value = []))
})

// Búsqueda con debounce: evita re-filtrar en cada tecla.
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (debouncedSearch.value = value.trim().toLowerCase()), 200)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

const typeNameByCode = computed(() => new Map(locationTypes.value.map((type) => [type.code, type.name])))

// Lista plana -> árbol padre/hijo (la API ya ordena por code, el mapa conserva ese orden).
const childrenByParent = computed(() => {
  const map = new Map<number, LocationItem[]>()
  for (const item of locations.value) {
    if (item.parentLocationId === null) continue
    const siblings = map.get(item.parentLocationId)
    if (siblings) siblings.push(item)
    else map.set(item.parentLocationId, [item])
  }
  return map
})

const roots = computed(() => locations.value.filter((item) => item.parentLocationId === null))

// Con búsqueda activa se muestran las coincidencias más sus ancestros (todo
// expandido, sin importar lo que el usuario expandió) y el resto se oculta.
const searchResult = computed<Set<number> | null>(() => {
  const term = debouncedSearch.value
  if (!term) return null
  const byId = new Map(locations.value.map((item) => [item.locationId, item]))
  const visibleIds = new Set<number>()
  for (const item of locations.value) {
    const typeName = typeNameByCode.value.get(item.locationTypeCode) ?? item.locationTypeCode
    const matches =
      item.name.toLowerCase().includes(term) ||
      item.code.toLowerCase().includes(term) ||
      typeName.toLowerCase().includes(term)
    if (!matches) continue
    // Sube por los padres para mantener el contexto del árbol.
    let current: LocationItem | undefined = item
    let guard = 0
    while (current && guard++ < 20) {
      visibleIds.add(current.locationId)
      current = current.parentLocationId !== null ? byId.get(current.parentLocationId) : undefined
    }
  }
  return visibleIds
})

const visibleRoots = computed(() =>
  roots.value.filter((root) => searchResult.value === null || searchResult.value.has(root.locationId)),
)

// Tras guardar: recarga la lista y expande el padre del nodo guardado.
function handleSaved(_locationId: number, parentLocationId: number | null) {
  locationsApi
    .list()
    .then((items) => {
      locations.value = items
      if (parentLocationId !== null) expanded.value = new Set([...expanded.value, parentLocationId])
    })
    .catch(() => (error.value = 'No se pudieron recargar las ubicaciones.'))
}

function toggle(locationId: number) {
  const next = new Set(expanded.value)
  if (next.has(locationId)) next.delete(locationId)
  else next.add(locationId)
  expanded.value = next
}
</script>

<template>
  <div class="space-y-4">
    <header class="flex flex-wrap items-center justify-between gap-2">
      <h1 class="font-display text-2xl font-semibold text-foreground">Ubicaciones</h1>
      <div class="flex items-center gap-3">
        <span v-if="state === 'ready'" class="text-sm text-muted-foreground">
          {{ locations.length }} {{ locations.length === 1 ? 'ubicación' : 'ubicaciones' }}
        </span>
        <UiButton
          v-if="canManage && state === 'ready'"
          variant="primary"
          @click="createForm = { open: true, parentLocationId: null }"
        >
          Nueva ubicación
        </UiButton>
      </div>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="state === 'error'" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
      No se pudieron cargar las ubicaciones.
    </p>

    <p v-if="state === 'loading'" class="text-sm text-muted-foreground">Cargando ubicaciones…</p>

    <div v-if="state === 'ready' && locations.length === 0" class="rounded-lg border border-dashed border-border p-8 text-center">
      <p class="font-medium text-foreground">Sin ubicaciones</p>
      <p class="mt-1 text-sm text-muted-foreground">Registra el primer edificio del plantel.</p>
    </div>

    <div v-if="state === 'ready' && locations.length > 0" class="space-y-3">
      <div>
        <label for="location-search" class="mb-1 block text-sm font-medium text-foreground">Buscar</label>
        <input
          id="location-search"
          v-model="search"
          type="search"
          placeholder="Nombre, código o tipo de ubicación"
          class="min-h-[44px] w-full max-w-md rounded-md border border-border bg-background px-3 text-foreground"
        />
      </div>

      <p
        v-if="searchResult !== null && searchResult.size === 0"
        class="rounded-md border border-dashed border-border px-3 py-2 text-sm text-muted-foreground"
      >
        Sin resultados para «{{ search.trim() }}».
      </p>

      <ul v-else class="space-y-0.5">
        <LocationTreeNode
          v-for="root in visibleRoots"
          :key="root.locationId"
          :item="root"
          :depth="0"
          :children-by-parent="childrenByParent"
          :expanded="expanded"
          :searching="searchResult !== null"
          :visible-ids="searchResult"
          :type-name-by-code="typeNameByCode"
          :can-manage="canManage"
          @toggle="toggle"
          @add-child="createForm = { open: true, parentLocationId: $event }"
        />
      </ul>
    </div>

    <LocationFormModal
      :open="createForm.open"
      mode="create"
      :parent-location-id="createForm.parentLocationId"
      :locations="locations"
      @close="createForm = { ...createForm, open: false }"
      @saved="handleSaved"
      @concurrency-conflict="error = 'Otra persona modificó las ubicaciones. Recarga la página.'"
    />
  </div>
</template>
