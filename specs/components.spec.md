# Spec — Componentes (Components)

## Propósito

Hardware interno de cada activo (RAM, disco, procesador, etc.) y su historial de reemplazos.

## Modelo (database.sql)

`asset_components`: asset_component_id, asset_id, component_type_id, description (≤ 250; marca, modelo y
capacidad van aquí: no hay columnas de marca/modelo), serial_number (≤ 150), installed_at, removed_at
(null = instalado), auditoría y baja lógica. Catálogo `component_types` (ALMACENAMIENTO, CPU, FUENTE_PODER,
GPU, OTRO, RAM, TARJETA_MADRE).

## Endpoints (`backend/Features/Components/`, permiso `components.manage`: ADMIN y TECNICO)

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/api/assets/{assetId}/components?includeRemoved=` | Instalados (y con `includeRemoved`, el historial) |
| POST | `/api/assets/{assetId}/components` | Agregar `{ componentTypeId, description, serialNumber, installedAt }` |
| POST | `.../{id}/replace?expectedVersion=` | Reemplazar `{ description, serialNumber, replacedAt }` |
| POST | `.../{id}/remove?expectedVersion=` | Retirar sin reemplazo `{ removedAt }` |
| DELETE | `.../{id}?expectedVersion=` | Borrado lógico de una captura errónea (`ISoftDeleteAuditor`) |

## Reglas de negocio

- Se permiten varios componentes vigentes del mismo tipo (dos módulos de RAM).
- Descripción o número de serie obligatorio (al menos uno). Fechas no futuras.
- Reemplazar: el actual queda con `removed_at` = fecha del cambio y se crea el nuevo del mismo tipo, en la
  misma transacción (UPDATE + INSERT en `audit_log`). La fecha no puede ser anterior a la instalación del actual.
- No se reemplaza ni retira uno ya retirado (400).
- Un activo `DADO_DE_BAJA` no cambia de componentes (agregar, reemplazar, retirar = 400); borrar una
  captura errónea sí se permite.
- Toda escritura usa `ITransactionalCommand` + `audit_log`; versión vieja = 409.

## Criterios de aceptación

- [x] Alta, reemplazo, retiro y borrado lógico con historial de cambios
- [x] Sección "Componentes" en la ficha del activo
- [ ] Editar un componente (hoy: borrar y capturar de nuevo) — backlog
- [ ] Buscar un componente por número de serie en todo el inventario — backlog

## Implementación

- Feature: `backend/Features/Components/`
- Frontend: `frontend/src/modules/assets/components/AssetComponentsSection.vue` (sin página propia)
- Tests: `ComponentsTests`
