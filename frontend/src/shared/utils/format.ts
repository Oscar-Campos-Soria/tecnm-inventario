const dateFormat = new Intl.DateTimeFormat('es-MX', {
  year: 'numeric',
  month: 'short',
  day: '2-digit',
})

const dateTimeFormat = new Intl.DateTimeFormat('es-MX', {
  year: 'numeric',
  month: 'short',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})

// "2026-11-08" (fecha sin hora, DateOnly del backend) se interpreta como día
// local: new Date() la tomaría como medianoche UTC y en México saldría el día anterior.
function parseDate(iso: string): Date {
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso)
  return dateOnly ? new Date(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3])) : new Date(iso)
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = parseDate(iso)
  return Number.isNaN(date.getTime()) ? '—' : dateFormat.format(date)
}

/** Hoy en formato YYYY-MM-DD (hora local), para inputs type="date". */
export function todayIso(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? '—' : dateTimeFormat.format(date)
}

const relativeFormat = new Intl.RelativeTimeFormat('es-MX', { numeric: 'auto' })

// "hace 2 días", "hace 3 horas": antigüedad de un ticket en la bandeja.
export function formatRelative(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return '—'
  const time = new Date(iso).getTime()
  if (Number.isNaN(time)) return '—'
  const minutes = Math.round((time - now) / 60000)
  if (Math.abs(minutes) < 60) return relativeFormat.format(minutes, 'minute')
  const hours = Math.round(minutes / 60)
  if (Math.abs(hours) < 24) return relativeFormat.format(hours, 'hour')
  return relativeFormat.format(Math.round(hours / 24), 'day')
}

// Tamaño de archivo para evidencias (límite del backend: 5 MB).
export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—'
  if (bytes < 1024) return `${bytes} B`
  const value = bytes / 1024
  if (value < 1024) return `${value.toFixed(1)} KB`
  return `${(value / 1024).toFixed(1)} MB`
}
