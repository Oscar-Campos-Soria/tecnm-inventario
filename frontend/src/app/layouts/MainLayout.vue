<script setup lang="ts">
import AssistantWidget from '../../modules/assistant/components/AssistantWidget.vue'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import BrandMark from '../../shared/components/BrandMark.vue'
import NavIcon from '../../shared/components/NavIcon.vue'
import { useAuth } from '../../shared/hooks/useAuth'
import { NAV_GROUPS, type NavEntry } from '../router/route-meta'

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
    .filter((item) => !item.hideWithPermission || !hasPermission(item.hideWithPermission))
    .sort((a, b) => a.order - b.order),
)

// Por áreas, para ubicar cada pantalla sin leer toda la lista; un área sin entradas para el rol no aparece.
const groups = computed(() =>
  [undefined, ...NAV_GROUPS]
    .map((name) => ({ name, items: activeItems.value.filter((item) => item.group === name) }))
    .filter((group) => group.items.length > 0),
)

// Activo también en las fichas (/assets/12 marca "Activos"); el panel solo en "/".
function isActive(to: string): boolean {
  return to === '/' ? route.path === '/' : route.path === to || route.path.startsWith(`${to}/`)
}

// La pestaña se desliza hasta la sección activa; sin sección en el menú (p. ej. Mi perfil) no se muestra.
const navEl = ref<HTMLElement | null>(null)
const tab = ref<{ top: number; height: number } | null>(null)
async function placeTab() {
  await nextTick()
  const link = navEl.value?.querySelector<HTMLElement>('[aria-current="page"]')
  tab.value = link ? { top: link.offsetTop, height: link.offsetHeight } : null
}
onMounted(placeTab)
watch([() => route.path, groups, menuOpen], placeTab)

const initial = computed(() =>
  (session.value?.firstName || session.value?.username || '?').charAt(0).toUpperCase(),
)

// Contadores del menú (meta.nav.badge). Un fallo deja la entrada sin número.
const badges = ref<Record<string, number>>({})
async function refreshBadges() {
  const entries = await Promise.all(
    activeItems.value
      .filter((item) => item.badge && (!item.badgePermission || hasPermission(item.badgePermission)))
      .map(async (item) => [item.to, await item.badge!().catch(() => 0)] as const),
  )
  badges.value = Object.fromEntries(entries)
}
watch(() => route.path, refreshBadges, { immediate: true })

async function handleLogout() {
  await logout()
  await router.replace('/login')
}
</script>

<template>
  <div class="flex min-h-screen">
    <!-- Barra móvil -->
    <header class="fixed inset-x-0 top-0 z-20 flex items-center justify-between bg-sidebar px-4 py-2.5 md:hidden">
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

    <div v-if="menuOpen" class="fixed inset-0 z-20 bg-sidebar/60 md:hidden" aria-hidden="true" @click="menuOpen = false" />

    <aside
      :class="[
        'fixed inset-y-0 left-0 z-30 flex w-64 flex-col bg-sidebar text-sidebar-foreground transition-[translate,visibility] duration-250 ease-firm md:visible md:sticky md:top-0 md:h-screen md:translate-x-0',
        menuOpen ? 'translate-x-0' : 'invisible -translate-x-full',
      ]"
      @keydown.esc="menuOpen = false"
    >
      <div class="flex items-center gap-3 px-5 pb-3 pt-5">
        <BrandMark inverted />
        <span class="font-display text-base font-bold">TecNM Inventario</span>
      </div>

      <!-- Sin barra de desplazamiento visible: cortaría la pestaña activa (se desplaza igual). -->
      <nav
        ref="navEl"
        class="relative isolate flex-1 overflow-y-auto pb-3 pl-3 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
        aria-label="Navegación principal"
      >
        <!-- Pestaña del color del contenido que "sale" del menú, como la ceja de un expediente. -->
        <span
          v-if="tab"
          aria-hidden="true"
          class="nav-tab absolute -z-10 left-3 right-0 top-0 rounded-l-lg bg-background transition-[translate,height] duration-300 ease-firm"
          :style="{ translate: `0 ${tab.top}px`, height: `${tab.height}px` }"
        />
        <div v-for="group in groups" :key="group.name ?? 'inicio'" class="pt-2">
          <p v-if="group.name" :id="`nav-${group.name}`" class="px-3 pb-1 pt-1 text-xs font-medium text-sidebar-muted/80">{{ group.name }}</p>
          <ul :aria-labelledby="group.name ? `nav-${group.name}` : undefined">
            <li v-for="item in group.items" :key="item.to">
              <RouterLink
                :to="item.to"
                :aria-current="isActive(item.to) ? 'page' : undefined"
                :class="[
                  'flex min-h-[44px] items-center gap-3 px-3 text-sm transition-colors duration-200 focus-visible:outline-white md:min-h-[36px]',
                  isActive(item.to)
                    ? 'rounded-l-lg font-semibold text-primary'
                    : 'mr-3 rounded-lg text-sidebar-muted hover:bg-sidebar-hover hover:text-sidebar-foreground',
                ]"
                @click="menuOpen = false"
              >
                <NavIcon :paths="item.icon" />
                {{ item.label }}
                <span
                  v-if="badges[item.to]"
                  class="ml-auto mr-2 rounded-md bg-state-maintenance px-1.5 py-0.5 text-xs font-bold text-foreground tabular-nums"
                >
                  {{ badges[item.to] }}<span class="sr-only"> {{ item.badgeLabel ?? 'pendientes' }}</span>
                </span>
              </RouterLink>
            </li>
          </ul>
        </div>
      </nav>

      <div class="flex items-center gap-1 border-t border-white/15 p-3">
        <RouterLink
          to="/profile"
          class="flex min-h-[44px] min-w-0 flex-1 items-center gap-2.5 rounded-lg px-1.5 transition-colors duration-150 hover:bg-sidebar-hover focus-visible:outline-white"
          @click="menuOpen = false"
        >
          <span class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-white font-display text-sm font-bold text-primary" aria-hidden="true">
            {{ initial }}
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate text-sm font-medium">{{ session ? `${session.firstName} ${session.lastName}` : '' }}</span>
            <span class="block truncate text-xs text-sidebar-muted">{{ session?.roleName }}<span class="sr-only">, ver mi perfil</span></span>
          </span>
        </RouterLink>
        <button
          type="button"
          aria-label="Cerrar sesión"
          title="Cerrar sesión"
          class="flex size-11 shrink-0 cursor-pointer items-center justify-center rounded-lg text-sidebar-muted transition-colors duration-150 hover:bg-sidebar-hover hover:text-sidebar-foreground focus-visible:outline-white"
          @click="handleLogout"
        >
          <NavIcon :paths="['M10 4H5v16h5', 'M15 8l4 4-4 4M19 12H9']" />
        </button>
      </div>
    </aside>

    <!-- pb: el botón del asistente no tapa el final de la página. -->
    <main class="min-w-0 flex-1 p-4 pb-24 pt-20 md:px-10 md:pb-24 md:pt-9">
      <div :key="route.path" class="page-enter mx-auto max-w-6xl">
        <slot />
      </div>
    </main>

    <!-- Asistente IA: solo si el rol lo permite; el componente se oculta solo si el modelo no responde. -->
    <AssistantWidget v-if="hasPermission('assistant.use')" />
  </div>
</template>

<style scoped>
/* Esquinas invertidas de la pestaña: la unen con el contenido (en móvil el menú flota y no hay contenido junto). */
@media (width < 48rem) {
  .nav-tab {
    right: 0.75rem;
    border-radius: 0.5rem;
  }

  .nav-tab::before,
  .nav-tab::after {
    display: none;
  }
}

.nav-tab::before,
.nav-tab::after {
  content: '';
  position: absolute;
  right: 0;
  width: 0.75rem;
  height: 0.75rem;
}

.nav-tab::before {
  top: -0.75rem;
  background: radial-gradient(circle at 0 0, transparent 11.5px, var(--color-background) 12px);
}

.nav-tab::after {
  bottom: -0.75rem;
  background: radial-gradient(circle at 0 100%, transparent 11.5px, var(--color-background) 12px);
}
</style>
