// Bitácora de auditoría (solo lectura; spec: README.md de este módulo).

export type { AuditLogItem } from '../../shared/api/audit'

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AuditFilters {
  table?: string
  recordId?: number
  userId?: number
  from?: string
  to?: string
}
