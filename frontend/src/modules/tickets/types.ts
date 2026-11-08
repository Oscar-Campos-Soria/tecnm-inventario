// Tipos del módulo de tickets (spec: specs/tickets.spec.md, README.md del módulo).
// PagedResult, CatalogItem y UserItem se duplican por módulo: modules no
// importa de otro módulo.

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

export type TicketStatus =
  | 'ABIERTO'
  | 'EN_ATENCION'
  | 'ESPERANDO_USUARIO'
  | 'RESUELTO'
  | 'CERRADO'
  | 'CANCELADO'

export type PriorityCode = 'BAJA' | 'MEDIA' | 'ALTA' | 'CRITICA'

export interface TicketSummary {
  ticketId: number
  title: string
  categoryCode: string
  categoryName: string
  priorityCode: PriorityCode
  priorityName: string
  status: TicketStatus
  requesterUsername: string
  requesterName: string
  assignedToUsername: string | null
  assetInventoryCode: string | null
  incidentId: number | null
  createdAt: string
  updatedAt: string
  version: number
}

export interface TicketComment {
  commentId: number
  authorId: number
  authorUsername: string
  authorName: string
  body: string
  isInternal: boolean
  createdAt: string
}

export interface TicketDetail {
  ticketId: number
  title: string
  description: string | null
  resolution: string | null
  categoryId: number
  categoryCode: string
  categoryName: string
  priorityId: number
  priorityCode: PriorityCode
  priorityName: string
  status: TicketStatus
  requesterId: number
  requesterUsername: string
  requesterName: string
  assignedTo: number | null
  assignedToUsername: string | null
  assignedToName: string | null
  assetId: number | null
  assetInventoryCode: string | null
  locationId: number | null
  locationName: string | null
  incidentId: number | null
  incidentStatusCode: string | null
  incidentStatusName: string | null
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  closedAt: string | null
  // Fecha del cierre automático (solo mientras está RESUELTO).
  autoCloseAt: string | null
  // El servidor dice si quien mira atiende tickets (tickets.manage).
  canManage: boolean
  comments: TicketComment[]
  version: number
}

export interface TicketFilters {
  status?: TicketStatus
  categoryId?: number
  priority?: PriorityCode
  assignedTo?: number
  unassigned?: boolean
  onlyOpen?: boolean
  search?: string
}

// Solo los catálogos que usa este módulo (GET /api/catalogs devuelve más).
export interface TicketCatalogs {
  ticketCategories: CatalogItem[]
  incidentPriorities: CatalogItem[]
  incidentTypes: CatalogItem[]
}

export interface CreateTicketInput {
  categoryId: number
  priorityId?: number
  title: string
  description?: string
  assetId?: number
  locationId?: number
}

export interface CreateTicketResult {
  ticketId: number
  status: TicketStatus
  priorityCode: PriorityCode
  createdAt: string
  version: number
}

export interface AssetOption {
  assetId: number
  inventoryCode: string
  hostname: string | null
}

export interface LocationOption {
  locationId: number
  code: string
  name: string
}

export interface CreateIncidentInput {
  assetId: number
  incidentTypeId: number
  incidentPriorityId: number
  title: string
  description?: string
}
