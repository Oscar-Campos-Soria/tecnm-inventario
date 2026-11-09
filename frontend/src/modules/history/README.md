# Módulo history

Ruta `/history` (permiso `history.view`, solo ADMIN). Solo lectura: `audit_log` es inmutable.

- Filtros: tabla, id de registro, id de usuario, rango de fechas. Más reciente primero, 25 por página.
- Cada fila muestra quién, cuándo, la acción (INSERT/UPDATE/DELETE) y solo los campos que cambiaron
  (`shared/components/AuditChanges.vue`).
- Los snapshots son proyecciones explícitas y nunca traen secretos (AGENTS regla 16).

La ficha del activo muestra su **línea de tiempo** (`GET /api/history/assets/{id}`, mismo permiso):
filas de `assets` del activo más las de otras tablas cuyo snapshot trae su `AssetId`
(movimientos, asignaciones, incidentes, mantenimientos, baja, conteos). Máximo 200 eventos.

API: `GET /api/history`, `GET /api/history/tables`, `GET /api/history/assets/{assetId}`.
