<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatBytes } from '../../../shared/utils/format'
import { bulkImportApi, type PreviewResult } from '../api'

const MAX_BYTES = 5 * 1024 * 1024
const fileId = useId()

const file = ref<File | null>(null)
const preview = ref<PreviewResult | null>(null)
const onlyErrors = ref(false)
const busy = ref<'template' | 'preview' | 'commit' | null>(null)
const error = ref('')
const created = ref<number | null>(null)

const invalid = computed(() => (preview.value ? preview.value.totalRows - preview.value.validRows : 0))
const shownRows = computed(() => preview.value?.rows.filter((r) => !onlyErrors.value || r.errors.length > 0) ?? [])

async function template() {
  busy.value = 'template'
  error.value = ''
  try {
    await bulkImportApi.template()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo descargar la plantilla.'
  } finally {
    busy.value = null
  }
}

async function onFile(event: Event) {
  const input = event.target as HTMLInputElement
  const chosen = input.files?.[0] ?? null
  preview.value = null
  created.value = null
  error.value = ''
  file.value = null
  if (!chosen) return
  if (!/\.(xlsx|csv)$/i.test(chosen.name)) return void (error.value = 'Sube un archivo .xlsx o .csv.')
  if (chosen.size > MAX_BYTES) return void (error.value = `El archivo pesa ${formatBytes(chosen.size)}; el máximo es 5 MB.`)
  file.value = chosen
  busy.value = 'preview'
  try {
    preview.value = await bulkImportApi.preview(chosen)
    onlyErrors.value = preview.value.validRows < preview.value.totalRows
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo revisar el archivo.'
  } finally {
    busy.value = null
    input.value = ''
  }
}

async function commit() {
  if (!file.value || !preview.value) return
  if (!window.confirm(`¿Dar de alta ${preview.value.totalRows} activos? Quedan DISPONIBLES en las ubicaciones del archivo.`)) return
  busy.value = 'commit'
  error.value = ''
  try {
    const r = await bulkImportApi.commit(file.value)
    created.value = r.created
    preview.value = null
    file.value = null
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo completar la carga.'
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <div class="space-y-4">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Carga masiva de activos</h1>
      <p class="mt-1 text-sm text-muted-foreground">Da de alta muchos equipos desde Excel o CSV. Primero se revisa el archivo; nada se guarda hasta que confirmas.</p>
    </header>

    <ol class="grid gap-3 md:grid-cols-3">
      <li class="rounded-xl border border-border bg-card p-5">
        <p class="text-xs font-semibold text-secondary">Paso 1</p>
        <h2 class="font-display text-base font-semibold text-foreground">Llena la plantilla</h2>
        <p class="mt-1 text-sm text-muted-foreground">Trae las columnas y una hoja con los tipos, ubicaciones y fabricantes válidos.</p>
        <UiButton class="mt-3" :disabled="busy !== null" @click="template">{{ busy === 'template' ? 'Descargando…' : 'Descargar plantilla' }}</UiButton>
      </li>
      <li class="rounded-xl border border-border bg-card p-5">
        <p class="text-xs font-semibold text-secondary">Paso 2</p>
        <h2 class="font-display text-base font-semibold text-foreground">Sube el archivo</h2>
        <p class="mt-1 text-sm text-muted-foreground">.xlsx o .csv, hasta 1000 filas y 5 MB.</p>
        <label :for="fileId" class="mt-3 inline-flex min-h-[44px] cursor-pointer items-center rounded-md border border-border bg-card px-4 text-sm text-primary hover:border-accent hover:bg-muted">
          {{ busy === 'preview' ? 'Revisando…' : 'Elegir archivo' }}
        </label>
        <input :id="fileId" type="file" accept=".xlsx,.csv" class="sr-only" :disabled="busy !== null" @change="onFile" />
        <p v-if="file" class="mt-2 truncate text-xs text-muted-foreground">{{ file.name }}</p>
      </li>
      <li class="rounded-xl border border-border bg-card p-5">
        <p class="text-xs font-semibold text-secondary">Paso 3</p>
        <h2 class="font-display text-base font-semibold text-foreground">Confirma</h2>
        <p class="mt-1 text-sm text-muted-foreground">Se habilita cuando todas las filas están bien. Se crean todas juntas o ninguna.</p>
        <UiButton class="mt-3" variant="primary" :disabled="!preview || invalid > 0 || busy !== null" @click="commit">
          {{ busy === 'commit' ? 'Creando…' : preview ? `Crear ${preview.totalRows} activos` : 'Crear activos' }}
        </UiButton>
      </li>
    </ol>

    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="created !== null" role="status" class="rounded-md bg-success/10 px-3 py-2 text-sm text-success">
      Se crearon {{ created }} activos. <RouterLink to="/assets" class="font-medium underline">Ver activos</RouterLink>
    </p>

    <section v-if="preview" aria-labelledby="revision" class="space-y-3">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="revision" class="font-display text-base font-semibold text-foreground">
          {{ preview.validRows }} de {{ preview.totalRows }} filas listas
          <span v-if="invalid" class="text-priority-high">, {{ invalid }} con errores</span>
        </h2>
        <label v-if="invalid" class="flex min-h-[44px] cursor-pointer items-center gap-2 text-sm text-foreground">
          <input v-model="onlyErrors" type="checkbox" class="h-4 w-4 accent-accent" />
          Solo filas con errores
        </label>
      </div>
      <p v-if="invalid" class="text-sm text-secondary">Corrige el archivo y vuelve a subirlo; los números de fila son los de tu hoja.</p>
      <div class="overflow-x-auto rounded-lg border border-border bg-card">
        <table class="w-full text-left text-sm">
          <thead>
            <tr class="border-b border-border bg-muted">
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Fila</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Código</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Tipo</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Ubicación</th>
              <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Revisión</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in shownRows" :key="r.rowNumber" :class="['border-b border-border last:border-0', r.errors.length ? 'bg-destructive/5' : '']">
              <td class="px-4 py-2.5 tabular-nums text-secondary">{{ r.rowNumber }}</td>
              <td class="px-4 py-2.5 font-medium text-foreground">{{ r.inventoryCode ?? '—' }}</td>
              <td class="px-4 py-2.5 text-secondary">{{ r.typeName ?? '—' }}</td>
              <td class="px-4 py-2.5 text-secondary">{{ r.locationName ?? '—' }}</td>
              <td class="px-4 py-2.5">
                <span v-if="r.errors.length === 0" class="text-success">Lista</span>
                <ul v-else class="list-disc space-y-0.5 pl-4 text-destructive">
                  <li v-for="e in r.errors" :key="e">{{ e }}</li>
                </ul>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
