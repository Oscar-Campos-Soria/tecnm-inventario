import { http } from '../../shared/api/http'
import type {
  AssetAssignment,
  AssetDisposalInfo,
  AuditLogItem,
  CreateWarrantyInput,
  SupplierOption,
  WarrantyItem,
  AssetFilters,
  AssetIncidentOption,
  CreateDisposalInput,
  CreateMaintenanceInput,
  MaintenanceItem,
  AssetDetail,
  AssetMovement,
  AssetSummary,
  CatalogItem,
  CreateAssignmentInput,
  CreateMovementInput,
  GetCatalogsResponse,
  LocationItem,
  PagedResult,
  UserItem,
} from './types'

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const assetsApi = {
  list(pageNumber = 1, pageSize = 20, filters: AssetFilters = {}) {
    return http.get<PagedResult<AssetSummary>>(
      `/api/assets${buildQuery({ pageNumber, pageSize, ...filters })}`,
    )
  },
  getById(assetId: number) {
    return http.get<AssetDetail>(`/api/assets/${assetId}`)
  },
  createMovement(assetId: number, expectedVersion: number, input: CreateMovementInput) {
    return http.post<{ assetMovementId: number; assetVersion: number }>(
      `/api/assets/${assetId}/movements?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  getMovements(assetId: number, pageNumber = 1, pageSize = 10) {
    return http.get<PagedResult<AssetMovement>>(
      `/api/assets/${assetId}/movements${buildQuery({ pageNumber, pageSize })}`,
    )
  },
  createAssignment(assetId: number, expectedVersion: number, input: CreateAssignmentInput) {
    return http.post<{ assetAssignmentId: number; assetVersion: number }>(
      `/api/assets/${assetId}/assignments?expectedVersion=${expectedVersion}`,
      input,
    )
  },
  getAssignments(assetId: number) {
    return http.get<AssetAssignment[]>(`/api/assets/${assetId}/assignments`)
  },
  returnAssignment(assetId: number, assignmentId: number, expectedVersion: number) {
    return http.post<{ assetAssignmentId: number; assetVersion: number }>(
      `/api/assets/${assetId}/assignments/${assignmentId}/return?expectedVersion=${expectedVersion}`,
      {},
    )
  },
  users(search?: string) {
    return http.get<UserItem[]>(`/api/users${buildQuery({ search })}`)
  },
  locations() {
    return http.get<LocationItem[]>('/api/locations')
  },
  catalogs() {
    return http.get<GetCatalogsResponse>('/api/catalogs')
  },
  maintenance(assetId: number, pageNumber = 1) {
    return http.get<PagedResult<MaintenanceItem>>(`/api/maintenance${buildQuery({ assetId, pageNumber, pageSize: 10 })}`)
  },
  addMaintenance(input: CreateMaintenanceInput) {
    return http.post<{ maintenanceRecordId: number }>('/api/maintenance', input)
  },
  removeMaintenance(recordId: number, expectedVersion: number) {
    return http.delete(`/api/maintenance/${recordId}?expectedVersion=${expectedVersion}`)
  },
  // Para ligar un correctivo a su incidente (requiere incidents.view).
  incidents(assetId: number) {
    return http
      .get<PagedResult<AssetIncidentOption>>(`/api/assets/${assetId}/incidents${buildQuery({ pageSize: 50 })}`)
      .then((page) => page.items)
  },
  // Baja formal: única vía a DADO_DE_BAJA (el cambio manual de estado no la acepta).
  disposal(assetId: number) {
    return http
      .get<PagedResult<AssetDisposalInfo>>(`/api/disposals${buildQuery({ assetId, pageSize: 1 })}`)
      .then((page) => page.items[0] ?? null)
  },
  dispose(expectedVersion: number, input: CreateDisposalInput) {
    return http.post<{ assetDisposalId: number }>(`/api/disposals?expectedVersion=${expectedVersion}`, input)
  },
  // Línea de tiempo (history.view, solo ADMIN).
  timeline(assetId: number) {
    return http.get<AuditLogItem[]>(`/api/history/assets/${assetId}`)
  },
  // Garantías (procurement.manage, solo ADMIN).
  warranties(assetId: number) {
    return http.get<WarrantyItem[]>(`/api/assets/${assetId}/warranties`)
  },
  addWarranty(input: CreateWarrantyInput) {
    return http.post<{ warrantyId: number }>('/api/warranties', input)
  },
  removeWarranty(warrantyId: number, expectedVersion: number) {
    return http.delete(`/api/warranties/${warrantyId}?expectedVersion=${expectedVersion}`)
  },
  suppliers() {
    return http.get<PagedResult<SupplierOption>>('/api/suppliers?pageSize=100').then((page) => page.items)
  },
}

export function catalogName(
  items: CatalogItem[] | undefined,
  code: string,
): string {
  return items?.find((item) => item.code === code)?.name ?? code
}
