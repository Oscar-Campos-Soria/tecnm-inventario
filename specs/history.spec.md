# Spec — Historial (History)

## Propósito

Consultar la bitácora `audit_log` (inmutable): quién cambió qué y cuándo, y la línea de tiempo de cada activo.

## Endpoints (permiso `history.view`, solo ADMIN)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/history?table=&recordId=&userId=&from=&to=&pageNumber=&pageSize=` | Bitácora paginada, más reciente primero |
| `GET` | `/api/history/tables` | Tablas presentes en la bitácora (filtro) |
| `GET` | `/api/history/assets/{assetId}` | Línea de tiempo del activo (máx. 200 eventos); 404 si no existe |

## Reglas

- Solo lectura. Los snapshots son proyecciones explícitas sin secretos (AGENTS regla 16).
- Línea de tiempo: filas `assets` del activo + filas de cualquier tabla cuyo `new_data`/`old_data`
  trae `"AssetId"` igual al activo (movimientos, asignaciones, incidentes, mantenimientos, baja, conteos).
  Depende de que los snapshots incluyan `AssetId`; uno que no lo traiga no aparece.
- Permiso: `scripts/add-admin-permissions.sql`.

## Implementación

- Feature: `backend/Features/History/` (GetAuditLog, GetAuditTables, GetAssetTimeline)
- Frontend: `frontend/src/modules/history/` (`/history`) y `assets/components/AssetTimelineSection.vue`
- Antes/después: `frontend/src/shared/components/AuditChanges.vue` (solo campos que cambiaron)
