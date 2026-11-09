<script setup lang="ts">
import { computed } from 'vue'
import NavIcon from '../../../shared/components/NavIcon.vue'
import { useAuth } from '../../../shared/hooks/useAuth'

const { session, hasPermission } = useAuth()

interface Shortcut {
  to: string
  title: string
  text: string
  icon: string[]
  permission: string
}

// Lo que cada rol hace más seguido; solo aparece lo que su permiso permite.
const SHORTCUTS: Shortcut[] = [
  {
    to: '/tickets?nuevo=1',
    title: 'Pedir ayuda',
    text: 'Levanta un ticket: falla de equipo, software, cuentas o red.',
    icon: ['M12 5v14M5 12h14'],
    permission: 'tickets.create',
  },
  {
    to: '/tickets',
    title: 'Tickets',
    text: 'Sigue tus solicitudes o atiende la bandeja.',
    icon: ['M4 7a2 2 0 012-2h12a2 2 0 012 2v2a2 2 0 000 4v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2a2 2 0 000-4z'],
    permission: 'tickets.create',
  },
  {
    to: '/assets',
    title: 'Activos',
    text: 'Busca un equipo por código, serie o ubicación.',
    icon: ['M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3z', 'M12 12l8-4.5M12 12v9M12 12L4 7.5'],
    permission: 'assets.view',
  },
  {
    to: '/inventory',
    title: 'Levantamiento físico',
    text: 'Abre un conteo y escanea los equipos de un área.',
    icon: ['M9 4h6v3H9z', 'M7 5H5v16h14V5h-2', 'M8.5 13l2.5 2.5L16 10.5'],
    permission: 'inventory.manage',
  },
  {
    to: '/maintenance',
    title: 'Mantenimiento',
    text: 'Preventivos por vencer y bitácora.',
    icon: ['M12 12a3 3 0 100-6 3 3 0 000 6z', 'M12 2v3M12 19v3M2 12h3M19 12h3'],
    permission: 'maintenance.manage',
  },
  {
    to: '/incidents',
    title: 'Incidentes',
    text: 'Fallas técnicas abiertas sobre los equipos.',
    icon: ['M12 4L2.5 20h19L12 4z', 'M12 10v4M12 17v.3'],
    permission: 'incidents.view',
  },
]

const shortcuts = computed(() => SHORTCUTS.filter((s) => hasPermission(s.permission)))
const greeting = computed(() => {
  const hour = new Date().getHours()
  return hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches'
})
</script>

<template>
  <div class="space-y-8">
    <header>
      <p class="text-sm text-muted-foreground">{{ greeting }},</p>
      <h1 class="font-display text-3xl font-semibold text-foreground">{{ session?.firstName ?? session?.username ?? 'usuario' }}</h1>
      <p class="mt-1 text-sm text-muted-foreground">{{ session?.roleName }}</p>
    </header>

    <nav aria-label="Accesos directos">
      <ul class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <li v-for="(s, i) in shortcuts" :key="s.to">
          <RouterLink
            :to="s.to"
            :class="[
              'flex h-full gap-4 rounded-xl border p-5 transition-colors duration-150',
              i === 0 ? 'border-accent bg-accent text-on-accent hover:bg-accent-hover' : 'border-border bg-card hover:border-accent hover:bg-muted',
            ]"
          >
            <span :class="['flex h-10 w-10 shrink-0 items-center justify-center rounded-lg', i === 0 ? 'bg-white/15' : 'bg-muted text-accent']">
              <NavIcon :paths="s.icon" />
            </span>
            <span>
              <span :class="['block font-display font-semibold', i === 0 ? '' : 'text-foreground']">{{ s.title }}</span>
              <span :class="['mt-0.5 block text-sm', i === 0 ? 'text-on-accent/85' : 'text-muted-foreground']">{{ s.text }}</span>
            </span>
          </RouterLink>
        </li>
      </ul>
    </nav>

    <section v-if="hasPermission('dashboard.view')" class="rounded-xl border border-dashed border-border p-6">
      <h2 class="font-display text-base font-semibold text-foreground">Métricas del inventario</h2>
      <p class="mt-1 text-sm text-muted-foreground">
        Próximamente: equipos por estado y por edificio (las vistas de la base ya existen; falta el endpoint de agregados).
      </p>
    </section>
  </div>
</template>
