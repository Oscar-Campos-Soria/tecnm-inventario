import type { ReactNode } from 'react'

/**
 * Layout para pantallas fuera de la aplicación autenticada (login,
 * recuperación de acceso): centrado, sin navegación.
 *
 * Pendiente de implementación — FASE 3 (ver plans/03-frontend-spa-plan.md).
 */
export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted p-6">
      <div className="w-full max-w-sm">{children}</div>
    </div>
  )
}
