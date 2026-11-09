<script setup lang="ts">
import { ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { procurementApi } from '../api'
import type { PagedResult, Supplier, SupplierInput } from '../types'

const result = ref<PagedResult<Supplier> | null>(null)
const page = ref(1)
const error = ref('')
const open = ref(false)
const editing = ref<Supplier | null>(null)
const saving = ref(false)
const formError = ref('')
const empty = (): SupplierInput => ({ name: '', taxId: '', contactName: '', email: '', phone: '', address: '', notes: '' })
const form = ref<SupplierInput>(empty())
const ids = { name: useId(), tax: useId(), contact: useId(), email: useId(), phone: useId(), address: useId(), notes: useId() }

function load() {
  procurementApi
    .suppliers(page.value)
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los proveedores.'))
}
watch(page, load, { immediate: true })

function openForm(s: Supplier | null) {
  editing.value = s
  form.value = s
    ? { name: s.name, taxId: s.taxId ?? '', contactName: s.contactName ?? '', email: s.email ?? '', phone: s.phone ?? '', address: s.address ?? '', notes: s.notes ?? '' }
    : empty()
  formError.value = ''
  open.value = true
}

async function submit() {
  if (!form.value.name.trim()) {
    formError.value = 'El nombre es obligatorio.'
    return
  }
  const clean = Object.fromEntries(Object.entries(form.value).map(([k, v]) => [k, (v ?? '').trim() || null])) as SupplierInput
  clean.name = form.value.name.trim()
  saving.value = true
  formError.value = ''
  try {
    if (editing.value) await procurementApi.updateSupplier(editing.value, clean)
    else await procurementApi.createSupplier(clean)
    open.value = false
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      open.value = false
      error.value = 'Otra persona modificó ese proveedor. Recargamos la lista; intenta de nuevo.'
      load()
    } else formError.value = err instanceof ApiError ? err.message : 'No se pudo guardar el proveedor.'
  } finally {
    saving.value = false
  }
}

async function remove(s: Supplier) {
  if (!window.confirm(`¿Eliminar al proveedor ${s.name}?`)) return
  try {
    await procurementApi.deleteSupplier(s)
    load()
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudo eliminar el proveedor.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <div class="space-y-3">
    <div class="flex justify-end"><UiButton variant="primary" @click="openForm(null)">Nuevo proveedor</UiButton></div>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando proveedores…</p>
    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Proveedor</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">RFC</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Contacto</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="result.items.length === 0">
            <td colspan="4" class="px-4 py-6 text-center text-muted-foreground">Sin proveedores registrados.</td>
          </tr>
          <tr v-for="s in result.items" :key="s.supplierId" class="border-b border-border last:border-0">
            <td class="px-4 py-3 font-medium text-foreground">{{ s.name }}</td>
            <td class="px-4 py-3 text-secondary">{{ s.taxId ?? '—' }}</td>
            <td class="px-4 py-3 text-secondary">
              {{ s.contactName ?? '—' }}
              <span v-if="s.email || s.phone" class="block text-xs text-muted-foreground">{{ [s.email, s.phone].filter(Boolean).join(', ') }}</span>
            </td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-2">
                <UiButton @click="openForm(s)">Editar</UiButton>
                <UiButton variant="danger" @click="remove(s)">Eliminar</UiButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <nav v-if="result && result.totalPages > 1" class="flex justify-end gap-2" aria-label="Paginación de proveedores">
      <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
      <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
    </nav>

    <UiModal :open="open" :title="editing ? `Editar ${editing.name}` : 'Nuevo proveedor'" @close="open = false">
      <form novalidate class="space-y-3" @submit.prevent="submit">
        <div>
          <label :for="ids.name" class="mb-1 block text-sm font-medium text-foreground">Nombre o razón social</label>
          <input :id="ids.name" v-model="form.name" type="text" maxlength="180" :class="field" />
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <div>
            <label :for="ids.tax" class="mb-1 block text-sm font-medium text-foreground">RFC</label>
            <input :id="ids.tax" v-model="form.taxId" type="text" maxlength="30" :class="[field, 'uppercase']" />
          </div>
          <div>
            <label :for="ids.contact" class="mb-1 block text-sm font-medium text-foreground">Contacto</label>
            <input :id="ids.contact" v-model="form.contactName" type="text" maxlength="150" :class="field" />
          </div>
          <div>
            <label :for="ids.email" class="mb-1 block text-sm font-medium text-foreground">Correo</label>
            <input :id="ids.email" v-model="form.email" type="email" maxlength="254" :class="field" />
          </div>
          <div>
            <label :for="ids.phone" class="mb-1 block text-sm font-medium text-foreground">Teléfono</label>
            <input :id="ids.phone" v-model="form.phone" type="tel" maxlength="50" :class="field" />
          </div>
        </div>
        <div>
          <label :for="ids.address" class="mb-1 block text-sm font-medium text-foreground">Dirección</label>
          <input :id="ids.address" v-model="form.address" type="text" :class="field" />
        </div>
        <div>
          <label :for="ids.notes" class="mb-1 block text-sm font-medium text-foreground">Notas</label>
          <textarea :id="ids.notes" v-model="form.notes" rows="2" class="w-full rounded-md border border-border bg-background px-3 py-2 text-foreground" />
        </div>
        <p v-if="formError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ formError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton :disabled="saving" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Guardar' }}</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
