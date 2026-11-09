-- ============================================================
-- PERMISOS DE ADMINISTRACIÓN: users.manage e history.view (solo ADMIN)
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-admin-permissions.sql
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('users.manage', 'Administrar usuarios', 'Editar datos, rol, estado y contraseña de los usuarios'),
  ('history.view', 'Consultar historial',  'Ver la bitácora de auditoría y la línea de tiempo de los activos')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code = 'ADMIN'
  AND p.code IN ('users.manage', 'history.view')
ON CONFLICT DO NOTHING;
