import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/assets',
    name: 'assets',
    component: () => import('./pages/AssetsPage.vue'),
    meta: {
      nav: {
        label: 'Activos',
        order: 20,
        permission: 'assets.view',
        icon: ['M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3z', 'M12 12l8-4.5M12 12v9M12 12L4 7.5'],
      },
    },
  },
  {
    // Equipos a cargo del usuario; para quien no ve el inventario completo (usuario general).
    path: '/my-assets',
    name: 'my-assets',
    component: () => import('./pages/MyAssetsPage.vue'),
    meta: {
      nav: {
        label: 'Mis equipos',
        order: 20,
        hideWithPermission: 'assets.view',
        icon: ['M4 5h16v10H4z', 'M8 19h8', 'M12 15v4'],
      },
    },
  },
  {
    path: '/assets/:assetId',
    name: 'asset-detail',
    component: () => import('./pages/AssetDetailPage.vue'),
  },
]

export default routes
