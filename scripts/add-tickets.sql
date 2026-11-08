-- ============================================================
-- MÓDULO TICKETS (mesa de ayuda) — specs/tickets.spec.md
-- Idempotente: seguro de re-ejecutar.
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/add-tickets.sql
-- Requiere: database.sql, seed-permissions.sql, seed-documents.sql.
-- ============================================================

SET search_path TO inventario, public;

-- ------------------------------------------------------------
-- 1. CATÁLOGO DE CATEGORÍAS (mismas columnas que incident_types)
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ticket_categories (
  ticket_category_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  code varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, description text,
  created_at timestamptz NOT NULL DEFAULT now(), created_by bigint REFERENCES app_users(user_id),
  updated_at timestamptz NOT NULL DEFAULT now(), updated_by bigint REFERENCES app_users(user_id),
  is_deleted boolean NOT NULL DEFAULT false, deleted_at timestamptz, deleted_by bigint REFERENCES app_users(user_id),
  version integer NOT NULL DEFAULT 1
);

INSERT INTO ticket_categories (code, name, description) VALUES
  ('FALLA_EQUIPO',     'Falla de equipo',      'El equipo no enciende, se traba, hace ruido, pantalla dañada…'),
  ('SOFTWARE',         'Software',             'Instalación, actualización o error de un programa'),
  ('CUENTAS_ACCESO',   'Cuentas y accesos',    'Contraseñas, correo institucional, permisos de sistemas'),
  ('RED_INTERNET',     'Red e internet',       'Sin conexión, WiFi lento, cableado'),
  ('SOLICITUD_EQUIPO', 'Solicitud de equipo',  'Préstamo o asignación de proyector, laptop, periféricos'),
  ('OTRO',             'Otro',                 'Cualquier otra solicitud de soporte')
ON CONFLICT (code) DO NOTHING;

-- ------------------------------------------------------------
-- 2. TICKETS
-- Sin soft-delete: un ticket no se borra, se cancela.
-- Reutiliza incident_priorities (BAJA/MEDIA/ALTA/CRITICA).
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tickets (
  ticket_id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  requester_id          bigint NOT NULL REFERENCES app_users(user_id),
  assigned_to           bigint REFERENCES app_users(user_id),
  ticket_category_id    bigint NOT NULL REFERENCES ticket_categories(ticket_category_id),
  incident_priority_id  bigint NOT NULL REFERENCES incident_priorities(incident_priority_id),
  status                varchar(20) NOT NULL DEFAULT 'ABIERTO'
                          CHECK (status IN ('ABIERTO','EN_ATENCION','ESPERANDO_USUARIO',
                                            'RESUELTO','CERRADO','CANCELADO')),
  asset_id              bigint REFERENCES assets(asset_id),
  location_id           bigint REFERENCES locations(location_id),
  incident_id           bigint REFERENCES incidents(incident_id),
  title                 varchar(200) NOT NULL,
  description           text,
  resolution            text,
  resolved_at           timestamptz,
  closed_at             timestamptz,
  created_at            timestamptz NOT NULL DEFAULT now(),
  created_by            bigint REFERENCES app_users(user_id),
  updated_at            timestamptz NOT NULL DEFAULT now(),
  updated_by            bigint REFERENCES app_users(user_id),
  version               integer NOT NULL DEFAULT 1
);
COMMENT ON TABLE tickets IS
'Mesa de ayuda: conversación con el usuario. No cambia el estado del activo; las fallas técnicas se registran en incidents y se vinculan por incident_id.';

CREATE INDEX IF NOT EXISTS ix_tickets_requester ON tickets(requester_id);
CREATE INDEX IF NOT EXISTS ix_tickets_status ON tickets(status);
-- Cierre automático: busca RESUELTO por fecha de resolución.
CREATE INDEX IF NOT EXISTS ix_tickets_resolved ON tickets(resolved_at) WHERE status = 'RESUELTO';

-- ------------------------------------------------------------
-- 3. COMENTARIOS (inmutables: sin version ni soft-delete)
-- is_internal = nota del técnico, nunca visible para el solicitante.
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ticket_comments (
  ticket_comment_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  ticket_id          bigint NOT NULL REFERENCES tickets(ticket_id),
  author_id          bigint NOT NULL REFERENCES app_users(user_id),
  body               text NOT NULL,
  is_internal        boolean NOT NULL DEFAULT false,
  created_at         timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ticket_comments_ticket ON ticket_comments(ticket_id);

-- ------------------------------------------------------------
-- 4. VERSIONADO (touch_version, igual que el resto de tablas)
-- ------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_ticket_categories_version ON ticket_categories;
CREATE TRIGGER trg_ticket_categories_version
BEFORE UPDATE ON ticket_categories
FOR EACH ROW EXECUTE FUNCTION touch_version();

DROP TRIGGER IF EXISTS trg_tickets_version ON tickets;
CREATE TRIGGER trg_tickets_version
BEFORE UPDATE ON tickets
FOR EACH ROW EXECUTE FUNCTION touch_version();

-- ------------------------------------------------------------
-- 5. ADJUNTOS: documents acepta owner_type = 'TICKET'
-- ------------------------------------------------------------
DO $$
DECLARE
  c text;
BEGIN
  -- El CHECK original es anónimo (nombre generado): se busca por definición.
  FOR c IN
    SELECT con.conname
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace ns ON ns.oid = rel.relnamespace
    WHERE ns.nspname = 'inventario' AND rel.relname = 'documents'
      AND con.contype = 'c'
      AND pg_get_constraintdef(con.oid) LIKE '%owner_type%'
  LOOP
    EXECUTE format('ALTER TABLE documents DROP CONSTRAINT %I', c);
  END LOOP;

  ALTER TABLE documents ADD CONSTRAINT ck_documents_owner_type
    CHECK (owner_type IN ('INCIDENT','MAINTENANCE','ASSET_DISPOSAL','ACQUISITION','TICKET'));
END $$;

CREATE OR REPLACE FUNCTION validate_document_owner()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  owner_exists boolean;
BEGIN
  CASE NEW.owner_type
    WHEN 'INCIDENT' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.incidents WHERE incident_id = NEW.owner_id) INTO owner_exists;
    WHEN 'MAINTENANCE' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.maintenance_records WHERE maintenance_record_id = NEW.owner_id) INTO owner_exists;
    WHEN 'ASSET_DISPOSAL' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.asset_disposals WHERE asset_disposal_id = NEW.owner_id) INTO owner_exists;
    WHEN 'ACQUISITION' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.acquisitions WHERE acquisition_id = NEW.owner_id) INTO owner_exists;
    WHEN 'TICKET' THEN
      SELECT EXISTS(SELECT 1 FROM inventario.tickets WHERE ticket_id = NEW.owner_id) INTO owner_exists;
    ELSE
      owner_exists := false;
  END CASE;

  IF NOT owner_exists THEN
    RAISE EXCEPTION 'documents.owner_id % no existe en la tabla correspondiente a owner_type=%',
      NEW.owner_id, NEW.owner_type;
  END IF;

  RETURN NEW;
END;
$$;

-- ------------------------------------------------------------
-- 6. PERMISOS
-- tickets.create: todos los roles (cada quien ve solo los suyos).
-- tickets.manage: bandeja completa, asignar, resolver, notas internas.
-- ------------------------------------------------------------
INSERT INTO permissions (code, name, description) VALUES
  ('tickets.create', 'Levantar tickets', 'Crear tickets de soporte y dar seguimiento a los propios'),
  ('tickets.manage', 'Atender tickets',  'Ver todos los tickets, asignarlos, responderlos y resolverlos')
ON CONFLICT (code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('ADMIN', 'TECNICO', 'RESPONSABLE_AREA', 'USUARIO_GENERAL')
  AND p.code = 'tickets.create'
ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r
CROSS JOIN permissions p
WHERE r.code IN ('ADMIN', 'TECNICO')
  AND p.code = 'tickets.manage'
ON CONFLICT DO NOTHING;
