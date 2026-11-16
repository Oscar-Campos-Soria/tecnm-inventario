-- ============================================================
-- PERMISO assistant.tickets.propose: el asistente IA prepara un borrador de ticket
-- (herramienta proponer_ticket). No escribe nada: el usuario lo confirma en la tarjeta y
-- se crea con POST /api/tickets (exige tickets.create).
-- Solo quien pide ayuda: RESPONSABLE_AREA y USUARIO_GENERAL (ADMIN y TECNICO atienden tickets).
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-assistant-ticket-permission.sql
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO permissions (code, name, description) VALUES
  ('assistant.tickets.propose', 'Proponer tickets con el asistente', 'El asistente IA prepara un borrador de ticket que el usuario confirma')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('RESPONSABLE_AREA', 'USUARIO_GENERAL')
  AND p.code = 'assistant.tickets.propose'
ON CONFLICT DO NOTHING;
