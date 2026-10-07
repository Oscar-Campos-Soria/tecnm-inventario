import { http } from '../../shared/api/http'
import type {
  AssetAssignment,
  AssetDetail,
  AssetMovement,
  AssetSummary,
  CatalogItem,
  CreateAssignmentInput,
  CreateMovementInput,
  GetCatalogsResponse,
  LocationItem,
  PagedResult,
  UserItem,
} from './types'

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const assetsApi = {
  list(pageNumber = 1, pageSize = 20) {
    return http.get<PagedResult<AssetSummary>>(
      `/api/assets${buildQuery({ pageNumber, pageSize })}`,
    )
  },
  getById(assetId: number) {
    return http.get<AssetDetail>(`/api/assets/${assetId}`)
  },
  createMovement(assetId: number, input: CreateMovementInput) {
    return http.post<{ assetMovementId: number }>(
      `/api/assets/${assetId}/movements`,
      input,
    )
  },
  getMovements(assetId: number, pageNumber = 1, pageSize = 10) {
    return http.get<PagedResult<AssetMovement>>(
      `/api/assets/${assetId}/movements${buildQuery({ pageNumber, pageSize })}`,
    )
  },
  createAssignment(assetId: number, input: CreateAssignmentInput) {
    return http.post<{ assetAssignmentId: number }>(
      `/api/assets/${assetId}/assignments`,
      input,
    )
  },
  getAssignments(assetId: number) {
    return http.get<AssetAssignment[]>(`/api/assets/${assetId}/assignments`)
  },
  returnAssignment(assetId: number, assignmentId: number) {
    return http.post<{ assetAssignmentId: number }>(
      `/api/assets/${assetId}/assignments/${assignmentId}/return`,
      {},
    )
  },
  users(search?: string) {
    return http.get<UserItem[]>(`/api/users${buildQuery({ search })}`)
  },
  locations() {
    return http.get<LocationItem[]>('/api/locations')
  },
  catalogs() {
    return http.get<GetCatalogsResponse>('/api/catalogs')
  },
}

export function catalogName(
  items: CatalogItem[] | undefined,
  code: string,
): string {
  return items?.find((item) => item.code === code)?.name ?? code
}
