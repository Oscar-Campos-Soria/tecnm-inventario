# Spec — Roles y permisos (Roles)

## Propósito

Administración de roles y su matriz de permisos (RBAC dinámico, sin redeploy).

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/roles` | `roles.manage` | Listado de roles con sus permisos |
| `GET` | `/api/permissions` | `roles.manage` | Catálogo de permisos con su módulo (prefijo del código) |
| `POST` | `/api/roles` | `roles.manage` | Crea rol (nace sin permisos) |
| `PUT` | `/api/roles/{roleId}/permissions` | `roles.manage` | Reemplaza los permisos del rol |

## Reglas de negocio

- `role` (code, name) ↔ `permission` (code) en relación n:m.
- Los permisos se siembran desde `scripts/seed-permissions.sql` con códigos `<modulo>.<accion>`.
- Las políticas de autorización se resuelven en caliente desde la BD (`PermissionPolicyProvider`).
- Anti-bloqueo (400): el rol ADMIN siempre conserva `roles.manage` y `users.manage`.
- Pantalla `/roles` (frontend `modules/roles`): matriz de casillas agrupada por módulo.

## Criterios de aceptación

- [x] Listado, creación y asignación de permisos a roles
- [x] Efecto inmediato en la autorización de endpoints

## Implementación

- Feature: `backend/Features/Roles/` (GetRoles, GetPermissions, CreateRole, AssignPermissions)
- Permisos sembrados: `scripts/seed-permissions.sql`, `scripts/add-procurement-permission.sql`, `scripts/add-incidents-permissions.sql`
