// Metadatos que cada módulo declara en su routes.ts. El router y el menú
// los leen; así agregar un módulo no obliga a tocar app/.
// Áreas del menú, en el orden en que aparecen.
export const NAV_GROUPS = ['Soporte', 'Equipos', 'Gestión', 'Sistema'] as const

export interface NavEntry {
  label: string
  // Área del menú; sin área, va arriba de todas (Inicio).
  group?: (typeof NAV_GROUPS)[number]
  // Trazos (atributo d) del ícono SVG 24x24.
  icon: string[]
  // Posición dentro de su área (menor = más arriba).
  order: number
  // Permiso que habilita la entrada (solo UX; la API valida).
  permission?: string
  // Oculta la entrada a quien tenga este permiso (p. ej. "Mis equipos" no aparece a quien ve "Activos").
  hideWithPermission?: string
  // Contador junto a la etiqueta (p. ej. tickets sin asignar). Solo se pide si el
  // usuario tiene badgePermission; se refresca al cambiar de ruta.
  badge?: () => Promise<number>
  badgePermission?: string
  // Texto para lectores de pantalla: "3 sin asignar".
  badgeLabel?: string
}

declare module 'vue-router' {
  interface RouteMeta {
    // Accesible sin sesión (login).
    public?: boolean
    // Shell de la página; por defecto 'main' (con menú).
    layout?: 'auth' | 'main'
    // Permiso para entrar a la ruta (solo UX; la API valida). Sin él, al panel.
    permission?: string
    nav?: NavEntry
  }
}
