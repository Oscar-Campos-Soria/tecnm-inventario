import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/maintenance',
    name: 'maintenance',
    component: () => import('./pages/MaintenancePage.vue'),
    meta: {
      permission: 'maintenance.manage',
      nav: {
        label: 'Mantenimiento',
        order: 45,
        permission: 'maintenance.manage',
        icon: ['M12 12a3 3 0 100-6 3 3 0 000 6z', 'M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9L7 7M17 17l2.1 2.1M19.1 4.9L17 7M7 17l-2.1 2.1'],
      },
    },
  },
]

export default routes
