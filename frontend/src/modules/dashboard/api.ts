import { http } from '../../shared/api/http'
import type { DashboardSummary } from './types'

export const dashboardApi = {
  summary: () => http.get<DashboardSummary>('/api/dashboard/summary'),
}
