import type { RouteRecordRaw } from 'vue-router'

// Sin entrada en el menú: se abre desde el nombre del usuario en el pie del menú.
const routes: RouteRecordRaw[] = [
  {
    path: '/profile',
    name: 'profile',
    component: () => import('./pages/ProfilePage.vue'),
  },
]

export default routes
