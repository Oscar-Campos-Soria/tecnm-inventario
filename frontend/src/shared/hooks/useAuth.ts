import { readonly, ref } from 'vue'
import { authApi } from '../api/auth'
import { setSessionLostHandler } from '../api/http'
import type { AuthSession } from '../api/http'

// Estado de sesión compartido por toda la app (un solo módulo = un solo
// estado; no hace falta Pinia). La sesión vive en cookies HttpOnly: aquí
// solo se guarda en memoria el perfil con permisos que devuelve /me.
const session = ref<AuthSession | null>(null)
// true mientras se consulta GET /api/auth/me al arrancar: la cookie
// HttpOnly no se puede leer desde JS, así que hasta entonces no se
// sabe si hay sesión.
const isLoading = ref(true)
let loadPromise: Promise<void> | null = null

setSessionLostHandler(() => {
  session.value = null
})

/** Carga la sesión una sola vez (el guard del router la espera). */
export function loadSession(): Promise<void> {
  loadPromise ??= authApi
    .me()
    .then((profile) => {
      session.value = profile
    })
    .catch(() => {
      session.value = null
    })
    .finally(() => {
      isLoading.value = false
    })
  return loadPromise
}

async function login(usernameOrEmail: string, password: string): Promise<AuthSession> {
  const profile = await authApi.login(usernameOrEmail, password)
  session.value = profile
  return profile
}

async function logout(): Promise<void> {
  try {
    await authApi.logout()
  } finally {
    session.value = null
  }
}

function hasPermission(permission: string): boolean {
  return session.value?.permissions.includes(permission) ?? false
}

export function useAuth() {
  return {
    session: readonly(session),
    isLoading: readonly(isLoading),
    login,
    logout,
    hasPermission,
  }
}
