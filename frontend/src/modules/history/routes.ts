import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/history',
    name: 'history',
    component: () => import('./pages/HistoryPage.vue'),
    meta: {
      permission: 'history.view',
      nav: {
        label: 'Historial',
        order: 90,
        permission: 'history.view',
        icon: ['M12 21a9 9 0 110-18 9 9 0 010 18z', 'M12 7v5l3 2'],
      },
    },
  },
]

export default routes
