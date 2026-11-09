# Módulo roles

Ruta `/roles` (permiso `roles.manage`, solo ADMIN).

- Lista de roles a la izquierda; al elegir uno se marcan sus permisos agrupados por módulo
  (prefijo del código: `tickets.manage` -> `tickets`).
- "Guardar permisos" reemplaza el conjunto completo (`PUT /api/roles/{id}/permissions`).
  Sin cambios no se audita. La caché de permisos del rol se invalida al confirmar.
- El rol ADMIN siempre conserva `roles.manage` y `users.manage` (el servidor responde 400;
  la pantalla deja esas casillas bloqueadas).
- Nuevo rol: código en MAYÚSCULAS, nombre y descripción; nace sin permisos.
- Quien tenga el rol ve los permisos nuevos al renovar su sesión.

API: `GET /api/roles`, `GET /api/permissions`, `POST /api/roles`, `PUT /api/roles/{id}/permissions`.
