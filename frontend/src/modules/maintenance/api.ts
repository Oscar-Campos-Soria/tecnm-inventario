import { http } from '../../shared/api/http'
import type { MaintenanceItem, PagedResult } from './types'

export const maintenanceApi = {
  // Preventivos con próxima fecha dentro de `days` días, más los vencidos.
  upcoming(days: number) {
    return http.get<MaintenanceItem[]>(`/api/maintenance/upcoming?days=${days}`)
  },
  history(pageNumber: number, type?: string) {
    const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: '20' })
    if (type) query.set('type', type)
    return http.get<PagedResult<MaintenanceItem>>(`/api/maintenance?${query}`)
  },
}
