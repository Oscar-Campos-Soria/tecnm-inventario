<script setup lang="ts">
import { onBeforeUnmount, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import DocumentList from '../../../shared/components/DocumentList.vue'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { formatDate, todayIso } from '../../../shared/utils/format'
import { procurementApi } from '../api'
import type { AcquisitionDetail, AcquisitionSummary, AssetOption, PagedResult, Supplier } from '../types'

const result = ref<PagedResult<AcquisitionSummary> | null>(null)
const page = ref(1)
const error = ref('')
const detail = ref<AcquisitionDetail | null>(null)
const suppliers = ref<Supplier[]>([])

const open = ref(false)
const saving = ref(false)
const formError = ref('')
const form = ref({ supplierId: '', po: '', invoice: '', date: '', subtotal: '', tax: '', total: '', currency: 'MXN', wStart: '', wEnd: '', notes: '' })
const lines = ref<{ asset: AssetOption; unitCost: string }[]>([])
const assetSearch = ref('')
const assetResults = ref<AssetOption[]>([])
const ids = { supplier: useId(), po: useId(), invoice: useId(), date: useId(), subtotal: useId(), tax: useId(), total: useId(), currency: useId(), wStart: useId(), wEnd: useId(), notes: useId(), search: useId() }

function load() {
  procurementApi
    .acquisitions(page.value)
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar las compras.'))
}
watch(page, load, { immediate: true })

function money(value: number | null, currency: string) {
  if (value === null) return '—'
  try {
    return new Intl.NumberFormat('es-MX', { style: 'currency', currency }).format(value)
  } catch {
    return `${value.toFixed(2)} ${currency}`
  }
}

async function show(a: AcquisitionSummary) {
  try {
    detail.value = await procurementApi.acquisition(a.acquisitionId)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo abrir la compra.'
  }
}

async function remove(a: AcquisitionSummary) {
  if (!window.confirm(`¿Eliminar la compra ${a.purchaseOrder ?? a.invoiceNumber ?? `#${a.acquisitionId}`}?`)) return
  try {
    await procurementApi.deleteAcquisition(a)
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar la compra.'
  }
}

async function openForm() {
  form.value = { supplierId: '', po: '', invoice: '', date: todayIso(), subtotal: '', tax: '', total: '', currency: 'MXN', wStart: '', wEnd: '', notes: '' }
  lines.value = []
  assetSearch.value = ''
  assetResults.value = []
  formError.value = ''
  open.value = true
  procurementApi.suppliers(1).then((r) => (suppliers.value = r.items)).catch(() => (suppliers.value = []))
}

let timer: ReturnType<typeof setTimeout> | undefined
watch(assetSearch, (v) => {
  clearTimeout(timer)
  const term = v.trim()
  if (term.length < 2) {
    assetResults.value = []
    return
  }
  timer = setTimeout(() => {
    procurementApi.searchAssets(term).then((r) => (assetResults.value = r)).catch(() => (assetResults.value = []))
  }, 250)
})
onBeforeUnmount(() => clearTimeout(timer))

function addAsset(a: AssetOption) {
  if (!lines.value.some((l) => l.asset.assetId === a.assetId)) lines.value.push({ asset: a, unitCost: '' })
  assetSearch.value = ''
  assetResults.value = []
}

// "" -> null; texto no numérico -> NaN para avisar.
function num(v: string): number | null {
  return v.trim() === '' ? null : Number(v)
}

async function submit() {
  const f = form.value
  const amounts = [num(f.subtotal), num(f.tax), num(f.total), ...lines.value.map((l) => num(l.unitCost))]
  if (lines.value.length === 0) return void (formError.value = 'Agrega al menos un activo a la compra.')
  if (amounts.some((x) => x !== null && (Number.isNaN(x) || x < 0))) return void (formError.value = 'Los montos deben ser números positivos.')
  if (!/^[A-Za-z]{3}$/.test(f.currency.trim())) return void (formError.value = 'La moneda debe tener 3 letras (MXN, USD).')
  if (f.wStart && f.wEnd && f.wEnd < f.wStart) return void (formError.value = 'El fin de la garantía no puede ser anterior al inicio.')
  saving.value = true
  formError.value = ''
  try {
    await procurementApi.createAcquisition({
      supplierId: f.supplierId ? Number(f.supplierId) : null,
      purchaseOrder: f.po.trim() || null,
      invoiceNumber: f.invoice.trim() || null,
      acquiredAt: f.date || null,
      subtotal: num(f.subtotal),
      taxAmount: num(f.tax),
      totalAmount: num(f.total),
      currencyCode: f.currency.trim().toUpperCase(),
      warrantyStart: f.wStart || null,
      warrantyEnd: f.wEnd || null,
      notes: f.notes.trim() || null,
      assets: lines.value.map((l) => ({ assetId: l.asset.assetId, unitCost: num(l.unitCost) })),
    })
    open.value = false
    page.value = 1
    load()
  } catch (err) {
    formError.value = err instanceof ApiError ? err.message : 'No se pudo registrar la compra.'
  } finally {
    saving.value = false
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const label = 'mb-1 block text-sm font-medium text-foreground'
</script>

<template>
  <div class="space-y-3">
    <div class="flex justify-end"><UiButton variant="primary" @click="openForm">Nueva compra</UiButton></div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando compras…</p>
    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Compra</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Proveedor</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Fecha</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Total</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Activos</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="result.items.length === 0">
            <td colspan="6" class="px-4 py-6 text-center text-muted-foreground">Sin compras registradas.</td>
          </tr>
          <tr v-for="a in result.items" :key="a.acquisitionId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <p class="font-medium text-foreground">{{ a.purchaseOrder ? `OC ${a.purchaseOrder}` : `#${a.acquisitionId}` }}</p>
              <p v-if="a.invoiceNumber" class="text-xs text-muted-foreground">Factura {{ a.invoiceNumber }}</p>
            </td>
            <td class="px-4 py-3 text-secondary">{{ a.supplierName ?? '—' }}</td>
            <td class="px-4 py-3 text-secondary">{{ formatDate(a.acquiredAt) }}</td>
            <td class="px-4 py-3 text-right text-foreground">{{ money(a.totalAmount, a.currencyCode) }}</td>
            <td class="px-4 py-3 text-right text-secondary">{{ a.assetCount }}</td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-2">
                <UiButton @click="show(a)">Ver</UiButton>
                <UiButton variant="danger" @click="remove(a)">Eliminar</UiButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <nav v-if="result && result.totalPages > 1" class="flex justify-end gap-2" aria-label="Paginación de compras">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
    </nav>

    <UiModal :open="detail !== null" :title="detail?.purchaseOrder ? `Compra OC ${detail.purchaseOrder}` : `Compra #${detail?.acquisitionId ?? ''}`" @close="detail = null">
      <div v-if="detail" class="space-y-3 text-sm">
        <dl class="grid grid-cols-2 gap-x-4 gap-y-1.5">
          <dt class="text-muted-foreground">Proveedor</dt><dd class="text-foreground">{{ detail.supplierName ?? '—' }}</dd>
          <dt class="text-muted-foreground">Factura</dt><dd class="text-foreground">{{ detail.invoiceNumber ?? '—' }}</dd>
          <dt class="text-muted-foreground">Fecha</dt><dd class="text-foreground">{{ formatDate(detail.acquiredAt) }}</dd>
          <dt class="text-muted-foreground">Subtotal</dt><dd class="text-foreground">{{ money(detail.subtotal, detail.currencyCode) }}</dd>
          <dt class="text-muted-foreground">Impuestos</dt><dd class="text-foreground">{{ money(detail.taxAmount, detail.currencyCode) }}</dd>
          <dt class="text-muted-foreground">Total</dt><dd class="font-semibold text-foreground">{{ money(detail.totalAmount, detail.currencyCode) }}</dd>
          <dt class="text-muted-foreground">Garantía</dt>
          <dd class="text-foreground">{{ detail.warrantyStart || detail.warrantyEnd ? `${formatDate(detail.warrantyStart)} a ${formatDate(detail.warrantyEnd)}` : '—' }}</dd>
        </dl>
        <p v-if="detail.notes" class="rounded-md bg-muted px-3 py-2 text-secondary">{{ detail.notes }}</p>
        <h3 class="font-display font-semibold text-foreground">Activos ({{ detail.assets.length }})</h3>
        <ul class="divide-y divide-border rounded-md border border-border">
          <li v-for="l in detail.assets" :key="l.assetId" class="flex justify-between px-3 py-2">
            <RouterLink :to="`/assets/${l.assetId}`" class="font-medium text-accent hover:underline">{{ l.inventoryCode }}</RouterLink>
            <span class="text-secondary">{{ money(l.unitCost, detail.currencyCode) }}</span>
          </li>
        </ul>
        <DocumentList
          owner-type="ACQUISITION"
          :owner-id="detail.acquisitionId"
          title="Factura y documentos"
          empty-text="Sin archivos. Adjunta la factura o la orden de compra (PDF o foto)."
          can-upload
          can-delete
        />
      </div>
    </UiModal>

    <UiModal :open="open" title="Nueva compra" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="ids.supplier" :class="label">Proveedor (opcional)</label>
          <select :id="ids.supplier" v-model="form.supplierId" :class="[field, 'cursor-pointer px-2.5']">
            <option value="">Sin proveedor</option>
            <option v-for="s in suppliers" :key="s.supplierId" :value="String(s.supplierId)">{{ s.name }}</option>
          </select>
        </div>
        <div class="grid gap-3 sm:grid-cols-3">
          <div><label :for="ids.po" :class="label">Orden de compra</label><input :id="ids.po" v-model="form.po" maxlength="100" :class="field" /></div>
          <div><label :for="ids.invoice" :class="label">Factura</label><input :id="ids.invoice" v-model="form.invoice" maxlength="100" :class="field" /></div>
          <div><label :for="ids.date" :class="label">Fecha</label><input :id="ids.date" v-model="form.date" type="date" :class="field" /></div>
          <div><label :for="ids.subtotal" :class="label">Subtotal</label><input :id="ids.subtotal" v-model="form.subtotal" inputmode="decimal" :class="field" /></div>
          <div><label :for="ids.tax" :class="label">Impuestos</label><input :id="ids.tax" v-model="form.tax" inputmode="decimal" :class="field" /></div>
          <div><label :for="ids.total" :class="label">Total</label><input :id="ids.total" v-model="form.total" inputmode="decimal" :class="field" /></div>
          <div><label :for="ids.currency" :class="label">Moneda</label><input :id="ids.currency" v-model="form.currency" maxlength="3" :class="[field, 'uppercase']" /></div>
          <div><label :for="ids.wStart" :class="label">Garantía desde</label><input :id="ids.wStart" v-model="form.wStart" type="date" :class="field" /></div>
          <div><label :for="ids.wEnd" :class="label">Garantía hasta</label><input :id="ids.wEnd" v-model="form.wEnd" type="date" :class="field" /></div>
        </div>
        <div>
          <label :for="ids.notes" :class="label">Notas</label>
          <textarea :id="ids.notes" v-model="form.notes" rows="2" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>

        <fieldset class="space-y-2 rounded-md border border-border p-3">
          <legend class="px-1 text-sm font-medium text-foreground">Activos de la compra</legend>
          <div class="relative">
            <label :for="ids.search" class="sr-only">Buscar activo</label>
            <input :id="ids.search" v-model="assetSearch" type="search" autocomplete="off" placeholder="Buscar por código, serie o hostname" :class="field" />
            <ul v-if="assetResults.length" class="absolute z-10 mt-1 max-h-48 w-full overflow-auto rounded-md border border-border bg-card shadow">
              <li v-for="a in assetResults" :key="a.assetId">
                <button type="button" class="w-full cursor-pointer px-3 py-2 text-left text-sm hover:bg-muted" @click="addAsset(a)">
                  <span class="font-medium text-foreground">{{ a.inventoryCode }}</span> <span class="text-muted-foreground">{{ a.assetTypeName }}</span>
                </button>
              </li>
            </ul>
          </div>
          <p v-if="lines.length === 0" class="text-xs text-muted-foreground">Sin activos todavía.</p>
          <ul v-else class="space-y-1.5">
            <li v-for="(l, i) in lines" :key="l.asset.assetId" class="flex items-center gap-2">
              <span class="flex-1 text-sm font-medium text-foreground">{{ l.asset.inventoryCode }}</span>
              <label :for="`cost-${l.asset.assetId}`" class="sr-only">Costo unitario de {{ l.asset.inventoryCode }}</label>
              <input :id="`cost-${l.asset.assetId}`" v-model="l.unitCost" inputmode="decimal" placeholder="Costo unitario" class="min-h-[40px] w-36 rounded-md border border-border bg-background px-2.5 text-sm text-foreground" />
              <UiButton :aria-label="`Quitar ${l.asset.inventoryCode}`" @click="lines.splice(i, 1)">Quitar</UiButton>
            </li>
          </ul>
        </fieldset>

        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Registrar compra' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
