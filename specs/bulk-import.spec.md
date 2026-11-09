# Spec — Carga masiva (BulkImport)

## Propósito

Alta masiva de activos desde **Excel (.xlsx) o CSV**, con revisión previa fila por fila.

## Endpoints (`backend/Features/BulkImport/`, permiso `assets.create`)

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/bulk-import/assets/template.xlsx` | Plantilla: hoja Activos (encabezados + ejemplo) y hoja Catálogos (tipos, ubicaciones, fabricantes) |
| POST | `/api/bulk-import/assets/preview` | `multipart/form-data` (`file`): revisa sin escribir; devuelve filas con sus errores |
| POST | `/api/bulk-import/assets/commit` | Mismo archivo: vuelve a revisar y crea todo en una transacción |

## Formato

Encabezados en la fila 1 (sin importar mayúsculas, acentos ni espacios): `codigo_inventario`*, `tipo`* (código o
nombre), `fabricante` (nombre), `modelo` (nombre; si se repite entre fabricantes, indicar fabricante), `serie`,
`hostname`, `ubicacion` (código), `notas`. Columnas desconocidas se ignoran; filas vacías se saltan. CSV en
UTF-8, separado por coma o punto y coma, con comillas dobles. Máximo 1000 filas y 5 MB.

## Reglas

- Mismas reglas que el alta individual (`CreateAssetValidator`): código obligatorio, único (también contra borrados)
  y de hasta 40 caracteres; catálogos existentes; longitudes de serie y hostname. Además, un código no puede
  repetirse dentro del archivo.
- Los activos se crean en `DISPONIBLE`. Cada uno deja su INSERT en `audit_log` con `Source = CARGA_MASIVA` y el
  nombre del archivo.
- **Todo o nada:** si al confirmar hay una sola fila con error (el archivo cambió o alguien tomó un código), no se
  crea ninguno.

## Criterios de aceptación

- [x] Plantilla, revisión y confirmación (prueba de integración con PostgreSQL real)
- [x] Pantalla `/bulk-import` de tres pasos
- [ ] Importar asignaciones o componentes — backlog

## Implementación

- Feature: `backend/Features/BulkImport/` (`Shared/AssetImportParser`, `Shared/AssetImportChecker`)
- Módulo frontend: `frontend/src/modules/bulk-import/`
