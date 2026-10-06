# Spec — Autenticación y Autorización (Auth)

## Propósito

Autenticar usuarios del sistema (JWT) y autorizar cada operación mediante RBAC con permisos granulares (`modulo.accion`).

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `POST` | `/api/auth/login` | público | Valida credenciales y devuelve JWT |
| `POST` | `/api/auth/register` | rol `ADMIN` | Crea un usuario con hash BCrypt |

## Reglas de negocio

- Contraseñas hasheadas con **BCrypt** (`PasswordHash` en `app_user`); utilidad `backend/tools/PasswordHasher` para sembrar usuarios.
- JWT simétrico: `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience` en **user-secrets** (nunca en appsettings). `ClockSkew` de 1 minuto; claims no se mapean automáticamente (`MapInboundClaims = false`).
- `app_user.status` controla habilitación de cuenta.
- Todo el API exige autenticación por defecto (`FallbackPolicy`); solo `login` es `[AllowAnonymous]`.
- Autorización por **políticas dinámicas** resueltas desde `permission.code` (ej. `assets.view`), no por roles hardcodeados — excepción: `register` exige rol `ADMIN`.

## Datos

- `app_user` (user_id, username, email, password_hash, first_name, last_name, role_id, status, auditoría + soft-delete)
- `role` (role_id, code, name) ↔ `permission` (permission_id, code) vía tabla n:m
- `audit_log` registra acciones relevantes (inmutable)

## Criterios de aceptación

- [x] Login con credenciales válidas devuelve token JWT
- [x] Login inválido rechaza sin filtrar información
- [x] Register solo con rol ADMIN; contraseña hasheada
- [x] Endpoint protegido sin token → 401; con token sin permiso → 403
- [x] Permisos editables en caliente (asignación a roles sin redeploy)

## Implementación

- Feature: `backend/Features/Auth/` (Login, Register)
- Cross-cutting: `Common/Security/JwtTokenGenerator`, `Common/Authorization/PermissionPolicyProvider`, `Common/Authorization/RolePermissionService`
