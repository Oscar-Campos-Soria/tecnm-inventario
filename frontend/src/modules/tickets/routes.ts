import type { RouteRecordRaw } from 'vue-router'
import { http } from '../../shared/api/http'

const routes: RouteRecordRaw[] = [
  {
    path: '/tickets',
    name: 'tickets',
    component: () => import('./pages/TicketsPage.vue'),
    meta: {
      nav: {
        label: 'Tickets',
        // Lo usan todos los roles: segundo en el menú, después del panel.
        order: 15,
        permission: 'tickets.create',
        // Tickets abiertos sin técnico: lo que la mesa de ayuda tiene que tomar.
        badgePermission: 'tickets.manage',
        badgeLabel: 'sin asignar',
        badge: () =>
          http
            .get<{ totalCount: number }>('/api/tickets?unassigned=true&onlyOpen=true&pageSize=1')
            .then((page) => page.totalCount),
        icon: ['M4 7a2 2 0 012-2h12a2 2 0 012 2v2a2 2 0 000 4v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2a2 2 0 000-4z', 'M14 6v1.5M14 11.25v1.5M14 16.5V18'],
      },
    },
  },
  {
    path: '/tickets/:ticketId',
    name: 'ticket-detail',
    component: () => import('./pages/TicketDetailPage.vue'),
  },
]

export default routes
