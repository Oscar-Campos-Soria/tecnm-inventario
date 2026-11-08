# Módulo locations

Árbol navegable de ubicaciones (Edificio → Planta → Área → Rack) con alta, edición y baja.

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/locations` | `pages/LocationsPage.vue` — árbol (`components/LocationTreeNode.vue`, recursivo) con búsqueda por nombre/código/tipo | "Ubicaciones", orden 30, `locations.manage` |
| `/locations/:locationId` | `pages/LocationDetailPage.vue` — breadcrumb con enlaces, datos generales, editar y eliminar | — |

## Reglas reflejadas

- Escritura solo con `locations.manage` (ADMIN).
- En edición el padre no puede ser la propia ubicación ni una descendiente.
- El código no se edita después de crear.
- 409 recarga y avisa; 400 de negocio (tiene sub-ubicaciones o activos) se muestra tal cual.
- Con búsqueda activa se muestran coincidencias y sus ancestros; el resto se oculta.

## Endpoints

`GET/POST /api/locations`, `GET/PUT/DELETE /api/locations/{id}` (con `expectedVersion`), `GET /api/catalogs` (tipos).

Especificación: `specs/locations.spec.md`
