import type { TicketStatus } from '../tickets/types'
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
  assetTypeName: string
  statusCode: string
  statusName: string
  locationId: number | null
  locationName: string | null
}

// Filtros de GET /api/assets (ubicación incluye sus sub-ubicaciones).
export interface AssetFilters {
  search?: string
  status?: string
  assetTypeId?: number
  locationId?: number
  includeDisposed?: boolean
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
  // Correctivo ligado: resolver también el incidente (requiere incidents.resolve).
  resolveIncident?: boolean
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

// Garantías del activo (procurement.manage).
export interface WarrantyItem {
  warrantyId: number
  supplierId: number | null
  warrantyNumber: string | null
  startsOn: string | null
  endsOn: string | null
  coverage: string | null
  version: number
}

export interface CreateWarrantyInput {
  assetId: number
  supplierId: number | null
  warrantyNumber: string | null
  startsOn: string | null
  endsOn: string | null
  coverage: string | null
  terms: string | null
}

export interface SupplierOption {
  supplierId: number
  name: string
}

// Componentes internos del activo (components.manage).
export interface AssetComponentItem {
  assetComponentId: number
  componentTypeId: number
  componentTypeCode: string
  componentTypeName: string
  description: string | null
  serialNumber: string | null
  installedAt: string
  // null = sigue instalado.
  removedAt: string | null
  version: number
}

// Software instalado en el activo (software.manage).
export interface AssetSoftwareItem {
  assetSoftwareId: number
  softwareId: number
  softwareName: string
  softwareVendor: string | null
  versionInstalled: string | null
  installedAt: string
  isCurrent: boolean
  // El equipo ocupa una licencia vigente de este programa.
  hasActiveLicense: boolean
  version: number
}

export interface SoftwareOption {
  softwareId: number
  name: string
  vendor: string | null
}

// GET /api/assets/mine: equipos con asignación vigente del usuario y sus propios tickets de cada uno.
export interface MyAsset {
  assetId: number
  inventoryCode: string
  assetTypeName: string
  manufacturerName: string | null
  modelName: string | null
  serialNumber: string | null
  statusCode: string
  statusName: string
  locationName: string | null
  assignmentTypeName: string
  assignedAt: string
  tickets: MyAssetTicket[]
}

export interface MyAssetTicket {
  ticketId: number
  title: string
  status: TicketStatus
  createdAt: string
}

export type { AuditLogItem } from '../../shared/api/audit'
