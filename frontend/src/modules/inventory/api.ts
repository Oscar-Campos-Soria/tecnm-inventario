import { http } from '../../shared/api/http'
import type {
  Adjustment,
  InventoryCatalogs,
  LocationOption,
  PagedResult,
  ScanResult,
  SessionDetail,
  SessionSummary,
} from './types'

const base = '/api/inventory-sessions'

export const inventoryApi = {
  list(pageNumber = 1) {
    return http.get<PagedResult<SessionSummary>>(`${base}?pageNumber=${pageNumber}&pageSize=20`)
  },
  get(sessionId: number) {
    return http.get<SessionDetail>(`${base}/${sessionId}`)
  },
  create(name: string, locationId: number) {
    return http.post<{ inventorySessionId: number; expectedAssets: number }>(base, { name, locationId })
  },
  scan(sessionId: number, inventoryCode: string, locationFoundId?: number) {
    return http.post<ScanResult>(`${base}/${sessionId}/scan`, { inventoryCode, locationFoundId })
  },
  markNotFound(sessionId: number, recordId: number, expectedVersion: number) {
    return http.post(`${base}/${sessionId}/records/${recordId}/not-found?expectedVersion=${expectedVersion}`)
  },
  close(sessionId: number, expectedVersion: number) {
    return http.post<{ pendingMarkedNotFound: number }>(`${base}/${sessionId}/close?expectedVersion=${expectedVersion}`)
  },
  adjust(sessionId: number, recordId: number, expectedVersion: number, adjustment: Adjustment, incidentId?: number) {
    return http.put(`${base}/${sessionId}/records/${recordId}/adjustment?expectedVersion=${expectedVersion}`, {
      adjustment,
      incidentId,
    })
  },
  locations() {
    return http.get<LocationOption[]>('/api/locations')
  },
  catalogs() {
    return http.get<InventoryCatalogs>('/api/catalogs')
  },
  // Conciliación: los ajustes reales usan los flujos existentes y luego se registran.
  moveAsset(assetId: number, assetVersion: number, toLocationId: number, movementReasonId: number, notes: string) {
    return http.post(`/api/assets/${assetId}/movements?expectedVersion=${assetVersion}`, { toLocationId, movementReasonId, notes })
  },
  reportMissing(input: { assetId: number; incidentTypeId: number; incidentPriorityId: number; title: string; description: string }) {
    return http.post<{ incidentId: number }>('/api/incidents', input)
  },
}
