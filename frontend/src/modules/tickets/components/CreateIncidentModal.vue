<script setup lang="ts">
import { ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { ticketsApi } from '../api'
import type { AssetOption, TicketCatalogs, TicketDetail } from '../types'

// Falla de hardware detectada desde un ticket: se levanta el incidente
// técnico (cambia el estado del activo) y se liga al ticket. Son dos pasos
// del backend; si el segundo falla el incidente ya existe y se avisa.
const props = defineProps<{
  open: boolean
  ticket: TicketDetail
  catalogs: TicketCatalogs | null
}>()

const emit = defineEmits<{ close: []; linked: []; conflict: [] }>()

const assetFieldId = useId()
const typeFieldId = useId()
const titleFieldId = useId()
const descriptionFieldId = useId()

const assetId = ref('')
const typeId = ref('')
const title = ref('')
const description = ref('')
const assets = ref<AssetOption[]>([])
const error = ref('')
const saving = ref(false)

watch(
  () => props.open,
  (open) => {
    if (!open) return
    assetId.value = props.ticket.assetId !== null ? String(props.ticket.assetId) : ''
    typeId.value = ''
    title.value = props.ticket.title
    description.value = props.ticket.description ?? ''
    error.value = ''
    saving.value = false
    if (props.ticket.assetId === null) {
      ticketsApi.assets().then((list) => (assets.value = list)).catch(() => (assets.value = []))
    }
  },
  { immediate: true },
)

async function handleSubmit() {
  error.value = ''
  if (!assetId.value || !typeId.value || !title.value.trim()) {
    error.value = 'Elige el equipo y el tipo de incidente, y escribe un título.'
    return
  }
  saving.value = true
  try {
    const incident = await ticketsApi.createIncident({
      assetId: Number(assetId.value),
      incidentTypeId: Number(typeId.value),
      incidentPriorityId: props.ticket.priorityId,
      title: title.value.trim(),
      description: description.value.trim() || undefined,
    })
    try {
      await ticketsApi.linkIncident(props.ticket.ticketId, props.ticket.version, incident.incidentId)
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        emit('conflict')
      }
      error.value = `Se creó el incidente #${incident.incidentId}, pero no se pudo ligar al ticket. Recarga e intenta ligarlo de nuevo.`
      return
    }
    emit('linked')
    emit('close')
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo crear el incidente.'
  } finally {
    saving.value = false
  }
}

const fieldClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <UiModal :open="open" title="Crear incidente técnico" @close="emit('close')">
    <form novalidate class="space-y-3" @submit.prevent="handleSubmit">
      <p class="text-sm text-secondary">
        El equipo pasará a <span class="font-medium text-foreground">con falla</span> hasta que se resuelva el incidente.
      </p>

      <div>
        <template v-if="ticket.assetId !== null">
          <span class="mb-1 block text-sm font-medium text-foreground">Equipo</span>
          <p class="text-sm text-foreground">{{ ticket.assetInventoryCode }}</p>
        </template>
        <label v-else :for="assetFieldId" class="mb-1 block text-sm font-medium text-foreground">Equipo</label>
        <select v-if="ticket.assetId === null" :id="assetFieldId" v-model="assetId" :class="[fieldClass, 'cursor-pointer px-2.5']">
          <option value="">Selecciona el equipo</option>
          <option v-for="asset in assets" :key="asset.assetId" :value="String(asset.assetId)">{{ asset.inventoryCode }}</option>
        </select>
      </div>

      <div>
        <label :for="typeFieldId" class="mb-1 block text-sm font-medium text-foreground">Tipo de incidente</label>
        <select :id="typeFieldId" v-model="typeId" :class="[fieldClass, 'cursor-pointer px-2.5']">
          <option value="">Selecciona un tipo</option>
          <option v-for="type in catalogs?.incidentTypes ?? []" :key="type.id" :value="String(type.id)">{{ type.name }}</option>
        </select>
      </div>

      <div>
        <label :for="titleFieldId" class="mb-1 block text-sm font-medium text-foreground">Título</label>
        <input :id="titleFieldId" v-model="title" type="text" maxlength="200" :class="fieldClass" />
      </div>

      <div>
        <label :for="descriptionFieldId" class="mb-1 block text-sm font-medium text-foreground">Descripción técnica</label>
        <textarea :id="descriptionFieldId" v-model="description" rows="3" maxlength="4000" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
      </div>

      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

      <div class="flex justify-end gap-2">
        <UiButton :disabled="saving" @click="emit('close')">Cancelar</UiButton>
        <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Creando…' : 'Crear y ligar' }}</UiButton>
      </div>
    </form>
  </UiModal>
</template>
