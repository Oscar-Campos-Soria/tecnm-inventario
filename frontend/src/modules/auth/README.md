# Módulo auth

Inicio de sesión local y federado (Microsoft Entra / Google) con sesión en cookies HttpOnly.

## Rutas (`routes.ts`)

| Ruta | Página | Meta |
| --- | --- | --- |
| `/login` | `pages/LoginPage.vue` | `public`, `layout: 'auth'` |

`?redirect=/ruta` regresa a donde iba el usuario; `?error=<código>` muestra el error del login federado (`sin_cuenta`, `dominio_no_permitido`, `cuenta_inactiva`, `federado_fallido`).

## Implementado

- `pages/LoginPage.vue` — botones de proveedores (`GET /api/auth/providers`), formulario usuario/contraseña, error inline (`role="alert"`).
- `components/ProviderLogo.vue` — logos de Microsoft y Google.
- Sesión: `shared/hooks/useAuth.ts` (`login`, `logout`, `hasPermission`, perfil de `GET /api/auth/me`); guard de rutas en `app/router`.

## Pendiente

- Recuperación de acceso / cambio de contraseña.
- UI para desvincular cuentas externas.

Especificación: `specs/auth.spec.md`
