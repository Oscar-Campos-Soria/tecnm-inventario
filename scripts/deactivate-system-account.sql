-- La cuenta técnica "system" solo firma procesos automáticos (cierre de tickets,
-- created_by iniciales); nadie inicia sesión con ella. Su hash no es bcrypt,
-- así que su login ya daba 401: se deja inactiva para que quede explícito.
-- Idempotente.
SET search_path TO inventario, public;

UPDATE app_users
SET status = 'inactivo'
WHERE username = 'system' AND status <> 'inactivo';
