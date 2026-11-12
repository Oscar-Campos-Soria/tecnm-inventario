-- ============================================================
-- USUARIO_GENERAL: solo lo suyo. Quita assets.view (inventario completo) e incidents.create
-- (reporta fallas por ticket; el técnico decide si se vuelve incidente). Ve sus equipos en
-- "Mis equipos" (GET /api/assets/mine, sin permiso especial) y sus tickets.
-- Va después de seed-permissions.sql, que concede esos permisos al sembrar.
-- Idempotente: seguro de re-ejecutar.
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/restrict-general-user.sql
-- ============================================================

SET search_path TO inventario, public;

DELETE FROM role_permissions rp
USING roles r, permissions p
WHERE rp.role_id = r.role_id
  AND rp.permission_id = p.permission_id
  AND r.code = 'USUARIO_GENERAL'
  AND p.code IN ('assets.view', 'incidents.create');
