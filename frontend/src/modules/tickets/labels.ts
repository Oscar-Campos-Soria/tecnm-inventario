import type { PriorityCode, TicketStatus } from './types'

// Textos y colores del módulo, escritos para quien levanta el ticket.

export function ticketNumber(ticketId: number): string {
  return `TK-${String(ticketId).padStart(6, '0')}`
}

export const OPEN_STATUSES: TicketStatus[] = ['ABIERTO', 'EN_ATENCION', 'ESPERANDO_USUARIO']
export const TERMINAL_STATUSES: TicketStatus[] = ['CERRADO', 'CANCELADO']

export function statusLabel(status: TicketStatus, isRequester: boolean): string {
  switch (status) {
    case 'ABIERTO':
      return 'Abierto'
    case 'EN_ATENCION':
      return 'En atención'
    case 'ESPERANDO_USUARIO':
      return isRequester ? 'Espera tu respuesta' : 'Esperando al usuario'
    case 'RESUELTO':
      return 'Resuelto'
    case 'CERRADO':
      return 'Cerrado'
    case 'CANCELADO':
      return 'Cancelado'
  }
}

export function statusClass(status: TicketStatus): string {
  switch (status) {
    case 'ABIERTO':
      return 'bg-accent/10 text-accent'
    case 'ESPERANDO_USUARIO':
      return 'bg-priority-high/10 text-priority-high'
    case 'RESUELTO':
      return 'bg-success/10 text-success'
    case 'EN_ATENCION':
      return 'bg-muted text-secondary'
    default:
      return 'bg-muted text-muted-foreground'
  }
}

// La franja izquierda es la única señal fuerte; Media y Baja no la llevan.
export function priorityStripe(priority: PriorityCode): string {
  switch (priority) {
    case 'CRITICA':
      return 'before:absolute before:inset-y-0 before:left-0 before:w-1.5 before:bg-destructive'
    case 'ALTA':
      return 'before:absolute before:inset-y-0 before:left-0 before:w-1 before:bg-priority-high'
    default:
      return ''
  }
}

// Lo que el usuario elige al levantar el ticket (Baja solo la pone el técnico).
export const REQUESTER_PRIORITIES: { code: PriorityCode; label: string; hint: string }[] = [
  { code: 'MEDIA', label: 'Normal', hint: 'Se atiende en orden' },
  { code: 'ALTA', label: 'Alta', hint: 'Me impide trabajar' },
  { code: 'CRITICA', label: 'Urgente', hint: 'Afecta a una clase o a varias personas' },
]

// Íconos de línea por tipo de ticket (códigos de ticket_categories).
const CATEGORY_ICONS: Record<string, string[]> = {
  FALLA_EQUIPO: ['M3 4h18v12H3z', 'M8 20h8M12 16v4'],
  SOFTWARE: ['M3 5h18v14H3z', 'M3 9h18M7 7h.01M10 7h.01'],
  CUENTAS_ACCESO: ['M14 10a4 4 0 11-3.5-4', 'M12.5 9.5L21 18v3h-3v-2h-2v-2h-2l-1.5-1.5'],
  RED_INTERNET: ['M2 9a15 15 0 0120 0', 'M5 12.5a10 10 0 0114 0', 'M8.5 16a5 5 0 017 0', 'M12 19.5h.01'],
  SOLICITUD_EQUIPO: ['M3 8l9-5 9 5v8l-9 5-9-5z', 'M3 8l9 5 9-5M12 13v8'],
  OTRO: ['M12 21a9 9 0 110-18 9 9 0 010 18z', 'M8 12h.01M12 12h.01M16 12h.01'],
}

export function categoryIcon(code: string): string[] {
  return CATEGORY_ICONS[code] ?? CATEGORY_ICONS.OTRO
}
