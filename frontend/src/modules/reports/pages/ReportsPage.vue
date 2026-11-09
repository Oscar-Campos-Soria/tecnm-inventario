<script setup lang="ts">
import { ref, useId } from 'vue'
import { ApiError, http } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { downloadFile } from '../../../shared/utils/download'

interface CatalogItem {
  id: number
  code: string
  name: string
}

type ReportCode = 'assets-by-location' | 'assets' | 'open-incidents' | 'maintenance' | 'licenses'
type Format = 'xlsx' | 'pdf'

const REPORTS: { code: ReportCode; title: string; text: string }[] = [
  { code: 'assets', title: 'Resumen de activos', text: 'Cada equipo con tipo, estado, marca, serie, ubicación y resguardante.' },
  { code: 'assets-by-location', title: 'Equipos por ubicación', text: 'Equipos en servicio agrupados por edificio y área.' },
  { code: 'open-incidents', title: 'Incidentes abiertos', text: 'Fallas sin resolver por prioridad, con técnico y días abiertos.' },
  { code: 'maintenance', title: 'Mantenimientos realizados', text: 'Bitácora de preventivos y correctivos con costo, por rango de fechas.' },
  { code: 'licenses', title: 'Licencias de software', text: 'Uso, disponibilidad y vencimiento. No incluye claves.' },
]

const statuses = ref<CatalogItem[]>([])
const types = ref<CatalogItem[]>([])
http
  .get<{ assetStatuses: CatalogItem[]; assetTypes: CatalogItem[] }>('/api/catalogs')
  .then((c) => {
    statuses.value = c.assetStatuses
    types.value = c.assetTypes
  })
  .catch(() => {})

const filters = ref({ status: '', typeId: '', from: '', to: '' })
const busy = ref<string | null>(null)
const error = ref('')
const ids = { status: useId(), type: useId(), from: useId(), to: useId() }

async function download(code: ReportCode, format: Format) {
  const f = filters.value
  if (code === 'maintenance' && f.from && f.to && f.to < f.from) return void (error.value = 'La fecha final no puede ser anterior a la inicial.')
  const query = new URLSearchParams({ format })
  if (code === 'assets') {
    if (f.status) query.set('status', f.status)
    if (f.typeId) query.set('assetTypeId', f.typeId)
  }
  if (code === 'maintenance') {
    if (f.from) query.set('from', f.from)
    if (f.to) query.set('to', f.to)
  }
  busy.value = `${code}-${format}`
  error.value = ''
  try {
    await downloadFile(`/api/reports/${code}?${query}`)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo generar el reporte.'
  } finally {
    busy.value = null
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-sm text-foreground'
const label = 'mb-1 block text-xs font-medium text-secondary'
</script>

<template>
  <div class="space-y-4">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Reportes</h1>
      <p class="mt-1 text-sm text-muted-foreground">Descarga el inventario en Excel para trabajarlo o en PDF para imprimirlo o archivarlo.</p>
    </header>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <ul class="grid gap-3 lg:grid-cols-2">
      <li v-for="r in REPORTS" :key="r.code" class="flex flex-col rounded-xl border border-border bg-card p-5">
        <h2 class="font-display text-base font-semibold text-foreground">{{ r.title }}</h2>
        <p class="mt-1 text-sm text-muted-foreground">{{ r.text }}</p>

        <div v-if="r.code === 'assets'" class="mt-3 grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.status" :class="label">Estado</label>
            <select :id="ids.status" v-model="filters.status" :class="[field, 'cursor-pointer px-2.5']">
              <option value="">En servicio (sin dados de baja)</option>
              <option v-for="s in statuses" :key="s.code" :value="s.code">{{ s.name }}</option>
            </select>
          </div>
          <div>
            <label :for="ids.type" :class="label">Tipo</label>
            <select :id="ids.type" v-model="filters.typeId" :class="[field, 'cursor-pointer px-2.5']">
              <option value="">Todos</option>
              <option v-for="t in types" :key="t.id" :value="String(t.id)">{{ t.name }}</option>
            </select>
          </div>
        </div>
        <div v-else-if="r.code === 'maintenance'" class="mt-3 grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.from" :class="label">Desde</label>
            <input :id="ids.from" v-model="filters.from" type="date" :class="field" />
          </div>
          <div>
            <label :for="ids.to" :class="label">Hasta</label>
            <input :id="ids.to" v-model="filters.to" type="date" :min="filters.from" :class="field" />
          </div>
        </div>

        <div class="mt-auto flex flex-wrap gap-2 pt-4">
          <UiButton variant="primary" :disabled="busy !== null" @click="download(r.code, 'xlsx')">
            {{ busy === `${r.code}-xlsx` ? 'Generando…' : 'Excel' }}
          </UiButton>
          <UiButton :disabled="busy !== null" @click="download(r.code, 'pdf')">
            {{ busy === `${r.code}-pdf` ? 'Generando…' : 'PDF' }}
          </UiButton>
        </div>
      </li>
    </ul>
    <p class="text-xs text-muted-foreground">Cada reporte trae hasta 10,000 filas; si hay más, usa los filtros.</p>
  </div>
</template>
