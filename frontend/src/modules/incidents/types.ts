// Tipos del módulo de incidentes (spec: specs/incidents.spec.md).
// PagedResult, CatalogItem y UserItem se duplican por módulo
// (patrón de assets y locations): modules no importa de otro módulo.

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

export interface UserItem {
  userId: number
  username: string
  firstName: string
  lastName: string
  roleCode: string
  status: string
}

export interface IncidentSummary {
  incidentId: number
  assetId: number
  assetInventoryCode: string
  title: string
  typeCode: string
  typeName: string
  priorityCode: string
  priorityName: string
  statusCode: string
  statusName: string
  reportedByUsername: string
  assignedToUsername: string | null
  reportedAt: string
  resolvedAt: string | null
  version: number
}

export interface IncidentDetail {
  incidentId: number
  assetId: number
  assetInventoryCode: string
  assetStatusCode: string
  title: string
  description: string | null
  diagnosis: string | null
  solution: string | null
  incidentTypeId: number
  typeCode: string
  typeName: string
  incidentPriorityId: number
  priorityCode: string
  priorityName: string
  statusCode: string
  statusName: string
  reportedById: number
  reportedByUsername: string
  reportedByName: string
  assignedToId: number | null
  assignedToUsername: string | null
  assignedToName: string | null
  reportedAt: string
  resolvedAt: string | null
  createdAt: string
  updatedAt: string
  version: number
}

export interface IncidentFilters {
  assetId?: number
  status?: string
  priority?: string
  incidentTypeId?: number
  assignedTo?: number
  onlyOpen?: boolean
  search?: string
}

// Solo los catálogos que usa este módulo (GET /api/catalogs
// devuelve más campos; TypeScript acepta el subconjunto).
export interface IncidentCatalogs {
  incidentTypes: CatalogItem[]
  incidentPriorities: CatalogItem[]
  incidentStatuses: CatalogItem[]
}

export interface CreateIncidentInput {
  assetId: number
  incidentTypeId: number
  incidentPriorityId: number
  title: string
  description?: string
}

export interface CreateIncidentResult {
  incidentId: number
  assetId: number
  incidentStatusCode: string
  assetStatusCode: string
  reportedAt: string
  version: number
}

export interface AssignTechnicianInput {
  assignedToUserId: number
}

// Respuestas alineadas con los records del backend (Features/Incidents/*Response.cs).
export interface AssignTechnicianResult {
  incidentId: number
  assignedToId: number
  assignedToUsername: string
  version: number
}

export interface ChangeStatusInput {
  newStatusCode: string
  reason?: string
}

export interface ChangeStatusResult {
  incidentId: number
  incidentStatusCode: string
  assetStatusCode: string
  version: number
}

export interface ResolveIncidentInput {
  solution: string
  diagnosis?: string
}

export interface ResolveIncidentResult {
  incidentId: number
  incidentStatusCode: string
  assetStatusCode: string
  // true si el activo volvió a DISPONIBLE/ASIGNADO (no quedan otros incidentes abiertos).
  assetReleased: boolean
  openIncidentsRemaining: number
  resolvedAt: string | null
  version: number
}

export interface DeleteIncidentResult {
  incidentId: number
  deleted: boolean
}
