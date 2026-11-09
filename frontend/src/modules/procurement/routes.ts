import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/procurement',
    name: 'procurement',
    component: () => import('./pages/ProcurementPage.vue'),
    meta: {
      permission: 'procurement.manage',
      nav: {
        label: 'Adquisiciones',
        order: 60,
        permission: 'procurement.manage',
        icon: ['M9 20a1 1 0 100-2 1 1 0 000 2z', 'M17 20a1 1 0 100-2 1 1 0 000 2z', 'M4 5h2l2.5 11h10L21 8H7'],
      },
    },
  },
]

export default routes
