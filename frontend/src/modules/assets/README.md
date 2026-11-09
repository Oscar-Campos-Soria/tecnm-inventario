# Módulo assets

Lista de activos y ficha detallada con movimientos de ubicación y asignaciones.

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/assets` | `pages/AssetsPage.vue` — tabla paginada (código, tipo, estado, ubicación, serie, hostname) con búsqueda (código/serie/hostname) y filtros por estado, tipo, ubicación (incluye sub-ubicaciones) y "incluir dados de baja" | "Activos", orden 20, `assets.view` |
| `/assets/:assetId` | `pages/AssetDetailPage.vue` — datos generales, tipo/estado, movimientos (paginados + registro), asignaciones (registro + devolución) | — |

## Reglas reflejadas

- Una sola asignación activa por activo (el backend bloquea duplicados).
- Movimiento con destino distinto a la ubicación actual.
- Selector de usuario con búsqueda (top 50, `users.view`).
- 409 en movimiento/asignación/devolución: recarga la ficha y avisa.
- Sin `locations.manage`, la ficha carga igual y la ubicación se muestra "—" (deuda `locations.view`).
- "Reportar problema" (con `tickets.create`) enlaza a `/tickets?nuevo=1&assetId=N`.
- "Componentes" (con `components.manage`, `AssetComponentsSection`): hardware instalado agrupado por tipo,
  agregar, reemplazar (el actual pasa al historial), retirar y eliminar capturas erróneas; historial plegable.
  Sin acciones en activos dados de baja.
- "Software instalado" (con `software.manage`, `AssetSoftwareSection`): programas vigentes con versión y si ocupan
  licencia, historial, registrar instalación (otra versión reemplaza a la vigente) y desinstalar. Si al
  desinstalar el equipo sigue ocupando una licencia, se avisa con enlace a `/software`. Oculto para
  registrar/desinstalar en activos dados de baja.

## Endpoints

`GET /api/assets`, `GET /api/assets/{id}`, `GET/POST /api/assets/{id}/movements`,
`GET/POST /api/assets/{id}/assignments`, `POST .../assignments/{id}/return`,
`GET /api/users`, `GET /api/locations`, `GET /api/catalogs`,
`GET/POST /api/assets/{id}/components` (+ `/replace`, `/remove`, `DELETE`),
`GET/POST /api/assets/{id}/software`, `POST .../software/{id}/uninstall`, `GET /api/software`.

Especificación: `specs/assets.spec.md`
