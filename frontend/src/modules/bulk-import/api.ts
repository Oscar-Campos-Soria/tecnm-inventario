import { http } from '../../shared/api/http'
import { downloadFile } from '../../shared/utils/download'

export interface PreviewRow {
  rowNumber: number
  inventoryCode: string | null
  typeName: string | null
  locationName: string | null
  errors: string[]
}

export interface PreviewResult {
  totalRows: number
  validRows: number
  rows: PreviewRow[]
}

function form(file: File): FormData {
  const data = new FormData()
  data.append('file', file)
  return data
}

const base = '/api/bulk-import/assets'

export const bulkImportApi = {
  template: () => downloadFile(`${base}/template.xlsx`),
  preview: (file: File) => http.postForm<PreviewResult>(`${base}/preview`, form(file)),
  commit: (file: File) => http.postForm<{ created: number; assetIds: number[] }>(`${base}/commit`, form(file)),
}
