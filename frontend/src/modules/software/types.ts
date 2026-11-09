// Programas, licencias y asignaciones a equipos (software.manage: ADMIN y TECNICO).

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

export interface SoftwareItem {
  softwareId: number
  name: string
  vendor: string | null
  softwareTypeId: number | null
  softwareTypeName: string | null
  licenseCount: number
  installCount: number
  version: number
}

export interface SoftwareInput {
  name: string
  vendor: string | null
  softwareTypeId: number | null
}

export interface LicenseSummary {
  licenseId: number
  softwareId: number
  softwareName: string
  softwareVendor: string | null
  licenseTypeCode: string
  licenseTypeName: string
  // null = sin límite de equipos.
  quantityPurchased: number | null
  inUse: number
  available: number | null
  validFrom: string | null
  validUntil: string | null
  isExpired: boolean
  supplierName: string | null
  version: number
}

export interface LicenseDetail extends LicenseSummary {
  licenseTypeId: number
  // Solo los últimos 4 caracteres; la API nunca devuelve la clave completa.
  maskedLicenseKey: string | null
  supplierId: number | null
  notes: string | null
}

export interface LicenseInput {
  softwareId: number
  licenseTypeId: number
  // Al editar: null conserva la clave guardada; '' la quita.
  licenseKey: string | null
  quantityPurchased: number | null
  validFrom: string | null
  validUntil: string | null
  supplierId: number | null
  notes: string | null
}

export interface LicenseAssignment {
  licenseAssignmentId: number
  assetId: number
  inventoryCode: string
  hostname: string | null
  locationName: string | null
  assignedAt: string
  endedAt: string | null
  version: number
}

export interface AssetOption {
  assetId: number
  inventoryCode: string
  assetTypeName: string
  statusCode: string
}

export interface SupplierOption {
  supplierId: number
  name: string
}
