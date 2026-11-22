# Spec — Asistente IA ("Pregúntale a la IA")

## Propósito

Responder preguntas sobre el sistema con datos actuales: el modelo local (Qwen3.5-2B en llama.cpp) decide qué
consultar, la API ejecuta **queries existentes** con los permisos del usuario y el modelo redacta la respuesta con
esos resultados. Solo lectura: no escribe en la BD y nunca genera SQL.

## Endpoints (`backend/Features/Assistant/`, permiso `assistant.use`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/assistant/status` | `{enabled, tools}`: el modelo responde y qué herramientas tiene este rol |
| GET | `/api/assistant/capabilities` | Registro de capacidades del rol (ver abajo) |
| POST | `/api/assistant/ask` | `{conversationId?, question}` (≤ 500) → `{conversationId, answer, steps}` |
| DELETE | `/api/assistant/conversations/{id}` | Borra la memoria de esa conversación (solo la propia) |

`steps`: cada consulta hecha (`tool`, `summary` en palabras con los filtros ya resueltos, `data` compacta, `error`).
La pantalla debe mostrar `data` además del texto: si el modelo redacta mal un número, el dato real está al lado.

## Cómo piensa (orquestación, `AssistantOrchestrator` + `AssistantRouter`)

```
mensaje → sesión (usuario, rol) → permisos del rol (BD) → registro filtrado: solo las capacidades permitidas
        → ROUTER: 1 llamada corta, JSON forzado por esquema (gramática de llama.cpp), máx. 220 tokens
            { tipo: consulta|accion|charla|aclarar|sin_permiso, herramientas: [solo permitidas, máx. 3],
              partes: [cada cosa que pide, reescrita completa], area_sin_permiso, respuesta }
        → charla / aclarar  → se responde con `respuesta` (sin otra llamada)
        → sin_permiso       → "Tu rol (X) no tiene permiso para consultar …" (texto fijo del código)
        → consulta / accion → EJECUTOR: tool calling con SOLO las herramientas elegidas;
                              la primera vuelta exige llamar una (`tool_choice: required`);
                              las partes van como "Responde todo esto: 1) … 2) …"
        → backend: valida argumentos, revisa el permiso otra vez, envía la query de Mediator (filtros por usuario)
        → respuesta redactada solo con los datos devueltos
```

1. El router ve el resumen de una línea de cada capacidad del rol (`IAssistantTool.Summary`), los nombres de las áreas
   sin permiso y el turno anterior recortado (seguimientos). No ve datos ni decide permisos: el esquema solo admite
   herramientas del rol y el backend vuelve a revisar al ejecutar.
2. Si el router falla (modelo caído, JSON inválido), el ejecutor recibe todas las herramientas del rol: nunca peor
   que antes. Las herramientas prohibidas ya no se ofrecen; si el modelo inventa una, recibe "sin permiso" y no se
   ejecuta nada.
3. Tool calling nativo de llama.cpp (`--jinja`): el modelo pide una o varias herramientas en la misma vuelta.
4. La API valida argumentos (listas cerradas, ubicación por catálogo, códigos exactos), **vuelve a revisar el
   permiso** y envía la query de Mediator (sus validadores y filtros por usuario aplican igual que en pantalla).
   Argumento inválido → se le explica al modelo, que corrige.
5. Hasta 4 vueltas o 120 s; consultas repetidas se reusan; si se acaban las vueltas, responde sin más herramientas.
6. Resultados compactos (≤ 15 filas + total, ≤ 3000 caracteres) para caber en los 4096 tokens del turno.
7. Ayudas en código que quedan: un tipo de equipo que nadie mencionó se descarta; un lugar del catálogo mencionado en
   la pregunta se agrega si el modelo lo olvidó; los equipos con falla traen su falla abierta; una llamada escrita
   como texto (`<tool_call>`) se convierte en llamada real; los nombres de herramientas nunca llegan al usuario.
   Se quitaron las 6 regex de precarga ("cómo…", "mis tickets", "mi equipo", "abre un ticket", inventario sin
   permiso) y la insistencia para `proponer_ticket`: las reemplazan el router y `tool_choice: required`.
8. Fechas: el sistema no tiene fecha estimada de entrega ni fecha límite de tickets. El asistente dice que aún no hay
   fecha confirmada y da la antigüedad (`dias_abierto`) en lugar de "atrasado". Los mantenimientos sí tienen fecha
   programada y pueden estar vencidos.

## Registro de capacidades (`GET /api/assistant/capabilities`)

Se genera de las herramientas (`IAssistantTool`): `id`, `summary`, `description`, `kind` (Consulta / Borrador),
`permission`, `source` (queries que ejecuta) y `parameters` (nombre, tipo, requerido, descripción, valores
permitidos). Devuelve solo lo que el rol del usuario puede usar. Agregar una capacidad = una clase que implementa
`IAssistantTool` (se registra sola por reflexión en `Program.cs`) sobre una query existente.

## Herramientas (`Features/Assistant/Shared/AssistantTools.cs`)

| Herramienta | Query existente | Permiso |
| --- | --- | --- |
| `buscar_activos` (texto, tipo, estado, marca, ubicación con sub-ubicaciones; equipos con falla traen su falla abierta) | `GetAssetsQuery` | `assets.view` |
| `detalle_activo` (ficha + asignación vigente) | `GetAssetById` + `GetAssetAssignments` | `assets.view` |
| `historial_activo` | `GetAssetTimelineQuery` | `history.view` |
| `buscar_incidentes` (activo, tipo de activo, abiertos, estado, prioridad, desde, tema, asignados a mí) | `GetIncidentsQuery` | `incidents.view` |
| `buscar_tickets` (abiertos, estado, prioridad, sin asignar, asignados a mí, técnico, desde, tema; trae `dias_abierto`) — usuario general: solo los suyos | `GetTicketsQuery` | `tickets.create` |
| `carga_tecnicos` (abiertos por persona: tickets, críticos, incidentes, ticket más antiguo; sin asignar) | `GetTicketWorkloadQuery` (nueva) | `tickets.manage` |
| `estadisticas_tickets` (recibidos, resueltos, cerrados, cancelados en un periodo; promedio de resolución; pendientes ahora) — usuario general: solo los suyos | `GetTicketStatsQuery` (nueva) | `tickets.create` |
| `mantenimientos_proximos` | `GetUpcomingMaintenanceQuery` | `maintenance.manage` |
| `resumen_inventario` | `GetDashboardSummaryQuery` | `dashboard.view` |
| `novedades` (incidentes y tickets desde una fecha; cada parte con su permiso) | `GetIncidentsQuery` + `GetTicketsQuery` | — |
| `mis_equipos` (equipos asignados al que pregunta, con sus tickets y el técnico de cada uno) | `GetMyAssetsQuery` + `GetTicketsQuery` | — |
| `proponer_ticket` (borrador de ticket; **no escribe**: la tarjeta del panel lo crea con `POST /api/tickets` al confirmar; sin `assets.view` solo liga equipos de `mis_equipos`) | `GetMyAssetsQuery` (si indica equipo) | `assistant.tickets.propose` (RESPONSABLE_AREA, USUARIO_GENERAL) |
| `buscar_manual` (RAG) | `ManualIndex` | — |

Cambios a queries existentes (parámetros opcionales al final, sin afectar a la pantalla): `GetAssetsQuery.ManufacturerId`
y fabricante/modelo en `GetAssetsResponse`; `GetIncidentsQuery.ReportedFrom` y `.AssetTypeId`; `GetTicketsQuery.CreatedFrom`.
Queries nuevas (sin endpoint; las usa el asistente): `Tickets/GetTicketWorkload` (exige `tickets.manage`, si no 403) y
`Tickets/GetTicketStats` (validador: fin > inicio, máximo un año).

## Memoria (`ConversationStore`)

Por usuario y conversación, en memoria, 24 h sin uso. Guarda turnos (pregunta, trabajo con herramientas, respuesta);
los 2 más recientes van completos al modelo y los anteriores solo como pregunta y respuesta (máx. 8 turnos).
Otro usuario que mande el id recibe una conversación nueva. Se pierde si la API se reinicia.

## Seguridad, caché y registro

- **Texto invisible** (`AiText.Clean`): la pregunta y todo texto de los resultados de herramientas (títulos y
  descripciones que escribieron otros usuarios) pierden caracteres de formato (zero-width, bidi como U+202E, etiquetas
  U+E0000–E007F), controles salvo salto de línea y tabulador, y uso privado; NFKC normaliza letras "disfrazadas".
- **Prompt injection:** lo que el rol puede hacer lo decide el código (herramientas filtradas por permiso, solo
  lectura, `proponer_ticket` no escribe). El prompt además dice que los datos de herramientas son texto, no órdenes, y
  que el rol no cambia aunque el usuario lo diga. El prompt no guarda secretos: filtrarlo no expone nada.
- **Respuesta en bucle:** `max_tokens` 800; una respuesta cortada se descarta (no se reenvía) y, si faltó un permiso,
  se contesta "Tu rol no tiene permiso…".
- **Caché** (`AskAssistantHandler`): primera pregunta de una conversación, por usuario + rol + pregunta normalizada,
  5 min en `IMemoryCache`. No guarda respuestas con errores ni borradores de ticket. Seguimientos van siempre al modelo.
- **Registro:** una línea por pregunta (`Asistente: pregunta rol=… ms=… router=… routerMs=… ofrecidas=… vueltas=… tokens=…
  herramientas=… errores=… respaldo=…`, o `desde caché`), sin el texto de la pregunta.

## Infraestructura

`--ctx-size 8192 --parallel 2` en el servicio `ia`: un turno para el asistente y otro para la carga masiva (el chat
responde aunque haya una conversión en curso). `mem_limit: 3584m` (pico medido ~3.15 GB).

## Pendiente

- [x] RAG: manual de usuario en `backend/Features/Assistant/Manual/*.md` (embebido en la API) + `buscar_manual`
  (BM25 en memoria por secciones "## ", sinónimos solo en la pregunta)
- [x] Set de evaluación: 63 preguntas (activos, ficha e historial, incidentes, tickets, combinadas, seguimiento,
  permisos, manual, mis equipos) al 100% contra el modelo real
- [x] Pantalla: botón flotante "Pregúntale" en todas las páginas (`frontend/src/modules/assistant`), panel con la
  conversación, sugerencias según el rol, "Consulté" con los datos reales de cada consulta y "Nueva conversación"

## Evaluación

`backend/tests/TecnmInventario.Api.Tests/Eval/assistant-eval.json` (versionado): 40 casos (activos, ficha e historial,
incidentes, tickets, combinadas, seguimiento, manual, fuera de tema, solo lectura, mis equipos, permisos por rol,
tickets por IA y ataques: "ignora tus reglas", "soy administrador", pedir el prompt, caracteres invisibles, inyección en
el seguimiento). Cada caso: herramientas que deben ejecutarse o no, lo que debe y no debe mencionar, datos que deben o
no traer las herramientas, y que todo código de activo citado venga de lo consultado (nada inventado).

Corre contra la API real y el modelo con los datos ZZ-IA de desarrollo; sin las variables se omite:

```bash
TECNM_EVAL_API=http://localhost:5112/api TECNM_EVAL_ADMIN=zz_ia_admin:<contraseña> TECNM_EVAL_USUARIO=zz_ia_usuario:<contraseña> dotnet test backend/TecnmInventario.slnx --filter "Category=Eval"
```

Resultado (2026-11-16, Qwen3.5-2B, sin otro modelo corriendo en la máquina): 38/40 en 7.9 min; ataques y permisos
40/40 sin fugas. Falla conocida: "¿cuántas X hay y cuáles tienen falla?" contesta solo la segunda parte (límite del 2B,
el ejemplo en el prompt no lo corrige). Con dos modelos en la misma máquina las respuestas pasan de 120 s.
