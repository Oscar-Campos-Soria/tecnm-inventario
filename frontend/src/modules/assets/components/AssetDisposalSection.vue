<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import DocumentList from '../../../shared/components/DocumentList.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import UiField from '../../../shared/components/UiField.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { AssetDetail, AssetDisposalInfo, GetCatalogsResponse } from '../types'

// Baja formal del activo: si ya está dado de baja muestra el acta; si no,
// ofrece el botón (assets.delete). Las precondiciones (incidentes abiertos,
// asignación activa) las valida el servidor y llegan como mensaje.
const props = defineProps<{
  asset: AssetDetail
  catalogs: GetCatalogsResponse | null
}>()

const emit = defineEmits<{ changed: []; conflict: [] }>()

const { hasPermission } = useAuth()
const canDispose = computed(() => hasPermission('assets.delete'))
const isDisposed = computed(() => props.asset.statusCode === 'DADO_DE_BAJA')

const reasonFieldId = useId()
const folioFieldId = useId()
const dateFieldId = useId()
const dictamenFieldId = useId()

const disposal = ref<AssetDisposalInfo | null>(null)
const open = ref(false)
const reasonId = ref('')
const folio = ref('')
const disposedAt = ref('')
const dictamen = ref('')
const saving = ref(false)
const error = ref('')

const today = todayIso

watch(
  isDisposed,
  (disposed) => {
    disposal.value = null
    if (disposed) {
      assetsApi.disposal(props.asset.assetId).then((d) => (disposal.value = d)).catch(() => (disposal.value = null))
    }
  },
  { immediate: true },
)

function openForm() {
  reasonId.value = ''
  folio.value = ''
  disposedAt.value = today()
  dictamen.value = ''
  error.value = ''
  open.value = true
}

async function submit() {
  error.value = ''
  if (!reasonId.value || !folio.value.trim()) {
    error.value = 'El motivo y el folio del acta son obligatorios.'
    return
  }
  saving.value = true
  try {
    await assetsApi.dispose(props.asset.version, {
      assetId: props.asset.assetId,
      disposalReasonId: Number(reasonId.value),
      folio: folio.value.trim(),
      dictamen: dictamen.value.trim() || undefined,
      disposedAt: disposedAt.value || undefined,
    })
    open.value = false
    emit('changed')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      open.value = false
      emit('conflict')
      return
    }
    error.value = err instanceof ApiError ? err.message : 'No se pudo registrar la baja.'
  } finally {
    saving.value = false
  }
}

const fieldClass = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <section v-if="isDisposed" aria-labelledby="baja-formal" class="space-y-4 rounded-lg border border-border bg-card p-5">
    <h2 id="baja-formal" class="font-display text-base font-semibold text-foreground">Baja formal</h2>
    <p v-if="!disposal" class="text-sm text-muted-foreground">Cargando datos de la baja…</p>
    <dl v-else class="grid gap-x-8 gap-y-3 sm:grid-cols-2">
      <UiField label="Folio del acta" :value="disposal.folio" />
      <UiField label="Motivo" :value="disposal.reasonName" />
      <UiField label="Fecha de baja" :value="formatDate(disposal.disposedAt)" />
      <UiField label="Registró" :value="disposal.createdByUsername" />
      <div class="sm:col-span-2">
        <UiField label="Dictamen" :value="disposal.dictamen" multiline />
      </div>
    </dl>
    <!-- El acta firmada solo la ve y la sube quien da de baja (assets.delete). -->
    <DocumentList
      v-if="disposal && canDispose"
      owner-type="ASSET_DISPOSAL"
      :owner-id="disposal.assetDisposalId"
      title="Acta firmada"
      empty-text="Aún no se adjunta el acta firmada (PDF)."
      can-upload
    />
  </section>

  <section v-else-if="canDispose" aria-labelledby="dar-de-baja" class="rounded-lg border border-border bg-card p-5">
    <h2 id="dar-de-baja" class="font-display text-base font-semibold text-foreground">Baja del activo</h2>
    <p class="mt-1 text-sm text-muted-foreground">
      Requiere folio del acta y que el activo no tenga incidentes abiertos ni una asignación sin devolver. Es definitiva.
    </p>
    <UiButton variant="danger" class="mt-3" @click="openForm">Dar de baja</UiButton>

    <UiModal :open="open" :title="`Dar de baja ${asset.inventoryCode}`" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="reasonFieldId" class="mb-1 block text-sm font-medium text-foreground">Motivo</label>
          <select :id="reasonFieldId" v-model="reasonId" :class="[fieldClass, 'cursor-pointer px-2.5']">
            <option value="">Selecciona un motivo</option>
            <option v-for="r in catalogs?.disposalReasons ?? []" :key="r.id" :value="String(r.id)">{{ r.name }}</option>
          </select>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="folioFieldId" class="mb-1 block text-sm font-medium text-foreground">Folio del acta</label>
            <input :id="folioFieldId" v-model="folio" type="text" maxlength="100" :class="fieldClass" placeholder="Folio oficial" />
          </div>
          <div>
            <label :for="dateFieldId" class="mb-1 block text-sm font-medium text-foreground">Fecha de baja</label>
            <input :id="dateFieldId" v-model="disposedAt" type="date" :max="today()" :class="fieldClass" />
          </div>
        </div>
        <div>
          <label :for="dictamenFieldId" class="mb-1 block text-sm font-medium text-foreground">
            Dictamen <span class="font-normal text-muted-foreground">(opcional)</span>
          </label>
          <textarea :id="dictamenFieldId" v-model="dictamen" rows="3" maxlength="4000" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <p class="text-sm text-muted-foreground">Después podrás adjuntar el acta firmada en PDF.</p>
        <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="danger" :disabled="saving">{{ saving ? 'Registrando…' : 'Confirmar baja' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
