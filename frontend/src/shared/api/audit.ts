// Fila de audit_log tal como la devuelve /api/history (bitácora y línea de tiempo del activo).
export interface AuditLogItem {
  auditLogId: number
  tableName: string
  recordId: number
  action: 'INSERT' | 'UPDATE' | 'DELETE'
  performedById: number | null
  performedByUsername: string | null
  performedAt: string
  oldData: string | null
  newData: string | null
}
