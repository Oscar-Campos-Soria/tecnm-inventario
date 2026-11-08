<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { fetchLocationTypes, locationsApi } from '../api'
import type { LocationItem, LocationTypeItem } from '../types'

const props = defineProps<{
  open: boolean
  mode: 'create' | 'edit'
  // Padre preseleccionado al crear (nulo = raíz).
  parentLocationId: number | null
  // Datos a editar; solo se usa en modo 'edit'.
  initial?: LocationItem | null
  // Lista plana del árbol, para el selector de padre.
  locations: LocationItem[]
}>()

const emit = defineEmits<{
  close: []
  // Éxito: id guardado y su padre (para recargar la lista y expandir el padre).
  saved: [locationId: number, parentLocationId: number | null]
  // 409: la página recarga los datos y avisa.
  concurrencyConflict: []
}>()

const codeFieldId = useId()
const nameFieldId = useId()
const typeFieldId = useId()
const parentFieldId = useId()

const code = ref('')
const name = ref('')
const locationTypeId = ref('')
const parent = ref('')
const locationTypes = ref<LocationTypeItem[]>([])
const formError = ref('')
const submitting = ref(false)

// Al abrir: carga el catálogo de tipos y resetea el formulario (con el
// padre preseleccionado al crear, o los valores actuales al editar).
watch(
  () => props.open,
  (open) => {
    if (!open) return
    fetchLocationTypes()
      .then((types) => {
        locationTypes.value = types
        formError.value = ''
        submitting.value = false
        if (props.mode === 'edit' && props.initial) {
          code.value = props.initial.code
          name.value = props.initial.name
          const match = types.find((type) => type.code === props.initial?.locationTypeCode)
          locationTypeId.value = match ? String(match.id) : ''
          parent.value = props.initial.parentLocationId !== null ? String(props.initial.parentLocationId) : ''
        } else {
          code.value = ''
          name.value = ''
          locationTypeId.value = ''
          parent.value = props.parentLocationId !== null ? String(props.parentLocationId) : ''
        }
      })
      .catch(() => (locationTypes.value = []))
  },
  { immediate: true },
)

// En edición, el padre no puede ser la propia ubicación ni ninguna de sus
// sub-ubicaciones (el backend rechaza ciclos; aquí se evitan antes de enviar).
const parentOptions = computed(() => {
  const initial = props.initial
  if (props.mode !== 'edit' || !initial) return props.locations

  const childrenByParent = new Map<number, LocationItem[]>()
  for (const item of props.locations) {
    if (item.parentLocationId === null) continue
    const siblings = childrenByParent.get(item.parentLocationId)
    if (siblings) siblings.push(item)
    else childrenByParent.set(item.parentLocationId, [item])
  }

  const excluded = new Set<number>([initial.locationId])
  const queue = [initial.locationId]
  while (queue.length > 0) {
    const current = queue.shift() as number
    for (const child of childrenByParent.get(current) ?? []) {
      if (!excluded.has(child.locationId)) {
        excluded.add(child.locationId)
        queue.push(child.locationId)
      }
    }
  }

  return props.locations.filter((item) => !excluded.has(item.locationId))
})

async function handleSubmit() {
  formError.value = ''
  const missing =
    props.mode === 'create'
      ? 'Código, nombre y tipo de ubicación son obligatorios.'
      : 'Nombre y tipo de ubicación son obligatorios.'

  if (!name.value.trim() || !locationTypeId.value || (props.mode === 'create' && !code.value.trim())) {
    formError.value = missing
    return
  }

  const resolvedParent = parent.value === '' ? null : Number(parent.value)
  submitting.value = true
  try {
    if (props.mode === 'create') {
      const created = await locationsApi.create({
        code: code.value.trim(),
        name: name.value.trim(),
        locationTypeId: Number(locationTypeId.value),
        parentLocationId: resolvedParent,
      })
      emit('saved', created.locationId, resolvedParent)
    } else {
      if (!props.initial) return
      await locationsApi.update(props.initial.locationId, props.initial.version, {
        name: name.value.trim(),
        locationTypeId: Number(locationTypeId.value),
        parentLocationId: resolvedParent,
      })
      emit('saved', props.initial.locationId, resolvedParent)
    }
    emit('close')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      // Otra persona modificó la ubicación: se cierra el formulario y la
      // página recarga (mismo aviso que en la ficha del activo).
      emit('close')
      emit('concurrencyConflict')
      return
    }
    // Errores de negocio (400: código duplicado, ciclo, tipo/padre inexistente) tal cual llegan.
    formError.value = err instanceof ApiError ? err.message : 'No se pudo guardar la ubicación.'
  } finally {
    submitting.value = false
  }
}

const inputClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const selectClass = 'min-h-[44px] w-full cursor-pointer rounded-md border border-border bg-background px-2.5 text-foreground'
</script>

<template>
  <UiModal :open="open" :title="mode === 'create' ? 'Nueva ubicación' : 'Editar ubicación'" @close="emit('close')">
    <form novalidate class="space-y-3" @submit.prevent="handleSubmit">
      <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
        {{ formError }}
      </p>

      <div v-if="mode === 'create'">
        <label :for="codeFieldId" class="mb-1 block text-sm font-medium text-foreground">Código</label>
        <input :id="codeFieldId" v-model="code" type="text" maxlength="40" placeholder="Ej. ED-B-P1-LAB" :class="inputClass" />
        <p class="mt-1 text-xs text-muted-foreground">Identificador estable: no se puede cambiar después de crear.</p>
      </div>
      <div v-else>
        <span class="mb-1 block text-xs font-medium text-muted-foreground">Código</span>
        <p class="text-sm text-foreground">{{ initial?.code }}</p>
        <p class="mt-1 text-xs text-muted-foreground">El código no se puede editar.</p>
      </div>

      <div>
        <label :for="nameFieldId" class="mb-1 block text-sm font-medium text-foreground">Nombre</label>
        <input :id="nameFieldId" v-model="name" type="text" maxlength="150" placeholder="Ej. Laboratorio de redes" :class="inputClass" />
      </div>

      <div>
        <label :for="typeFieldId" class="mb-1 block text-sm font-medium text-foreground">Tipo de ubicación</label>
        <select :id="typeFieldId" v-model="locationTypeId" :class="selectClass">
          <option value="">Selecciona un tipo</option>
          <option v-for="type in locationTypes" :key="type.id" :value="String(type.id)">{{ type.name }}</option>
        </select>
      </div>

      <div>
        <label :for="parentFieldId" class="mb-1 block text-sm font-medium text-foreground">Ubicación padre</label>
        <select :id="parentFieldId" v-model="parent" :class="selectClass">
          <option value="">Ninguna (raíz)</option>
          <option v-for="item in parentOptions" :key="item.locationId" :value="String(item.locationId)">
            {{ item.name }} ({{ item.code }})
          </option>
        </select>
      </div>

      <div class="flex justify-end gap-2 pt-1">
        <UiButton :disabled="submitting" @click="emit('close')">Cancelar</UiButton>
        <UiButton type="submit" variant="primary" :disabled="submitting">
          {{ submitting ? 'Guardando…' : 'Guardar' }}
        </UiButton>
      </div>
    </form>
  </UiModal>
</template>
