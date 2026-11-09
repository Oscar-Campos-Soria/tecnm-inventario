<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { assetsApi } from '../api'
import type { AssetSoftwareItem, SoftwareOption } from '../types'

// Programas instalados en el equipo (software.manage: ADMIN y TECNICO).
const props = defineProps<{ assetId: number; disposed: boolean }>()

const { hasPermission } = useAuth()
const canManage = computed(() => hasPermission('software.manage'))
const ids = { software: useId(), version: useId(), date: useId() }

const items = ref<AssetSoftwareItem[] | null>(null)
const showHistory = ref(false)
const listError = ref('')
const notice = ref('')
const catalog = ref<SoftwareOption[]>([])
const open = ref(false)
const saving = ref(false)
const error = ref('')
const form = ref({ softwareId: '', version: '', date: '' })

function load() {
  assetsApi
    .software(props.assetId, showHistory.value)
    .then((r) => {
      listError.value = ''
      items.value = r
    })
    .catch((err) => (listError.value = err instanceof ApiError ? err.message : 'No se pudo cargar el software instalado.'))
}
watch(() => [props.assetId, canManage.value, showHistory.value], () => canManage.value && load(), { immediate: true })

const current = computed(() => items.value?.filter((i) => i.isCurrent) ?? [])
const previous = computed(() => items.value?.filter((i) => !i.isCurrent) ?? [])

async function openForm() {
  form.value = { softwareId: '', version: '', date: todayIso() }
  error.value = ''
  open.value = true
  catalog.value = await assetsApi.softwareCatalog().catch(() => [])
}

async function submit() {
  const f = form.value
  if (!f.softwareId) return void (error.value = 'Elige el programa instalado.')
  if (f.date && f.date > todayIso()) return void (error.value = 'La fecha de instalación no puede estar en el futuro.')
  saving.value = true
  error.value = ''
  try {
    await assetsApi.installSoftware(props.assetId, {
      softwareId: Number(f.softwareId),
      versionInstalled: f.version.trim() || null,
      // La fecha del input es local; mediodía evita que cambie de día al pasar a UTC.
      installedAt: f.date ? new Date(`${f.date}T12:00:00`).toISOString() : null,
    })
    open.value = false
    notice.value = ''
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo registrar la instalación.'
  } finally {
    saving.value = false
  }
}

async function uninstall(item: AssetSoftwareItem) {
  if (!window.confirm(`¿Registrar que ${item.softwareName} se desinstaló de este equipo?`)) return
  try {
    const r = await assetsApi.uninstallSoftware(props.assetId, item)
    notice.value = r.hasActiveLicense
      ? `${item.softwareName} se desinstaló, pero el equipo sigue ocupando una licencia. Libérala en Software, pestaña Licencias.`
      : ''
  } catch (err) {
    listError.value =
      err instanceof ApiError && err.status === 409
        ? 'Otra persona modificó esa instalación. Recargamos la lista; intenta de nuevo.'
        : err instanceof ApiError ? err.message : 'No se pudo registrar la desinstalación.'
  }
  load()
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <section v-if="canManage" aria-labelledby="software-instalado" class="rounded-lg border border-border bg-card p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 id="software-instalado" class="font-display text-base font-semibold text-foreground">Software instalado</h2>
      <UiButton v-if="!disposed" @click="openForm">Registrar instalación</UiButton>
    </div>
    <p v-if="listError" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ listError }}</p>
    <p v-if="notice" role="status" class="mb-3 rounded-md bg-muted px-3 py-2 text-sm text-secondary">
      {{ notice }} <RouterLink to="/software" class="font-medium text-accent hover:underline">Ir a Software</RouterLink>
    </p>
    <p v-if="!items && !listError" class="text-sm text-muted-foreground">Cargando software…</p>
    <template v-else-if="items">
      <p v-if="current.length === 0" class="text-sm text-muted-foreground">No hay programas registrados en este equipo.</p>
      <ul v-else class="space-y-2">
        <li v-for="i in current" :key="i.assetSoftwareId" class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2.5 text-sm">
          <div>
            <p class="font-medium text-foreground">
              {{ i.softwareName }}<template v-if="i.versionInstalled"> {{ i.versionInstalled }}</template>
              <span :class="['ml-2 rounded-full px-2 py-0.5 text-xs font-medium', i.hasActiveLicense ? 'bg-success/10 text-success' : 'bg-muted text-muted-foreground']">
                {{ i.hasActiveLicense ? 'Con licencia' : 'Sin licencia asignada' }}
              </span>
            </p>
            <p class="text-xs text-muted-foreground">{{ i.softwareVendor ? `${i.softwareVendor}, instalado` : 'Instalado' }} el {{ formatDate(i.installedAt) }}</p>
          </div>
          <UiButton v-if="!disposed" @click="uninstall(i)">Desinstalar</UiButton>
        </li>
      </ul>

      <div class="mt-3">
        <UiButton :aria-expanded="showHistory" @click="showHistory = !showHistory">
          {{ showHistory ? 'Ocultar instalaciones anteriores' : 'Ver instalaciones anteriores' }}
        </UiButton>
        <template v-if="showHistory">
          <p v-if="previous.length === 0" class="mt-2 text-sm text-muted-foreground">Sin versiones anteriores ni desinstalaciones.</p>
          <ul v-else class="mt-2 divide-y divide-border rounded-md border border-border text-sm">
            <li v-for="i in previous" :key="i.assetSoftwareId" class="px-3 py-2">
              <span class="text-foreground">{{ i.softwareName }}<template v-if="i.versionInstalled"> {{ i.versionInstalled }}</template></span>
              <span class="text-muted-foreground">, instalado el {{ formatDate(i.installedAt) }}</span>
            </li>
          </ul>
        </template>
      </div>
    </template>

    <UiModal :open="open" title="Registrar instalación" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="ids.software" class="mb-1 block text-sm font-medium text-foreground">Programa</label>
          <select :id="ids.software" v-model="form.softwareId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="" disabled>Elige un programa</option>
            <option v-for="s in catalog" :key="s.softwareId" :value="String(s.softwareId)">{{ s.vendor ? `${s.name} (${s.vendor})` : s.name }}</option>
          </select>
          <p class="mt-1 text-xs text-muted-foreground">
            ¿No aparece? Regístralo en <RouterLink to="/software" class="text-accent hover:underline">Software</RouterLink>. Si ya estaba instalado con otra versión, esta la reemplaza.
          </p>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.version" class="mb-1 block text-sm font-medium text-foreground">Versión (opcional)</label>
            <input :id="ids.version" v-model="form.version" type="text" maxlength="60" placeholder="16.0" :class="field" />
          </div>
          <div>
            <label :for="ids.date" class="mb-1 block text-sm font-medium text-foreground">Fecha de instalación</label>
            <input :id="ids.date" v-model="form.date" type="date" :max="todayIso()" :class="field" />
          </div>
        </div>
        <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Registrar instalación' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </section>
</template>
