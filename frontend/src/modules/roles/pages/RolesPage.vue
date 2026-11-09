<script setup lang="ts">
import { computed, onMounted, ref, useId } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { rolesApi } from '../api'
import type { Permission, Role } from '../types'

// El servidor rechaza quitarle estos a ADMIN (anti-bloqueo); aquí solo se bloquean.
const LOCKED_FOR_ADMIN = ['roles.manage', 'users.manage']

const roles = ref<Role[]>([])
const permissions = ref<Permission[]>([])
const selectedId = ref<number | null>(null)
const checked = ref<Set<string>>(new Set())
const error = ref('')
const notice = ref('')
const saving = ref(false)
const createOpen = ref(false)
const draft = ref({ code: '', name: '', description: '' })
const createError = ref('')
const ids = { code: useId(), name: useId(), desc: useId() }

const selected = computed(() => roles.value.find((r) => r.roleId === selectedId.value) ?? null)

const groups = computed(() => {
  const map = new Map<string, Permission[]>()
  for (const p of permissions.value) map.set(p.module, [...(map.get(p.module) ?? []), p])
  return [...map.entries()]
})

const dirty = computed(() => {
  const current = selected.value?.permissions ?? []
  return current.length !== checked.value.size || current.some((c) => !checked.value.has(c))
})

function select(role: Role) {
  selectedId.value = role.roleId
  checked.value = new Set(role.permissions)
  notice.value = ''
  error.value = ''
}

function isLocked(code: string) {
  return selected.value?.code === 'ADMIN' && LOCKED_FOR_ADMIN.includes(code)
}

function toggle(code: string) {
  if (isLocked(code)) return
  const next = new Set(checked.value)
  if (next.has(code)) next.delete(code)
  else next.add(code)
  checked.value = next
}

async function load(keep?: number) {
  try {
    const [r, p] = await Promise.all([rolesApi.list(), rolesApi.permissions()])
    roles.value = r
    permissions.value = p
    const target = r.find((x) => x.roleId === keep) ?? r[0]
    if (target) select(target)
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los roles.'
  }
}
onMounted(() => load())

async function save() {
  if (!selected.value) return
  saving.value = true
  error.value = ''
  try {
    const id = selected.value.roleId
    await rolesApi.assign(id, [...checked.value])
    await load(id)
    notice.value = 'Permisos guardados. Quien tenga este rol los verá al renovar su sesión.'
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : 'No se pudieron guardar los permisos.'
  } finally {
    saving.value = false
  }
}

function openCreate() {
  draft.value = { code: '', name: '', description: '' }
  createError.value = ''
  createOpen.value = true
}

async function create() {
  const code = draft.value.code.trim().toUpperCase()
  const name = draft.value.name.trim()
  if (!/^[A-Z][A-Z0-9_]*$/.test(code) || !name) {
    createError.value = 'Código en mayúsculas (letras, números y guion bajo) y nombre obligatorios.'
    return
  }
  try {
    const { roleId } = await rolesApi.create({ code, name, description: draft.value.description.trim() || null })
    createOpen.value = false
    await load(roleId)
    notice.value = `Rol ${code} creado sin permisos; márcalos y guarda.`
  } catch (err) {
    createError.value = err instanceof ApiError ? err.message : 'No se pudo crear el rol.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <div class="space-y-4">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-semibold text-foreground">Roles y permisos</h1>
        <p class="mt-1 text-sm text-muted-foreground">Qué puede hacer cada tipo de usuario.</p>
      </div>
      <UiButton variant="primary" @click="openCreate">Nuevo rol</UiButton>
    </header>

    <p v-if="notice" role="status" class="rounded-md bg-success/10 px-3 py-2 text-sm text-success">{{ notice }}</p>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>

    <div class="grid gap-4 lg:grid-cols-[16rem_1fr]">
      <nav aria-label="Roles" class="rounded-lg border border-border bg-card p-2">
        <ul class="space-y-1">
          <li v-for="r in roles" :key="r.roleId">
            <button
              type="button"
              :aria-current="r.roleId === selectedId ? 'true' : undefined"
              :class="['w-full cursor-pointer rounded-md px-3 py-2 text-left transition-colors', r.roleId === selectedId ? 'bg-muted font-semibold text-foreground' : 'text-secondary hover:bg-muted']"
              @click="select(r)"
            >
              <span class="block text-sm">{{ r.name }}</span>
              <span class="block text-xs text-muted-foreground">{{ r.code }}, {{ r.permissions.length }} permisos</span>
            </button>
          </li>
        </ul>
      </nav>

      <section v-if="selected" class="rounded-lg border border-border bg-card p-4" :aria-label="`Permisos de ${selected.name}`">
        <div class="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 class="font-display text-lg font-semibold text-foreground">{{ selected.name }}</h2>
            <p v-if="selected.description" class="text-sm text-muted-foreground">{{ selected.description }}</p>
          </div>
          <UiButton variant="primary" :disabled="!dirty || saving" @click="save">{{ saving ? 'Guardando…' : 'Guardar permisos' }}</UiButton>
        </div>
        <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <fieldset v-for="[module, items] in groups" :key="module" class="rounded-md border border-border p-3">
            <legend class="px-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{{ module }}</legend>
            <label v-for="p in items" :key="p.code" :class="['flex min-h-[40px] items-start gap-2 py-1 text-sm', isLocked(p.code) ? 'cursor-not-allowed' : 'cursor-pointer']">
              <input type="checkbox" class="mt-0.5 size-4 accent-accent" :checked="checked.has(p.code)" :disabled="isLocked(p.code)" @change="toggle(p.code)" />
              <span>
                <span class="block text-foreground">{{ p.name }}</span>
                <span class="block text-xs text-muted-foreground">{{ p.code }}<template v-if="isLocked(p.code)">, obligatorio para ADMIN</template></span>
              </span>
            </label>
          </fieldset>
        </div>
      </section>
    </div>

    <UiModal :open="createOpen" title="Nuevo rol" @close="createOpen = false">
      <form novalidate class="space-y-3" @submit.prevent="create">
        <div>
          <label :for="ids.code" class="mb-1 block text-sm font-medium text-foreground">Código</label>
          <input :id="ids.code" v-model="draft.code" type="text" maxlength="50" :class="[field, 'uppercase']" placeholder="JEFE_LAB" />
        </div>
        <div>
          <label :for="ids.name" class="mb-1 block text-sm font-medium text-foreground">Nombre</label>
          <input :id="ids.name" v-model="draft.name" type="text" maxlength="100" :class="field" />
        </div>
        <div>
          <label :for="ids.desc" class="mb-1 block text-sm font-medium text-foreground">Descripción (opcional)</label>
          <input :id="ids.desc" v-model="draft.description" type="text" maxlength="255" :class="field" />
        </div>
        <p v-if="createError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ createError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton @click="createOpen = false">Cancelar</UiButton>
          <UiButton type="submit" variant="primary">Crear rol</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
