<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { AssetComponentItem, CatalogItem } from '../types'

// Hardware interno del equipo (components.manage: ADMIN y TECNICO).
const props = defineProps<{ assetId: number; disposed: boolean; componentTypes: CatalogItem[] }>()

const { hasPermission } = useAuth()
const canManage = computed(() => hasPermission('components.manage'))
const ids = { type: useId(), description: useId(), serial: useId(), date: useId() }

const items = ref<AssetComponentItem[] | null>(null)
const showHistory = ref(false)
const listError = ref('')
const open = ref(false)
// null = agregar; un componente = reemplazar ese.
const replacing = ref<AssetComponentItem | null>(null)
const saving = ref(false)
const error = ref('')
const form = ref({ typeId: '', description: '', serial: '', date: '' })

function load() {
  assetsApi
    .components(props.assetId, showHistory.value)
    .then((r) => {
      listError.value = ''
      items.value = r
    })
    .catch((err) => (listError.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los componentes.'))
}
watch(() => [props.assetId, canManage.value, showHistory.value], () => canManage.value && load(), { immediate: true })

const current = computed(() => items.value?.filter((c) => c.removedAt === null) ?? [])
const removed = computed(() => items.value?.filter((c) => c.removedAt !== null) ?? [])

function openForm(c: AssetComponentItem | null) {
  replacing.value = c
  form.value = { typeId: c ? String(c.componentTypeId) : '', description: '', serial: '', date: todayIso() }
  error.value = ''
  open.value = true
}

// Fecha local del input a mediodía: no cambia de día al pasar a UTC.
const toIso = (date: string) => (date ? new Date(`${date}T12:00:00`).toISOString() : null)

async function submit() {
  const f = form.value
  if (!replacing.value && !f.typeId) return void (error.value = 'Elige el tipo de componente.')
  if (!f.description.trim() && !f.serial.trim()) return void (error.value = 'Describe el componente (marca, modelo, capacidad) o indica su número de serie.')
  if (f.date && f.date > todayIso()) return void (error.value = 'La fecha no puede estar en el futuro.')
  const detail = { description: f.description.trim() || null, serialNumber: f.serial.trim() || null }
  saving.value = true
  error.value = ''
  try {
    if (replacing.value) await assetsApi.replaceComponent(props.assetId, replacing.value, { ...detail, replacedAt: toIso(f.date) })
    else await assetsApi.addComponent(props.assetId, { ...detail, componentTypeId: Number(f.typeId), installedAt: toIso(f.date) })
    open.value = false
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      open.value = false
      listError.value = 'Otra persona modificó ese componente. Recargamos la lista; intenta de nuevo.'
      load()
    } else error.value = err instanceof ApiError ? err.message : 'No se pudo guardar el componente.'
  } finally {
    saving.value = false
  }
}

async function act(c: AssetComponentItem, kind: 'remove' | 'delete') {
  const question =
    kind === 'remove'
      ? `¿Registrar que se retiró ${label(c)} sin reemplazo?`
      : `¿Eliminar ${label(c)}? Úsalo solo si se capturó por error; queda en la bitácora de auditoría.`
  if (!window.confirm(question)) return
  try {
    if (kind === 'remove') await assetsApi.removeComponent(props.assetId, c)
    else await assetsApi.deleteComponent(props.assetId, c)
  } catch (err) {
    listError.value =
      err instanceof ApiError && err.status === 409
        ? 'Otra persona modificó ese componente. Recargamos la lista; intenta de nuevo.'
        : err instanceof ApiError ? err.message : 'No se pudo actualizar el componente.'
  }
  load()
}

function label(c: AssetComponentItem) {
  return c.description ?? `${c.componentTypeName} ${c.serialNumber ?? ''}`.trim()
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <section v-if="canManage" aria-labelledby="componentes" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="componentes" class="font-display text-base font-semibold text-foreground">Componentes</h2>
      <UiButton v-if="!disposed" @click="openForm(null)">Agregar componente</UiButton>
    </div>
    <p v-if="listError" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ listError }}</p>
    <p v-if="!items && !listError" class="text-sm text-muted-foreground">Cargando componentes…</p>
    <template v-else-if="items">
      <p v-if="current.length === 0" class="text-sm text-muted-foreground">
        Sin componentes registrados. Agrega la memoria, el disco o el procesador para llevar su historial.
      </p>
      <ul v-else class="space-y-2">
        <li v-for="c in current" :key="c.assetComponentId" class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2.5 text-sm">
          <div>
            <p class="text-xs font-medium text-secondary">{{ c.componentTypeName }}</p>
            <p class="font-medium text-foreground">{{ c.description ?? 'Sin descripción' }}</p>
            <p class="text-xs text-muted-foreground">
              <template v-if="c.serialNumber">Serie {{ c.serialNumber }}. </template>Instalado el {{ formatDate(c.installedAt) }}
            </p>
          </div>
          <div v-if="!disposed" class="flex flex-wrap gap-2">
            <UiButton @click="openForm(c)">Reemplazar</UiButton>
            <UiButton @click="act(c, 'remove')">Retirar</UiButton>
            <UiButton variant="danger" :aria-label="`Eliminar ${label(c)} (captura errónea)`" @click="act(c, 'delete')">Eliminar</UiButton>
          </div>
        </li>
      </ul>

      <div class="mt-3">
        <UiButton :aria-expanded="showHistory" @click="showHistory = !showHistory">
          {{ showHistory ? 'Ocultar componentes anteriores' : 'Ver componentes anteriores' }}
        </UiButton>
        <template v-if="showHistory">
          <p v-if="removed.length === 0" class="mt-2 text-sm text-muted-foreground">Ningún componente se ha reemplazado ni retirado.</p>
          <ul v-else class="mt-2 divide-y divide-border rounded-md border border-border text-sm">
            <li v-for="c in removed" :key="c.assetComponentId" class="px-3 py-2">
              <span class="text-foreground">{{ c.componentTypeName }}: {{ label(c) }}</span>
              <span class="text-muted-foreground">, del {{ formatDate(c.installedAt) }} al {{ formatDate(c.removedAt) }}</span>
            </li>
          </ul>
        </template>
      </div>
    </template>

    <UiModal :open="open" :title="replacing ? `Reemplazar ${label(replacing)}` : 'Agregar componente'" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <p v-if="replacing" class="rounded-md bg-muted px-3 py-2 text-sm text-secondary">
          El actual queda en el historial como retirado en la fecha del cambio; el nuevo es del mismo tipo ({{ replacing.componentTypeName }}).
        </p>
        <div v-else>
          <label :for="ids.type" class="mb-1 block text-sm font-medium text-foreground">Tipo</label>
          <select :id="ids.type" v-model="form.typeId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="" disabled>Elige el tipo</option>
            <option v-for="t in componentTypes" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
          </select>
        </div>
        <div>
          <label :for="ids.description" class="mb-1 block text-sm font-medium text-foreground">Descripción</label>
          <input :id="ids.description" v-model="form.description" type="text" maxlength="250" placeholder="Kingston 16 GB DDR4 3200" :class="field" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.serial" class="mb-1 block text-sm font-medium text-foreground">Número de serie (opcional)</label>
            <input :id="ids.serial" v-model="form.serial" type="text" maxlength="150" :class="field" />
          </div>
          <div>
            <label :for="ids.date" class="mb-1 block text-sm font-medium text-foreground">{{ replacing ? 'Fecha del cambio' : 'Fecha de instalación' }}</label>
            <input :id="ids.date" v-model="form.date" type="date" :max="todayIso()" :class="field" />
          </div>
        </div>
        <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : replacing ? 'Reemplazar componente' : 'Agregar componente' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
