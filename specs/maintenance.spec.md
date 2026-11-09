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
| `GET` | `/api/maintenance/scheduled?days=90` | Programados sin realizar dentro de `days` (1-730) más los atrasados |
| `POST` | `/api/maintenance/scheduled` | Programa a futuro: `assetId`, `maintenanceTypeId`, `scheduledAt`, `description`, `performedBy` (asignado, opcional) |
| `POST` | `/api/maintenance/{id}/complete?expectedVersion=N` | Marca un programado como realizado (`performedAt`, `description`, `partsUsed`, `cost`, `nextMaintenanceAt`) |
| `DELETE` | `/api/maintenance/{id}?expectedVersion=N` | Baja lógica de un registro capturado por error o de un programado que se cancela (`ISoftDeleteAuditor`) |

Body del POST: `assetId`, `maintenanceTypeId`, `performedAt` (no futura),
`nextMaintenanceAt` (opcional, posterior), `description` (obligatoria, ≤ 4000),
`partsUsed` (≤ 2000), `cost` (≥ 0, MXN), `performedBy` (opcional; por defecto
quien registra; debe estar activo y tener `maintenance.manage`), `incidentId`
(opcional; debe ser del mismo activo), `resolveIncident` (opcional, solo correctivo con
`incidentId`) e `incidentSolution` (opcional; por defecto la descripción).

## Reglas de negocio

- El activo debe existir y no estar `DADO_DE_BAJA`.
- Un correctivo ligado puede resolver su incidente en el mismo paso (`resolveIncident`): el registro, el
  incidente resuelto y el activo liberado van en una sola transacción (`IncidentResolver`, compartido con
  `ResolveIncident`); si resolver falla, no queda el correctivo. Sin la casilla, se resuelve aparte.
- Programado: `scheduled_at` sin `performed_at`. No sale en el historial ni en el calendario de preventivos
  hasta marcarse realizado; la fecha no puede estar en el pasado (≤ 2 años).
- Calendario: por activo cuenta solo su preventivo más reciente; uno posterior
  "atiende" al anterior. Activos dados de baja no aparecen.
- Escritura + `audit_log` en la transacción de `TransactionBehavior` (`ITransactionalCommand`).

## Datos

`maintenance_records` (+ `incident_id`, `cost` por `scripts/add-maintenance-fields.sql`),
`maintenance_types` (PREVENTIVO, CORRECTIVO). `scheduled_at` = mantenimiento programado.

## Frontend

- Ficha del activo: sección "Mantenimiento" (historial paginado, registrar, eliminar).
- `/maintenance`: preventivos por vencer (30/60/90 días; vencidos en rojo) y bitácora general.

## Criterios de aceptación

- [x] Registro, historial y baja lógica de mantenimientos
- [x] Calendario de preventivos por vencer
- [x] Programar mantenimientos a futuro (`scheduled_at`) y marcarlos realizados
- [x] Correctivo que resuelve su incidente en un solo paso
- [ ] Recordatorios por correo (backlog)
