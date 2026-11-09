import { http } from '../../shared/api/http'
import type {
  AssetOption,
  CatalogItem,
  LicenseAssignment,
  LicenseDetail,
  LicenseInput,
  LicenseSummary,
  PagedResult,
  SoftwareInput,
  SoftwareItem,
  SupplierOption,
} from './types'

function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== null && v !== undefined && v !== '') q.set(k, String(v))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export const softwareApi = {
  software: (pageNumber = 1, search = '', pageSize = 20) =>
    http.get<PagedResult<SoftwareItem>>(`/api/software${query({ pageNumber, pageSize, search })}`),
  createSoftware: (input: SoftwareInput) => http.post<{ softwareId: number }>('/api/software', input),
  updateSoftware: (s: SoftwareItem, input: SoftwareInput) =>
    http.put(`/api/software/${s.softwareId}?expectedVersion=${s.version}`, input),
  deleteSoftware: (s: SoftwareItem) => http.delete(`/api/software/${s.softwareId}?expectedVersion=${s.version}`),

  licenses: (pageNumber = 1, search = '', expiringInDays: number | null = null) =>
    http.get<PagedResult<LicenseSummary>>(
      `/api/software/licenses${query({ pageNumber, pageSize: 20, search, expiringInDays })}`,
    ),
  license: (id: number) => http.get<LicenseDetail>(`/api/software/licenses/${id}`),
  createLicense: (input: LicenseInput) => http.post<{ licenseId: number }>('/api/software/licenses', input),
  updateLicense: (l: LicenseDetail, input: Omit<LicenseInput, 'softwareId'>) =>
    http.put(`/api/software/licenses/${l.licenseId}?expectedVersion=${l.version}`, input),
  deleteLicense: (l: LicenseSummary) =>
    http.delete(`/api/software/licenses/${l.licenseId}?expectedVersion=${l.version}`),

  assignments: (licenseId: number, onlyActive: boolean) =>
    http.get<PagedResult<LicenseAssignment>>(
      `/api/software/licenses/${licenseId}/assignments${query({ onlyActive, pageSize: 100 })}`,
    ),
  assign: (licenseId: number, assetId: number) =>
    http.post(`/api/software/licenses/${licenseId}/assignments`, { assetId }),
  release: (a: LicenseAssignment) =>
    http.post(`/api/software/licenses/assignments/${a.licenseAssignmentId}/end?expectedVersion=${a.version}`, {}),

  catalogs: () => http.get<{ softwareTypes: CatalogItem[]; licenseTypes: CatalogItem[] }>('/api/catalogs'),
  // Solo con procurement.manage (ADMIN); sin ese permiso la licencia se registra sin proveedor.
  suppliers: () =>
    http.get<PagedResult<SupplierOption>>('/api/suppliers?pageSize=100').then((page) => page.items),
  // Buscador de equipos para asignar una licencia (assets.view).
  searchAssets: (search: string) =>
    http
      .get<PagedResult<AssetOption>>(`/api/assets${query({ pageSize: 10, search })}`)
      .then((page) => page.items),
}
