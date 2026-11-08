-- ============================================================
-- PERMISO: locations.view (solo lectura de ubicaciones)
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-locations-view-permission.sql
--
-- Hoy GET /api/locations exige locations.manage (solo ADMIN), así que los
-- selectores de ubicación (movimientos, asignaciones, nuevo ticket) no cargan
-- para otros roles. Este permiso deja VER la lista; crear, editar y borrar
-- siguen con locations.manage. Sin efecto hasta que los GET de
-- LocationsController pidan locations.view.
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('locations.view', 'Ver ubicaciones', 'Consultar el árbol y el detalle de ubicaciones (sin modificarlas)')
ON CONFLICT (code) DO NOTHING;

-- Todos los roles: cualquiera puede necesitar elegir un edificio o salón.
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('ADMIN', 'TECNICO', 'RESPONSABLE_AREA', 'USUARIO_GENERAL')
  AND p.code = 'locations.view'
ON CONFLICT DO NOTHING;
