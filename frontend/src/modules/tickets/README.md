# Módulo tickets (mesa de ayuda)

Cualquier usuario levanta solicitudes de soporte y les da seguimiento; los
técnicos las atienden desde una bandeja. El ticket es la conversación; el
incidente es el registro técnico del activo (se liga, no se mezcla).

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/tickets` | `TicketsPage.vue` — "Mis tickets" (usuario) / "Bandeja de tickets" (`tickets.manage`) | "Tickets", orden 15, permiso `tickets.create` |
| `/tickets?nuevo=1&assetId=N` | Abre "Nuevo ticket" con el equipo elegido (lo usa "Reportar problema" de la ficha del activo) | — |
| `/tickets/:ticketId` | `TicketDetailPage.vue` — conversación, archivos, datos y panel de atención | — |

## Permisos

- `tickets.create` (todos los roles): levantar, ver y comentar **sus** tickets, cancelar, confirmar o reabrir.
- `tickets.manage` (ADMIN, TECNICO): bandeja completa, asignar, prioridad, esperar respuesta, resolver, notas internas, crear/ligar incidente (además requiere `incidents.create`).
- El servidor decide la visibilidad: un ticket ajeno responde 404 ("Ticket no encontrado"). `canManage` del detalle viene del servidor.

## Endpoints

`GET/POST /api/tickets`, `GET /api/tickets/{id}`, `POST /{id}/comments`,
`PUT /{id}/assignment`, `PATCH /{id}/status`, `PATCH /{id}/priority`,
`POST /{id}/resolve`, `PUT /{id}/incident` (con `expectedVersion`; 409 recarga);
`POST /api/incidents` (crear incidente desde el ticket); adjuntos con
`shared/api/documents.ts` (`ownerType=TICKET`); catálogos `ticketCategories`,
`incidentPriorities`, `incidentTypes` de `GET /api/catalogs`.

## Pantallas

- **Lista:** pestañas por rol (usuario: Abiertos, Por confirmar, Todos; técnico: Pendientes, Sin asignar, Asignados a mí, Resueltos, Todos), búsqueda por título y filtro por tipo. Franja izquierda por prioridad: roja + "Urgente" (Crítica), naranja (Alta); Media/Baja sin franja. "El técnico necesita tu respuesta" resaltado.
- **Nuevo ticket:** tipo con íconos, título, detalles, urgencia (Normal = MEDIA, Alta, Urgente = CRITICA; Baja solo la pone el técnico), equipo opcional (`assets.view`), foto/PDF opcional (≤ 5 MB) que se sube tras crear.
- **Detalle:** hilo (lo propio a la derecha, notas internas en ámbar con candado, mensajes del sistema al centro), banner "¿Quedó bien?" con fecha del cierre automático, aviso de respuesta pendiente, archivos, datos y cancelar; panel "Atención" para el técnico.

## Pendiente

- Contador de "sin asignar" en el menú (hoy solo en la pestaña).
- Selector de equipo limitado a 100 (sin búsqueda en `GET /api/assets`).
- Ubicación del ticket (el backend la acepta; `GET /api/locations` exige `locations.manage`).

Especificación: `specs/tickets.spec.md`
