# Spec — Adquisiciones (Procurement)

## Propósito

Registrar compras de equipo, sus proveedores y las garantías de cada activo.

## Endpoints (permiso `procurement.manage`, solo ADMIN)

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET/POST` | `/api/suppliers` | Lista paginada (incluye dirección y notas) / alta |
| `PUT/DELETE` | `/api/suppliers/{id}?expectedVersion=` | Edición completa / borrado lógico |
| `GET/POST` | `/api/acquisitions` | Lista paginada / alta con activos (mínimo uno, sin repetir) |
| `GET/DELETE` | `/api/acquisitions/{id}` | Detalle con activos y costo unitario / borrado lógico |
| `GET` | `/api/assets/{id}/warranties` | Garantías del activo |
| `POST/DELETE` | `/api/warranties` | Alta / borrado lógico de garantía |

## Reglas

- Moneda de 3 letras; montos >= 0; fin de garantía >= inicio.
- `UpdateSupplier` reemplaza todos los campos: el cliente manda dirección y notas actuales.

## Implementación

- Feature: `backend/Features/Procurement/` (Suppliers, Acquisitions, Warranties)
- Frontend: `frontend/src/modules/procurement/` (`/procurement`, pestañas Compras y Proveedores)
  y `assets/components/AssetWarrantySection.vue` en la ficha del activo.
