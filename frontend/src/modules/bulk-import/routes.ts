import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/bulk-import',
    name: 'bulk-import',
    component: () => import('./pages/BulkImportPage.vue'),
    meta: {
      permission: 'assets.create',
      nav: {
        label: 'Carga masiva',
        order: 25,
        permission: 'assets.create',
        icon: ['M12 16V4M7 9l5-5 5 5M4 20h16'],
      },
    },
  },
]

export default routes
