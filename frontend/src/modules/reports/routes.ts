import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/reports',
    name: 'reports',
    component: () => import('./pages/ReportsPage.vue'),
    meta: {
      permission: 'reports.view',
      nav: {
        label: 'Reportes',
        order: 70,
        permission: 'reports.view',
        icon: ['M7 3h7l5 5v13H7z', 'M14 3v5h5M10 13h5M10 17h5'],
      },
    },
  },
]

export default routes
