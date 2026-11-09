# Arquitectura Frontend — TecNM Assets Management

SPA construida con **Vue 3 + TypeScript + Vite 8 + Tailwind CSS 4** (`<script setup lang="ts">`, Composition API), organizada en tres capas: `app` (global), `modules` (dominio) y `shared` (reutilizable). Rutas modulares descentralizadas y una especificación por slice.

## Estructura de carpetas

```text
frontend/
├── scripts/                     # Validaciones / scripts de build
├── src/
│   ├── app/                     # Configuración global (no contiene negocio)
│   │   ├── layouts/
│   │   │   ├── MainLayout.vue   # Shell con menú (autenticado); el menú sale de meta.nav de cada módulo
│   │   │   └── AuthLayout.vue   # Shell minimalista (login)
│   │   ├── router/
│   │   │   ├── index.ts         # Junta modules/*/routes.ts (import.meta.glob) + guard de sesión
│   │   │   └── route-meta.ts    # Tipos de meta: public, layout, nav
│   │   ├── App.vue              # Elige layout según route.meta.layout
│   │   └── main.ts              # Entry point (monta tras la primera navegación)
│   ├── modules/<modulo>/        # Un slice por dominio (aislado)
│   │   ├── routes.ts            # Rutas del módulo (+ entrada de menú en meta.nav)
│   │   ├── pages/               # Vistas (*.vue)
│   │   ├── components/          # Componentes del módulo
│   │   ├── api.ts               # Llamadas al backend vía shared/api/http.ts
│   │   ├── types.ts             # Tipos de la API
│   │   └── README.md            # Especificación del slice (rutas, permisos, endpoints, reglas)
│   └── shared/                  # Transversal, sin conocer el dominio
│       ├── api/                 # http.ts (fetch + cookies + CSRF + refresh), auth.ts, documents.ts
│       ├── components/          # UI kit: UiButton, UiModal, UiField, NavIcon, BrandMark, DocumentList
│       ├── hooks/               # Composables: useAuth (sesión compartida)
│       ├── styles/              # Tailwind (CSS-first, @theme)
│       └── utils/               # format, permissions, redirect
├── index.html
├── package.json
├── tsconfig.json / tsconfig.app.json / tsconfig.node.json
└── vite.config.ts
```

Módulos con pantallas: `auth`, `dashboard`, `assets`, `locations`, `inventory`, `incidents`, `maintenance`, `disposal`, `tickets`, `users`, `roles`, `history`, `procurement`, `software`, `profile`, `reports`, `bulk-import`. Componentes vive en la ficha del activo (`modules/components` solo tiene README). `documents` solo tiene su README y aparece deshabilitado en el menú (lista `PLANNED` de `MainLayout.vue`).

## Principios

1. **`modules/` es soberano:** cada módulo contiene sus páginas, componentes, tipos, llamadas a API y rutas. No importa de otro módulo ni de `app/`; lo compartido vive en `shared/`. Para enlazar a otro módulo se usa una ruta (`RouterLink to="/tickets?nuevo=1&assetId=5"`), no un import.
2. **Rutas descentralizadas:** cada módulo exporta `routes.ts` (default `RouteRecordRaw[]`). `app/router` las recoge con `import.meta.glob('../../modules/*/routes.ts')`; un módulo nuevo no requiere tocar `app/`. Las páginas se cargan con `() => import(...)` (un chunk por página).
3. **Menú desde las rutas:** la ruta principal de un módulo declara `meta.nav` (`label`, `icon`, `order`, `permission`); `MainLayout` arma el menú con `router.getRoutes()` y filtra por permisos efectivos de `/api/auth/me` (solo UX; la API valida).
4. **Spec por slice:** `modules/<modulo>/README.md` describe rutas, permisos, endpoints y reglas del slice. La especificación funcional completa sigue en `specs/<modulo>.spec.md`.
5. **`shared/` no conoce el dominio:** UI kit, cliente HTTP, composables genéricos y utilidades. `documents.ts` / `DocumentList.vue` viven aquí porque Documentos es transversal en el backend (`Features/Documents`, cualquier `ownerType`).
6. **`app/` orquesta:** layouts, router y bootstrap.

## Sesión y router

- Sesión en cookies HttpOnly; el JS nunca ve el token. `useAuth()` (`shared/hooks/useAuth.ts`) guarda en memoria el perfil con permisos de `GET /api/auth/me`; estado único a nivel de módulo (sin Pinia).
- Guard global (`router.beforeEach`): espera `loadSession()`; ruta sin `meta.public` y sin sesión → `/login?redirect=<ruta>`; ruta con `meta.permission` sin ese permiso → panel (`/`). Con sesión, `/login` redirige a `redirect` (solo rutas internas, `shared/utils/redirect.ts`).
- `http.ts`: CSRF (`X-CSRF-Token`) en métodos que escriben, refresh único ante 401 y, si la sesión se pierde, `/login?redirect=` conservando la ruta.

## Estilo y configuración

- **Tailwind CSS v4** CSS-first (`@import "tailwindcss"` y `@theme` en `src/shared/styles/index.css`). Sin `tailwind.config.js`.
- Tokens blanco/azul institucional: menú lateral navy (`--color-sidebar` `#0f2747`, sección activa como pestaña del color del contenido), navy `#1b396a` para marca y enlaces, azul de acción `#1d5fd6` (`--color-accent`) solo para botones principales y foco, tinte cielo `#eaf2fd` (`--color-muted`). `--color-priority-high` (`#c2410c`) para prioridad Alta de tickets. Lexend (display) + Source Sans 3 (texto). Plano, con bordes en vez de sombras.
- En componentes del UI kit, `class` del llamador se **suma** a la del componente (Vue): no pasar utilidades que choquen (p. ej. `px-5` sobre `UiButton`); para tamaños hay props (`BrandMark size`).
- ESLint: `@eslint/js` + `typescript-eslint` + `eslint-plugin-vue` (`flat/recommended`). TypeScript estricto; `vue-tsc -b` revisa tipos dentro de `.vue`.

## Convenciones

- Componentes `.vue` en PascalCase; UI kit con prefijo `Ui`.
- Composables con prefijo `use` en `shared/hooks/`.
- Tipos de API en `modules/<modulo>/types.ts`; nada de `any`. `PagedResult`, `CatalogItem`, `UserItem` se duplican por módulo (aislamiento).
- Concurrencia: las mutaciones envían `expectedVersion`; un 409 recarga la vista y avisa "otra persona modificó…".
- Variables de entorno: solo las expuestas con prefijo `VITE_`.

## Comandos

```bash
cd frontend
npm install
npm run dev       # servidor de desarrollo (Vite, proxy /api -> :5112)
npm run build     # vue-tsc -b && vite build
npm run lint      # eslint
```
