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
```

| Script | Contenido |
| --- | --- |
| `seed-database.sql` | Catálogos base (activos, ubicaciones, incidentes, mantenimiento, software, procurement) |
| `seed-permissions.sql` | Permisos `<modulo>.<accion>` para RBAC |
| `seed-documents.sql` | Datos de documentos / evidencias |
| `add-procurement-permission.sql` | Permiso de procurement |
| `add-incidents-permissions.sql` | Permisos de incidents |
| `seed-users-view-permission.sql` | Permiso `users.view` (selector de usuarios para asignaciones) |

El esquema autoritativo del sistema es `database.sql` en la raíz del repo.
