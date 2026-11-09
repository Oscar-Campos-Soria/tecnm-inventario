<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { formatDate, formatDateTime } from '../../../shared/utils/format'
import { softwareApi } from '../api'
import { expiry, usage } from '../status'
import type { AssetOption, LicenseAssignment, LicenseDetail } from '../types'
import UsageBar from './UsageBar.vue'

// Detalle de una licencia: en qué equipos está, asignarla a otro y liberarla.
const props = defineProps<{ licenseId: number | null }>()
const emit = defineEmits<{ close: []; changed: [] }>()

const license = ref<LicenseDetail | null>(null)
const assignments = ref<LicenseAssignment[]>([])
const error = ref('')
const notice = ref('')
const busy = ref(false)
const showHistory = ref(false)
const assetSearch = ref('')
const assetResults = ref<AssetOption[]>([])
const searchId = useId()

const active = computed(() => assignments.value.filter((a) => a.endedAt === null))
const ended = computed(() => assignments.value.filter((a) => a.endedAt !== null))
const blockedReason = computed(() => {
  const l = license.value
  if (!l) return ''
  if (l.isExpired) return 'La licencia ya venció; renuévala (edita la fecha de vencimiento) para asignarla.'
  if (usage(l).full) return 'Ya no quedan lugares. Libera un equipo o aumenta la cantidad permitida.'
  return ''
})

async function load(id: number) {
  try {
    const [l, a] = await Promise.all([softwareApi.license(id), softwareApi.assignments(id, false)])
    license.value = l
    assignments.value = a.items
    error.value = ''
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo cargar la licencia.'
  }
}

watch(
  () => props.licenseId,
  (id) => {
    license.value = null
    assignments.value = []
    error.value = ''
    notice.value = ''
    showHistory.value = false
    assetSearch.value = ''
    if (id !== null) load(id)
  },
)

let timer: ReturnType<typeof setTimeout> | undefined
watch(assetSearch, (v) => {
  clearTimeout(timer)
  const term = v.trim()
  if (term.length < 2) return void (assetResults.value = [])
  timer = setTimeout(() => {
    softwareApi.searchAssets(term).then((r) => (assetResults.value = r)).catch(() => (assetResults.value = []))
  }, 250)
})
onBeforeUnmount(() => clearTimeout(timer))

async function assign(asset: AssetOption) {
  if (!license.value) return
  assetSearch.value = ''
  assetResults.value = []
  busy.value = true
  error.value = ''
  try {
    await softwareApi.assign(license.value.licenseId, asset.assetId)
    notice.value = `Licencia asignada a ${asset.inventoryCode}.`
    await load(license.value.licenseId)
    emit('changed')
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo asignar la licencia.'
  } finally {
    busy.value = false
  }
}

async function release(a: LicenseAssignment) {
  if (!license.value || !window.confirm(`¿Liberar la licencia de ${a.inventoryCode}?`)) return
  busy.value = true
  error.value = ''
  try {
    await softwareApi.release(a)
    notice.value = `${a.inventoryCode} ya no ocupa esta licencia.`
  } catch (err) {
    error.value =
      err instanceof ApiError && err.status === 409
        ? 'Otra persona modificó esa asignación. Recargamos la lista; intenta de nuevo.'
        : err instanceof ApiError ? err.message : 'No se pudo liberar la licencia.'
  } finally {
    await load(license.value.licenseId)
    emit('changed')
    busy.value = false
  }
}
</script>

<template>
  <UiModal :open="licenseId !== null" :title="license ? `Licencia de ${license.softwareName}` : 'Licencia'" @close="emit('close')">
    <p v-if="!license && !error" class="text-sm text-muted-foreground">Cargando licencia…</p>
    <div v-if="license" class="space-y-4 text-sm">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <UsageBar :license="license" />
        <span :class="['rounded-full px-2 py-0.5 text-xs font-medium', expiry(license).cls]">{{ expiry(license).text }}</span>
      </div>
      <dl class="grid grid-cols-2 gap-x-4 gap-y-1.5">
        <dt class="text-muted-foreground">Tipo</dt><dd class="text-foreground">{{ license.licenseTypeName }}</dd>
        <dt class="text-muted-foreground">Clave</dt><dd class="text-foreground">{{ license.maskedLicenseKey ?? 'Sin clave' }}</dd>
        <dt class="text-muted-foreground">Vigencia</dt>
        <dd class="text-foreground">{{ license.validFrom ? formatDate(license.validFrom) : 'Sin inicio' }} a {{ license.validUntil ? formatDate(license.validUntil) : 'sin vencimiento' }}</dd>
        <dt class="text-muted-foreground">Proveedor</dt><dd class="text-foreground">{{ license.supplierName ?? '—' }}</dd>
      </dl>
      <p v-if="license.notes" class="rounded-md bg-muted px-3 py-2 text-secondary">{{ license.notes }}</p>

      <p v-if="notice" role="status" class="rounded-md bg-success/10 px-3 py-2 text-success">{{ notice }}</p>
      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-destructive">{{ error }}</p>

      <div>
        <label :for="searchId" class="mb-1 block font-medium text-foreground">Asignar a un equipo</label>
        <p v-if="blockedReason" class="rounded-md bg-muted px-3 py-2 text-secondary">{{ blockedReason }}</p>
        <div v-else class="relative">
          <input
            :id="searchId"
            v-model="assetSearch"
            type="search"
            autocomplete="off"
            :disabled="busy"
            placeholder="Buscar por código, serie o hostname"
            class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground"
          />
          <ul v-if="assetResults.length" class="absolute z-10 mt-1 max-h-48 w-full overflow-auto rounded-md border border-border bg-card shadow">
            <li v-for="a in assetResults" :key="a.assetId">
              <button
                type="button"
                class="w-full cursor-pointer px-3 py-2 text-left hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
                :disabled="active.some((x) => x.assetId === a.assetId)"
                @click="assign(a)"
              >
                <span class="font-medium text-foreground">{{ a.inventoryCode }}</span>
                <span class="text-muted-foreground"> {{ a.assetTypeName }}</span>
                <span v-if="active.some((x) => x.assetId === a.assetId)" class="text-muted-foreground">, ya tiene esta licencia</span>
              </button>
            </li>
          </ul>
        </div>
      </div>

      <div>
        <h3 class="mb-2 font-display font-semibold text-foreground">Equipos con la licencia ({{ active.length }})</h3>
        <p v-if="active.length === 0" class="text-muted-foreground">Ningún equipo la usa todavía.</p>
        <ul v-else class="divide-y divide-border rounded-md border border-border">
          <li v-for="a in active" :key="a.licenseAssignmentId" class="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
            <div>
              <RouterLink :to="`/assets/${a.assetId}`" class="font-medium text-accent hover:underline">{{ a.inventoryCode }}</RouterLink>
              <p class="text-xs text-muted-foreground">
                {{ [a.hostname, a.locationName].filter(Boolean).join(', ') || 'Sin hostname ni ubicación' }}. Desde {{ formatDateTime(a.assignedAt) }}
              </p>
            </div>
            <UiButton :disabled="busy" @click="release(a)">Liberar</UiButton>
          </li>
        </ul>
      </div>

      <div v-if="ended.length">
        <UiButton :aria-expanded="showHistory" @click="showHistory = !showHistory">
          {{ showHistory ? 'Ocultar equipos anteriores' : `Ver equipos anteriores (${ended.length})` }}
        </UiButton>
        <ul v-if="showHistory" class="mt-2 divide-y divide-border rounded-md border border-border">
          <li v-for="a in ended" :key="a.licenseAssignmentId" class="px-3 py-2">
            <span class="font-medium text-foreground">{{ a.inventoryCode }}</span>
            <span class="text-muted-foreground"> del {{ formatDateTime(a.assignedAt) }} al {{ formatDateTime(a.endedAt) }}</span>
          </li>
        </ul>
      </div>
    </div>
    <p v-else-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
  </UiModal>
</template>
