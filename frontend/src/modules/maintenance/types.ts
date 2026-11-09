// Tipos del módulo de mantenimiento (spec: specs/maintenance.spec.md).

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface MaintenanceItem {
  maintenanceRecordId: number
  assetId: number
  assetInventoryCode: string
  typeCode: string
  typeName: string
  performedByName: string | null
  performedAt: string | null
  nextMaintenanceAt: string | null
  description: string | null
  partsUsed: string | null
  cost: number | null
  incidentId: number | null
  version: number
  // Solo en programados sin realizar.
  scheduledAt: string | null
}
