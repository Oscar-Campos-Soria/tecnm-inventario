import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { loadSession, useAuth } from '../../shared/hooks/useAuth'
import { safeRedirect } from '../../shared/utils/redirect'
import './route-meta'

// Rutas descentralizadas: cada módulo exporta las suyas en
// modules/<modulo>/routes.ts y aquí se recogen todas (Vite resuelve el
// glob al compilar). Un módulo nuevo no requiere editar este archivo.
const moduleRoutes = import.meta.glob<{ default: RouteRecordRaw[] }>(
  '../../modules/*/routes.ts',
  { eager: true },
)

const routes: RouteRecordRaw[] = [
  ...Object.values(moduleRoutes).flatMap((module) => module.default),
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach(async (to) => {
  // La cookie HttpOnly no se lee desde JS: hasta que GET /api/auth/me
  // responda no se sabe si hay sesión, así que se espera aquí.
  await loadSession()
  const { session } = useAuth()

  if (to.meta.public) {
    // Con sesión, el login ya no tiene sentido: a donde iba.
    return session.value ? safeRedirect(to.query.redirect) : true
  }

  if (!session.value) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  return true
})
