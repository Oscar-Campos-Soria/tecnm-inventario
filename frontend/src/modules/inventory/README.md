# Módulo inventory (levantamiento físico)

Conteos por área: escanear equipos, cerrar y conciliar diferencias una por una.

## Rutas (`routes.ts`, ambas con `meta.permission: 'inventory.manage'`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/inventory` | `pages/InventorySessionsPage.vue` — conteos con avance y "Nuevo conteo" | "Levantamiento", orden 35 |
| `/inventory/:sessionId` | `pages/InventorySessionPage.vue` — escaneo (abierto) o conciliación (cerrado) | — |

## Componentes

- `components/CameraScanner.vue` — QR/código de barras con `BarcodeDetector` (sin librerías); solo se muestra donde el navegador lo soporta.

## Endpoints

`/api/inventory-sessions/*`; para conciliar también `POST /api/assets/{id}/movements`
(`assets.move`, motivo `AJUSTE_INVENTARIO`) y `POST /api/incidents` (`incidents.create`, tipo `ROBO_EXTRAVIO`).

Especificación: `specs/inventory.spec.md`
