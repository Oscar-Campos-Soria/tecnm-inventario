# Módulo reports

Ruta `/reports` (permiso `reports.view`; menú "Reportes", orden 70). Una tarjeta por reporte con botones
**Excel** y **PDF**; el resumen de activos filtra por estado y tipo, y mantenimientos por rango de fechas.
La descarga usa `shared/utils/download.ts` (nombre del archivo lo da el servidor).

API: `GET /api/reports/{reporte}?format=xlsx|pdf`. Especificación: `specs/reports.spec.md`.
