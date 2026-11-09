<script setup lang="ts">
import { ref, useId, watch } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import UiModal from '../../../shared/components/UiModal.vue'
import { usersApi } from '../api'
import type { ManagedUser, RoleOption } from '../types'

// Alta (sin `user`) o edición de datos y rol. La contraseña inicial solo se
// pide al dar de alta; después se cambia con "Nueva contraseña".
const props = defineProps<{
  open: boolean
  user: ManagedUser | null
  roles: RoleOption[]
}>()

const emit = defineEmits<{ close: []; saved: []; conflict: [] }>()

const ids = { username: useId(), email: useId(), first: useId(), last: useId(), role: useId(), password: useId() }
const form = ref({ username: '', email: '', firstName: '', lastName: '', roleCode: '', password: '' })
const saving = ref(false)
const error = ref('')

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = ''
    const u = props.user
    form.value = u
      ? { username: u.username, email: u.email, firstName: u.firstName, lastName: u.lastName, roleCode: u.roleCode, password: '' }
      : { username: '', email: '', firstName: '', lastName: '', roleCode: 'USUARIO_GENERAL', password: '' }
  },
  { immediate: true },
)

async function submit() {
  error.value = ''
  const f = form.value
  if (!f.email.trim() || !f.firstName.trim() || !f.lastName.trim() || !f.roleCode || (!props.user && (!f.username.trim() || f.password.length < 8))) {
    error.value = props.user
      ? 'Correo, nombre, apellido y rol son obligatorios.'
      : 'Completa todos los campos; la contraseña debe tener al menos 8 caracteres.'
    return
  }
  saving.value = true
  try {
    if (props.user) {
      await usersApi.update(props.user.userId, props.user.version, {
        email: f.email.trim(), firstName: f.firstName.trim(), lastName: f.lastName.trim(), roleCode: f.roleCode,
      })
    } else {
      await usersApi.create({
        username: f.username.trim(), email: f.email.trim(), password: f.password,
        firstName: f.firstName.trim(), lastName: f.lastName.trim(), roleCode: f.roleCode,
      })
    }
    emit('saved')
    emit('close')
  } catch (err) {
    if (err instanceof ApiError && err.status === 409) {
      emit('close')
      emit('conflict')
      return
    }
    error.value = err instanceof ApiError ? err.message : 'No se pudo guardar el usuario.'
  } finally {
    saving.value = false
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
</script>

<template>
  <UiModal :open="open" :title="user ? `Editar ${user.username}` : 'Nuevo usuario'" @close="emit('close')">
    <form novalidate class="space-y-3" @submit.prevent="submit">
      <div v-if="!user">
        <label :for="ids.username" class="mb-1 block text-sm font-medium text-foreground">Usuario</label>
        <input :id="ids.username" v-model="form.username" type="text" maxlength="60" autocomplete="off" :class="field" placeholder="nombre.apellido" />
      </div>
      <div class="grid gap-3 sm:grid-cols-2">
        <div>
          <label :for="ids.first" class="mb-1 block text-sm font-medium text-foreground">Nombre</label>
          <input :id="ids.first" v-model="form.firstName" type="text" maxlength="100" :class="field" />
        </div>
        <div>
          <label :for="ids.last" class="mb-1 block text-sm font-medium text-foreground">Apellido</label>
          <input :id="ids.last" v-model="form.lastName" type="text" maxlength="100" :class="field" />
        </div>
      </div>
      <div>
        <label :for="ids.email" class="mb-1 block text-sm font-medium text-foreground">Correo</label>
        <input :id="ids.email" v-model="form.email" type="email" maxlength="254" :class="field" placeholder="usuario@tecnm.mx" />
      </div>
      <div>
        <label :for="ids.role" class="mb-1 block text-sm font-medium text-foreground">Rol</label>
        <select :id="ids.role" v-model="form.roleCode" :class="[field, 'cursor-pointer px-2.5']">
          <option v-for="r in roles" :key="r.roleId" :value="r.code">{{ r.name }}</option>
        </select>
        <p v-if="user && user.roleCode !== form.roleCode" class="mt-1 text-xs text-muted-foreground">
          Al cambiar el rol se cierran sus sesiones abiertas; entrará de nuevo con los permisos nuevos.
        </p>
      </div>
      <div v-if="!user">
        <label :for="ids.password" class="mb-1 block text-sm font-medium text-foreground">Contraseña inicial</label>
        <input :id="ids.password" v-model="form.password" type="text" minlength="8" autocomplete="new-password" :class="field" />
        <p class="mt-1 text-xs text-muted-foreground">Mínimo 8 caracteres. Entrégala a la persona por un medio seguro.</p>
      </div>
      <p v-if="error" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ error }}</p>
      <div class="flex justify-end gap-2">
        <UiButton :disabled="saving" @click="emit('close')">Cancelar</UiButton>
        <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : user ? 'Guardar cambios' : 'Crear usuario' }}</UiButton>
      </div>
    </form>
  </UiModal>
</template>
