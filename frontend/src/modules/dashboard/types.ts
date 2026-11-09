// GET /api/dashboard/summary (dashboard.view). Cacheado 60 s en el servidor.

export interface CountItem {
  code: string
  name: string
  count: number
}

export interface DashboardSummary {
  // Activos en servicio (sin dados de baja).
  totalAssets: number
  // Todos los estados, incluido DADO_DE_BAJA.
  assetsByStatus: CountItem[]
  assetsByType: CountItem[]
  openIncidentsByPriority: CountItem[]
  preventiveOverdue: number
  preventiveDueSoon: number
  // Las 10 ubicaciones con más activos en servicio.
  assetsByLocation: CountItem[]
  licensesExpired: number
  licensesExpiringSoon: number
  windowDays: number
  generatedAt: string
}
