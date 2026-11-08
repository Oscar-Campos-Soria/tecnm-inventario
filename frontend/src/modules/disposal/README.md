# Módulo disposal

Lista de activos dados de baja. La baja se registra desde la ficha del activo
(módulo assets, sección "Baja formal").

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/disposals` | `pages/DisposalsPage.vue` — folio, activo, motivo, fecha y quién registró; búsqueda por folio/código y filtro por motivo | "Bajas", orden 50, `assets.delete` |

## Endpoints

`GET /api/disposals` (`assets.view`), `GET /api/catalogs` (`disposalReasons`).

Especificación: `specs/disposal.spec.md`
