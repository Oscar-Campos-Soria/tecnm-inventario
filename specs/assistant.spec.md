# Spec — Asistente IA ("Pregúntale a la IA")

## Propósito

Responder preguntas sobre el sistema con datos actuales: el modelo local (Qwen3.5-2B en llama.cpp) decide qué
consultar, la API ejecuta **queries existentes** con los permisos del usuario y el modelo redacta la respuesta con
esos resultados. Solo lectura: no escribe en la BD y nunca genera SQL.

## Endpoints (`backend/Features/Assistant/`, permiso `assistant.use`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/assistant/status` | `{enabled, tools}`: el modelo responde y qué herramientas tiene este rol |
| POST | `/api/assistant/ask` | `{conversationId?, question}` (≤ 500) → `{conversationId, answer, steps}` |
| DELETE | `/api/assistant/conversations/{id}` | Borra la memoria de esa conversación (solo la propia) |

`steps`: cada consulta hecha (`tool`, `summary` en palabras con los filtros ya resueltos, `data` compacta, `error`).
La pantalla debe mostrar `data` además del texto: si el modelo redacta mal un número, el dato real está al lado.

## Cómo piensa (orquestación, `AssistantOrchestrator`)

1. Las herramientas que el rol no permite se le muestran **sin parámetros** y marcadas sin permiso: si las pide recibe
   "tu rol no tiene permiso" (no se ejecuta nada) y lo dice, en vez de inventar una respuesta con otra herramienta.
2. Tool calling nativo de llama.cpp (`--jinja`): el modelo pide una o varias herramientas en la misma vuelta.
3. La API valida argumentos (listas cerradas, ubicación por catálogo, códigos exactos), **vuelve a revisar el
   permiso** y envía la query de Mediator (sus validadores y filtros por usuario aplican igual que en pantalla).
   Argumento inválido → se le explica al modelo, que corrige.
4. Hasta 4 vueltas o 120 s; consultas repetidas se reusan; si se acaban las vueltas, responde sin más herramientas.
5. Resultados compactos (≤ 15 filas + total, ≤ 3000 caracteres) para caber en los 4096 tokens del turno.
6. Ayudas en código donde el modelo de 2B falla (medido con el set de evaluación): preguntas de "cómo / ¿se puede? /
   ¿qué pasa?" consultan el manual antes de la primera vuelta; un tipo de equipo que nadie mencionó se descarta; un lugar
   del catálogo mencionado en la pregunta se agrega si el modelo lo olvidó; los equipos con falla traen su falla abierta;
   una llamada escrita como texto (`<tool_call>`) se convierte en llamada real y nunca se muestra al usuario.

## Herramientas (`Features/Assistant/Shared/AssistantTools.cs`)

| Herramienta | Query existente | Permiso |
| --- | --- | --- |
| `buscar_activos` (texto, tipo, estado, marca, ubicación con sub-ubicaciones; equipos con falla traen su falla abierta) | `GetAssetsQuery` | `assets.view` |
| `detalle_activo` (ficha + asignación vigente) | `GetAssetById` + `GetAssetAssignments` | `assets.view` |
| `historial_activo` | `GetAssetTimelineQuery` | `history.view` |
| `buscar_incidentes` (activo, tipo de activo, abiertos, estado, prioridad, desde, tema) | `GetIncidentsQuery` | `incidents.view` |
| `buscar_tickets` (abiertos, estado, sin asignar, desde, tema) — usuario general: solo los suyos | `GetTicketsQuery` | `tickets.create` |
| `mantenimientos_proximos` | `GetUpcomingMaintenanceQuery` | `maintenance.manage` |
| `resumen_inventario` | `GetDashboardSummaryQuery` | `dashboard.view` |
| `novedades` (incidentes y tickets desde una fecha; cada parte con su permiso) | `GetIncidentsQuery` + `GetTicketsQuery` | — |
| `mis_equipos` (equipos asignados al que pregunta) | `GetMyAssetsQuery` | — |
| `buscar_manual` (RAG) | `ManualIndex` | — |

Cambios a queries existentes (parámetros opcionales al final, sin afectar a la pantalla): `GetAssetsQuery.ManufacturerId`
y fabricante/modelo en `GetAssetsResponse`; `GetIncidentsQuery.ReportedFrom` y `.AssetTypeId`; `GetTicketsQuery.CreatedFrom`.

## Memoria (`ConversationStore`)

Por usuario y conversación, en memoria, 24 h sin uso. Guarda turnos (pregunta, trabajo con herramientas, respuesta);
los 2 más recientes van completos al modelo y los anteriores solo como pregunta y respuesta (máx. 8 turnos).
Otro usuario que mande el id recibe una conversación nueva. Se pierde si la API se reinicia.

## Infraestructura

`--ctx-size 8192 --parallel 2` en el servicio `ia`: un turno para el asistente y otro para la carga masiva (el chat
responde aunque haya una conversión en curso). `mem_limit: 3584m` (pico medido ~3.15 GB).

## Pendiente

- [x] RAG: manual de usuario en `backend/Features/Assistant/Manual/*.md` (embebido en la API) + `buscar_manual`
  (BM25 en memoria por secciones "## ", sinónimos solo en la pregunta)
- [ ] Pantalla con botón flotante — **solo después de pasar el set de evaluación**

## Evaluación

Set de preguntas con herramientas esperadas, datos que debe mencionar la respuesta y verificación de que todo código
de activo citado venga de los resultados (nada inventado). Corre contra la API real con usuarios de prueba.
