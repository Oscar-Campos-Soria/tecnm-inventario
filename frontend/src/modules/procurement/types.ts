// Proveedores y compras (procurement.manage, solo ADMIN).

export interface PagedResult<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface Supplier {
  supplierId: number
  name: string
  taxId: string | null
  contactName: string | null
  email: string | null
  phone: string | null
  address: string | null
  notes: string | null
  version: number
}

export type SupplierInput = Omit<Supplier, 'supplierId' | 'version'>

export interface AcquisitionSummary {
  acquisitionId: number
  supplierId: number | null
  supplierName: string | null
  purchaseOrder: string | null
  invoiceNumber: string | null
  acquiredAt: string | null
  totalAmount: number | null
  currencyCode: string
  assetCount: number
  version: number
}

export interface AcquisitionDetail extends Omit<AcquisitionSummary, 'assetCount'> {
  subtotal: number | null
  taxAmount: number | null
  warrantyStart: string | null
  warrantyEnd: string | null
  notes: string | null
  assets: { assetId: number; inventoryCode: string; unitCost: number | null }[]
}

export interface AcquisitionInput {
  supplierId: number | null
  purchaseOrder: string | null
  invoiceNumber: string | null
  acquiredAt: string | null
  subtotal: number | null
  taxAmount: number | null
  totalAmount: number | null
  currencyCode: string
  warrantyStart: string | null
  warrantyEnd: string | null
  notes: string | null
  assets: { assetId: number; unitCost: number | null }[]
}

export interface AssetOption {
  assetId: number
  inventoryCode: string
  assetTypeName: string
}
