import type { ReactNode } from 'react'

/**
 * Layout principal de la aplicación autenticada: barra de navegación,
 * menú lateral visible según permisos del usuario y zona de contenido.
 *
 * Pendiente de implementación — FASE 3 (ver plans/03-frontend-spa-plan.md).
 */
export function MainLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-64 border-r p-4 md:block">
        {/* Navegación por módulos, filtrada por permisos RBAC (FASE 3) */}
        <nav aria-label="Navegación principal">
          <span className="text-sm font-semibold">TecNM Assets</span>
        </nav>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  )
}
