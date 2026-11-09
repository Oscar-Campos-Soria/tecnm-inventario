import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/software',
    name: 'software',
    component: () => import('./pages/SoftwarePage.vue'),
    meta: {
      permission: 'software.manage',
      nav: {
        label: 'Software',
        order: 65,
        permission: 'software.manage',
        icon: ['M7 7h10v10H7z', 'M10 10h4v4h-4z', 'M9 3v4M15 3v4M9 17v4M15 17v4M3 9h4M3 15h4M17 9h4M17 15h4'],
      },
    },
  },
]

export default routes
