# Módulo procurement

Ruta `/procurement` (permiso `procurement.manage`, solo ADMIN). Dos pestañas:

- **Compras**: lista paginada (proveedor, orden de compra, factura, fecha, total, núm. de activos).
  "Ver" abre el detalle con los activos y su costo unitario. "Nueva compra" pide proveedor (opcional),
  OC, factura, fecha, montos, moneda (3 letras, MXN por omisión), garantía y los activos (buscador por
  código, mínimo uno, sin repetir). Eliminar es borrado lógico con `expectedVersion`.
- **Proveedores**: alta, edición (todos los campos; la lista trae dirección y notas para no borrarlas)
  y eliminación lógica.

Las **garantías** por activo viven en la ficha del activo (`AssetWarrantySection`).

API: `/api/suppliers`, `/api/acquisitions`, `/api/warranties`, `GET /api/assets/{id}/warranties`.
