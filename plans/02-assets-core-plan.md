# Plan — FASE 2: Núcleo de activos

**Objetivo:** completar el ciclo de vida del activo y sus entidades satélite, cerrando las funcionalidades backend pendientes y preparando los endpoints que consumirá el SPA.

## Alcance

### Activos
- [ ] Completar máquina de estados con transiciones configurables
- [ ] Movimientos de ubicación (`asset_movements`) con historial
- [ ] Asignaciones a usuarios (`asset_assignments`)
- [ ] Escáner QR: generación de código y endpoint de resolución

### Módulos satélite
- [ ] Componentes con historial de actualizaciones (`Features/Components`)
- [ ] Software y control de licencias (`Features/Software`)
- [ ] Mantenimiento preventivo/correctivo + calendario (`Features/Maintenance`)
- [ ] Baja formal de activos con acta (`Features/AssetDisposal`)
- [ ] Carga masiva Excel/CSV con validación (`Features/BulkImport`)
- [ ] Adquisiciones, proveedores y garantías (`Features/Procurement`)
- [ ] Historia/timeline y reportes (`Features/History`, `Features/Reports`)
- [ ] Métricas de dashboard (`Features/Dashboard`)

### Pruebas
- [ ] Tests unitarios de validadores y reglas de transición de estado
- [ ] Tests de integración contra PostgreSQL en Docker

## Criterios de salida

- Todos los endpoints de `specs/*.spec.md` implementados y probados
- API consumible desde el SPA (FASE 3)

## Estado: 🟡 En progreso

Completado: Assets (CRUD + estado), Locations, Incidents, Documents, Catalogs, Roles.
Pendiente: Maintenance, Disposal, Components, Software, BulkImport, History, Reports, Dashboard.
