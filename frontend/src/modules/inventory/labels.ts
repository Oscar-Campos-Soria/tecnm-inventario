import type { RecordStatus, SessionStatus } from './types'

export const RECORD_LABELS: Record<RecordStatus, string> = {
  PENDIENTE: 'Pendiente',
  ENCONTRADO: 'En su lugar',
  EN_OTRO_LUGAR: 'En otro lugar',
  NO_ENCONTRADO: 'No encontrado',
  SOBRANTE: 'Sobrante',
}

export const RECORD_CLASSES: Record<RecordStatus, string> = {
  PENDIENTE: 'bg-muted text-secondary',
  ENCONTRADO: 'bg-success/10 text-success',
  EN_OTRO_LUGAR: 'bg-priority-high/10 text-priority-high',
  NO_ENCONTRADO: 'bg-destructive/10 text-destructive',
  SOBRANTE: 'bg-accent/10 text-accent',
}

export const SESSION_LABELS: Record<SessionStatus, string> = {
  abierta: 'Abierto',
  cerrada: 'Cerrado',
  cancelada: 'Cancelado',
}
