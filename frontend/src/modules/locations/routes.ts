import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/locations',
    name: 'locations',
    component: () => import('./pages/LocationsPage.vue'),
    meta: {
      nav: {
        label: 'Ubicaciones',
        order: 30,
        // Ver: locations.view (todos los roles). Crear/editar/borrar: locations.manage.
        permission: 'locations.view',
        icon: ['M12 21s-6-5.2-6-10a6 6 0 1112 0c0 4.8-6 10-6 10z', 'M12 11a2 2 0 100-4 2 2 0 000 4z'],
      },
    },
  },
  {
    path: '/locations/:locationId',
    name: 'location-detail',
    component: () => import('./pages/LocationDetailPage.vue'),
  },
]

export default routes
