# Módulo users

Ruta `/users` (permiso `users.manage`, solo ADMIN). Spec: `specs/users.spec.md`.

- Lista paginada (20) con todos los estados; filtros por texto (usuario, correo, nombre), rol y estado.
- **Nuevo usuario**: usa `POST /api/auth/register` (solo ADMIN) con contraseña inicial (mínimo 8).
- **Editar**: correo, nombre, apellido y rol. Cambiar el rol cierra sus sesiones (el JWT trae el rol).
- **Contraseña**: el admin fija una nueva; se cierran sus sesiones. Nunca se audita el hash.
- **Activar / Desactivar**: desactivar cierra sus sesiones. No aparece para la propia cuenta.
- Protecciones del servidor (400): nadie se desactiva ni se quita el rol ADMIN a sí mismo y
  siempre queda al menos un ADMIN activo. Versión vieja -> 409 (la pantalla recarga la lista).

API: `GET /api/users/manage`, `PUT /api/users/{id}`, `PATCH /api/users/{id}/status`,
`PUT /api/users/{id}/password` (todos con `expectedVersion`). El selector `GET /api/users`
(solo activos, máx. 50) acepta `roles=ADMIN,TECNICO`.
