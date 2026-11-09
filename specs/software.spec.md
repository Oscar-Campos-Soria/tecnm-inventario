# Spec — Software y licencias (Software)

## Propósito

Inventario de programas, software instalado por equipo y control de licencias. Las licencias se
asignan **solo por máquina** (activo); el usuario se deduce por el resguardante del equipo.

## Modelo (database.sql)

- `software` (software_id, name, vendor, software_type_id) — `UNIQUE (name, vendor)`, baja lógica.
- `software_licenses` (license_id, software_id, license_type_id, license_key, quantity_purchased,
  valid_from, valid_until, supplier_id, notes) — baja lógica.
- `software_license_assignments` (license_assignment_id, license_id, asset_id, assigned_at, ended_at) —
  sin `is_deleted`: liberar = llenar `ended_at`.
- `asset_software` (asset_software_id, asset_id, software_id, version_installed, installed_at, is_current) —
  historial por equipo.

## Reglas de negocio

- Permiso único `software.manage` (ADMIN y TECNICO) para consultar y escribir.
- Programa: nombre + fabricante no se repite (un fabricante vacío también cuenta). No se borra si tiene
  licencias o sigue instalado en algún equipo.
- Cupo: en uso = asignaciones con `ended_at IS NULL`. `quantity_purchased` null = sin límite; 0 = sin cupo.
  No se asigna por encima del cupo ni se baja la cantidad por debajo de lo asignado. La fila de la licencia
  se bloquea (`SELECT ... FOR UPDATE`) al asignar, editar o borrar.
- No se asigna una licencia vencida (`valid_until < hoy`), a un activo `DADO_DE_BAJA` ni dos veces al mismo equipo.
- No se borra una licencia con asignaciones vigentes.
- Clave de licencia: se escribe pero la API solo la devuelve enmascarada (últimos 4); nunca entra a
  `audit_log` (el snapshot solo dice `HasLicenseKey`). Al editar: null conserva la clave, `""` la quita.
- Instalar otra versión del mismo programa deja la anterior con `is_current=false`. No se instala en
  activos dados de baja. Desinstalar no libera la licencia: la respuesta trae `hasActiveLicense` para avisar.
- Toda escritura usa `ITransactionalCommand` + `audit_log`; versión vieja = 409.

## Endpoints

| Método | Ruta |
| --- | --- |
| GET/POST | `/api/software` (`search`, `softwareTypeId`) |
| PUT/DELETE | `/api/software/{id}?expectedVersion=` |
| GET/POST | `/api/software/licenses` (`softwareId`, `expiringInDays`, `search`) |
| GET/PUT/DELETE | `/api/software/licenses/{id}` |
| GET/POST | `/api/software/licenses/{id}/assignments` (`onlyActive`; body `{ assetId }`) |
| POST | `/api/software/licenses/assignments/{id}/end?expectedVersion=` |
| GET/POST | `/api/assets/{assetId}/software` (`includeHistory`) |
| POST | `/api/assets/{assetId}/software/{assetSoftwareId}/uninstall?expectedVersion=` |

## Criterios de aceptación

- [x] CRUD de software y licencias
- [x] Control de conteo de licencias (incluye concurrencia, probado con PostgreSQL real)
- [x] Software instalado por equipo con historial de versiones
- [x] Pantallas `/software` y sección en la ficha del activo
- [ ] `asset_os_history` (cambios de sistema operativo) — backlog
- [ ] Asignación por usuario (requiere `user_id` en `software_license_assignments`) — backlog
- [ ] Alertas de licencias por vencer en el Dashboard (el filtro `expiringInDays` ya existe)

## Implementación

- Feature: `backend/Features/Software/{Programs,Licenses,Installations}`
- Módulo frontend: `frontend/src/modules/software/` y `modules/assets/components/AssetSoftwareSection.vue`
- Tests: `SoftwareLicensesTests`, `AssetSoftwareTests`, `Integration/SoftwareLicensesIntegrationTests`
