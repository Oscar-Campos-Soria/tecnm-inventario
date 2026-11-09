import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'dashboard',
    component: () => import('./pages/DashboardPage.vue'),
    meta: {
      nav: {
        label: 'Inicio',
        order: 10,
        // Inicio con accesos según el rol: lo ven todos.
        icon: ['M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z'],
      },
    },
  },
]

export default routes
