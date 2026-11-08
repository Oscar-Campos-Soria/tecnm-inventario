<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { authApi } from '../../../shared/api/auth'
import type { ExternalProvider } from '../../../shared/api/auth'
import { ApiError } from '../../../shared/api/http'
import BrandMark from '../../../shared/components/BrandMark.vue'
import { useAuth } from '../../../shared/hooks/useAuth'
import { safeRedirect } from '../../../shared/utils/redirect'
import ProviderLogo from '../components/ProviderLogo.vue'

// Códigos que devuelve el callback federado en /login?error=...
// (ExternalLoginCallbackHandler).
const EXTERNAL_ERRORS: Record<string, string> = {
  sin_cuenta:
    'Tu cuenta institucional no está registrada en el inventario. Pide a un administrador que te dé de alta con ese correo.',
  dominio_no_permitido: 'Usa tu cuenta de Google institucional del TecNM.',
  cuenta_inactiva: 'Tu cuenta está desactivada. Contacta a un administrador.',
  federado_fallido: 'No se pudo completar el inicio de sesión con el proveedor. Intenta de nuevo.',
}

const PROVIDER_LABELS: Record<ExternalProvider, string> = {
  microsoft: 'Continuar con Microsoft',
  google: 'Continuar con Google',
}

const { login } = useAuth()
const router = useRouter()
const route = useRoute()

// Ruta a la que quería ir el usuario antes de que el guard lo mandara aquí.
const from = computed(() => safeRedirect(route.query.redirect))

const usernameOrEmail = ref('')
const password = ref('')
const error = ref(EXTERNAL_ERRORS[String(route.query.error ?? '')] ?? '')
const isSubmitting = ref(false)
const providers = ref<ExternalProvider[]>([])

onMounted(() => {
  authApi
    .providers()
    .then((list) => (providers.value = list))
    .catch(() => (providers.value = []))
})

async function handleSubmit() {
  error.value = ''

  if (!usernameOrEmail.value.trim() || !password.value) {
    error.value = 'Ingresa tu usuario y contraseña.'
    return
  }

  isSubmitting.value = true
  try {
    await login(usernameOrEmail.value.trim(), password.value)
    await router.replace(from.value)
  } catch (err) {
    error.value =
      err instanceof ApiError
        ? err.message
        : 'No se pudo conectar con el servidor. Verifica que la API esté activa.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div>
    <div class="mb-8 md:hidden">
      <BrandMark size="h-10 w-10" />
    </div>

    <h1 class="font-display text-2xl font-semibold text-foreground">Inicia sesión</h1>
    <p class="mt-1.5 text-sm text-muted-foreground">
      Inventario de activos tecnológicos del TecNM.
    </p>

    <template v-if="providers.length > 0">
      <div class="mt-8 space-y-3">
        <a
          v-for="provider in providers"
          :key="provider"
          :href="authApi.externalLoginUrl(provider, from)"
          class="flex min-h-[48px] w-full items-center justify-center gap-3 rounded-lg border border-border bg-card px-4 text-sm font-semibold text-foreground transition-colors duration-150 hover:border-accent hover:bg-muted"
        >
          <ProviderLogo :provider="provider" />
          {{ PROVIDER_LABELS[provider] }}
        </a>
      </div>

      <div class="my-6 flex items-center gap-3 text-xs text-muted-foreground" role="separator">
        <span class="h-px flex-1 bg-border" />
        o con tu usuario del sistema
        <span class="h-px flex-1 bg-border" />
      </div>
    </template>

    <form :class="providers.length > 0 ? '' : 'mt-8'" novalidate @submit.prevent="handleSubmit">
      <div class="mb-4">
        <label for="username" class="mb-1.5 block text-sm font-medium text-foreground">
          Usuario o correo
        </label>
        <input
          id="username"
          v-model="usernameOrEmail"
          type="text"
          autocomplete="username"
          autofocus
          class="min-h-[48px] w-full rounded-lg border border-border bg-card px-3.5 text-foreground placeholder:text-muted-foreground/70 focus:border-accent"
          placeholder="tu.usuario@tecnm.mx"
        />
      </div>

      <div class="mb-6">
        <label for="password" class="mb-1.5 block text-sm font-medium text-foreground">
          Contraseña
        </label>
        <input
          id="password"
          v-model="password"
          type="password"
          autocomplete="current-password"
          class="min-h-[48px] w-full rounded-lg border border-border bg-card px-3.5 text-foreground placeholder:text-muted-foreground/70 focus:border-accent"
        />
      </div>

      <p v-if="error" role="alert" class="mb-4 rounded-lg bg-destructive/10 px-3.5 py-2.5 text-sm text-destructive">
        {{ error }}
      </p>

      <button
        type="submit"
        :disabled="isSubmitting"
        class="min-h-[48px] w-full cursor-pointer rounded-lg bg-primary font-semibold text-on-primary transition-colors duration-150 hover:bg-secondary disabled:cursor-wait disabled:opacity-60"
      >
        {{ isSubmitting ? 'Iniciando sesión…' : 'Iniciar sesión' }}
      </button>
    </form>
  </div>
</template>
