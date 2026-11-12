-- ============================================================
-- PERMISO DEL ASISTENTE IA: assistant.use (todos los roles)
-- El asistente solo lee y cada herramienta exige además su propio permiso
-- (assets.view, incidents.view, ...): un rol ve con el asistente lo mismo que en pantalla.
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-assistant-permission.sql
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('assistant.use', 'Usar el asistente IA', 'Hacer preguntas al asistente (solo lectura, con los permisos del propio rol)')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('ADMIN', 'TECNICO', 'RESPONSABLE_AREA', 'USUARIO_GENERAL')
  AND p.code = 'assistant.use'
ON CONFLICT DO NOTHING;
