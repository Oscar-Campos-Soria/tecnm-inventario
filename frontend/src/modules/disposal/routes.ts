import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/disposals',
    name: 'disposals',
    component: () => import('./pages/DisposalsPage.vue'),
    meta: {
      nav: {
        label: 'Bajas',
        order: 50,
        // La lista la puede leer assets.view; en el menú solo quien da de baja.
        permission: 'assets.delete',
        icon: ['M4 4h16v4H4z', 'M5 8h14v12H5z', 'M10 12h4'],
      },
    },
  },
]

export default routes
