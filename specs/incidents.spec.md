# Spec — Incidentes (Incidents)

## Propósito

Levantamiento, asignación y seguimiento de fallas reportadas sobre activos, con resolución y eliminación lógica de incidentes cerrados.

## Endpoints

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/incidents` | `incidents.view` | Listado paginado con filtros |
| `GET` | `/api/incidents/{incidentId}` | `incidents.view` | Detalle |
| `POST` | `/api/incidents` | `incidents.create` | Levanta incidente |
| `PUT` | `/api/incidents/{incidentId}/assign` | `incidents.update` | Asigna a técnico (`assigned_to`) |
| `PUT` | `/api/incidents/{incidentId}/status` | `incidents.update` | Cambia estado |
| `POST` | `/api/incidents/{incidentId}/resolve` | `incidents.update` | Resuelve (diagnóstico + solución + `resolved_at`) |
| `DELETE` | `/api/incidents/{incidentId}` | `incidents.delete` | Eliminación lógica (solo resueltos, auditada) |

## Filtros de listado

`assetId`, `status`, `priority`, `incidentTypeId`, `assignedTo`, `onlyOpen`, `search` (texto libre sobre título/descripción).

## Reglas de negocio

- Todo incidente se reporta **contra un activo existente** (`asset_id` obligatorio) y lo reporta un usuario (`reported_by`).
- Solo los incidentes **resueltos** pueden eliminarse lógicamente; la eliminación queda registrada en `audit_log` (`SoftDeleteAuditor`).
- La resolución exige diagnóstico y solución, y sella `resolved_at`.
- Estado y prioridad son catálogos (`incident_status`, `incident_priority`, `incident_type`).

## Datos

`incident`, `incident_type`, `incident_priority`, `incident_status` (+ auditoría y soft-delete estándar).

## Criterios de aceptación

- [x] CRUD con validación, filtros y paginación
- [x] Asignación y resolución con trazabilidad de usuario
- [x] Eliminación lógica solo de resueltos, con auditoría
- [ ] Seguimiento en tiempo real / notificaciones (backlog)

## Implementación

- Feature: `backend/Features/Incidents/` (CreateIncident, GetIncidents, GetIncidentById, AssignIncident, UpdateIncidentStatus, ResolveIncident, DeleteIncident)
