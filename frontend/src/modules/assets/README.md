# Módulo assets

Lista de activos y ficha detallada con movimientos de ubicación y asignaciones.

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/assets` | `pages/AssetsPage.vue` — tabla paginada (código, serie, hostname) | "Activos", orden 20, `assets.view` |
| `/assets/:assetId` | `pages/AssetDetailPage.vue` — datos generales, tipo/estado, movimientos (paginados + registro), asignaciones (registro + devolución) | — |

## Reglas reflejadas

- Una sola asignación activa por activo (el backend bloquea duplicados).
- Movimiento con destino distinto a la ubicación actual.
- Selector de usuario con búsqueda (top 50, `users.view`).
- 409 en movimiento/asignación/devolución: recarga la ficha y avisa.
- Sin `locations.manage`, la ficha carga igual y la ubicación se muestra "—" (deuda `locations.view`).
- "Reportar problema" (con `tickets.create`) enlaza a `/tickets?nuevo=1&assetId=N`.

## Endpoints

`GET /api/assets`, `GET /api/assets/{id}`, `GET/POST /api/assets/{id}/movements`,
`GET/POST /api/assets/{id}/assignments`, `POST .../assignments/{id}/return`,
`GET /api/users`, `GET /api/locations`, `GET /api/catalogs`.

Especificación: `specs/assets.spec.md`
