export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AssetSummary {
  assetId: number
  inventoryCode: string
  serialNumber: string | null
  hostname: string | null
  version: number
}

export interface AssetDetail {
  assetId: number
  inventoryCode: string
  assetTypeCode: string
  // GetAssetByIdResponse lo llama StatusCode (antes el cliente esperaba assetStatusCode y salía vacío).
  statusCode: string
  manufacturerName: string | null
  modelName: string | null
  currentLocationId: number | null
  serialNumber: string | null
  hostname: string | null
  notes: string | null
  version: number
}

export interface AssetMovement {
  assetMovementId: number
  fromLocationId: number | null
  fromLocationName: string | null
  toLocationId: number
  toLocationName: string
  movementReasonId: number
  movementReasonCode: string
  movementReasonName: string
  movedBy: number
  movedByUsername: string
  movedAt: string
  notes: string | null
}

export interface AssetAssignment {
  assetAssignmentId: number
  assignedTo: number
  assignedToUsername: string
  assignmentTypeId: number
  assignmentTypeCode: string
  assignmentTypeName: string
  locationId: number | null
  locationName: string | null
  assignedAt: string
  returnedAt: string | null
  isActive: boolean
  notes: string | null
}

export interface UserItem {
  userId: number
  username: string
  firstName: string
  lastName: string
  roleCode: string
  status: string
}

export interface LocationItem {
  locationId: number
  code: string
  name: string
  locationTypeCode: string
  parentLocationId: number | null
  version: number
}

export interface CatalogItem {
  id: number
  code: string
  name: string
}

export interface GetCatalogsResponse {
  assetTypes: CatalogItem[]
  assetStatuses: CatalogItem[]
  locationTypes: CatalogItem[]
  incidentTypes: CatalogItem[]
  incidentPriorities: CatalogItem[]
  incidentStatuses: CatalogItem[]
  maintenanceTypes: CatalogItem[]
  movementReasons: CatalogItem[]
  assignmentTypes: CatalogItem[]
  componentTypes: CatalogItem[]
  disposalReasons: CatalogItem[]
  softwareTypes: CatalogItem[]
  licenseTypes: CatalogItem[]
}

// Bitácora de mantenimiento (backend/Features/Maintenance).
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
}

export interface CreateMaintenanceInput {
  assetId: number
  maintenanceTypeId: number
  performedAt: string
  nextMaintenanceAt?: string
  description: string
  partsUsed?: string
  cost?: number
  incidentId?: number
}

export interface AssetIncidentOption {
  incidentId: number
  title: string
  statusName: string
}

// Baja formal (backend/Features/Disposals).
export interface AssetDisposalInfo {
  assetDisposalId: number
  assetId: number
  reasonCode: string
  reasonName: string
  folio: string
  dictamen: string | null
  disposedAt: string
  createdByUsername: string | null
}

export interface CreateDisposalInput {
  assetId: number
  disposalReasonId: number
  folio: string
  dictamen?: string
  disposedAt?: string
}

export interface CreateMovementInput {
  toLocationId: number
  movementReasonId: number
  movedAt?: string
  notes?: string
}

export interface CreateAssignmentInput {
  assignedTo: number
  assignmentTypeId: number
  locationId?: number | null
  assignedAt?: string
  notes?: string
}
