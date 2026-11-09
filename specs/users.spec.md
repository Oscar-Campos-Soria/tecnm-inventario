# Spec — Usuarios (Users)

## Propósito

Administración de usuarios del sistema (alta, edición, habilitación/inhabilitación) con asignación de rol.

## Modelo

`app_users`: user_id, username (único), email (único), password_hash, first_name, last_name, role_id, status (`activo`/`inactivo`/`suspendido`), version, auditoría + soft-delete.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/users?search=&roles=ADMIN,TECNICO` | `users.view` | Selector: solo activos, máx. 50 |
| `GET` | `/api/users/manage?search=&role=&status=` | `users.manage` | Lista paginada de administración (todos los estados) |
| `POST` | `/api/auth/register` | rol ADMIN | Alta con contraseña inicial |
| `PUT` | `/api/users/{id}?expectedVersion=` | `users.manage` | Correo, nombre, apellido, rol |
| `PATCH` | `/api/users/{id}/status?expectedVersion=` | `users.manage` | activo / inactivo / suspendido |
| `PUT` | `/api/users/{id}/password?expectedVersion=` | `users.manage` | Contraseña nueva fijada por el admin |

## Reglas de negocio

- Creación y restablecimiento de contraseña con hash BCrypt.
- Deshabilitar (`status`) revoca accesos sin borrar el historial de auditoría del usuario:
  se revocan sus refresh tokens vigentes (el access token vive hasta su expiración corta).
- Cambiar el rol o la contraseña también revoca sus sesiones.
- Anti-bloqueo (400): nadie se desactiva ni se quita el rol ADMIN a sí mismo; siempre queda
  al menos un ADMIN activo.
- Regla 15: sin cambios reales no se audita; `expectedVersion` vieja -> 409.
- Regla 16: los snapshots de `audit_log` nunca llevan `password_hash` (solo `PasswordChanged: true`).
- Permiso `users.manage` (solo ADMIN): `scripts/add-admin-permissions.sql`.
- El usuario es quien firma `created_by` / `updated_by` / `performed_by` en todo el sistema.

## Criterios de aceptación

- [x] Alta, edición, rol, estado y contraseña desde `/users` (solo ADMIN)
- [x] Protecciones anti-bloqueo y cierre de sesiones (pruebas: `UserAdministrationTests`)
- [ ] Perfil y cambio de contraseña propio (backlog)

## Implementación

- Feature: `backend/Features/Users/`
- Módulo frontend: `frontend/src/modules/users/`
