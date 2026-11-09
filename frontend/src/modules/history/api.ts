import { http } from '../../shared/api/http'
import type { AuditFilters, AuditLogItem, PagedResult } from './types'

// Todo con history.view (solo ADMIN).
export const historyApi = {
  list(filters: AuditFilters, pageNumber = 1) {
    const q = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: '25' })
    for (const [k, v] of Object.entries(filters)) if (v !== undefined && v !== '') q.set(k, String(v))
    return http.get<PagedResult<AuditLogItem>>(`/api/history?${q}`)
  },
  tables: () => http.get<string[]>('/api/history/tables'),
}
