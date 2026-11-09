# Spec — Reportes (Reports)

## Propósito

Generación y exportación de reportes del inventario en **Excel** (ClosedXML) y **PDF** (QuestPDF).

## Endpoint

`GET /api/reports/{reporte}?format=xlsx|pdf` (`backend/Features/Reports`, permiso `reports.view`: ADMIN y
RESPONSABLE_AREA). Solo lectura: no escribe en `audit_log`.

| Reporte | Contenido | Filtros |
| --- | --- | --- |
| `assets` | Código, tipo, estado, marca, modelo, serie, hostname, ubicación, resguardante vigente, alta | `status` (sin él, excluye dados de baja), `assetTypeId` |
| `assets-by-location` | Equipos en servicio ordenados por ubicación | — |
| `open-incidents` | Incidentes no resueltos: prioridad, estado, quién reportó, técnico, días abiertos | — |
| `maintenance` | Mantenimientos realizados con costo, técnico, incidente y próximo | `from`, `to` (fecha de realización) |
| `licenses` | Uso, permitidos, disponibles, vigencia y proveedor. **Nunca la clave** | — |

## Reglas

- Hasta 10,000 filas por reporte; si hay más, el archivo lo avisa y pide filtrar.
- Excel: una hoja con título, filtros y fecha de generación, y una tabla con formato (encabezado fijo, fechas y
  montos con formato). PDF: carta horizontal, encabezado institucional y paginación.
- Fechas impresas en hora del centro de México.

## Criterios de aceptación

- [x] Cinco reportes en Excel y PDF (prueba de integración con PostgreSQL real que relee el Excel)
- [x] Pantalla `/reports` con filtros y descarga
- [ ] Reportes programados por correo — backlog

## Implementación

- Feature: `backend/Features/Reports/` (`GetReport`, `Shared/ReportTable`, `Shared/ReportRenderer`)
- Módulo frontend: `frontend/src/modules/reports/`
