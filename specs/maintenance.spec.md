# Spec — Mantenimiento (Maintenance)

## Propósito

Bitácora de intervenciones **preventivas y correctivas** sobre activos y
calendario de preventivos por vencer. El mantenimiento **no cambia el estado
del activo** (eso lo controlan los incidentes).

## Endpoints (`backend/Features/Maintenance/`, permiso `maintenance.manage`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/maintenance?assetId&type&pageNumber&pageSize` | Historial paginado (más reciente primero) |
| `GET` | `/api/maintenance/upcoming?days=30` | Preventivos con próxima fecha dentro de `days` (1-365) o vencidos |
| `POST` | `/api/maintenance` | Registra una intervención realizada |
| `DELETE` | `/api/maintenance/{id}?expectedVersion=N` | Baja lógica de un registro capturado por error (`ISoftDeleteAuditor`) |

Body del POST: `assetId`, `maintenanceTypeId`, `performedAt` (no futura),
`nextMaintenanceAt` (opcional, posterior), `description` (obligatoria, ≤ 4000),
`partsUsed` (≤ 2000), `cost` (≥ 0, MXN), `performedBy` (opcional; por defecto
quien registra; debe estar activo y tener `maintenance.manage`), `incidentId`
(opcional; debe ser del mismo activo).

## Reglas de negocio

- El activo debe existir y no estar `DADO_DE_BAJA`.
- Registrar un correctivo **no** resuelve el incidente ligado (se resuelve aparte).
- Calendario: por activo cuenta solo su preventivo más reciente; uno posterior
  "atiende" al anterior. Activos dados de baja no aparecen.
- Escritura + `audit_log` en transacción explícita.

## Datos

`maintenance_records` (+ `incident_id`, `cost` por `scripts/add-maintenance-fields.sql`),
`maintenance_types` (PREVENTIVO, CORRECTIVO). `scheduled_at` existe pero no se usa todavía.

## Frontend

- Ficha del activo: sección "Mantenimiento" (historial paginado, registrar, eliminar).
- `/maintenance`: preventivos por vencer (30/60/90 días; vencidos en rojo) y bitácora general.

## Criterios de aceptación

- [x] Registro, historial y baja lógica de mantenimientos
- [x] Calendario de preventivos por vencer
- [ ] Programar mantenimientos a futuro (`scheduled_at`) y recordatorios (backlog)
