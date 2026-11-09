# Módulo bulk-import

Ruta `/bulk-import` (permiso `assets.create`; menú "Carga masiva", orden 25). Tres pasos: descargar la plantilla,
subir el archivo (.xlsx o .csv; se revisa al instante y se listan los errores por número de fila, con filtro
"Solo filas con errores") y confirmar, que solo se habilita si todas las filas están bien.

API: `GET /api/bulk-import/assets/template.xlsx`, `POST .../preview`, `POST .../commit`.
Especificación: `specs/bulk-import.spec.md`
