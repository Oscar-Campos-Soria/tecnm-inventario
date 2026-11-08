-- ============================================================
-- MANTENIMIENTO: trazabilidad con el incidente y costo (specs/maintenance.spec.md)
-- Idempotente: seguro de re-ejecutar.
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-maintenance-fields.sql
-- ============================================================

SET search_path TO inventario, public;

-- Correctivo originado por un incidente (opcional).
ALTER TABLE maintenance_records
  ADD COLUMN IF NOT EXISTS incident_id bigint REFERENCES incidents(incident_id);

-- Costo de la intervención en MXN (opcional).
ALTER TABLE maintenance_records
  ADD COLUMN IF NOT EXISTS cost numeric(12,2) CHECK (cost IS NULL OR cost >= 0);

CREATE INDEX IF NOT EXISTS ix_maintenance_records_asset
  ON maintenance_records(asset_id) WHERE is_deleted = false;

-- Calendario de preventivos: se consulta por próxima fecha.
CREATE INDEX IF NOT EXISTS ix_maintenance_records_next
  ON maintenance_records(next_maintenance_at) WHERE is_deleted = false AND next_maintenance_at IS NOT NULL;
