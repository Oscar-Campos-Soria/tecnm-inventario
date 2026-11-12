# Spec — Carga masiva con IA (convertir archivos a la plantilla)

## Propósito

Convertir un inventario "como venga" (Excel con otro formato, CSV, PDF de texto, PDF escaneado o foto de un
oficio) a la **plantilla oficial** de la carga masiva. La IA **no escribe en la BD**: entrega la plantilla
llena para que una persona la revise y la suba por la carga masiva normal (`preview` → `commit`, auditada).

Requiere el asistente IA encendido (`tecnm ia instalar`, ver specs/deployment.spec.md). Si está apagado,
`ai/status` responde `enabled: false` y la pantalla oculta la opción.

## Endpoints (`backend/Features/BulkImport/`, permiso `assets.create`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/bulk-import/assets/ai/status` | `{enabled}`: el modelo responde (cacheado 30 s) |
| POST | `/api/bulk-import/assets/ai/jobs` | `multipart/form-data` (`file`: .xlsx .csv .pdf .jpg .png, ≤ 5 MB) → **202** `{jobId}` |
| GET | `/api/bulk-import/assets/ai/jobs/{id}` | `{status, step, done, total, found, toReview, error}`; `status`: EN_COLA, PROCESANDO, LISTO, ERROR |
| GET | `/api/bulk-import/assets/ai/jobs/{id}/result.xlsx` | Plantilla llena (400 si aún no termina) |

Solo el usuario que inició el trabajo lo ve (otro usuario recibe 404). Un trabajo activo por usuario y
máximo 5 en cola; el modelo atiende uno a la vez (`AiImportWorker`).

## Cómo funciona

1. **Archivo → líneas con origen** (`SourceReader`, sin IA): Excel/CSV por hoja, con cada celda etiquetada por su
   encabezado (`Clave Inv.="TEC-1"`); PDF con PdfPig (texto por página; si una página no trae texto se toma su
   imagen incrustada); fotos y páginas escaneadas las **transcribe el modelo** (visión).
2. **El modelo extrae** por bloques de 20 líneas: código, tipo (lista cerrada del catálogo), fabricante, modelo,
   serie, hostname. JSON forzado por esquema (gramática de llama.cpp).
3. **Reglas sin IA** (`ExtractionRules`), medidas con archivos de prueba porque el modelo falla en esto:
   - el origen (hoja y fila, o página y línea) lo busca el código por el código o la serie;
   - si la hoja tiene columna de inventario/clave, serie o nombre en red, ese dato sale de la columna;
   - **anti-alucinación**: código, serie, fabricante y modelo deben aparecer en su fila (o líneas vecinas en
     texto corrido); si no, se dejan vacíos y se avisa;
   - ubicación: la de la fila o el título de sección más cercano hacia arriba, emparejado con el catálogo
     (ruta completa "Edificio B, Planta 1, Laboratorio de redes") por palabras en común;
   - tipo corregido si la fila dice explícitamente "switch", "laptop", etc.
4. **Validación de la carga masiva** (`AssetImportChecker`) sobre lo extraído: sus errores van a `revisar`.
5. **Plantilla**: la oficial (hoja Catálogos incluida) + columnas `origen` y `revisar`; filas con avisos en
   amarillo. El parser de la carga masiva ignora esas dos columnas, así que se sube tal cual.

## Límites

5 MB, 10 páginas de PDF, 30 bloques (~600 líneas) y 300 equipos por archivo. Los trabajos viven **en memoria**
1 hora: si la API se reinicia se pierden (el usuario vuelve a subir el archivo).

## Medición (archivos de prueba, Qwen3.5-2B en CPU)

| Archivo | Equipos | Campos correctos | Tiempo |
| --- | --- | --- | --- |
| Excel desordenado (2 hojas, secciones, firmas) | 10/10 | 58/60 | ~47 s |
| PDF tipo oficio (texto corrido) | 4/4 | 24/24 | ~22 s |
| Foto del mismo oficio | 4/4 | 24/24 | ~43 s |

## Criterios de aceptación

- [x] Endpoints, cola y reglas con pruebas (`BulkImportAiTests`, IA simulada) y prueba real contra el modelo
- [x] La conversión no escribe en la BD (activos y `audit_log` sin cambios)
- [ ] Pantalla en `/bulk-import` (tarjeta "Conviértelo con IA" con progreso y descarga)
- [ ] Crear fabricantes y modelos que no existen en el catálogo — hoy el usuario los da de alta antes de subir

## Implementación

- Feature: `backend/Features/BulkImport/` (`StartAiConvert`, `GetAiJob`, `GetAiJobResult`, `GetAiStatus`, `Shared/Ai/`)
- Configuración: `Ia:BaseUrl` (producción `http://ia:8080`; desarrollo `http://localhost:8081` con
  `docker compose --profile ia up -d ia` en `database/docker-compose.yml`)
