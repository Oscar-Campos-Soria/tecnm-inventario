# TecNM Assets Management

Sistema web para la **gestión, control, seguimiento e inventario de activos tecnológicos** del Tecnológico Nacional
de México: alta, ubicación, asignación, movimientos, incidentes, mesa de ayuda, mantenimiento, bajas, levantamiento
físico, reportes y auditoría. Incluye un **asistente IA local** (sin internet) que responde preguntas sobre el
inventario y convierte archivos de cualquier formato a la carga masiva.

Monorrepo: API REST (.NET 10) + SPA (Vue 3 + TypeScript + Vite) + PostgreSQL en Docker + modelo de IA local (llama.cpp).

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core Web API, EF Core (PostgreSQL), Mediator, FluentValidation, ClosedXML, QuestPDF, PdfPig |
| Frontend | Vue 3, TypeScript, Vite, Tailwind CSS 4, vue-router |
| Base de datos | PostgreSQL 18 (Docker) |
| IA local | llama.cpp (`llama-server`) + Qwen3.5-2B con visión, en Docker, sin salida a internet |
| Autenticación | Cookies HttpOnly + CSRF, JWT con el rol, permisos por rol (`modulo.accion`) resueltos en servidor |

## Requisitos

| Para | Necesitas |
| --- | --- |
| Servidor (producción) | Docker con Compose y Git. Nada más: el CLI `tecnm` se compila dentro de un contenedor |
| Desarrollo | Docker, Git, [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 22+ y npm |
| Asistente IA (opcional) | ~2 GB de disco para el modelo y ~3.5 GB de RAM libres para su contenedor |

En Windows, los comandos de este README son de **Git Bash**.

---

## Instalación en un servidor (producción)

Todo lo hace el CLI `tecnm`: genera secretos aleatorios, emite el certificado HTTPS, levanta los contenedores, aplica
el esquema y los scripts, y crea el primer administrador.

```bash
git clone <url-del-repo> tecnm-inventario
cd tecnm-inventario

# 1. Compilar el CLI (Linux; solo necesita Docker)
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish backend/tools/TecnmCli/TecnmCli.csproj -c Release -r linux-x64 \
  --self-contained -p:PublishSingleFile=true -o backend/tools/TecnmCli/dist

# 2. Instalar: abre el menú y elige "9) Instalar por primera vez"
backend/tools/TecnmCli/dist/tecnm
```

En Windows: `dotnet publish backend/tools/TecnmCli/TecnmCli.csproj -c Release -o backend/tools/TecnmCli/dist` y
ejecuta `backend\tools\TecnmCli\dist\tecnm.exe`.

Al terminar:

- **Pantallas:** `https://<IP-o-nombre>:6010` · **API:** `https://<IP-o-nombre>:6011`. La BD y la IA no tienen puertos publicados.
- Instala el certificado raíz (`database/certs/ca.crt`) en cada equipo para quitar la advertencia: menú → `C`.
- Programa el respaldo diario: menú → `P`.

**Asistente IA (opcional):** `tecnm ia instalar` descarga el modelo (~1.9 GB), comprueba su huella SHA256 y lo
enciende. Sin internet: copia los dos `.gguf` por USB y usa `tecnm ia instalar --desde <carpeta>`.
`tecnm ia estado | probar | desactivar` para administrarlo. Si está apagado, el sistema funciona igual sin la IA.

Detalles (puertos, seguridad, respaldos, actualizar): [`specs/deployment.spec.md`](specs/deployment.spec.md).

---

## Instalación para desarrollo

Desde la raíz del repo clonado:

```bash
# 1. Base de datos (PostgreSQL en el puerto 5433)
cp database/.env.example database/.env        # edita POSTGRES_PASSWORD
docker compose -f database/docker-compose.yml --env-file database/.env up -d

# 2. Esquema y scripts, en el orden de scripts/README.md (se lee de ahí para no duplicarlo).
#    Incluye los datos de ejemplo de desarrollo; los que piden una variable (-v) van en el paso 4.
cd scripts
grep -oE '^psql "\$DATABASE_URL" -f [^ ]+' README.md | awk '{print $4}' | while read -r f; do
  echo "== $f"
  docker exec -i tecnm_inventario_db sh -c 'psql -q -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < "$f" || break
done
cd ..

# 3. Secretos de la API (user-secrets: no viven en el repo). Usa la contraseña de database/.env.
API=backend/TecnmInventario.Api.csproj
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=tecnm_inventario;Username=tecnm_dev;Password=<la-de-database/.env>" --project $API
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 64 | tr -d '\n')" --project $API
dotnet user-secrets set "Jwt:Issuer" "tecnm-inventario" --project $API
dotnet user-secrets set "Jwt:Audience" "tecnm-inventario" --project $API
dotnet user-secrets set "Storage:LocalPath" "$PWD/backend/storage/documents" --project $API

# 4. Primer administrador (admin_local) con la contraseña que elijas
HASH=$(dotnet run --project backend/tools/PasswordHasher -- "<tu-contraseña>" | tail -1)
docker exec -i tecnm_inventario_db sh -c "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -v ON_ERROR_STOP=1 -v hash='$HASH'" < scripts/seed-dev-admin.sql

# 5. API (http://localhost:5112) y pantallas (http://localhost:5173), en dos terminales
dotnet run --project backend/TecnmInventario.Api.csproj
cd frontend && npm install && npm run dev
```

Entra a **http://localhost:5173** con `admin_local`. La SPA llama a `/api` y Vite la envía a la API (mismo origen,
las cookies funcionan sin configurar CORS). Atajo: el CLI tiene un modo **sandbox** (menú → `S`) que levanta la BD,
la API y las pantallas juntas.

Usuarios de prueba de los otros roles (`test_responsable`, `test_usuario`): `scripts/seed-test-users.sql` con el mismo
`-v hash=...`.

### Asistente IA en desarrollo (opcional)

```bash
mkdir -p database/models && cd database/models
curl -L -O https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/Qwen3.5-2B-Q4_K_M.gguf
curl -L -O https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/mmproj-F16.gguf
sha256sum *.gguf     # deben coincidir con backend/tools/TecnmCli/AiModel.cs
cd ../..
docker compose -f database/docker-compose.yml --env-file database/.env --profile ia up -d ia
```

Queda en `127.0.0.1:8081` (`Ia:BaseUrl` en `appsettings.Development.json`). La API detecta si responde: si no, el
asistente y la conversión con IA se ocultan y el resto funciona igual.

### Pruebas

```bash
cd backend
dotnet test TecnmInventario.slnx --filter "Category!=Integration"   # rápidas (InMemory)
dotnet test TecnmInventario.slnx --filter "Category=Integration"    # PostgreSQL real con Testcontainers (requiere Docker)
cd ../frontend && npm run build && npm run lint
```

---

## Estructura

```text
├── backend/        # API REST — Vertical Slice: Common / Core / Data / Features/<Modulo>/<CasoDeUso>
│   ├── Features/Assistant/   # asistente IA: herramientas de solo lectura, orquestación, manual (RAG)
│   ├── tests/                # xUnit (InMemory + Testcontainers)
│   └── tools/                # CLI tecnm (despliegue) y PasswordHasher
├── frontend/       # SPA Vue 3 — app / modules / shared, rutas por módulo
├── database/       # docker-compose (desarrollo y producción), modelos de IA (no versionados)
├── scripts/        # SQL de seeds y permisos, idempotentes (orden en scripts/README.md)
├── specs/          # Especificaciones funcionales por módulo
├── plans/          # Hojas de ruta por fases
├── database.sql    # Esquema completo de PostgreSQL (autoritativo)
├── BACKEND_ARCHITECTURE.md
├── FRONTEND_ARCHITECTURE.md
└── DIAGRAMA_ER.md
```

## Roles

| Rol | Puede |
| --- | --- |
| ADMIN | Todo: usuarios, roles, bajas, historial, reportes, carga masiva |
| TECNICO | Alta, movimiento y asignación de activos; incidentes, tickets, mantenimiento y levantamiento físico |
| RESPONSABLE_AREA | Consulta activos, incidentes y reportes; levanta tickets |
| USUARIO_GENERAL | Solo lo suyo: "Mis equipos" (los que tiene asignados) y sus tickets |

## Asistente IA

- **Carga masiva con IA** ([`specs/bulk-import-ai.spec.md`](specs/bulk-import-ai.spec.md)): convierte un Excel, CSV,
  PDF o foto de un oficio a la plantilla oficial; la persona la revisa y la sube. La IA nunca escribe en la BD.
- **Asistente** ([`specs/assistant.spec.md`](specs/assistant.spec.md)): responde con datos actuales usando herramientas
  de solo lectura sobre las consultas existentes, con los permisos del rol del usuario; no genera SQL.

## Decisiones y desviaciones de la plantilla

- **Nombre del ensamblado:** se conserva `TecnmInventario.Api` (y su `UserSecretsId`) para no invalidar los user-secrets que contienen `Jwt:Secret`. El nombre de producto es *TecNM Assets Management*.
- **`backend/Common/`:** capa transversal (autorización, auditoría, middleware, paginación, seguridad, almacenamiento, cliente de IA) no contemplada en la plantilla, pero requerida por el código existente.
- **`database/`:** infraestructura (`docker-compose.yml`, `docker-compose.prod.yml`, `.env`). Los SQL viven en `database.sql` (raíz) y `scripts/`.
- **`backend/tests/` y `backend/tools/`:** pruebas y utilidades (CLI `tecnm`, `PasswordHasher`).
- **Tailwind v4:** configuración CSS-first (`@theme` en `frontend/src/shared/styles/index.css`); no se usa `tailwind.config.js`.
