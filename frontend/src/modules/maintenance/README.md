# Módulo maintenance

Calendario de preventivos por vencer y bitácora general. Los mantenimientos se
registran desde la ficha de cada activo (módulo assets, sección "Mantenimiento").

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/maintenance` | `pages/MaintenancePage.vue` — preventivos por vencer (30/60/90 días, vencidos en rojo, ≤ 7 días en naranja) y bitácora con filtro por tipo | "Mantenimiento", orden 45, `maintenance.manage` |

## Endpoints

`GET /api/maintenance/upcoming?days=N`, `GET /api/maintenance?type=&pageNumber=`.

Especificación: `specs/maintenance.spec.md`
