<script setup lang="ts">
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import type { LocationItem } from '../types'

defineProps<{
  open: boolean
  location: LocationItem | null
  deleting: boolean
  // Errores de negocio del backend (400: tiene sub-ubicaciones o activos asignados), tal cual llegan.
  error: string
}>()

const emit = defineEmits<{ cancel: []; confirm: [] }>()
</script>

<template>
  <UiModal :open="open" title="Eliminar ubicación" @close="emit('cancel')">
    <div v-if="location" class="space-y-4">
      <p class="text-sm text-secondary">
        ¿Eliminar <span class="font-medium text-foreground">{{ location.name }}</span>
        ({{ location.code }})? La ubicación dejará de estar disponible para
        movimientos y asignaciones.
      </p>

      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
        {{ error }}
      </p>

      <div class="flex justify-end gap-2">
        <UiButton :disabled="deleting" @click="emit('cancel')">Cancelar</UiButton>
        <UiButton variant="danger" :disabled="deleting" @click="emit('confirm')">
          {{ deleting ? 'Eliminando…' : 'Eliminar' }}
        </UiButton>
      </div>
    </div>
  </UiModal>
</template>
