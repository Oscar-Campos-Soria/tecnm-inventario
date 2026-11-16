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

En Windows, la instalación de producción es en **PowerShell**; los comandos de desarrollo de este README son de **Git Bash**.

---

## Instalación en un servidor (producción)

Todo lo hace el CLI `tecnm`: genera secretos aleatorios, emite el certificado HTTPS, levanta los contenedores, aplica
el esquema y los scripts, y crea el primer administrador. El instalador lo compila dentro de un contenedor (el
servidor no necesita .NET) y deja el comando `tecnm` disponible en cualquier terminal.

**Windows (PowerShell)** — requiere Docker Desktop con WSL2:

```powershell
git clone <url-del-repo> tecnm-inventario
cd tecnm-inventario
powershell -ExecutionPolicy Bypass -File database\instalar.ps1                # pregunta lo necesario
powershell -ExecutionPolicy Bypass -File database\instalar.ps1 -HostName 10.0.0.5
```

**Linux (Bash)** — requiere Docker Engine con el plugin compose:

```bash
git clone <url-del-repo> tecnm-inventario
cd tecnm-inventario
bash database/instalar.sh                  # pregunta lo necesario
bash database/instalar.sh --host 10.0.0.5
```

Los dos hacen lo mismo: revisan Docker, compilan `tecnm`, lo registran (Windows: PATH del usuario y `TECNM_ROOT`;
Linux: `/usr/local/bin/tecnm`, o `~/.local/bin/tecnm` sin sudo), ejecutan `tecnm install` y, si hay una persona en
la terminal, ofrecen crear el primer administrador. Volver a ejecutarlos es seguro: recompilan el CLI y la
instalación se salta lo que ya existe. `-SoloCli` / `--solo-cli` solo actualiza el comando `tecnm`.

Al terminar:

- **Pantallas:** `https://<IP-o-nombre>:6010` · **API:** `https://<IP-o-nombre>:6011`. La BD y la IA no tienen puertos publicados.
- Instala el certificado raíz (`database/certs/ca.crt`) en cada equipo para quitar la advertencia: `tecnm cert help`.
- Programa el respaldo diario: `tecnm schedule-backup` (Programador de tareas en Windows, crontab en Linux).

### Administrar el sistema

`tecnm` (o `tecnm menu`) abre el menú: panel de estado, flechas ↑↓ y Enter, o la tecla de cada opción. Cada opción
muestra abajo su comando directo. Mismos comandos en Windows y Linux:

| Tarea | Comando |
| --- | --- |
| Ver si está en línea | `tecnm status` (sale con 1 si no responde) |
| Iniciar / detener / reiniciar | `tecnm start` · `tecnm stop` · `tecnm restart` |
| Ver logs | `tecnm logs [--tail 200]` |
| Actualizar (después de `git pull`) | `tecnm update --yes` (respalda antes) |
| Respaldar / listar / restaurar | `tecnm backup` · `tecnm backup list` · `tecnm restore --backup <fecha> --yes` |
| Administrador | `tecnm admin --user admin --email <correo> --password-stdin` |
| Certificado | `tecnm cert` · `tecnm cert renew --yes` · `tecnm cert help` |
| Asistente IA (opcional) | `tecnm ai install [--from <carpeta>]` · `tecnm ai status` · `tecnm ai test` · `tecnm ai disable` |
| Todo | `tecnm help` |

**Para scripts y agentes:** ningún comando directo pregunta nada; `--json` escribe un solo objeto
(`command`, `ok`, `exitCode`, `data`, `messages`, `error {code, message, hint}`); lo que reemplaza datos o reinicia
(`update`, `restore`, `cert renew`) exige `--yes`. Códigos de salida: `0` bien · `1` falló · `2` uso incorrecto ·
`3` falta `--yes` · `4` falta un requisito (Docker, instalación). La contraseña nunca va como argumento:
`echo "$PASS" | tecnm admin --user admin --email a@b.mx --password-stdin --json`.

**Asistente IA (opcional):** `tecnm ai install` descarga el modelo (~1.9 GB), comprueba su huella SHA256 y lo
enciende. Sin internet: copia los dos `.gguf` por USB y usa `tecnm ai install --from <carpeta>`. Si está apagado,
el sistema funciona igual sin la IA. Los nombres de antes (`tecnm respaldar`, `estado`, `ia instalar --desde`…)
siguen funcionando.

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
├── frontend/       # SPA Vue 3 — app / modules / shared, rutas por módulo (modules/assistant: botón "Pregúntale")
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
- **Asistente "Pregúntale"** ([`specs/assistant.spec.md`](specs/assistant.spec.md)): botón flotante en todas las
  páginas. Responde con datos actuales usando herramientas de solo lectura sobre las consultas existentes, con los
  permisos del rol del usuario (no genera SQL), recuerda la conversación para preguntas de seguimiento, explica cómo se
  hacen las cosas con el manual integrado y muestra bajo cada respuesta qué consultó y los datos reales.
- Si el modelo está apagado, el botón y la conversión con IA no aparecen y el resto del sistema funciona igual.

## Decisiones y desviaciones de la plantilla

- **Nombre del ensamblado:** se conserva `TecnmInventario.Api` (y su `UserSecretsId`) para no invalidar los user-secrets que contienen `Jwt:Secret`. El nombre de producto es *TecNM Assets Management*.
- **`backend/Common/`:** capa transversal (autorización, auditoría, middleware, paginación, seguridad, almacenamiento, cliente de IA) no contemplada en la plantilla, pero requerida por el código existente.
- **`database/`:** infraestructura (`docker-compose.yml`, `docker-compose.prod.yml`, `.env`). Los SQL viven en `database.sql` (raíz) y `scripts/`.
- **`backend/tests/` y `backend/tools/`:** pruebas y utilidades (CLI `tecnm`, `PasswordHasher`).
- **Tailwind v4:** configuración CSS-first (`@theme` en `frontend/src/shared/styles/index.css`); no se usa `tailwind.config.js`.
