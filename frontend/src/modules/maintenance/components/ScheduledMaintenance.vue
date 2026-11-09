<script setup lang="ts">
import { onBeforeUnmount, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { maintenanceApi } from '../api'
import type { MaintenanceItem } from '../types'

// Mantenimientos programados a futuro: se agendan aquí y se marcan como realizados.
const emit = defineEmits<{ completed: [] }>()

const items = ref<MaintenanceItem[] | null>(null)
const error = ref('')
const types = ref<{ id: number; code: string; name: string }[]>([])
const ids = { asset: useId(), type: useId(), date: useId(), description: useId(), done: useId(), parts: useId(), cost: useId(), next: useId(), doneDescription: useId() }

function load() {
  maintenanceApi
    .scheduled(90)
    .then((r) => {
      error.value = ''
      items.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los programados.'))
}
load()

const today = todayIso()
const isLate = (iso: string | null) => !!iso && iso.slice(0, 10) < today
// "2026-11-08" del input -> mediodía local en ISO (no cambia de día al pasar a UTC).
const toIso = (date: string) => new Date(`${date}T12:00:00`).toISOString()

// ---------- Programar ----------
const scheduling = ref(false)
const saving = ref(false)
const formError = ref('')
const form = ref({ typeId: '', date: '', description: '' })
const assetSearch = ref('')
const assetResults = ref<{ assetId: number; inventoryCode: string; assetTypeName: string }[]>([])
const asset = ref<{ assetId: number; inventoryCode: string } | null>(null)

async function openSchedule() {
  form.value = { typeId: '', date: '', description: '' }
  asset.value = null
  assetSearch.value = ''
  formError.value = ''
  scheduling.value = true
  if (types.value.length === 0) types.value = (await maintenanceApi.catalogs().catch(() => ({ maintenanceTypes: [] }))).maintenanceTypes
}

let timer: ReturnType<typeof setTimeout> | undefined
watch(assetSearch, (v) => {
  clearTimeout(timer)
  if (v.trim().length < 2) return void (assetResults.value = [])
  timer = setTimeout(() => {
    maintenanceApi.searchAssets(v.trim()).then((r) => (assetResults.value = r)).catch(() => (assetResults.value = []))
  }, 250)
})
onBeforeUnmount(() => clearTimeout(timer))

async function schedule() {
  const f = form.value
  if (!asset.value) return void (formError.value = 'Elige el equipo.')
  if (!f.typeId || !f.date || !f.description.trim()) return void (formError.value = 'Tipo, fecha y descripción son obligatorios.')
  if (f.date < today) return void (formError.value = 'La fecha programada no puede estar en el pasado.')
  saving.value = true
  formError.value = ''
  try {
    await maintenanceApi.schedule({ assetId: asset.value.assetId, maintenanceTypeId: Number(f.typeId), scheduledAt: toIso(f.date), description: f.description.trim() })
    scheduling.value = false
    load()
  } catch (err) {
    formError.value = err instanceof ApiError ? err.message : 'No se pudo programar el mantenimiento.'
  } finally {
    saving.value = false
  }
}

// ---------- Marcar realizado ----------
const completing = ref<MaintenanceItem | null>(null)
const done = ref({ date: '', description: '', parts: '', cost: '', next: '' })

function openComplete(item: MaintenanceItem) {
  completing.value = item
  done.value = { date: today, description: item.description ?? '', parts: '', cost: '', next: '' }
  formError.value = ''
}

async function complete() {
  const item = completing.value
  const d = done.value
  if (!item) return
  if (!d.date || d.date > today) return void (formError.value = 'Indica cuándo se realizó (no puede ser una fecha futura).')
  if (d.next && d.next <= d.date) return void (formError.value = 'El próximo mantenimiento debe ser posterior a la realización.')
  const cost = d.cost.trim() === '' ? null : Number(d.cost)
  if (cost !== null && (Number.isNaN(cost) || cost < 0)) return void (formError.value = 'El costo debe ser un número positivo.')
  saving.value = true
  formError.value = ''
  try {
    await maintenanceApi.complete(item, {
      performedAt: toIso(d.date),
      description: d.description.trim() || null,
      partsUsed: d.parts.trim() || null,
      cost,
      nextMaintenanceAt: d.next ? toIso(d.next) : null,
    })
    completing.value = null
    load()
    emit('completed')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      completing.value = null
      error.value = 'Otra persona modificó ese mantenimiento. Recargamos la lista; intenta de nuevo.'
      load()
    } else formError.value = err instanceof ApiError ? err.message : 'No se pudo registrar.'
  } finally {
    saving.value = false
  }
}

async function cancel(item: MaintenanceItem) {
  if (!window.confirm(`¿Cancelar el mantenimiento programado de ${item.assetInventoryCode}?`)) return
  try {
    await maintenanceApi.cancelScheduled(item)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo cancelar.'
  }
  load()
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const label = 'mb-1 block text-sm font-medium text-foreground'
</script>

<template>
  <section aria-labelledby="programados" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="programados" class="font-display text-base font-semibold text-foreground">Programados</h2>
      <UiButton variant="primary" @click="openSchedule">Programar mantenimiento</UiButton>
    </div>
    <p v-if="error" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="items === null && !error" class="text-sm text-muted-foreground">Cargando…</p>
    <p v-else-if="items && items.length === 0" class="text-sm text-muted-foreground">Nada programado en los próximos 90 días.</p>
    <ul v-else-if="items" class="divide-y divide-border">
      <li v-for="item in items" :key="item.maintenanceRecordId" class="flex flex-wrap items-center gap-x-4 gap-y-2 py-2.5 text-sm">
        <span :class="['rounded-full px-2.5 py-0.5 text-xs font-semibold', isLate(item.scheduledAt) ? 'bg-destructive text-on-destructive' : 'bg-muted text-secondary']">
          {{ isLate(item.scheduledAt) ? `Atrasado, era el ${formatDate(item.scheduledAt)}` : formatDate(item.scheduledAt) }}
        </span>
        <RouterLink :to="`/assets/${item.assetId}`" class="font-medium text-accent hover:underline">{{ item.assetInventoryCode }}</RouterLink>
        <span class="text-secondary">{{ item.typeName }}</span>
        <span class="min-w-0 flex-1 truncate text-muted-foreground">{{ item.description }}</span>
        <span v-if="item.performedByName" class="text-xs text-muted-foreground">{{ item.performedByName }}</span>
        <div class="flex gap-2">
          <UiButton @click="openComplete(item)">Marcar realizado</UiButton>
          <UiButton @click="cancel(item)">Cancelar</UiButton>
        </div>
      </li>
    </ul>

    <UiModal :open="scheduling" title="Programar mantenimiento" @close="scheduling = false">
      <form novalidate class="space-y-3" @submit.prevent="schedule">
        <div class="relative">
          <label :for="ids.asset" :class="label">Equipo</label>
          <p v-if="asset" class="flex items-center justify-between rounded-md border border-border px-3 py-2 text-sm">
            <span class="font-medium text-foreground">{{ asset.inventoryCode }}</span>
            <button type="button" class="cursor-pointer text-accent hover:underline" @click="asset = null">Cambiar</button>
          </p>
          <template v-else>
            <input :id="ids.asset" v-model="assetSearch" type="search" autocomplete="off" placeholder="Buscar por código, serie o hostname" :class="field" />
            <ul v-if="assetResults.length" class="absolute z-10 mt-1 max-h-48 w-full overflow-auto rounded-md border border-border bg-card shadow">
              <li v-for="a in assetResults" :key="a.assetId">
                <button type="button" class="w-full cursor-pointer px-3 py-2 text-left text-sm hover:bg-muted" @click="(asset = a), (assetSearch = ''), (assetResults = [])">
                  <span class="font-medium text-foreground">{{ a.inventoryCode }}</span> <span class="text-muted-foreground">{{ a.assetTypeName }}</span>
                </button>
              </li>
            </ul>
          </template>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.type" :class="label">Tipo</label>
            <select :id="ids.type" v-model="form.typeId" :class="[field, 'cursor-pointer px-2.5']">
              <option value="" disabled>Elige el tipo</option>
              <option v-for="t in types" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </div>
          <div>
            <label :for="ids.date" :class="label">Fecha</label>
            <input :id="ids.date" v-model="form.date" type="date" :min="today" :class="field" />
          </div>
        </div>
        <div>
          <label :for="ids.description" :class="label">Qué se va a hacer</label>
          <textarea :id="ids.description" v-model="form.description" rows="2" maxlength="4000" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="scheduling = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Programar' }}</UiButton>
        </div>
      </form>
    </UiModal>

    <UiModal :open="completing !== null" :title="completing ? `Realizado en ${completing.assetInventoryCode}` : ''" @close="completing = null">
      <form novalidate class="space-y-3" @submit.prevent="complete">
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.done" :class="label">Fecha de realización</label>
            <input :id="ids.done" v-model="done.date" type="date" :max="today" :class="field" />
          </div>
          <div>
            <label :for="ids.next" :class="label">Próximo (opcional)</label>
            <input :id="ids.next" v-model="done.next" type="date" :min="done.date" :class="field" />
          </div>
        </div>
        <div>
          <label :for="ids.doneDescription" :class="label">Qué se hizo</label>
          <textarea :id="ids.doneDescription" v-model="done.description" rows="2" maxlength="4000" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.parts" :class="label">Refacciones (opcional)</label>
            <input :id="ids.parts" v-model="done.parts" type="text" maxlength="2000" :class="field" />
          </div>
          <div>
            <label :for="ids.cost" :class="label">Costo MXN (opcional)</label>
            <input :id="ids.cost" v-model="done.cost" inputmode="decimal" :class="field" />
          </div>
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="completing = null">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Registrar realizado' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
