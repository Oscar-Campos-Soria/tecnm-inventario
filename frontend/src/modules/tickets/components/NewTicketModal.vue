<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import { ALLOWED_MIME_TYPES, MAX_DOCUMENT_BYTES, documentsApi } from '../../../shared/api/documents'
import NavIcon from '../../../shared/components/NavIcon.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatBytes } from '../../../shared/utils/format'
import { ticketsApi } from '../api'
import { REQUESTER_PRIORITIES, categoryIcon } from '../labels'
import type { AssetOption, LocationOption, PriorityCode, TicketCatalogs } from '../types'

const props = defineProps<{
  open: boolean
  catalogs: TicketCatalogs | null
  // Equipo preseleccionado ("Reportar problema" desde la ficha del activo).
  presetAssetId: number | null
}>()

// created: la página navega al detalle (una sola navegación; si el modal
// también navegara, competiría con la limpieza de ?nuevo=1 de la página).
const emit = defineEmits<{ close: []; created: [ticketId: number, photoFailed: boolean] }>()

const { hasPermission } = useAuth()
const titleId = useId()
const descriptionId = useId()
const assetFieldId = useId()
const locationFieldId = useId()

const categoryId = ref<number | null>(null)
const priority = ref<PriorityCode>('MEDIA')
const title = ref('')
const description = ref('')
const assetId = ref('')
const assets = ref<AssetOption[]>([])
const locationId = ref('')
const locations = ref<LocationOption[]>([])
const photo = ref<File | null>(null)
const error = ref('')
const submitting = ref(false)

const canPickAsset = computed(() => hasPermission('assets.view'))
const canPickLocation = computed(() => hasPermission('locations.view'))

watch(
  () => props.open,
  (open) => {
    if (!open) return
    categoryId.value = null
    priority.value = 'MEDIA'
    title.value = ''
    description.value = ''
    assetId.value = props.presetAssetId !== null ? String(props.presetAssetId) : ''
    locationId.value = ''
    photo.value = null
    error.value = ''
    submitting.value = false
    if (canPickAsset.value) {
      ticketsApi.assets().then((list) => (assets.value = list)).catch(() => (assets.value = []))
    }
    if (canPickLocation.value) {
      ticketsApi.locations().then((list) => (locations.value = list)).catch(() => (locations.value = []))
    }
  },
  { immediate: true },
)

function priorityId(code: PriorityCode): number | undefined {
  return props.catalogs?.incidentPriorities.find((p) => p.code === code)?.id
}

function onPhoto(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  error.value = ''
  if (file && !ALLOWED_MIME_TYPES.includes(file.type)) {
    error.value = 'Solo se aceptan fotos JPG o PNG, o archivos PDF.'
    input.value = ''
    return
  }
  if (file && file.size > MAX_DOCUMENT_BYTES) {
    error.value = `El archivo pesa ${formatBytes(file.size)}; el máximo es 5 MB.`
    input.value = ''
    return
  }
  photo.value = file
}

async function handleSubmit() {
  error.value = ''
  if (categoryId.value === null) {
    error.value = 'Elige qué tipo de ayuda necesitas.'
    return
  }
  if (!title.value.trim()) {
    error.value = 'Escribe en una frase qué pasa.'
    return
  }

  submitting.value = true
  try {
    const created = await ticketsApi.create({
      categoryId: categoryId.value,
      priorityId: priorityId(priority.value),
      title: title.value.trim(),
      description: description.value.trim() || undefined,
      assetId: assetId.value ? Number(assetId.value) : undefined,
      locationId: locationId.value ? Number(locationId.value) : undefined,
    })

    // La foto se sube después de crear el ticket (necesita su id). Si falla,
    // el ticket ya existe: se avisa en el detalle en vez de perderlo.
    let photoFailed = false
    if (photo.value) {
      try {
        await documentsApi.upload('TICKET', created.ticketId, photo.value)
      } catch {
        photoFailed = true
      }
    }

    emit('created', created.ticketId, photoFailed)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo levantar el ticket. Intenta de nuevo.'
  } finally {
    submitting.value = false
  }
}

const inputClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <UiModal :open="open" title="Nuevo ticket" @close="emit('close')">
    <form novalidate class="space-y-5" @submit.prevent="handleSubmit">
      <fieldset>
        <legend class="mb-2 text-sm font-medium text-foreground">¿Con qué necesitas ayuda?</legend>
        <div class="grid grid-cols-2 gap-2 sm:grid-cols-3">
          <label
            v-for="category in catalogs?.ticketCategories ?? []"
            :key="category.id"
            :class="[
              'flex min-h-[72px] cursor-pointer flex-col items-start justify-between gap-2 rounded-lg border p-3 text-sm transition-colors duration-150 has-focus-visible:ring-2 has-focus-visible:ring-ring',
              categoryId === category.id
                ? 'border-accent bg-accent/5 font-semibold text-primary'
                : 'border-border text-foreground hover:bg-muted',
            ]"
          >
            <input v-model="categoryId" type="radio" name="ticket-category" :value="category.id" class="sr-only" />
            <NavIcon :paths="categoryIcon(category.code)" />
            {{ category.name }}
          </label>
        </div>
      </fieldset>

      <div>
        <label :for="titleId" class="mb-1 block text-sm font-medium text-foreground">¿Qué pasa?</label>
        <input :id="titleId" v-model="title" type="text" maxlength="200" :class="inputClass" placeholder="Ej. No hay internet en el aula 12" />
      </div>

      <div>
        <label :for="descriptionId" class="mb-1 block text-sm font-medium text-foreground">
          Detalles <span class="font-normal text-muted-foreground">(opcional)</span>
        </label>
        <textarea
          :id="descriptionId"
          v-model="description"
          rows="3"
          maxlength="4000"
          class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground"
          placeholder="Desde cuándo pasa, qué mensaje aparece, qué ya intentaste"
        />
      </div>

      <fieldset>
        <legend class="mb-2 text-sm font-medium text-foreground">¿Qué tan urgente es?</legend>
        <div class="grid gap-2 sm:grid-cols-3">
          <label
            v-for="option in REQUESTER_PRIORITIES"
            :key="option.code"
            :class="[
              'flex min-h-[44px] cursor-pointer flex-col rounded-lg border px-3 py-2 text-sm transition-colors duration-150 has-focus-visible:ring-2 has-focus-visible:ring-ring',
              priority === option.code
                ? option.code === 'CRITICA'
                  ? 'border-destructive bg-destructive/5 text-destructive'
                  : option.code === 'ALTA'
                    ? 'border-priority-high bg-priority-high/5 text-priority-high'
                    : 'border-accent bg-accent/5 text-primary'
                : 'border-border text-foreground hover:bg-muted',
            ]"
          >
            <input v-model="priority" type="radio" name="ticket-priority" :value="option.code" class="sr-only" />
            <span class="font-semibold">{{ option.label }}</span>
            <span class="text-xs opacity-80">{{ option.hint }}</span>
          </label>
        </div>
      </fieldset>

      <div v-if="canPickAsset">
        <label :for="assetFieldId" class="mb-1 block text-sm font-medium text-foreground">
          Equipo <span class="font-normal text-muted-foreground">(opcional)</span>
        </label>
        <select :id="assetFieldId" v-model="assetId" class="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground">
          <option value="">No es de un equipo en particular</option>
          <option v-for="asset in assets" :key="asset.assetId" :value="String(asset.assetId)">
            {{ asset.inventoryCode }}<template v-if="asset.hostname"> ({{ asset.hostname }})</template>
          </option>
        </select>
      </div>

      <div v-if="canPickLocation">
        <label :for="locationFieldId" class="mb-1 block text-sm font-medium text-foreground">
          ¿Dónde? <span class="font-normal text-muted-foreground">(opcional)</span>
        </label>
        <select :id="locationFieldId" v-model="locationId" class="min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground">
          <option value="">Sin ubicación</option>
          <option v-for="location in locations" :key="location.locationId" :value="String(location.locationId)">
            {{ location.name }} ({{ location.code }})
          </option>
        </select>
      </div>

      <div>
        <span class="mb-1 block text-sm font-medium text-foreground">
          Foto o PDF <span class="font-normal text-muted-foreground">(opcional, hasta 5 MB)</span>
        </span>
        <label class="flex min-h-[44px] cursor-pointer items-center gap-2 rounded-md border border-dashed border-border px-3 text-sm text-secondary hover:bg-muted has-focus-visible:ring-2 has-focus-visible:ring-ring">
          <input type="file" accept="image/jpeg,image/png,application/pdf" class="sr-only" @change="onPhoto" />
          {{ photo ? `${photo.name} (${formatBytes(photo.size)})` : 'Elegir archivo o tomar foto' }}
        </label>
      </div>

      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

      <div class="flex justify-end gap-2">
        <UiButton :disabled="submitting" @click="emit('close')">Cancelar</UiButton>
        <UiButton type="submit" variant="primary" :disabled="submitting">
          {{ submitting ? 'Enviando…' : 'Enviar ticket' }}
        </UiButton>
      </div>
    </form>
  </UiModal>
</template>
