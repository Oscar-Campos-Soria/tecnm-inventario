import { http } from '../../shared/api/http'
import type {
  AssetOption,
  LocationOption,
  CreateIncidentInput,
  CreateTicketInput,
  CreateTicketResult,
  PagedResult,
  TicketCatalogs,
  TicketDetail,
  TicketFilters,
  TicketStatus,
  TicketSummary,
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

// Todas las mutaciones llevan expectedVersion: un 409 significa que alguien
// más cambió el ticket y la pantalla recarga.
export const ticketsApi = {
  list(filters: TicketFilters, pageNumber = 1, pageSize = 20) {
    return http.get<PagedResult<TicketSummary>>(`/api/tickets${buildQuery({ pageNumber, pageSize, ...filters })}`)
  },
  getById(ticketId: number) {
    return http.get<TicketDetail>(`/api/tickets/${ticketId}`)
  },
  create(input: CreateTicketInput) {
    return http.post<CreateTicketResult>('/api/tickets', input)
  },
  comment(ticketId: number, body: string, isInternal = false) {
    return http.post<{ ticketStatus: TicketStatus; ticketVersion: number }>(`/api/tickets/${ticketId}/comments`, { body, isInternal })
  },
  assign(ticketId: number, expectedVersion: number, assignedToUserId: number) {
    return http.put(`/api/tickets/${ticketId}/assignment?expectedVersion=${expectedVersion}`, { assignedToUserId })
  },
  setStatus(ticketId: number, expectedVersion: number, newStatus: TicketStatus, reason?: string) {
    return http.patch(`/api/tickets/${ticketId}/status?expectedVersion=${expectedVersion}`, { newStatus, reason })
  },
  setPriority(ticketId: number, expectedVersion: number, priorityId: number) {
    return http.patch(`/api/tickets/${ticketId}/priority?expectedVersion=${expectedVersion}`, { priorityId })
  },
  resolve(ticketId: number, expectedVersion: number, resolution: string) {
    return http.post(`/api/tickets/${ticketId}/resolve?expectedVersion=${expectedVersion}`, { resolution })
  },
  linkIncident(ticketId: number, expectedVersion: number, incidentId: number) {
    return http.put(`/api/tickets/${ticketId}/incident?expectedVersion=${expectedVersion}`, { incidentId })
  },
  // GET /api/catalogs solo exige estar autenticado.
  catalogs() {
    return http.get<TicketCatalogs>('/api/catalogs')
  },
  // Quien atiende tickets: el backend exige tickets.manage (ADMIN y TECNICO).
  technicians() {
    return http
      .get<UserItem[]>('/api/users')
      .then((users) => users.filter((user) => user.roleCode === 'ADMIN' || user.roleCode === 'TECNICO'))
  },
  // Búsqueda de equipo por código, serie o hostname (sin dados de baja); 20 resultados.
  assets(search = '') {
    return http
      .get<PagedResult<AssetOption>>(`/api/assets${buildQuery({ pageNumber: 1, pageSize: 20, search: search || undefined })}`)
      .then((page) => page.items)
  },
  asset(assetId: number) {
    return http.get<{ assetId: number; inventoryCode: string; hostname: string | null }>(`/api/assets/${assetId}`)
  },
  // Lista plana ordenada por código (GET /api/locations, permiso locations.view).
  locations() {
    return http.get<LocationOption[]>('/api/locations')
  },
  // Paso 1 de "crear incidente desde el ticket" (paso 2: linkIncident).
  createIncident(input: CreateIncidentInput) {
    return http.post<{ incidentId: number }>('/api/incidents', input)
  },
}
