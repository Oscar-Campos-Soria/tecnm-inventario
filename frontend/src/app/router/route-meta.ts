// Metadatos que cada módulo declara en su routes.ts. El router y el menú
// los leen; así agregar un módulo no obliga a tocar app/.
export interface NavEntry {
  label: string
  // Trazos (atributo d) del ícono SVG 24x24.
  icon: string[]
  // Posición en el menú (menor = más arriba).
  order: number
  // Permiso que habilita la entrada (solo UX; la API valida).
  permission?: string
}

declare module 'vue-router' {
  interface RouteMeta {
    // Accesible sin sesión (login).
    public?: boolean
    // Shell de la página; por defecto 'main' (con menú).
    layout?: 'auth' | 'main'
    nav?: NavEntry
  }
}
