import { http } from '../../shared/api/http'
import type { AcquisitionDetail, AcquisitionInput, AcquisitionSummary, AssetOption, PagedResult, Supplier, SupplierInput } from './types'

export const procurementApi = {
  suppliers: (pageNumber = 1) => http.get<PagedResult<Supplier>>(`/api/suppliers?pageNumber=${pageNumber}&pageSize=20`),
  createSupplier: (input: SupplierInput) => http.post<{ supplierId: number }>('/api/suppliers', input),
  updateSupplier: (s: Supplier, input: SupplierInput) => http.put(`/api/suppliers/${s.supplierId}?expectedVersion=${s.version}`, input),
  deleteSupplier: (s: Supplier) => http.delete(`/api/suppliers/${s.supplierId}?expectedVersion=${s.version}`),

  acquisitions: (pageNumber = 1) => http.get<PagedResult<AcquisitionSummary>>(`/api/acquisitions?pageNumber=${pageNumber}&pageSize=20`),
  acquisition: (id: number) => http.get<AcquisitionDetail>(`/api/acquisitions/${id}`),
  createAcquisition: (input: AcquisitionInput) => http.post<{ acquisitionId: number }>('/api/acquisitions', input),
  deleteAcquisition: (a: AcquisitionSummary) => http.delete(`/api/acquisitions/${a.acquisitionId}?expectedVersion=${a.version}`),

  // Buscador de activos para ligarlos a la compra (assets.view).
  searchAssets: (search: string) =>
    http
      .get<PagedResult<AssetOption>>(`/api/assets?pageSize=10&search=${encodeURIComponent(search)}`)
      .then((page) => page.items),
}
