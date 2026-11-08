<script setup lang="ts">
import { onBeforeUnmount, ref, useId, watch } from 'vue'
import { ticketsApi } from '../api'
import type { AssetOption } from '../types'

// Buscador de equipo: escribe código, serie o hostname y elige de los
// resultados (GET /api/assets?search=). v-model = assetId como texto ('' = ninguno).
const props = withDefaults(
  defineProps<{
    label?: string
    emptyLabel?: string
    // Equipo ya elegido (p. ej. "Reportar problema"): se muestra aunque no salga en la búsqueda.
    initial?: AssetOption | null
    optional?: boolean
  }>(),
  { label: 'Equipo', emptyLabel: 'Sin equipo', initial: null, optional: true },
)

const model = defineModel<string>({ required: true })

const searchId = useId()
const selectId = useId()
const search = ref('')
const options = ref<AssetOption[]>([])
const loading = ref(false)

let timer: ReturnType<typeof setTimeout> | undefined
watch(
  search,
  (term) => {
    clearTimeout(timer)
    timer = setTimeout(() => {
      loading.value = true
      ticketsApi
        .assets(term.trim())
        .then((list) => {
          // Conserva el elegido aunque la búsqueda nueva no lo traiga.
          const chosen = props.initial && !list.some((a) => a.assetId === props.initial?.assetId) ? [props.initial] : []
          options.value = [...chosen, ...list]
        })
        .catch(() => (options.value = props.initial ? [props.initial] : []))
        .finally(() => (loading.value = false))
    }, 250)
  },
  { immediate: true },
)
onBeforeUnmount(() => clearTimeout(timer))
</script>

<template>
  <div>
    <label :for="selectId" class="mb-1 block text-sm font-medium text-foreground">
      {{ label }} <span v-if="optional" class="font-normal text-muted-foreground">(opcional)</span>
    </label>
    <label :for="searchId" class="sr-only">Buscar equipo</label>
    <input
      :id="searchId"
      v-model="search"
      type="search"
      placeholder="Busca por código, serie o nombre del equipo"
      class="mb-1.5 min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-sm text-foreground"
    />
    <select :id="selectId" v-model="model" class="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground">
      <option value="">{{ loading ? 'Buscando…' : emptyLabel }}</option>
      <option v-for="asset in options" :key="asset.assetId" :value="String(asset.assetId)">
        {{ asset.inventoryCode }}<template v-if="asset.hostname"> ({{ asset.hostname }})</template>
      </option>
    </select>
  </div>
</template>
