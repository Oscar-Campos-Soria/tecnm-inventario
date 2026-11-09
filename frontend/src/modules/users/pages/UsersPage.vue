<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate } from '../../../shared/utils/format'
import { usersApi } from '../api'
import UserFormModal from '../components/UserFormModal.vue'
import type { ManagedUser, PagedResult, RoleOption, UserFilters, UserStatus } from '../types'

const { session } = useAuth()
const passwordFieldId = useId()

const result = ref<PagedResult<ManagedUser> | null>(null)
const roles = ref<RoleOption[]>([])
const page = ref(1)
const search = ref('')
const debounced = ref('')
const roleFilter = ref('')
const statusFilter = ref<UserStatus | ''>('')
const error = ref('')
const notice = ref('')
const formOpen = ref(false)
const editing = ref<ManagedUser | null>(null)
const pwdUser = ref<ManagedUser | null>(null)
const newPassword = ref('')
const pwdError = ref('')
const busy = ref<number | null>(null)

onMounted(() => {
  usersApi.roles().then((r) => (roles.value = r)).catch(() => (roles.value = []))
})

let timer: ReturnType<typeof setTimeout> | undefined
watch(search, (v) => {
  clearTimeout(timer)
  timer = setTimeout(() => (debounced.value = v.trim()), 250)
})
onBeforeUnmount(() => clearTimeout(timer))
watch([search, roleFilter, statusFilter], () => (page.value = 1))

const filters = computed<UserFilters>(() => ({
  search: debounced.value || undefined,
  role: roleFilter.value || undefined,
  status: statusFilter.value || undefined,
}))

function load() {
  usersApi
    .list(filters.value, page.value)
    .then((r) => {
      error.value = ''
      result.value = r
    })
    .catch((err) => (error.value = err instanceof ApiError ? err.message : 'No se pudieron cargar los usuarios.'))
}
watch([filters, page], load, { immediate: true })

function conflict() {
  error.value = 'Otra persona modificó ese usuario. Recargamos la lista; intenta de nuevo.'
  load()
}

function openNew() {
  editing.value = null
  formOpen.value = true
}

function openEdit(u: ManagedUser) {
  editing.value = u
  formOpen.value = true
}

async function changeStatus(u: ManagedUser, status: UserStatus) {
  const verb = status === 'activo' ? 'activar' : status === 'suspendido' ? 'suspender' : 'desactivar'
  if (status !== 'activo' && !window.confirm(`¿${verb[0].toUpperCase() + verb.slice(1)} a ${u.username}? Se cerrarán sus sesiones abiertas.`)) return
  busy.value = u.userId
  error.value = ''
  notice.value = ''
  try {
    await usersApi.setStatus(u.userId, u.version, status)
    notice.value = `${u.username}: cuenta ${status}.`
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) conflict()
    else error.value = err instanceof ApiError ? err.message : `No se pudo ${verb}.`
  } finally {
    busy.value = null
  }
}

function openPassword(u: ManagedUser) {
  pwdUser.value = u
  newPassword.value = ''
  pwdError.value = ''
}

async function savePassword() {
  if (!pwdUser.value) return
  if (newPassword.value.length < 8) {
    pwdError.value = 'La contraseña debe tener al menos 8 caracteres.'
    return
  }
  try {
    await usersApi.resetPassword(pwdUser.value.userId, pwdUser.value.version, newPassword.value)
    notice.value = `Contraseña de ${pwdUser.value.username} actualizada; sus sesiones se cerraron.`
    pwdUser.value = null
    load()
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      pwdUser.value = null
      conflict()
    } else pwdError.value = err instanceof ApiError ? err.message : 'No se pudo cambiar la contraseña.'
  }
}

const STATUS_CHIP: Record<UserStatus, string> = {
  activo: 'bg-success/10 text-success',
  inactivo: 'bg-muted text-muted-foreground',
  suspendido: 'bg-priority-high/10 text-priority-high',
}
const select = 'min-h-[44px] cursor-pointer rounded-md border border-border bg-background px-2.5 text-sm text-foreground'
</script>

<template>
  <div class="space-y-4">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="font-display text-2xl font-semibold text-foreground">Usuarios</h1>
        <p class="mt-1 text-sm text-muted-foreground">Altas, roles y acceso de las personas que usan el sistema.</p>
      </div>
      <UiButton variant="primary" @click="openNew">Nuevo usuario</UiButton>
    </header>

    <div class="flex flex-wrap gap-2">
      <label for="user-search" class="sr-only">Buscar</label>
      <input id="user-search" v-model="search" type="search" placeholder="Nombre, usuario o correo" class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-sm text-foreground sm:w-72" />
      <label for="user-role" class="sr-only">Rol</label>
      <select id="user-role" v-model="roleFilter" :class="select">
        <option value="">Todos los roles</option>
        <option v-for="r in roles" :key="r.roleId" :value="r.code">{{ r.name }}</option>
      </select>
      <label for="user-status" class="sr-only">Estado</label>
      <select id="user-status" v-model="statusFilter" :class="select">
        <option value="">Todos los estados</option>
        <option value="activo">Activos</option>
        <option value="inactivo">Inactivos</option>
        <option value="suspendido">Suspendidos</option>
      </select>
    </div>

    <p v-if="notice" role="status" class="rounded-md bg-success/10 px-3 py-2 text-sm text-success">{{ notice }}</p>
    <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
    <p v-if="!result && !error" class="text-sm text-muted-foreground">Cargando usuarios…</p>

    <div v-else-if="result" class="overflow-x-auto rounded-lg border border-border bg-card">
      <table class="w-full min-w-[52rem] text-left text-sm">
        <thead>
          <tr class="border-b border-border bg-muted">
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Persona</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Rol</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Estado</th>
            <th scope="col" class="px-4 py-2.5 font-semibold text-foreground">Alta</th>
            <th scope="col" class="px-4 py-2.5 text-right font-semibold text-foreground">Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="result.items.length === 0">
            <td colspan="5" class="px-4 py-6 text-center text-muted-foreground">Ningún usuario coincide.</td>
          </tr>
          <tr v-for="u in result.items" :key="u.userId" class="border-b border-border last:border-0">
            <td class="px-4 py-3">
              <p class="font-medium text-foreground">{{ u.firstName }} {{ u.lastName }}<span v-if="u.userId === session?.userId" class="ml-2 text-xs font-normal text-accent">(tú)</span></p>
              <p class="text-xs text-muted-foreground">{{ u.username }}, {{ u.email }}</p>
            </td>
            <td class="px-4 py-3 text-secondary">{{ u.roleName }}</td>
            <td class="px-4 py-3"><span :class="['rounded-full px-2.5 py-0.5 text-xs font-medium', STATUS_CHIP[u.status]]">{{ u.status }}</span></td>
            <td class="px-4 py-3 text-secondary">{{ formatDate(u.createdAt) }}</td>
            <td class="px-4 py-3">
              <div class="flex flex-wrap justify-end gap-2">
                <UiButton :disabled="busy === u.userId" @click="openEdit(u)">Editar</UiButton>
                <UiButton :disabled="busy === u.userId" @click="openPassword(u)">Contraseña</UiButton>
                <template v-if="u.userId !== session?.userId">
                  <UiButton v-if="u.status !== 'activo'" :disabled="busy === u.userId" @click="changeStatus(u, 'activo')">Activar</UiButton>
                  <UiButton v-else variant="danger" :disabled="busy === u.userId" @click="changeStatus(u, 'inactivo')">Desactivar</UiButton>
                </template>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <nav v-if="result && result.totalPages > 1" class="flex items-center justify-between" aria-label="Paginación de usuarios">
      <p class="text-sm text-muted-foreground">Página {{ result.pageNumber }} de {{ result.totalPages }}</p>
      <div class="flex gap-2">
        <UiButton :disabled="page <= 1" @click="page--">Anterior</UiButton>
        <UiButton :disabled="page >= result.totalPages" @click="page++">Siguiente</UiButton>
      </div>
    </nav>

    <UserFormModal :open="formOpen" :user="editing" :roles="roles" @close="formOpen = false" @saved="load" @conflict="conflict" />

    <UiModal :open="pwdUser !== null" :title="`Nueva contraseña para ${pwdUser?.username ?? ''}`" @close="pwdUser = null">
      <form novalidate class="space-y-3" @submit.prevent="savePassword">
        <div>
          <label :for="passwordFieldId" class="mb-1 block text-sm font-medium text-foreground">Contraseña nueva</label>
          <input :id="passwordFieldId" v-model="newPassword" type="text" autocomplete="new-password" minlength="8" class="min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground" />
          <p class="mt-1 text-xs text-muted-foreground">Mínimo 8 caracteres. Sus sesiones abiertas se cerrarán.</p>
        </div>
        <p v-if="pwdError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ pwdError }}</p>
        <div class="flex justify-end gap-2">
          <UiButton @click="pwdUser = null">Cancelar</UiButton>
          <UiButton type="submit" variant="primary">Guardar contraseña</UiButton>
        </div>
      </form>
    </UiModal>
  </div>
</template>
