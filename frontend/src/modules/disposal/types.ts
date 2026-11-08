// Tipos del módulo de bajas (spec: specs/disposal.spec.md).

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface CatalogItem {
  id: number
  code: string
  name: string
}

export interface DisposalSummary {
  assetDisposalId: number
  assetId: number
  assetInventoryCode: string
  assetTypeName: string
  reasonCode: string
  reasonName: string
  folio: string
  dictamen: string | null
  disposedAt: string
  createdByUsername: string | null
  createdAt: string
}

export interface DisposalFilters {
  disposalReasonId?: number
  search?: string
}
