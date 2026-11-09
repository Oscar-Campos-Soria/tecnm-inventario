# Módulo profile

Ruta `/profile` (cualquier usuario con sesión; sin entrada en el menú, se abre desde el nombre en el pie
del menú lateral).

- Datos de la cuenta (solo lectura; los cambia un administrador en Usuarios).
- Cambiar contraseña: pide la actual y la nueva dos veces (mínimo 8 caracteres, distinta de la actual).
  Cierra las sesiones abiertas en otros equipos; la actual sigue. Limitado como el login (10 por minuto por IP).
- Cuentas vinculadas (Microsoft, Google): lista y "Desvincular". La cuenta conserva su contraseña.

API: `GET /api/auth/profile`, `POST /api/auth/profile/password`, `DELETE /api/auth/profile/external-logins/{provider}`
(bajo `/api/auth` porque la cookie de refresh solo viaja ahí y hace falta para no cerrar la sesión actual).
