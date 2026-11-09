<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import { documentsApi, type AssetDocument } from '../../../shared/api/documents'
import { useAuth } from '../../../shared/hooks/useAuth'
import { downloadFile } from '../../../shared/utils/download'
import { formatBytes, formatDate } from '../../../shared/utils/format'

// Todos los archivos del equipo, de sus incidentes, mantenimientos, baja,
// compras y tickets. Solo lectura: se adjuntan y borran en cada registro.
// El servidor omite los registros que el usuario no puede ver.
const props = defineProps<{ assetId: number }>()

const { hasPermission } = useAuth()
const items = ref<AssetDocument[] | null>(null)
const error = ref('')
let blobUrls: string[] = []

watch(
  () => props.assetId,
  (id) =>
    documentsApi
      .forAsset(id)
      .then((d) => {
        error.value = ''
        items.value = d
      })
      .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los documentos.')),
  { immediate: true },
)

onBeforeUnmount(() => blobUrls.forEach((url) => URL.revokeObjectURL(url)))

// Enlace al registro dueño cuando tiene página propia y el usuario puede abrirla.
function ownerLink(d: AssetDocument): string | null {
  if (d.ownerType === 'INCIDENT' && hasPermission('incidents.view')) return `/incidents/${d.ownerId}`
  if (d.ownerType === 'TICKET') return `/tickets/${d.ownerId}`
  if (d.ownerType === 'ACQUISITION' && hasPermission('procurement.manage')) return '/procurement'
  return null
}

async function view(d: AssetDocument) {
  error.value = ''
  try {
    const { blob } = await documentsApi.download(d.documentId)
    const url = URL.createObjectURL(blob)
    if (!window.open(url, '_blank')) {
      URL.revokeObjectURL(url)
      error.value = 'El navegador bloqueó la pestaña. Usa Descargar.'
      return
    }
    blobUrls.push(url)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo abrir el archivo.'
  }
}

async function download(d: AssetDocument) {
  error.value = ''
  try {
    await downloadFile(`/api/documents/${d.documentId}/download`)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo descargar el archivo.'
  }
}

const buttonClass =
  'min-h-[44px] cursor-pointer rounded-md border border-border px-3 text-sm text-foreground transition-colors duration-150 hover:bg-muted'
</script>

<template>
  <section aria-labelledby="documentos" class="rounded-lg border border-border bg-card p-5">
    <h2 id="documentos" class="mb-1 font-display text-base font-semibold text-foreground">Documentos</h2>
    <p class="mb-3 text-sm text-muted-foreground">Fotos, facturas y actas de este equipo. Se adjuntan desde cada incidente, mantenimiento, compra o ticket.</p>

    <p v-if="error" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="items === null && !error" class="text-sm text-muted-foreground">Cargando documentos…</p>
    <p v-else-if="items !== null && items.length === 0" class="text-sm text-muted-foreground">Este equipo aún no tiene archivos.</p>

    <ul v-if="items !== null && items.length > 0" class="space-y-2">
      <li v-for="d in items" :key="d.documentId" class="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border border-border px-3 py-2.5 text-sm">
        <div class="min-w-0 flex-1">
          <p class="truncate font-medium text-foreground">{{ d.fileName }}</p>
          <p class="text-xs text-muted-foreground">
            <RouterLink v-if="ownerLink(d)" :to="ownerLink(d)!" class="text-primary underline-offset-2 hover:underline">{{ d.ownerLabel }}</RouterLink>
            <span v-else>{{ d.ownerLabel }}</span>
            <span>, {{ formatBytes(d.sizeBytes) }}, {{ formatDate(d.createdAt) }}</span>
            <span v-if="d.description">. {{ d.description }}</span>
          </p>
        </div>
        <button type="button" :aria-label="`Ver ${d.fileName}`" :class="buttonClass" @click="view(d)">Ver</button>
        <button type="button" :aria-label="`Descargar ${d.fileName}`" :class="buttonClass" @click="download(d)">Descargar</button>
      </li>
    </ul>
  </section>
</template>
