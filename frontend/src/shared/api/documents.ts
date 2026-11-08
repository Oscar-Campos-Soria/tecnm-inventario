import { http } from './http'

// Adjuntos de backend/Features/Documents (transversal: incidentes, tickets…).
// El servidor decide quién ve qué (permiso + dueño del registro).

export type DocumentOwnerType = 'INCIDENT' | 'TICKET' | 'ASSET_DISPOSAL'

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
  download(documentId: number) {
    return http.download(`/api/documents/${documentId}/download`)
  },
}
