# Spec — Tickets (mesa de ayuda)

## Propósito

Cualquier usuario levanta solicitudes de soporte (fallas, software, cuentas,
red, préstamo de equipo) y les da seguimiento; los técnicos las atienden desde
una bandeja. **El ticket es la conversación con el usuario; el incidente es el
registro técnico del activo.** Un ticket nunca cambia el estado de un activo:
si resulta ser una falla de hardware, el técnico levanta un incidente con el
flujo existente y lo vincula (`incident_id`).

## Endpoints (`/api/tickets`)

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/tickets` | `tickets.create` | Paginado. Sin `tickets.manage` solo devuelve los propios |
| `GET` | `/api/tickets/{ticketId}` | `tickets.create` | Detalle + comentarios. Ajeno sin `manage` → **404** |
| `POST` | `/api/tickets` | `tickets.create` | Levanta ticket (estado `ABIERTO`) |
| `POST` | `/api/tickets/{ticketId}/comments` | `tickets.create` | Comenta (dueño o `manage`) |
| `PUT` | `/api/tickets/{ticketId}/assignment?expectedVersion=N` | `tickets.manage` | Asigna técnico |
| `PATCH` | `/api/tickets/{ticketId}/status?expectedVersion=N` | `tickets.create` | Cambio de estado (reglas por rol abajo) |
| `PATCH` | `/api/tickets/{ticketId}/priority?expectedVersion=N` | `tickets.manage` | Corrige la prioridad |
| `POST` | `/api/tickets/{ticketId}/resolve?expectedVersion=N` | `tickets.manage` | Resuelve (resolución obligatoria) |
| `PUT` | `/api/tickets/{ticketId}/incident?expectedVersion=N` | `tickets.manage` | Vincula un incidente existente |

Filtros del listado: `status`, `categoryId`, `priority` (código),
`assignedTo`, `unassigned`, `onlyOpen`, `search` (título). Orden: prioridad
(CRITICA → BAJA) y después antigüedad (más viejo primero) cuando `onlyOpen`;
si no, más recientes primero.

## Estados

```
ABIERTO ──asignar──► EN_ATENCION ⇄ ESPERANDO_USUARIO
   │                     └─resolve─► RESUELTO ──► CERRADO
   │                                    └─reabrir─► EN_ATENCION
   └─cancelar─► CANCELADO
```

`CERRADO` y `CANCELADO` son terminales. Abiertos = `ABIERTO`, `EN_ATENCION`,
`ESPERANDO_USUARIO`.

| Transición | Quién |
| --- | --- |
| `ABIERTO → EN_ATENCION` | Automático al asignar |
| `EN_ATENCION ⇄ ESPERANDO_USUARIO` | `manage` |
| `ESPERANDO_USUARIO → EN_ATENCION` | Automático cuando el solicitante comenta |
| `EN_ATENCION/ESPERANDO_USUARIO → RESUELTO` | `manage`, solo vía `/resolve` |
| `RESUELTO → CERRADO` | Solicitante (confirma) o cierre automático |
| `RESUELTO → EN_ATENCION` | Solicitante (reabre: "no se resolvió") |
| `ABIERTO/EN_ATENCION/ESPERANDO_USUARIO → CANCELADO` | Solicitante o `manage` |

## Reglas de negocio

- Equipo del ticket: quien no tiene `assets.view` (usuario general) solo puede ligar un equipo que tenga asignado
  (400 "Solo puedes reportar equipos que tienes asignados."). En la pantalla elige entre "Mis equipos".
- Título obligatorio (≤ 200); descripción opcional (≤ 4000); comentario
  obligatorio (≤ 4000); resolución obligatoria (≤ 4000).
- Prioridad: catálogo `incident_priorities`. Si el solicitante no la indica,
  `MEDIA`. `manage` puede corregirla.
- `assetId` / `locationId` opcionales; si vienen, deben existir. El activo no
  puede estar `DADO_DE_BAJA`. **No se toca el estado del activo.**
- Asignar: no sobre `RESUELTO`/`CERRADO`/`CANCELADO`; el destino debe estar
  **activo** y su rol tener `tickets.manage`.
- Comentario interno (`isInternal`): solo `manage`. Nunca se devuelve a quien
  no tiene `manage`.
- No se comenta en `CERRADO`/`CANCELADO`.
- Vincular incidente: debe existir; si el ticket tiene activo, el incidente
  debe ser del mismo activo.
- Mutaciones con `expectedVersion` (409 si no coincide). Toda escritura +
  `audit_log` en una transacción explícita.
- Un ticket no se elimina: se cancela.

## Cierre automático

- `TicketAutoCloseService` (BackgroundService, cada hora) cierra los
  `RESUELTO` con `resolved_at` más viejo que `Tickets:AutoCloseDays`
  (appsettings, por defecto **3**).
- Deja un comentario "Cerrado automáticamente…" y una fila `audit_log`
  firmada por la cuenta `system`.
- La respuesta del detalle incluye `autoCloseAt` para el aviso en pantalla.

## Prioridad visual (frontend)

Sin tiempos de respuesta por horas. `CRITICA` en rojo fuerte con etiqueta
"Urgente"; `ALTA` en rojo/naranja suave; `MEDIA`/`BAJA` neutros.

## Adjuntos (`Features/Documents`, `ownerType = TICKET`)

`IDocumentAccessAuthorizer` revisa **permiso y dueño**:

| Acción | Quién |
| --- | --- |
| Ver / descargar | Solicitante del ticket o `tickets.manage` |
| Subir | Solicitante o `manage`; no en `CERRADO`/`CANCELADO` |
| Eliminar | Quien lo subió o `manage` |

Ajeno → 404 (no revela existencia). Mismos límites: JPG/PNG/PDF ≤ 5 MB.

También se cierra el mismo hueco en `INCIDENT`: sin `incidents.resolve` solo
se accede a la evidencia de incidentes reportados por uno mismo.

## Permisos por rol (`scripts/add-tickets.sql`)

| Rol | create | manage |
| --- | --- | --- |
| ADMIN | ✅ | ✅ |
| TECNICO | ✅ | ✅ |
| RESPONSABLE_AREA | ✅ | ❌ |
| USUARIO_GENERAL | ✅ | ❌ |

## Datos

`tickets`, `ticket_comments`, `ticket_categories` (servido en
`GET /api/catalogs` como `ticketCategories`).

## Fuera de alcance (backlog)

- Notificaciones por correo.
- Crear el incidente desde el ticket en un solo paso.
- Sincronizar el ticket al resolverse el incidente vinculado.
