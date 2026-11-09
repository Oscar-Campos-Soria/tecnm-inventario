<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { ApiError } from '../api/http'
import { ALLOWED_MIME_TYPES, MAX_DOCUMENT_BYTES, documentsApi } from '../api/documents'
import type { DocumentOwnerType, StoredDocument } from '../api/documents'
import { formatBytes, formatDate } from '../utils/format'
import { useAuth } from '../hooks/useAuth'
import UiModal from './UiModal.vue'

// Lista de adjuntos de un registro (evidencias de incidente, fotos de ticket).
const props = withDefaults(
  defineProps<{
    ownerType: DocumentOwnerType
    ownerId: number
    title?: string
    emptyText?: string
    // Muestra el botón para adjuntar (el servidor valida dueño y estado).
    canUpload?: boolean
    // Puede eliminar cualquier archivo; sin esto, solo los que subió (el servidor decide).
    canDelete?: boolean
  }>(),
  { title: 'Evidencias', emptyText: 'Sin evidencias adjuntas.', canUpload: false, canDelete: false },
)

const { session } = useAuth()
const deleting = ref<number | null>(null)

function canRemove(file: StoredDocument) {
  return props.canDelete || (file.createdBy !== null && file.createdBy === session.value?.userId)
}

async function handleDelete(file: StoredDocument) {
  if (!window.confirm(`¿Eliminar ${file.fileName}? Ya no se podrá ver ni descargar.`)) return
  error.value = ''
  deleting.value = file.documentId
  try {
    await documentsApi.remove(file.documentId, file.version)
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      error.value = 'Alguien más cambió este archivo. Se recargó la lista.'
      load()
    } else {
      error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar el archivo.'
    }
  } finally {
    deleting.value = null
  }
}

const items = ref<StoredDocument[] | null>(null)
const error = ref('')
const uploading = ref(false)
const preview = ref<{ url: string; fileName: string } | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
// Blob URLs de PDFs abiertos en pestaña nueva: se revocan al desmontar.
let blobUrls: string[] = []

function load() {
  documentsApi
    .list(props.ownerType, props.ownerId)
    .then((documents) => (items.value = documents))
    .catch((err) => {
      error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los archivos.'
    })
}

watch(() => [props.ownerType, props.ownerId], load, { immediate: true })

onBeforeUnmount(() => blobUrls.forEach((url) => URL.revokeObjectURL(url)))

async function handlePreview(file: StoredDocument) {
  error.value = ''
  try {
    const { blob, fileName } = await documentsApi.download(file.documentId)
    const url = URL.createObjectURL(blob)
    if (file.mimeType === 'application/pdf') {
      if (!window.open(url, '_blank')) {
        error.value = 'El navegador bloqueó la pestaña. Usa Descargar.'
        URL.revokeObjectURL(url)
        return
      }
      blobUrls.push(url)
      return
    }
    // Vista previa de imagen: el URL se revoca al cerrar.
    blobUrls.push(url)
    preview.value = { url, fileName }
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo abrir el archivo.'
  }
}

function closePreview() {
  if (!preview.value) return
  const { url } = preview.value
  URL.revokeObjectURL(url)
  blobUrls = blobUrls.filter((u) => u !== url)
  preview.value = null
}

async function handleDownload(file: StoredDocument) {
  error.value = ''
  try {
    const { blob, fileName } = await documentsApi.download(file.documentId)
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    // Diferido para que el navegador alcance a iniciar la descarga.
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo descargar el archivo.'
  }
}

async function handleFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  error.value = ''
  // Mismo límite que el backend, para avisar antes de subir.
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    error.value = 'Solo se aceptan fotos JPG o PNG, o archivos PDF.'
    return
  }
  if (file.size > MAX_DOCUMENT_BYTES) {
    error.value = `El archivo pesa ${formatBytes(file.size)}; el máximo es 5 MB.`
    return
  }
  uploading.value = true
  try {
    await documentsApi.upload(props.ownerType, props.ownerId, file)
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo subir el archivo.'
  } finally {
    uploading.value = false
  }
}

const buttonClass =
  'min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted'
</script>

<template>
  <section :aria-label="title" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex items-center justify-between gap-3">
      <h2 class="font-display text-base font-semibold text-foreground">{{ title }}</h2>
      <template v-if="canUpload">
        <button type="button" :class="[buttonClass, 'disabled:cursor-wait disabled:opacity-60']" :disabled="uploading" @click="fileInput?.click()">
          {{ uploading ? 'Subiendo…' : 'Adjuntar foto o PDF' }}
        </button>
        <!-- capture no se fuerza: en el celular el sistema ofrece cámara o galería. -->
        <input ref="fileInput" type="file" class="sr-only" accept="image/jpeg,image/png,application/pdf" tabindex="-1" aria-hidden="true" @change="handleFile" />
      </template>
    </div>

    <p v-if="error" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <p v-if="items === null && !error" class="text-sm text-muted-foreground">Cargando archivos…</p>
    <p v-else-if="items !== null && items.length === 0" class="text-sm text-muted-foreground">{{ emptyText }}</p>

    <ul v-if="items !== null && items.length > 0" class="space-y-2">
      <li v-for="file in items" :key="file.documentId" class="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border px-3 py-2.5 text-sm">
        <svg viewBox="0 0 24 24" class="h-5 w-5 shrink-0 text-secondary" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <template v-if="file.mimeType.startsWith('image/')">
            <rect x="3" y="4" width="18" height="16" rx="2" />
            <circle cx="9" cy="10" r="1.6" />
            <path d="M4 18l5-5 3 3 4-4 4 4" />
          </template>
          <template v-else>
            <path d="M7 3h7l4 4v14H7z" />
            <path d="M14 3v4h4" />
            <path d="M10 12h5M10 16h5" />
          </template>
        </svg>
        <div class="min-w-0 flex-1">
          <p class="truncate font-medium text-foreground">{{ file.fileName }}</p>
          <p class="text-xs text-muted-foreground">
            {{ formatBytes(file.sizeBytes) }}, {{ formatDate(file.createdAt) }}<template v-if="file.description">. {{ file.description }}</template>
          </p>
        </div>
        <button type="button" :aria-label="`Ver ${file.fileName}`" :class="buttonClass" @click="handlePreview(file)">Ver</button>
        <button type="button" :aria-label="`Descargar ${file.fileName}`" :class="buttonClass" @click="handleDownload(file)">Descargar</button>
        <button
          v-if="canRemove(file)"
          type="button"
          :aria-label="`Eliminar ${file.fileName}`"
          :disabled="deleting !== null"
          class="min-h-[44px] cursor-pointer rounded-md px-2 text-xs text-muted-foreground underline-offset-2 hover:text-destructive hover:underline disabled:cursor-wait disabled:opacity-60"
          @click="handleDelete(file)"
        >
          {{ deleting === file.documentId ? 'Eliminando…' : 'Eliminar' }}
        </button>
      </li>
    </ul>

    <UiModal :open="preview !== null" :title="preview?.fileName ?? 'Archivo'" @close="closePreview">
      <img v-if="preview" :src="preview.url" :alt="preview.fileName" class="max-h-[70vh] w-full rounded-md border border-border object-contain" />
    </UiModal>
  </section>
</template>
