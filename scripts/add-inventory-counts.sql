-- ============================================================
-- LEVANTAMIENTO FÍSICO (specs/inventory.spec.md)
-- Idempotente: seguro de re-ejecutar.
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-inventory-counts.sql
-- ============================================================

SET search_path TO inventario, public;

-- Alcance del conteo: un edificio/área (incluye sus sub-ubicaciones).
ALTER TABLE inventory_sessions
  ADD COLUMN IF NOT EXISTS location_id bigint REFERENCES locations(location_id);

-- Dónde debía estar el equipo al abrir el conteo (NULL = sobrante: apareció
-- pero no estaba en la lista), cuándo se revisó y el ajuste aplicado al conciliar.
ALTER TABLE inventory_records
  ADD COLUMN IF NOT EXISTS expected_location_id bigint REFERENCES locations(location_id),
  ADD COLUMN IF NOT EXISTS checked_at timestamptz,
  ADD COLUMN IF NOT EXISTS checked_by bigint REFERENCES app_users(user_id),
  ADD COLUMN IF NOT EXISTS adjustment varchar(20)
    CHECK (adjustment IS NULL OR adjustment IN ('MOVIMIENTO','INCIDENTE','SIN_CAMBIO')),
  ADD COLUMN IF NOT EXISTS adjusted_at timestamptz,
  ADD COLUMN IF NOT EXISTS adjusted_by bigint REFERENCES app_users(user_id),
  ADD COLUMN IF NOT EXISTS incident_id bigint REFERENCES incidents(incident_id);

CREATE INDEX IF NOT EXISTS ix_inventory_records_session ON inventory_records(inventory_session_id);

-- Motivo de movimiento para corregir la ubicación tras un conteo.
INSERT INTO movement_reasons (code, name) VALUES
  ('AJUSTE_INVENTARIO', 'Ajuste por levantamiento físico')
ON CONFLICT (code) DO NOTHING;

-- Permiso: abrir, escanear, cerrar y conciliar conteos.
INSERT INTO permissions (code, name, description) VALUES
  ('inventory.manage', 'Levantamiento físico', 'Abrir conteos, registrar equipos encontrados, cerrar y conciliar')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('ADMIN', 'TECNICO')
  AND p.code = 'inventory.manage'
ON CONFLICT DO NOTHING;
