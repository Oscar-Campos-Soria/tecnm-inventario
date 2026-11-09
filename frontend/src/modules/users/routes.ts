import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/users',
    name: 'users',
    component: () => import('./pages/UsersPage.vue'),
    meta: {
      permission: 'users.manage',
      nav: {
        label: 'Usuarios',
        order: 80,
        permission: 'users.manage',
        icon: ['M9 11a3 3 0 100-6 3 3 0 000 6z', 'M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6', 'M16 11h5M18.5 8.5v5'],
      },
    },
  },
]

export default routes
