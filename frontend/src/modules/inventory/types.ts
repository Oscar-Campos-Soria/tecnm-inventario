// Tipos del levantamiento físico (spec: specs/inventory.spec.md).

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

export interface LocationOption {
  locationId: number
  code: string
  name: string
  parentLocationId: number | null
}

export type SessionStatus = 'abierta' | 'cerrada' | 'cancelada'
export type RecordStatus = 'PENDIENTE' | 'ENCONTRADO' | 'EN_OTRO_LUGAR' | 'NO_ENCONTRADO' | 'SOBRANTE'
export type Adjustment = 'MOVIMIENTO' | 'INCIDENTE' | 'SIN_CAMBIO'

export interface SessionSummary {
  inventorySessionId: number
  name: string
  locationName: string | null
  status: SessionStatus
  startedAt: string
  endedAt: string | null
  totalRecords: number
  checkedRecords: number
  foundRecords: number
  createdByUsername: string | null
}

export interface InventoryRecord {
  inventoryRecordId: number
  assetId: number
  inventoryCode: string
  assetTypeName: string
  assetStatusCode: string
  assetVersion: number
  assetCurrentLocationId: number | null
  expectedLocationId: number | null
  expectedLocationName: string | null
  locationFoundId: number | null
  locationFoundName: string | null
  status: RecordStatus
  checkedAt: string | null
  checkedByUsername: string | null
  notes: string | null
  adjustment: Adjustment | null
  incidentId: number | null
  version: number
}

export interface SessionDetail {
  inventorySessionId: number
  name: string
  locationId: number | null
  locationName: string | null
  status: SessionStatus
  startedAt: string
  endedAt: string | null
  summary: Partial<Record<RecordStatus, number>>
  records: InventoryRecord[]
  version: number
}

export interface ScanResult {
  inventoryRecordId: number
  assetId: number
  inventoryCode: string
  status: RecordStatus
  alreadyChecked: boolean
  assetStatusCode: string
}

export interface InventoryCatalogs {
  movementReasons: CatalogItem[]
  incidentTypes: CatalogItem[]
  incidentPriorities: CatalogItem[]
}
