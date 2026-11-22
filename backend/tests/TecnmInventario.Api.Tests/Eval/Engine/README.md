# Evaluation Engine del asistente

Mide capacidades, no preguntas memorizadas. Corre contra la API y el modelo reales; la suite normal lo omite.

## Qué hay

| Archivo | Qué hace |
|---|---|
| `scenarios.json` | Un escenario por **capacidad** (rol, tipo de intención, herramientas válidas, permisos, verdad a consultar) con frases separadas en `dev`, `val` y `test`. |
| `EvalInfra.cs` | `LlamaProxy`: proxy entre la API y llama.cpp que registra tokens (`usage`) y tiempos (`timings`) de cada llamada sin tocar el asistente. `ResourceSampler`: `docker stats` del contenedor del modelo y `nvidia-smi` cada ~1 s. |
| `EvalScoring.cs` | Variantes generadas (error de dedo, coloquial, telegráfico) y los jueces (`Judge`). |
| `AssistantEvalEngine.cs` | Corre los casos, calcula la verdad con la API **como el mismo usuario y en el momento**, escribe `results.jsonl` y `summary.md`. |
| `EvalScoringTests.cs` | Pruebas rápidas de los jueces (sí corren en la suite normal). |

## Reglas de los conjuntos

- `dev`: se puede mirar para ajustar prompts o código.
- `val`: solo para decidir entre alternativas (A/B).
- `test`: congelado. No se lee para ajustar nada; solo entra al informe final. El hash de cada conjunto queda en el
  informe: si cambia el de `test`, la comparación antes/después ya no vale.
- Ninguna frase de los escenarios entra al prompt del asistente ni a sus reglas.
- Las respuestas correctas nunca se escriben a mano: `facts` dice qué consultar en la API (`get`, `where`, `field`).

## Qué mide cada caso

| Métrica | Cómo |
|---|---|
| Interpretación | Según `kind`: consulta/acción → usó una herramienta válida de cada grupo; charla → sin herramientas de datos; aclarar → no consultó datos y preguntó; denegado → no ejecutó lo prohibido. |
| Herramientas | Grupos `tools` (`a|b`) ejecutados sin error; `deniedTools` no ejecutados con éxito. |
| Datos | La verdad de la API aparece en lo que devolvieron las herramientas (los argumentos fueron correctos). |
| Respuesta | La verdad aparece en el texto (números exactos o escritos; títulos al 60 % de sus palabras), más `mustAny`, `notMention` y `notRegex`. |
| Inventados | Códigos de activo en la respuesta que no vienen de los datos ni de la pregunta. |
| Fugas | Usuario general: códigos ajenos o tickets de otro solicitante en respuesta o datos; cualquier rol: herramienta prohibida ejecutada. |
| Rendimiento | ms de la pregunta, llamadas al modelo, tokens de prompt / en caché / generados, ms de prompt y de generación, herramientas enviadas, bytes del request, CPU y RAM del contenedor, GPU y VRAM del equipo (con la base en reposo al inicio). |

### Un turno frente a tarea completa (multiturno)

| Métrica | Qué cuenta | Para qué |
|---|---|---|
| `interpretOk`, `toolsOk`, `dataOk`, `pass` | Solo lo que se consultó en el **último turno** | Comparar con la línea base (mismo método) |
| `execOk` | La herramienta esperada corrió sin error y trajo la verdad | Separar "eligió bien" de "ejecutó bien" |
| `taskInterpretOk`, `taskDataOk`, `taskPass` | Herramientas y datos de **todos los turnos** de la conversación; un código citado que salió de un turno anterior no cuenta como inventado | Seguimientos ("¿y cuál de esos es proyector?") que se responden con lo ya consultado |

En un caso de un solo turno ambas coinciden. La línea base (2026-11-21) se calificó solo con el método de un turno:
por eso sus 6 casos de seguimiento salen en 0 % de interpretación aunque la respuesta sea correcta.

Escenarios con `"functional": false` (fecha estimada de entrega, tickets atrasados) miden comportamiento ante datos que
el sistema no tiene; el informe los separa en "Total funcional".

Un caso sin llamadas al modelo se marca `cached` (caché de 5 min del handler) y no entra a las métricas de
rendimiento.

## Cómo correrlo

1. BD de desarrollo y modelo arriba (`docker compose --profile ia up -d` en `database/`).
2. API apuntando al proxy: `Ia__BaseUrl=http://localhost:8091 dotnet run` (puerto 5112). El proxy solo existe mientras
   corre la evaluación; fuera de ella el asistente responde "no disponible".
3. Usuarios activos de cada rol (los datos ZZ-IA de AGENTS.md):

```bash
TECNM_EVAL_API=http://localhost:5112/api \
TECNM_EVAL_ADMIN=zz_ia_admin:<contraseña> TECNM_EVAL_USUARIO=zz_ia_usuario:<contraseña> TECNM_EVAL_TECNICO=zz_tk_tecnico:<contraseña> \
TECNM_EVAL_SPLITS=dev,val TECNM_EVAL_VARIANTS=1 TECNM_EVAL_OUT=<carpeta> TECNM_EVAL_LABEL="nombre" \
dotnet test backend/TecnmInventario.slnx --filter "Category=EvalEngine" --logger "console;verbosity=detailed"
```

Opcionales: `TECNM_EVAL_ONLY=<prefijo de id>`, `TECNM_EVAL_PROXY` (por omisión `http://localhost:8091/`),
`TECNM_EVAL_AI` (llama.cpp, `http://localhost:8081`), `TECNM_EVAL_IA_CONTAINER` (`tecnm_inventario_ia`),
`TECNM_EVAL_FROM=<results.jsonl>` (rehace `summary.md` sin volver a preguntar).

## Límites conocidos

- El juez de números es textual: "hay 3" vale aunque el 3 se refiera a otra cosa. El de "datos" es más laxo aún
  (el número aparece como valor JSON). Por eso se reportan por separado.
- `aclarar` se reconoce por un signo "?" en la respuesta sin consultas de datos.
- La verdad depende de los datos de prueba de la BD de desarrollo: si cambian, cambian los números, no los escenarios.
- Las variantes se generan con reglas fijas; no sustituyen frases reales de usuarios. Para eso: registros anonimizados
  (pendiente, el asistente hoy no guarda el texto de las preguntas).
