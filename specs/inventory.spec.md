# Spec — Levantamiento físico (Inventory)

## Propósito

Comprobar físicamente que lo registrado existe y está donde dice: se abre un
conteo por área, se escanea cada equipo y al cerrar se concilian las
diferencias **una por una** (nada cambia solo).

## Endpoints (`backend/Features/Inventory/`, permiso `inventory.manage`: ADMIN, TECNICO)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/inventory-sessions` | Conteos (paginado) con avance |
| `POST` | `/api/inventory-sessions` | Abre conteo `{name, locationId}` |
| `GET` | `/api/inventory-sessions/{id}` | Detalle con renglones, estado calculado y resumen |
| `POST` | `/api/inventory-sessions/{id}/scan` | Registra un equipo `{inventoryCode, locationFoundId?, notes?}` |
| `POST` | `/api/inventory-sessions/{id}/records/{recordId}/not-found?expectedVersion=N` | Marca no encontrado |
| `POST` | `/api/inventory-sessions/{id}/close?expectedVersion=N` | Cierra el conteo |
| `PUT` | `/api/inventory-sessions/{id}/records/{recordId}/adjustment?expectedVersion=N` | Concilia `{adjustment: MOVIMIENTO\|INCIDENTE\|SIN_CAMBIO, incidentId?}` |

## Reglas de negocio

- **Abrir:** la lista esperada son los activos no dados de baja cuya ubicación
  actual está en el área o sus sub-ubicaciones (`LocationTree`). Un solo conteo
  abierto por área.
- **Escanear** (conteo abierto): código sin distinguir mayúsculas; código
  inexistente → 404 ("regístralo primero en Activos"); repetido → no cambia
  nada (`alreadyChecked`); equipo fuera de la lista → renglón **sobrante**.
  `locationFoundId` por defecto = área del conteo.
- **Estado del renglón** (`InventoryRecordStatusCodes.Of`, calculado):
  PENDIENTE (sin revisar), ENCONTRADO (en su lugar), EN_OTRO_LUGAR,
  NO_ENCONTRADO, SOBRANTE (no estaba en la lista).
- **Cerrar:** lo pendiente pasa a NO_ENCONTRADO. No toca activos.
- **Conciliar** (conteo cerrado, solo EN_OTRO_LUGAR / NO_ENCONTRADO / SOBRANTE, una vez por renglón):
  - `MOVIMIENTO`: exige que el activo ya esté en la ubicación donde se encontró
    (el movimiento se registra antes con `POST /api/assets/{id}/movements`,
    motivo `AJUSTE_INVENTARIO`). No aplica a no encontrados.
  - `INCIDENTE`: solo NO_ENCONTRADO; el incidente (tipo `ROBO_EXTRAVIO`) debe ser del mismo activo.
  - `SIN_CAMBIO`: cualquier diferencia.
- Concurrencia por `Version` (409); cada escritura + `audit_log` en transacción.

## Casos conocidos

- Equipo **dado de baja** que aparece: sobrante; no se puede mover (baja
  terminal) → `SIN_CAMBIO`. La pantalla avisa "debió retirarse".
- Equipo con falla encontrado en otro lugar: mover exige DISPONIBLE/ASIGNADO →
  resolver su incidente antes o `SIN_CAMBIO`.
- "Reportar extraviado" deja el activo en `CON_FALLA` (efecto del incidente).

## Frontend (`modules/inventory`)

- `/inventory`: conteos y "Nuevo conteo".
- `/inventory/:id`: abierto → escaneo (lector USB o teclado con foco
  automático; cámara con `BarcodeDetector` donde exista), "¿Dónde estás?",
  "No está" por renglón, filtros por resultado y "Cerrar conteo". Cerrado →
  conciliación con "Mover a …", "Reportar extraviado" y "Sin cambio".

## Datos

`inventory_sessions` (+ `location_id`), `inventory_records` (+ `expected_location_id`,
`checked_at/by`, `adjustment`, `adjusted_at/by`, `incident_id`) por
`scripts/add-inventory-counts.sql`.

## Pendiente

- Cancelar un conteo abierto.
- Etiquetas QR imprimibles.
