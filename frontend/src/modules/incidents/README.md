# Módulo incidents

Consulta de fallas reportadas sobre activos: lista con filtros y detalle con evidencias.

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/incidents` | `pages/IncidentsPage.vue` — filtros (título, estado, prioridad, tipo, técnico, solo abiertos) y paginación | "Incidentes", orden 40, `incidents.view` |
| `/incidents/:incidentId` | `pages/IncidentDetailPage.vue` — activo afectado y su estado, detalles, seguimiento, evidencias (`shared/components/DocumentList.vue`, `ownerType=INCIDENT`) | — |

## Notas

- Los incidentes se crean desde un ticket (módulo tickets, "Crear incidente"); este módulo aún no tiene acciones de técnico (asignar, cambiar estado, resolver).
- `USUARIO_GENERAL` no tiene `incidents.view`; da seguimiento a sus problemas desde Tickets.

Especificación: `specs/incidents.spec.md`
