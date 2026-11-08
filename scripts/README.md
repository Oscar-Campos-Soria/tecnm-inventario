# Scripts de inicialización de base de datos

Ejecutar **en este orden** contra la base de datos (`database/docker-compose.yml`,
puerto 5433):

```bash
# 1. Esquema completo (raíz del repo)
psql "$DATABASE_URL" -f ../database.sql

# 2. Seeds y permisos
psql "$DATABASE_URL" -f seed-database.sql          # catálogos (tipos, estados, fabricantes, modelos, ubicaciones…)
psql "$DATABASE_URL" -f seed-permissions.sql       # matriz de permisos RBAC
psql "$DATABASE_URL" -f seed-documents.sql         # tipos/registros de documentos
psql "$DATABASE_URL" -f add-procurement-permission.sql
psql "$DATABASE_URL" -f add-incidents-permissions.sql
psql "$DATABASE_URL" -f seed-users-view-permission.sql   # permiso users.view (selector de asignaciones)
psql "$DATABASE_URL" -f add-auth-sessions.sql      # refresh_tokens + user_external_logins (cookies HttpOnly, login federado)
psql "$DATABASE_URL" -f add-tickets.sql           # mesa de ayuda: tickets, comentarios, categorías, permisos tickets.*, adjuntos TICKET
psql "$DATABASE_URL" -f add-maintenance-fields.sql       # maintenance_records: incident_id y cost
psql "$DATABASE_URL" -f add-locations-view-permission.sql   # permiso locations.view (solo lectura de ubicaciones, todos los roles)
psql "$DATABASE_URL" -f seed-locations-demo.sql  # OPCIONAL, solo desarrollo: árbol de ejemplo (Edificio B)
psql "$DATABASE_URL" -v hash="$HASH" -f seed-test-users.sql   # OPCIONAL, solo desarrollo: usuarios de prueba RBAC (el hash se pasa por variable, no vive en el repo)
```

| Script | Contenido |
| --- | --- |
| `seed-database.sql` | Catálogos base (activos, ubicaciones, incidentes, mantenimiento, software, procurement) |
| `seed-permissions.sql` | Permisos `<modulo>.<accion>` para RBAC |
| `seed-documents.sql` | Datos de documentos / evidencias |
| `add-procurement-permission.sql` | Permiso de procurement |
| `add-incidents-permissions.sql` | Permisos de incidents |
| `seed-users-view-permission.sql` | Permiso `users.view` (selector de usuarios para asignaciones) |
| `add-auth-sessions.sql` | Tablas `refresh_tokens` (solo hash SHA-256, rotación por familia) y `user_external_logins` (vínculo Entra/Google por `subject`) |
| `add-maintenance-fields.sql` | Columnas `incident_id` (correctivo ligado a incidente) y `cost` en `maintenance_records`, e índices por activo y próxima fecha |
| `add-locations-view-permission.sql` | Permiso `locations.view` (ver ubicaciones sin modificarlas) para ADMIN, TECNICO, RESPONSABLE_AREA y USUARIO_GENERAL |
| `add-tickets.sql` | Tablas `ticket_categories` (catálogo + seed), `tickets` y `ticket_comments`; permisos `tickets.create` (todos los roles) y `tickets.manage` (ADMIN, TECNICO); `documents.owner_type` acepta `TICKET` |
| `seed-locations-demo.sql` | **Opcional, solo desarrollo:** árbol de ejemplo (Edificio B → Plantas → Laboratorios/Oficinas → Rack); idempotente por `code`, no toca ED-A ni PRUEBA-LAB |
| `seed-test-users.sql` | **Opcional, solo desarrollo:** usuarios `test_responsable` (RESPONSABLE_AREA) y `test_usuario` (USUARIO_GENERAL); idempotente por username; exige `-v hash=...` |

El esquema autoritativo del sistema es `database.sql` en la raíz del repo.
