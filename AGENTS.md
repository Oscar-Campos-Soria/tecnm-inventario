# AGENTS.md — Sistema de inventario de activos tecnológicos (TecNM)

## Proyecto
Sistema web para gestionar el ciclo de vida de activos tecnológicos (alta, ubicación, asignación,
movimientos, incidentes, mantenimiento, baja formal, auditoría). Idioma de trabajo: español.

## Stack
- Backend: C#, .NET 10, ASP.NET Core Web API, EF Core Database-First, Mediator (martinothamar), FluentValidation.
  Archivos: ClosedXML (Excel), QuestPDF (PDF, licencia Community: `QuestPDF.Settings.License` en Program.cs), QRCoder (QR).
- BD: PostgreSQL 18 en Docker (contenedor tecnm_inventario_db, puerto 5433, base tecnm_inventario,
  usuario tecnm_dev, esquema `inventario`). Credenciales en database/.env (no imprimir ni commitear).
- Frontend: Vue 3 (`<script setup lang="ts">`), TypeScript, Vite, Tailwind v4, vue-router. Gestor de paquetes: **npm**.
  Rutas descentralizadas (`modules/<modulo>/routes.ts`, menú en `meta.nav`) y spec por slice (`modules/<modulo>/README.md`).
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
13. **Toda escritura de negocio nueva implementa `ITransactionalCommand` y llama
    `IAuditLogService.LogAsync` con snapshot antes/después; `action` solo INSERT/UPDATE/DELETE.**
    El `TransactionBehavior` abre y confirma/revierte la transacción; el handler no la abre.
14. **Los hooks de `ITransactionHooks` solo funcionan cuando `TransactionBehavior` es el dueño de la
    transacción: un handler que abre su propia transacción NO puede usar hooks** (no se ejecutarán).
15. **Un UPDATE sin cambios reales (con `ExpectedVersion` correcta) no escribe en `audit_log`;
    con `ExpectedVersion` vieja devuelve 409**, aunque los datos enviados sean idénticos.
16. **Los snapshots de auditoría son proyecciones explícitas; nunca incluyen secretos**
    (`password_hash`, tokens, claves de licencia).
17. **Build y test siempre con `backend/TecnmInventario.slnx`.**

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
- Frontend: frontend/src/app (layouts, router, App.vue, main.ts), frontend/src/modules/<modulo>/ (routes.ts, pages, components, api, types, README.md),
  frontend/src/shared (api, assets, components, hooks, styles, utils). Tailwind v4 sin tailwind.config.js.
- Raíz del repo: backend/, frontend/, database/ (docker-compose), scripts/ (SQL), specs/, plans/, database.sql,
  DIAGRAMA_ER.md, BACKEND_ARCHITECTURE.md, FRONTEND_ARCHITECTURE.md, README.md, AGENTS.md, .obsidian/.
- No crear carpetas de herramientas de agentes en la raíz ni versionarlas.

## Deuda técnica

- Despliegue (specs/deployment.spec.md): `database/docker-compose.prod.yml`, nginx HTTPS en 6010 (pantallas) y 6011 (proxy a la API); api y db sin puertos. CLI `tecnm` en backend/tools/TecnmCli (instalar, actualizar, respaldos, administrador, certificado con CA propia, sandbox). Probado en local; `database/.env.prod`, `database/certs/` y `database/backups/` de prueba (localhost, admin zz_cli_admin) no se versionan. `database.sql` ya se puede re-ejecutar (los triggers dinámicos ignoran vistas).
- Tests de integración con PostgreSQL real vía Testcontainers (`Category=Integration`); la suite rápida
  sigue en InMemory (`--filter "Category!=Integration"`). Sin Docker se omiten salvo
  `REQUIRE_DOCKER_TESTS=1`, que los convierte en fallo.
- Cuenta técnica `system`: inactiva (scripts/deactivate-system-account.sql); firma el cierre automático de tickets, nadie inicia sesión con ella.
- Datos de prueba en la BD de desarrollo (usuario prueba_movimientos id 5 ADMIN, ubicación PRUEBA-LAB id 4, activos 3 a 7): desactivar, no borrar, porque audit_log los referencia. Usuarios zz_admin_test (id 6, ADMIN) y zz_tecnico_test (id 7, TECNICO): creados en el checkpoint 1 de Incidentes y desactivados (status inactivo). test_responsable y test_usuario (RESPONSABLE_AREA y USUARIO_GENERAL) se crean con scripts/seed-test-users.sql (hash por variable psql).
- Sesión: login limitado a 10 intentos por minuto por IP (429; detrás de nginx usa la IP real vía `ForwardedHeaders:KnownNetworks`). Limpieza diaria de familias de `refresh_tokens` vencidas hace más de 30 días (`RefreshTokenCleanupService`). Perfil propio en `/profile` (`/api/auth/profile`: cambio de contraseña que conserva la sesión actual y desvincular Microsoft/Google). Login federado sin probar contra app registrations reales.
- USUARIO_GENERAL solo ve lo suyo (scripts/restrict-general-user.sql): sin `assets.view` ni `incidents.create`; ve sus equipos asignados en "Mis equipos" (`GET /api/assets/mine`, `/my-assets`) y sus tickets; un ticket solo puede ligar un equipo suyo. RESPONSABLE_AREA sin cambios.
- Usuarios de demostración activos en la BD de desarrollo: demo_admin (id 9, ADMIN) y demo_usuario (id 10, USUARIO_GENERAL); contraseñas entregadas al usuario, no viven en el repo. Desactivar al terminar las pruebas.
- Bajas, mantenimiento, búsqueda de activos y levantamiento físico implementados (specs/disposal, maintenance, inventory). Cancelar conteo, mantenimientos programados y correctivo que resuelve su incidente en un paso: hechos. Acta de baja en PDF (`GET /api/disposals/{id}/act.pdf`) y etiquetas QR en hoja carta (`GET /api/assets/labels.pdf?ids=`, máx. 90; el QR lleva el código de inventario): hechos. En Linux/Docker QuestPDF necesita fuentes instaladas. Datos de prueba: activos TEC-PRUEBA-TK-01..06 (01 y 02 dados de baja), incidentes, mantenimientos y conteos 1-2.
- Tickets (mesa de ayuda): backend y frontend listos (specs/tickets.spec.md, frontend/src/modules/tickets). Contador de "sin asignar" en el menú (`meta.nav.badge`) y ubicación en el formulario: hechos. Usuario zz_tk_admin (id 8, ADMIN) creado para la verificación en navegador y desactivado; activo TEC-PRUEBA-TK-01 (id 1, queda CON_FALLA por el incidente de prueba) y tickets 5-13 son datos de prueba. Usuarios zz_tk_tecnico, zz_tk_usuario_a, zz_tk_usuario_b (ids 5-7) creados para la verificación y desactivados; tickets 1-4 son datos de prueba (el 4 se cerró con resolved_at adelantado a mano para probar el cierre automático).
- GET /api/users (selector) devuelve como máximo 50 activos; ya acepta `roles=ADMIN,TECNICO`. La administración usa GET /api/users/manage (paginado).
- Componentes internos en la ficha del activo (specs/components.spec.md, `components.manage`). Pendiente: editar y buscar por serie. Datos de prueba: componentes 1-2 (RAM, retirados) sobre el activo 5.
- Documentos (specs/documents.spec.md): sin página global; adjuntos en incidentes, tickets, baja, mantenimientos y compras, eliminar desde `DocumentList` y consolidado en la ficha (`GET /api/assets/{id}/documents`). Datos de prueba: documento 6 (MAINTENANCE 1) y 7 (ACQUISITION 1, borrado) sobre el activo 3.
- Carga masiva de activos desde Excel/CSV (specs/bulk-import.spec.md, `/bulk-import`, `assets.create`): revisar y luego confirmar, todo o nada. Datos de prueba: activos ZZ-CARGA-01 y ZZ-CARGA-02 (ids 7 y 8) y mantenimiento programado-realizado id 5 sobre el activo 5.
- Reportes en Excel y PDF (specs/reports.spec.md, `/reports`, `reports.view`: ADMIN y RESPONSABLE_AREA; TECNICO no lo tiene). Hasta 10,000 filas.
- Dashboard con métricas (specs/dashboard.spec.md): `GET /api/dashboard/summary` cacheado 60 s. Pendiente: gráficas históricas.
- Software y licencias con pantalla (specs/software.spec.md): licencias solo por máquina, permiso único `software.manage`. Pendiente: `asset_os_history`, asignación por usuario, selectores de programas con tope de 100. Datos de prueba: programa `ZZ Office Prueba` (id 1), licencia id 1, asignaciones 1-2, instalaciones 1-2 sobre el activo 5. Usuarios zz_sw_admin (id 12, ADMIN) y zz_sw_usuario (id 13, USUARIO_GENERAL) creados para la verificación y desactivados.
- Usuarios, Roles, Historial y Adquisiciones con pantalla (specs/users, roles, history, procurement). Permisos `users.manage` e `history.view` en scripts/add-admin-permissions.sql. La línea de tiempo del activo incluye sus compras vía `acquisition_assets`. Datos de prueba: usuario zz_tk_nuevo (id 11, desactivado), proveedor `ZZ Proveedor Prueba` (id 1), compra ZZ-OC-1 (id 1) y garantía ZZ-G-1 (id 1) sobre el activo 3.
- Asistente IA local para la carga masiva (specs/deployment.spec.md, "Asistente IA"): servicio `ia` (llama.cpp + Qwen3.5-2B Q4_K_M con visión) con perfil de compose, sin puerto, red `back`; `tecnm ia instalar|estado|probar|desactivar`. Backend de conversión hecho (specs/bulk-import-ai.spec.md: Excel, CSV, PDF con PdfPig, fotos y escaneos; trabajos en memoria). Pendiente: pantalla y botón "Pregúntale a la IA". RAM: ~1.3 GB en reposo, pico ~2.9 GB al leer una imagen. Usuario zz_ia_admin (id 14, ADMIN) creado para la verificación y desactivado.
- Datos de prueba del asistente IA (por la API, auditados): activos ZZ-IA-01..12 (ids 9-20, fabricantes y modelos de scripts/seed-assistant-demo.sql), incidentes 7-9 (ZZ-IA-01, 06, 09), tickets 15-16, asignaciones ZZ-IA-07 → demo_usuario y ZZ-IA-08 → zz_ia_usuario. Usuarios zz_ia_admin (id 14, ADMIN) y zz_ia_usuario (id 15, USUARIO_GENERAL) activos para pruebas en navegador: desactivar al terminar. Set de evaluación del asistente (63 preguntas) en el scratchpad de la sesión, no versionado.
- Instalación desde un clon documentada en README.md (producción con el CLI; desarrollo con database/.env.example, user-secrets y scripts/seed-dev-admin.sql para el primer ADMIN).
