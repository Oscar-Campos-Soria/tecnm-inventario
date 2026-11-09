import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/inventory',
    name: 'inventory',
    component: () => import('./pages/InventorySessionsPage.vue'),
    meta: {
      permission: 'inventory.manage',
      nav: {
        label: 'Levantamiento',
        order: 35,
        permission: 'inventory.manage',
        icon: ['M9 4h6v3H9z', 'M7 5H5v16h14V5h-2', 'M8.5 13l2.5 2.5L16 10.5'],
      },
    },
  },
  {
    path: '/inventory/:sessionId',
    name: 'inventory-session',
    component: () => import('./pages/InventorySessionPage.vue'),
    meta: { permission: 'inventory.manage' },
  },
]

export default routes
