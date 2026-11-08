import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/incidents',
    name: 'incidents',
    component: () => import('./pages/IncidentsPage.vue'),
    meta: {
      nav: {
        label: 'Incidentes',
        order: 40,
        permission: 'incidents.view',
        icon: ['M12 4L2.5 20h19L12 4z', 'M12 10v4M12 17v.3'],
      },
    },
  },
  {
    path: '/incidents/:incidentId',
    name: 'incident-detail',
    component: () => import('./pages/IncidentDetailPage.vue'),
  },
]

export default routes
