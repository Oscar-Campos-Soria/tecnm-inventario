# Spec — Baja de activos (Disposals)

## Propósito

Baja formal de activos con folio de acta, motivo, dictamen y acta firmada
adjunta, con trazabilidad completa. Es la **única** vía para llevar un activo
a `DADO_DE_BAJA`: el cambio manual de estado (`PATCH /api/assets/{id}/status`)
ya no acepta ese destino.

## Endpoints (`backend/Features/Disposals/`)

| Método | Ruta | Permiso | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/disposals` | `assets.view` | Paginado; filtros `assetId`, `disposalReasonId`, `search` (folio o código de inventario) |
| `POST` | `/api/disposals?expectedVersion=N` | `assets.delete` | Registra la baja (`expectedVersion` = versión del **activo**) |

Body del POST: `assetId`, `disposalReasonId`, `folio` (obligatorio, ≤ 100,
único, lo captura el admin), `dictamen` (opcional, ≤ 4000), `disposedAt`
(opcional, fecha; por defecto hoy; no futura).

## Reglas de negocio

- Un activo solo se da de baja una vez (`asset_disposals.asset_id` UNIQUE).
- Folio único (`asset_disposals.folio` UNIQUE); repetido → 400.
- Precondiciones → 400: sin incidentes abiertos y sin asignación activa.
- 409 si la versión del activo cambió.
- En una transacción: fila en `asset_disposals`, activo → `DADO_DE_BAJA`
  (`IAssetStatusChanger`, terminal) y `audit_log` (INSERT de la baja + UPDATE del activo).
- El activo permanece en `assets`.

## Acta

PDF firmado que se adjunta después de registrar la baja
(`documents.owner_type = 'ASSET_DISPOSAL'`, `owner_id = asset_disposal_id`,
permiso `assets.delete`). El sistema no genera el PDF.
`asset_disposals.act_document_path` no se usa (el acta vive en `documents`).

## Frontend

- Ficha del activo (`modules/assets`): sección "Baja formal" con botón
  "Dar de baja" (`assets.delete`) o, si ya está dado de baja, folio, motivo,
  dictamen y acta. Un activo dado de baja no muestra formularios de
  movimiento/asignación ni "Reportar problema".
- `/disposals` (`modules/disposal`): lista con búsqueda y filtro por motivo.

## Datos

`asset_disposals`, `disposal_reasons` (OBSOLESCENCIA, DANIO_IRREPARABLE, ROBO, EXTRAVIO).

## Criterios de aceptación

- [x] Baja con validación de precondiciones
- [x] Acta firmada adjunta (PDF)
- [ ] Generación del acta en PDF (backlog; requeriría librería nueva)
