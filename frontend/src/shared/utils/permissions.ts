/**
 * Acciones de incidentes (solo UX; el servidor manda).
 * Se resuelven con los permisos efectivos de GET /api/auth/me,
 * ya no con una tabla por rol en el cliente.
 * 'resolve' cubre asignar técnico, cambiar estado y resolver.
 */
export type IncidentAction = 'view' | 'create' | 'resolve' | 'delete'

// Solo lee permissions: acepta la sesión de solo lectura que expone useAuth.
export function can(session: { readonly permissions: readonly string[] } | null, action: IncidentAction): boolean {
  return session?.permissions.includes(`incidents.${action}`) ?? false
}
