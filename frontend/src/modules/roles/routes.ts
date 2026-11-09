import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/roles',
    name: 'roles',
    component: () => import('./pages/RolesPage.vue'),
    meta: {
      permission: 'roles.manage',
      nav: {
        label: 'Roles',
        order: 85,
        permission: 'roles.manage',
        icon: ['M12 3l7 3v5c0 5-3.5 8-7 10-3.5-2-7-5-7-10V6l7-3z'],
      },
    },
  },
]

export default routes
