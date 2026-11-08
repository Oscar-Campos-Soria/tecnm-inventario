import { http } from '../../shared/api/http'
import type { CatalogItem, DisposalFilters, DisposalSummary, PagedResult } from './types'

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const disposalApi = {
  list(filters: DisposalFilters, pageNumber = 1, pageSize = 20) {
    return http.get<PagedResult<DisposalSummary>>(`/api/disposals${buildQuery({ pageNumber, pageSize, ...filters })}`)
  },
  reasons() {
    return http.get<{ disposalReasons: CatalogItem[] }>('/api/catalogs').then((c) => c.disposalReasons)
  },
}
