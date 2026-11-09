import { http } from './http'

// Adjuntos de backend/Features/Documents (transversal: incidentes, tickets, mantenimientos, bajas, compras).
// El servidor decide quién ve qué (permiso + dueño del registro).

export type DocumentOwnerType = 'INCIDENT' | 'TICKET' | 'ASSET_DISPOSAL' | 'MAINTENANCE' | 'ACQUISITION'

export interface StoredDocument {
  documentId: number
  fileName: string
  mimeType: string
  sizeBytes: number
  description: string | null
  createdAt: string
  createdBy: number | null
  version: number
}

// Archivo de la ficha del activo, con el registro al que pertenece.
export interface AssetDocument {
  documentId: number
  fileName: string
  mimeType: string
  sizeBytes: number
  description: string | null
  createdAt: string
  ownerType: DocumentOwnerType
  ownerId: number
  ownerLabel: string
}

export interface UploadDocumentResult {
  documentId: number
  ownerType: string
  ownerId: number
  fileName: string
  mimeType: string
  sizeBytes: number
}

// Límites del backend (CHECK de documents): JPG, PNG o PDF de hasta 5 MB.
export const ALLOWED_MIME_TYPES = ['image/jpeg', 'image/png', 'application/pdf']
export const MAX_DOCUMENT_BYTES = 5 * 1024 * 1024

export const documentsApi = {
  list(ownerType: DocumentOwnerType, ownerId: number) {
    const query = new URLSearchParams({ ownerType, ownerId: String(ownerId) })
    return http.get<StoredDocument[]>(`/api/documents?${query}`)
  },
  upload(ownerType: DocumentOwnerType, ownerId: number, file: File) {
    const form = new FormData()
    form.append('ownerType', ownerType)
    form.append('ownerId', String(ownerId))
    form.append('file', file)
    return http.postForm<UploadDocumentResult>('/api/documents', form)
  },
  forAsset(assetId: number) {
    return http.get<AssetDocument[]>(`/api/assets/${assetId}/documents`)
  },
  remove(documentId: number, expectedVersion: number) {
    return http.delete<unknown>(`/api/documents/${documentId}?expectedVersion=${expectedVersion}`)
  },
  download(documentId: number) {
    return http.download(`/api/documents/${documentId}/download`)
  },
}
