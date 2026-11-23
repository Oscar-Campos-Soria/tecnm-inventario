import type { RecordStatus, SessionStatus } from './types'

export const RECORD_LABELS: Record<RecordStatus, string> = {
  PENDIENTE: 'Pendiente',
  ENCONTRADO: 'En su lugar',
  EN_OTRO_LUGAR: 'En otro lugar',
  NO_ENCONTRADO: 'No encontrado',
  SOBRANTE: 'Sobrante',
}

export const RECORD_CLASSES: Record<RecordStatus, string> = {
  PENDIENTE: 'chip-neutral',
  ENCONTRADO: 'chip-success',
  EN_OTRO_LUGAR: 'chip-alert',
  NO_ENCONTRADO: 'chip-danger',
  SOBRANTE: 'chip-info',
}

export const SESSION_LABELS: Record<SessionStatus, string> = {
  abierta: 'Abierto',
  cerrada: 'Cerrado',
  cancelada: 'Cancelado',
}
