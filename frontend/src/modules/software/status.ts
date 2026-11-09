import { formatDate, todayIso } from '../../shared/utils/format'
import type { LicenseSummary } from './types'

// Días desde hoy hasta una fecha ISO (yyyy-mm-dd), en calendario local.
function daysUntil(iso: string): number {
  const [y, m, d] = iso.split('-').map(Number)
  const [ty, tm, td] = todayIso().split('-').map(Number)
  return Math.round((Date.UTC(y, m - 1, d) - Date.UTC(ty, tm - 1, td)) / 86_400_000)
}

export const EXPIRING_DAYS = 30

export function expiry(l: Pick<LicenseSummary, 'validUntil' | 'isExpired'>): { text: string; cls: string } {
  if (!l.validUntil) return { text: 'Sin vencimiento', cls: 'bg-muted text-muted-foreground' }
  if (l.isExpired) return { text: `Venció el ${formatDate(l.validUntil)}`, cls: 'bg-destructive/10 text-destructive' }
  const days = daysUntil(l.validUntil)
  if (days <= EXPIRING_DAYS) {
    return { text: days === 0 ? 'Vence hoy' : `Vence en ${days} ${days === 1 ? 'día' : 'días'}`, cls: 'bg-priority-high/10 text-priority-high' }
  }
  return { text: `Hasta ${formatDate(l.validUntil)}`, cls: 'bg-success/10 text-success' }
}

export function usage(l: Pick<LicenseSummary, 'inUse' | 'quantityPurchased'>): { text: string; percent: number | null; full: boolean } {
  if (l.quantityPurchased === null) return { text: `${l.inUse} en uso, sin límite`, percent: null, full: false }
  const total = l.quantityPurchased
  return {
    text: `${l.inUse} de ${total} en uso`,
    percent: total === 0 ? 100 : Math.min(100, Math.round((l.inUse / total) * 100)),
    full: l.inUse >= total,
  }
}
