# Spec — Dashboard

## Propósito

Métricas e indicadores visuales generales del inventario para el panel principal.

## Endpoint

`GET /api/dashboard/summary` (`backend/Features/Dashboard/GetSummary`, permiso `dashboard.view`: ADMIN, TECNICO,
RESPONSABLE_AREA).

| Campo | Fuente |
| --- | --- |
| `totalAssets` | Activos en servicio (`v_asset_summary` sin `DADO_DE_BAJA`) |
| `assetsByStatus` | `v_asset_summary` agrupado por estado (incluye dados de baja) |
| `assetsByType` | Activos en servicio por `asset_type` |
| `openIncidentsByPriority` | Incidentes no `RESUELTO`/`CERRADO` por prioridad (todas las prioridades, aunque sean 0) |
| `preventiveOverdue` / `preventiveDueSoon` | Misma regla que `/api/maintenance/upcoming?days=30` (preventivo más reciente por activo) |
| `assetsByLocation` | Top 10 ubicaciones por activos en servicio |
| `licensesExpired` / `licensesExpiringSoon` | `software_licenses.valid_until` antes de hoy / dentro de 30 días |

## Reglas de negocio

- Solo lectura; no escribe en `audit_log`.
- Caché en memoria 60 s (`IMemoryCache`, clave `dashboard:summary`): un cambio tarda hasta un minuto en verse.
- Por ubicación se usa `v_asset_summary` y no `v_location_equipment_summary`, porque esta última cuenta también
  los equipos dados de baja.
- `v_asset_summary` es una vista normal (no materializada).

## Criterios de aceptación

- [x] Endpoint de métricas agregadas (prueba de integración con PostgreSQL real)
- [x] Panel visual en frontend (`modules/dashboard/components/DashboardMetrics.vue`)
- [ ] Gráficas históricas (altas/bajas por mes) — backlog

## Implementación

- Feature: `backend/Features/Dashboard/`
- Módulo frontend: `frontend/src/modules/dashboard/`
