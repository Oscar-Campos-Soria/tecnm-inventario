-- ============================================================
-- OPCIONAL, SOLO DESARROLLO: primer administrador (admin_local) para entrar a la BD de desarrollo.
-- En producción el administrador se crea con el CLI tecnm (opción 8).
-- El hash NO vive en el repo: se genera con backend/tools/PasswordHasher y se pasa por variable.
-- Idempotente (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -v hash="$HASH" -f scripts/seed-dev-admin.sql
-- ============================================================
\if :{?hash}
\else
  \echo 'Falta -v hash=...'
  \quit
\endif

SET search_path TO inventario, public;

INSERT INTO app_users (username, email, password_hash, first_name, last_name, role_id, status, version)
SELECT 'admin_local', 'admin-local@localhost', :'hash', 'Administrador', 'Local', role_id, 'activo', 1
FROM roles WHERE code = 'ADMIN'
ON CONFLICT (username) DO NOTHING;
