# Módulo dashboard

Inicio por rol y métricas del inventario.

## Rutas (`routes.ts`)

| Ruta | Página | Menú |
| --- | --- | --- |
| `/` | `pages/DashboardPage.vue` — saludo, accesos directos según permisos y, con `dashboard.view`, `DashboardMetrics` | "Inicio", orden 10, visible para todos |

## Métricas (`components/DashboardMetrics.vue`, `dashboard.view`)

- Frase resumen (en servicio y detenidos) y banda por estado en orden del ciclo de vida, con lista de número, símbolo y
  explicación (el color no es el único dato); cada estado abre `/assets?status=CODIGO` si el rol tiene `assets.view`.
  Los dados de baja van aparte, fuera de la banda.
- Tres avisos: incidentes abiertos (desglose por prioridad; resaltado si hay ALTA o CRÍTICA), preventivos vencidos
  (y cuántos vencen en 30 días) y licencias por vencer (y cuántas ya vencieron). Cada aviso enlaza a su pantalla
  solo si el rol tiene el permiso (`incidents.view`, `maintenance.manage`, `software.manage`); licencias abre
  `/software?vencen=1`.
- Equipos por tipo y las 10 ubicaciones con más equipos (barras relativas al mayor).

Endpoint: `GET /api/dashboard/summary`. El servidor lo cachea 60 s: un cambio puede tardar hasta un minuto en verse.

Especificación: `specs/dashboard.spec.md`
