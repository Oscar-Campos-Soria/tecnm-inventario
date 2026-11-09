<script setup lang="ts">
import { ref, useId } from 'vue'
import { ApiError } from '../../../shared/api/http'
import UiButton from '../../../shared/components/UiButton.vue'
import { formatDate } from '../../../shared/utils/format'
import { profileApi } from '../api'
import type { ExternalLoginItem, Profile } from '../types'

const PROVIDER_NAMES: Record<string, string> = { microsoft: 'Microsoft (cuenta institucional)', google: 'Google' }

const profile = ref<Profile | null>(null)
const loadError = ref('')
const ids = { current: useId(), next: useId(), confirm: useId(), rules: useId() }
const form = ref({ current: '', next: '', confirm: '' })
const saving = ref(false)
const passwordError = ref('')
const passwordDone = ref('')
const linkError = ref('')

function load() {
  profileApi
    .get()
    .then((p) => (profile.value = p))
    .catch((err) => (loadError.value = err instanceof ApiError ? err.message : 'No se pudo cargar tu perfil.'))
}
load()

async function changePassword() {
  const f = form.value
  passwordDone.value = ''
  if (!f.current) return void (passwordError.value = 'Escribe tu contraseña actual.')
  if (f.next.length < 8) return void (passwordError.value = 'La contraseña nueva debe tener al menos 8 caracteres.')
  if (f.next !== f.confirm) return void (passwordError.value = 'La confirmación no coincide con la contraseña nueva.')
  if (f.next === f.current) return void (passwordError.value = 'La contraseña nueva debe ser distinta de la actual.')
  saving.value = true
  passwordError.value = ''
  try {
    const r = await profileApi.changePassword(f.current, f.next)
    form.value = { current: '', next: '', confirm: '' }
    passwordDone.value =
      r.otherSessionsClosed > 0
        ? `Contraseña cambiada. Cerramos tu sesión en ${r.otherSessionsClosed} ${r.otherSessionsClosed === 1 ? 'otro equipo' : 'otros equipos'}.`
        : 'Contraseña cambiada.'
  } catch (err) {
    passwordError.value = err instanceof ApiError ? err.message : 'No se pudo cambiar la contraseña.'
  } finally {
    saving.value = false
  }
}

async function unlink(l: ExternalLoginItem) {
  const name = PROVIDER_NAMES[l.provider] ?? l.provider
  if (!window.confirm(`¿Desvincular ${name}? Ya no podrás entrar con ese botón; tu contraseña sigue funcionando.`)) return
  linkError.value = ''
  try {
    await profileApi.unlink(l.provider)
    load()
  } catch (err) {
    linkError.value = err instanceof ApiError ? err.message : 'No se pudo desvincular la cuenta.'
  }
}

const field = 'min-h-[44px] w-full rounded-md border border-border bg-background px-3 text-foreground'
const label = 'mb-1 block text-sm font-medium text-foreground'
</script>

<template>
  <div class="max-w-2xl space-y-4">
    <header>
      <h1 class="font-display text-2xl font-semibold text-foreground">Mi perfil</h1>
      <p class="mt-1 text-sm text-muted-foreground">Tu cuenta, tu contraseña y las cuentas con las que puedes entrar.</p>
    </header>

    <p v-if="loadError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ loadError }}</p>
    <p v-else-if="!profile" class="text-sm text-muted-foreground">Cargando perfil…</p>

    <template v-else>
      <section aria-labelledby="cuenta" class="rounded-lg border border-border bg-card p-5">
        <h2 id="cuenta" class="mb-3 font-display text-base font-semibold text-foreground">Cuenta</h2>
        <dl class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1.5 text-sm">
          <dt class="text-muted-foreground">Nombre</dt><dd class="text-foreground">{{ profile.firstName }} {{ profile.lastName }}</dd>
          <dt class="text-muted-foreground">Usuario</dt><dd class="text-foreground">{{ profile.username }}</dd>
          <dt class="text-muted-foreground">Correo</dt><dd class="break-all text-foreground">{{ profile.email }}</dd>
          <dt class="text-muted-foreground">Rol</dt><dd class="text-foreground">{{ profile.roleName }}</dd>
        </dl>
        <p class="mt-3 text-xs text-muted-foreground">Para corregir tu nombre o correo, pídelo a un administrador.</p>
      </section>

      <section aria-labelledby="contrasena" class="rounded-lg border border-border bg-card p-5">
        <h2 id="contrasena" class="mb-3 font-display text-base font-semibold text-foreground">Cambiar contraseña</h2>
        <form novalidate class="space-y-3" @submit.prevent="changePassword">
          <div>
            <label :for="ids.current" :class="label">Contraseña actual</label>
            <input :id="ids.current" v-model="form.current" type="password" autocomplete="current-password" :class="field" />
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <div>
              <label :for="ids.next" :class="label">Contraseña nueva</label>
              <input :id="ids.next" v-model="form.next" type="password" autocomplete="new-password" :aria-describedby="ids.rules" :class="field" />
            </div>
            <div>
              <label :for="ids.confirm" :class="label">Repite la nueva</label>
              <input :id="ids.confirm" v-model="form.confirm" type="password" autocomplete="new-password" :class="field" />
            </div>
          </div>
          <p :id="ids.rules" class="text-xs text-muted-foreground">Mínimo 8 caracteres. Al cambiarla se cierran tus sesiones en otros equipos.</p>
          <p v-if="passwordError" role="alert" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ passwordError }}</p>
          <p v-if="passwordDone" role="status" class="rounded-md bg-success/10 px-3 py-2 text-sm text-success">{{ passwordDone }}</p>
          <div class="flex justify-end">
            <UiButton type="submit" variant="primary" :disabled="saving">{{ saving ? 'Guardando…' : 'Cambiar contraseña' }}</UiButton>
          </div>
        </form>
      </section>

      <section aria-labelledby="vinculos" class="rounded-lg border border-border bg-card p-5">
        <h2 id="vinculos" class="mb-3 font-display text-base font-semibold text-foreground">Cuentas vinculadas</h2>
        <p v-if="linkError" role="alert" class="mb-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ linkError }}</p>
        <p v-if="profile.externalLogins.length === 0" class="text-sm text-muted-foreground">
          No tienes cuentas de Microsoft ni Google vinculadas. Se vinculan la primera vez que entras con ellas usando este mismo correo.
        </p>
        <ul v-else class="space-y-2">
          <li v-for="l in profile.externalLogins" :key="l.provider" class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2.5 text-sm">
            <div>
              <p class="font-medium text-foreground">{{ PROVIDER_NAMES[l.provider] ?? l.provider }}</p>
              <p class="text-xs text-muted-foreground">{{ l.email }}. Vinculada el {{ formatDate(l.linkedAt) }}</p>
            </div>
            <UiButton @click="unlink(l)">Desvincular</UiButton>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
