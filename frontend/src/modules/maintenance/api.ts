import { http } from '../../shared/api/http'
import type { MaintenanceItem, PagedResult } from './types'

export const maintenanceApi = {
  // Preventivos con próxima fecha dentro de `days` días, más los vencidos.
  upcoming(days: number) {
    return http.get<MaintenanceItem[]>(`/api/maintenance/upcoming?days=${days}`)
  },
  // Programados sin realizar dentro de `days` días, más los atrasados.
  scheduled(days: number) {
    return http.get<MaintenanceItem[]>(`/api/maintenance/scheduled?days=${days}`)
  },
  schedule(input: { assetId: number; maintenanceTypeId: number; scheduledAt: string; description: string }) {
    return http.post<{ maintenanceRecordId: number }>('/api/maintenance/scheduled', input)
  },
  complete(item: MaintenanceItem, input: { performedAt: string; description: string | null; partsUsed: string | null; cost: number | null; nextMaintenanceAt: string | null }) {
    return http.post(`/api/maintenance/${item.maintenanceRecordId}/complete?expectedVersion=${item.version}`, input)
  },
  cancelScheduled(item: MaintenanceItem) {
    return http.delete(`/api/maintenance/${item.maintenanceRecordId}?expectedVersion=${item.version}`)
  },
  catalogs() {
    return http.get<{ maintenanceTypes: { id: number; code: string; name: string }[] }>('/api/catalogs')
  },
  searchAssets(search: string) {
    return http
      .get<PagedResult<{ assetId: number; inventoryCode: string; assetTypeName: string }>>(`/api/assets?pageSize=10&search=${encodeURIComponent(search)}`)
      .then((page) => page.items)
  },
  history(pageNumber: number, type?: string) {
    const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: '20' })
    if (type) query.set('type', type)
    return http.get<PagedResult<MaintenanceItem>>(`/api/maintenance?${query}`)
  },
}
