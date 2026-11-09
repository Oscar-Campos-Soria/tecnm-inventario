# Módulo software

Ruta `/software` (permiso `software.manage`: ADMIN y TECNICO). Menú "Software", orden 65. Dos pestañas:

- **Licencias** (por omisión): búsqueda por programa y filtro "solo las que vencen en 30 días".
  Cada fila muestra el uso (`UsageBar`: "3 de 10 en uso", en naranja cuando ya no hay lugares;
  "sin límite" si la cantidad está vacía) y la vigencia (vencida, vence en N días, hasta fecha).
  - "Equipos" abre `LicenseDetailModal`: datos, clave enmascarada, buscador de equipos para asignar
    (deshabilitado si está vencida o sin lugares), equipos vigentes con "Liberar" e historial.
  - "Nueva licencia" / "Editar": tipo, equipos permitidos (vacío = sin límite), vigencia, clave
    (campo de contraseña; al editar, vacío conserva la guardada y "Quitar la clave guardada" la borra),
    proveedor (solo con `procurement.manage`) y notas.
- **Programas**: alta, edición y borrado lógico (bloqueado si tiene licencias o sigue instalado).

El software instalado por equipo se registra en la ficha del activo (`modules/assets`,
`AssetSoftwareSection`).

409: recarga la lista y avisa "Otra persona modificó…".

API: ver `specs/software.spec.md`. Usa también `GET /api/catalogs`, `GET /api/assets` (buscador)
y `GET /api/suppliers`.
