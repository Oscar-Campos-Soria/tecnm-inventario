# AGENTS.md — Sistema de inventario de activos tecnológicos (TecNM)

## Proyecto
Sistema web para gestionar el ciclo de vida de activos tecnológicos (alta, ubicación, asignación,
movimientos, incidentes, mantenimiento, baja formal, auditoría). Idioma de trabajo: español.

## Stack
- Backend: C#, .NET 10, ASP.NET Core Web API, EF Core Database-First, Mediator (martinothamar), FluentValidation.
- BD: PostgreSQL 18 en Docker (contenedor tecnm_inventario_db, puerto 5433, base tecnm_inventario,
  usuario tecnm_dev, esquema `inventario`). Credenciales en database/.env (no imprimir ni commitear).
- Frontend: React 19, TypeScript, Vite, Tailwind v4, react-router-dom. Gestor de paquetes: **npm**.
- Seguridad: JWT (solo el rol va en el token) + permisos por rol resueltos en servidor.

## Estructura real del repo
- backend/Common (Audit, Authorization, Behaviors, Deletion, Domain, Errors, Middleware, Pagination, Security, Storage)
- backend/Core (Contracts, Entities, Entities/Extensions), backend/Data (DbContext)
- backend/Features/<Modulo>/<CasoDeUso>/ -> Command|Query + Validator + Handler + Response
- frontend/src/{app,modules,shared}; cada módulo en frontend/src/modules/<modulo>/
- scripts/ = SQL de seeds y permisos (idempotentes). database.sql (raíz) = esquema autoritativo.
- Leer BACKEND_ARCHITECTURE.md, FRONTEND_ARCHITECTURE.md, specs/ y plans/ antes de proponer cambios.
- Solución: backend/TecnmInventario.slnx. API: backend/TecnmInventario.Api.csproj. Tests: backend/tests/TecnmInventario.Api.Tests. Utilidad: backend/tools/PasswordHasher.

## Reglas obligatorias
1. Propón el diseño y espera mi aprobación antes de escribir código de negocio nuevo.
2. NO hagas `git commit` ni `git push` sin que yo lo autorice explícitamente.
3. Controllers delgados: solo despachan a IMediator. Nunca expongas entidades EF; usa DTOs de Response.
4. Toda escritura de negocio + IAuditLogService.LogAsync va en UNA transacción explícita
   (BeginTransactionAsync/CommitAsync). audit_log solo acepta action INSERT, UPDATE o DELETE.
5. Reglas de negocio violadas -> BusinessRuleViolationException (400). Sin textos mágicos:
   usa AssetStatusCodes / IncidentStatusCodes (Common/Domain).
6. Concurrencia optimista con `Version` (409 en conflicto). Borrados con ISoftDeleteAuditor.
7. Permisos nuevos: script idempotente en scripts/ (ON CONFLICT DO NOTHING) y registrarlo en scripts/README.md.
8. No toques archivos generados del DbContext sin un script que verifique que el marcador aparece exactamente una vez.
9. Nombres de tablas, columnas, entidades y DbSet se leen del código o de database.sql; no se adivinan.
10. No agregues librerías nuevas (frontend o backend) sin proponerlo y que yo apruebe.
11. Tras cada cambio de backend: borrar bin/obj, `dotnet restore`, `dotnet build`, reiniciar la API
    (`pkill -f TecnmInventario.Api` y `dotnet run`; puerto 5112). Frontend: `npm run build` y `npm run lint`.
12. Verifica con evidencia (curl/psql, estado final del activo y filas de audit_log), no con resúmenes.

## Máquina de estados del activo
DISPONIBLE, ASIGNADO, EN_MANTENIMIENTO, CON_FALLA, DADO_DE_BAJA (terminal).
- Mover: solo DISPONIBLE o ASIGNADO. Asignar: solo DISPONIBLE y sin asignación activa.
- Devolver asignación: activo -> DISPONIBLE, salvo que tenga incidentes abiertos.
- Incidente nuevo -> CON_FALLA; EN_REPARACION -> EN_MANTENIMIENTO; al resolver el último abierto -> DISPONIBLE
  (o ASIGNADO si hay asignación vigente). Respetar AssetStatusTransitions.

## Skills
- Disponibles (globales, en ~/.claude/skills): frontend-design, ponytail, ui-ux-pro-max.
- Para pantallas del frontend usa frontend-design y ui-ux-pro-max.
- Antes de usar una skill, lee su SKILL.md y dime en una línea qué hace.

## Cómo reportar
Al terminar cada bloque entrega un informe: qué se hizo, archivos modificados, salida de build/tests,
evidencia SQL, y "Hecho / Pendiente / Riesgos".

## Estructura objetivo (obligatoria, no reorganizar por cuenta propia)
- Backend plano en backend/: Common/, Core/{Contracts,Entities}, Data/, Features/<Modulo>/, Properties/, tests/, tools/, Program.cs.
- Cada controller vive dentro de su feature (Features/<Modulo>/<Modulo>Controller.cs); no existe carpeta Controllers/ global.
- Cada caso de uso: Features/<Modulo>/<CasoDeUso>/{Command|Query, Validator, Handler, Response}. No usar Services genéricos.
- Frontend: frontend/src/app (layouts, router, App, main), frontend/src/modules/<modulo>/ (pages, components, hooks, api, types),
  frontend/src/shared (api, assets, components, hooks, styles, utils). Tailwind v4 sin tailwind.config.js.
- Raíz del repo: backend/, frontend/, database/ (docker-compose), scripts/ (SQL), specs/, plans/, database.sql,
  DIAGRAMA_ER.md, BACKEND_ARCHITECTURE.md, FRONTEND_ARCHITECTURE.md, README.md, AGENTS.md, .obsidian/.
- No crear carpetas de herramientas de agentes en la raíz ni versionarlas.

## Deuda técnica

- 4 errores de lint preexistentes (AssetsPage.tsx, useAuth.tsx, AssetDetailPage.tsx líneas 60 y 63).
- 3 tests omitidos pendientes de Testcontainers en Fase 4.
- Datos de prueba en la BD de desarrollo (usuario prueba_movimientos id 5 ADMIN, ubicación PRUEBA-LAB id 4, activos 3 a 7): desactivar, no borrar, porque audit_log los referencia.
