# TecNM Assets Management

Sistema web profesional para la **gestión, control, seguimiento e inventario de activos tecnológicos** del Tecnológico Nacional de México.

Monorrepo modular: API REST (.NET 10) + SPA (React + TypeScript + Vite) + specs y planes de desarrollo en la raíz.

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core Web API, EF Core (PostgreSQL), MediatR, FluentValidation |
| Frontend | React 19, TypeScript, Vite 8, Tailwind CSS 4 |
| Base de datos | PostgreSQL 18 (Docker) |
| Autenticación | JWT (Bearer) + RBAC por permisos (`modulo.accion`) |

## Arranque rápido

```bash
# 1. Base de datos
cd database
cp .env.example .env        # si aplica; configura POSTGRES_PASSWORD
docker compose up -d
psql ... -f ../database.sql # esquema
psql ... -f ../scripts/seed-database.sql

# 2. Backend (requiere user-secrets con Jwt:Secret/Issuer/Audience)
cd ../backend
dotnet run --project TecnmInventario.Api.csproj

# 3. Frontend
cd ../frontend
npm install
npm run dev
```

## Estructura

```text
├── backend/        # API REST — Arquitectura Vertical Slice (Core / Data / Features / Common)
├── frontend/       # SPA React — modular (app / modules / shared)
├── database/       # docker-compose de PostgreSQL (infraestructura de BD)
├── scripts/        # Scripts SQL de seed e inicialización
├── specs/          # Especificaciones funcionales por módulo
├── plans/          # Hojas de ruta por fases
├── database.sql    # Esquema completo de PostgreSQL (autoritativo)
├── BACKEND_ARCHITECTURE.md
├── FRONTEND_ARCHITECTURE.md
└── DIAGRAMA_ER.md
```

## Fases de desarrollo

1. **FASE 1 — Base de datos y autenticación** (`plans/01-database-and-auth-plan.md`)
2. **FASE 2 — Núcleo de activos** (`plans/02-assets-core-plan.md`)
3. **FASE 3 — SPA frontend** (`plans/03-frontend-spa-plan.md`)

## Decisiones y desviaciones de la plantilla

- **Nombre del ensamblado:** se conserva `TecnmInventario.Api` (y su `UserSecretsId`) para no invalidar los user-secrets que contienen `Jwt:Secret`. El nombre de producto es *TecNM Assets Management*.
- **`backend/Common/`:** capa transversal (autorización, auditoría, middleware, paginación, seguridad, almacenamiento) no contemplada en la plantilla, pero requerida por el código existente.
- **`database/`:** solo infraestructura de BD (`docker-compose.yml`, `.env`). Los SQL migraron a `database.sql` (raíz) y `scripts/`.
- **`backend/tests/` y `backend/tools/PasswordHasher`:** se conservan (utilidades de desarrollo).
- **Tailwind v4:** configuración CSS-first (`@theme` en `frontend/src/shared/styles/index.css`); no se usa `tailwind.config.js`.
