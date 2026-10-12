/**
 * Acciones de incidentes por rol (solo UX; el servidor manda).
 * Origen: scripts/seed-permissions.sql y scripts/add-incidents-permissions.sql.
 * 'resolve' cubre asignar técnico, cambiar estado y resolver.
 */
export type IncidentAction = 'view' | 'create' | 'resolve' | 'delete'

const ACTIONS_BY_ROLE: Record<string, readonly IncidentAction[]> = {
  ADMIN: ['view', 'create', 'resolve', 'delete'],
  BIBLIOTECARIO: ['create'],
  TECNICO: ['view', 'create', 'resolve'],
  RESPONSABLE_AREA: ['view', 'create'],
  // USUARIO_GENERAL: create sin view. La UI le oculta el menú
  // y la lista (deuda registrada en AGENTS.md: "mis incidentes").
  USUARIO_GENERAL: ['create'],
}

export function can(roleCode: string | undefined, action: IncidentAction): boolean {
  return ACTIONS_BY_ROLE[roleCode ?? '']?.includes(action) ?? false
}
