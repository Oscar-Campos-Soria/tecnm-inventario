<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import BrandMark from '../../shared/components/BrandMark.vue'
import NavIcon from '../../shared/components/NavIcon.vue'
import { useAuth } from '../../shared/hooks/useAuth'
import type { NavEntry } from '../router/route-meta'

interface PlannedItem {
  to: string
  label: string
  icon: string[]
  // Sin permiso propio, los módulos pendientes solo los ve quien administra roles.
  permission?: string
  note: string
}

// Módulos aún sin pantallas: se muestran deshabilitados. Al implementarse,
// el módulo declara meta.nav en su routes.ts y se quita de aquí.
const PLANNED: PlannedItem[] = [
  { to: '/bulk-import', label: 'Carga masiva', icon: ['M12 16V4M7 9l5-5 5 5M4 20h16'], note: 'FASE 2' },
  { to: '/reports', permission: 'reports.view', label: 'Reportes', icon: ['M7 3h7l5 5v13H7z', 'M14 3v5h5M10 13h5M10 17h5'], note: 'FASE 2' },
  { to: '/software', permission: 'software.manage', label: 'Software', icon: ['M7 7h10v10H7z', 'M10 10h4v4h-4z', 'M9 3v4M15 3v4M9 17v4M15 17v4M3 9h4M3 15h4M17 9h4M17 15h4'], note: 'FASE 2' },
  { to: '/components', permission: 'components.manage', label: 'Componentes', icon: ['M12 3l9 5-9 5-9-5 9-5z', 'M3 13l9 5 9-5'], note: 'FASE 2' },
  { to: '/documents', label: 'Documentos', icon: ['M3 6h6l2 2h10v11H3z'], note: 'FASE 3' },
  { to: '/history', label: 'Historial', icon: ['M12 21a9 9 0 110-18 9 9 0 010 18z', 'M12 7v5l3 2'], note: 'FASE 3' },
  { to: '/users', permission: 'users.view', label: 'Usuarios', icon: ['M9 11a3 3 0 100-6 3 3 0 000 6z', 'M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6'], note: 'FASE 3' },
  { to: '/roles', permission: 'roles.manage', label: 'Roles', icon: ['M12 3l7 3v5c0 5-3.5 8-7 10-3.5-2-7-5-7-10V6l7-3z'], note: 'FASE 3' },
  { to: '/procurement', label: 'Adquisiciones', icon: ['M9 20a1 1 0 100-2 1 1 0 000 2z', 'M17 20a1 1 0 100-2 1 1 0 000 2z', 'M4 5h2l2.5 11h10L21 8H7'], note: 'FASE 2' },
]

const { session, logout, hasPermission } = useAuth()
const router = useRouter()
const route = useRoute()
const menuOpen = ref(false)

// Entradas activas: las que cada módulo declara en meta.nav (rutas descentralizadas).
const activeItems = computed(() =>
  router
    .getRoutes()
    .filter((record) => record.meta.nav)
    .map((record) => ({ to: record.path, ...(record.meta.nav as NavEntry) }))
    .filter((item) => !item.permission || hasPermission(item.permission))
    .sort((a, b) => a.order - b.order),
)

// Menú por permisos efectivos del servidor (solo UX; la API valida).
const plannedItems = computed(() =>
  PLANNED.filter((item) => hasPermission(item.permission ?? 'roles.manage')),
)

// Activo también en las fichas (/assets/12 marca "Activos"); el panel solo en "/".
function isActive(to: string): boolean {
  return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
}

const initial = computed(() =>
  (session.value?.firstName || session.value?.username || '?').charAt(0).toUpperCase(),
)

async function handleLogout() {
  await logout()
  await router.replace('/login')
}
</script>

<template>
  <div class="flex min-h-screen">
    <!-- Barra móvil -->
    <header class="fixed inset-x-0 top-0 z-10 flex items-center justify-between bg-sidebar px-4 py-2.5 md:hidden">
      <span class="flex items-center gap-2 font-display text-base font-semibold text-sidebar-foreground">
        <BrandMark size="h-7 w-7" inverted />
        TecNM Inventario
      </span>
      <button
        type="button"
        :aria-expanded="menuOpen"
        aria-label="Abrir menú"
        class="min-h-[44px] min-w-[44px] cursor-pointer rounded-md px-3 text-sidebar-foreground focus-visible:outline-white"
        @click="menuOpen = !menuOpen"
      >
        <svg viewBox="0 0 24 24" class="h-6 w-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
          <path d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      </button>
    </header>

    <!-- Barra lateral navy. La sección activa es una pestaña del color del
         contenido que "sale" del menú, como la ceja de un expediente. -->
    <aside
      :class="[
        'fixed inset-y-0 left-0 z-20 w-60 flex-col bg-sidebar text-sidebar-foreground md:sticky md:top-0 md:flex md:h-screen',
        menuOpen ? 'flex pt-14' : 'hidden',
      ]"
    >
      <div class="flex items-center gap-2.5 px-5 py-6">
        <BrandMark inverted />
        <span class="font-display text-base font-semibold">TecNM Inventario</span>
      </div>

      <!-- Sin barra de desplazamiento visible: cortaría la pestaña activa (se desplaza igual). -->
      <nav class="flex-1 overflow-y-auto py-2 pl-3 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden" aria-label="Navegación principal">
        <ul class="space-y-1">
          <li v-for="item in activeItems" :key="item.to">
            <RouterLink
              :to="item.to"
              :aria-current="isActive(item.to) ? 'page' : undefined"
              :class="[
                'flex min-h-[44px] items-center gap-3 px-3 text-sm transition-colors duration-150 focus-visible:outline-white',
                isActive(item.to)
                  ? 'rounded-l-lg bg-background font-semibold text-primary'
                  : 'mr-3 rounded-lg text-sidebar-muted hover:bg-sidebar-hover hover:text-sidebar-foreground',
              ]"
              @click="menuOpen = false"
            >
              <NavIcon :paths="item.icon" />
              {{ item.label }}
            </RouterLink>
          </li>
          <li v-for="item in plannedItems" :key="item.to" class="mr-3">
            <div
              :title="`Pendiente (${item.note})`"
              class="flex min-h-[44px] cursor-default items-center gap-3 rounded-lg px-3 text-sm text-sidebar-muted/55"
            >
              <NavIcon :paths="item.icon" />
              {{ item.label }}
              <span class="ml-auto rounded-full bg-white/10 px-2 py-0.5 text-[11px] font-medium text-sidebar-muted">
                {{ item.note }}
              </span>
            </div>
          </li>
        </ul>
      </nav>

      <div class="border-t border-white/10 p-3">
        <div class="mb-2 flex items-center gap-2.5 px-1">
          <div class="flex h-9 w-9 items-center justify-center rounded-full bg-white font-display text-sm font-semibold text-primary" aria-hidden="true">
            {{ initial }}
          </div>
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium">
              {{ session ? `${session.firstName} ${session.lastName}` : '' }}
            </p>
            <p class="truncate text-xs text-sidebar-muted">{{ session?.roleName }}</p>
          </div>
        </div>
        <button
          type="button"
          class="min-h-[44px] w-full cursor-pointer rounded-lg px-3 text-left text-sm text-sidebar-muted transition-colors duration-150 hover:bg-sidebar-hover hover:text-sidebar-foreground focus-visible:outline-white"
          @click="handleLogout"
        >
          Cerrar sesión
        </button>
      </div>
    </aside>

    <main class="min-w-0 flex-1 p-4 pt-20 md:p-8 md:pt-8">
      <div class="mx-auto max-w-6xl">
        <slot />
      </div>
    </main>
  </div>
</template>
