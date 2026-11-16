# Spec — Despliegue (sistema interno)

## Arquitectura

| Puerto | Servicio | Contenido |
| --- | --- | --- |
| `6010` | `web` (nginx, HTTPS) | Pantallas (SPA compilada). Toda ruta sin archivo devuelve `index.html` |
| `6011` | `web` (nginx, HTTPS) | Proxy a `api:8080`. Reemplaza `X-Forwarded-For` con la IP real |
| — | `api` (.NET) | Sin puerto publicado. Redes `front` (172.30.10.0/24) y `back` |
| — | `db` (PostgreSQL 18) | Sin puerto publicado. Solo red `back` (`internal`: sin salida a internet) |

Archivos: `database/docker-compose.prod.yml`, `database/.env.prod` (no se versiona; plantilla
`database/env.prod.example`), `database/certs/server.{crt,key}` (no se versiona),
`backend/Dockerfile`, `frontend/Dockerfile`, `frontend/nginx/default.conf.template`.

## Reglas

- Pantallas y API en orígenes distintos: la SPA se compila con `VITE_API_BASE_URL=https://<host>:6011`
  y la API acepta CORS con credenciales solo de `Cors:AllowedOrigins` (`https://<host>:6010`).
  Expone `Content-Disposition` para el nombre de las descargas.
- Cookies `Secure`; mismo host en ambos puertos = mismo sitio, así que `SameSite` y el CSRF de doble
  envío funcionan igual que en desarrollo.
- `ForwardedHeaders:KnownNetworks`: solo se cree el `X-Forwarded-For` que llega de la red de nginx.
  El límite de login (10/min) cuenta por la IP real y no se puede falsear con la cabecera.
- `GET /api/health` (anónimo): `{status, database}`; 503 si la BD no responde. Sin datos del inventario.
- `api` corre como usuario `app`, sistema de archivos de solo lectura (escribe en `/data/documents` y `/tmp`).
- La imagen de `api` instala fuentes (DejaVu) para QuestPDF.
- nginx: CSP, `X-Frame-Options DENY`, `nosniff`, `Referrer-Policy`. Sin HSTS mientras el certificado
  sea de la CA propia (un equipo sin el certificado raíz no podría ni aceptar la advertencia).
- Los archivos compilados van en `/static/` (`build.assetsDir`): `/assets/:id` es ruta de la SPA.

## Inicialización de la BD

`database.sql` y luego los scripts de `scripts/README.md` en su orden, sin los marcados como
"solo desarrollo". (Lo automatiza el CLI `tecnm`, bloque 3.)

## Verificación (local, 2026-11-09)

- [x] `/api/health` 200; `/`, `/assets/3`, `/tickets/5` devuelven `index.html`; `/static/*` con caché inmutable
- [x] Cabeceras de seguridad presentes; preflight de otro origen sin `Access-Control-Allow-Origin`
- [x] Login entre 6010 y 6011: cookies `secure`; POST sin CSRF 403, con CSRF 201
- [x] PDF generado en Linux (fuentes) con `Content-Disposition` expuesto
- [x] 12 logins con `X-Forwarded-For` falsos: 429 al 10.º (la cabecera del cliente se ignora)
- [x] `api:8080` y `db:5432` sin publicar; `db` sin salida a internet; `/app` de solo lectura
- [x] CLI `tecnm`: instalación desde cero, respaldo, restauración, actualización, certificado y sandbox (ver abajo)

## CLI `tecnm` (`backend/tools/TecnmCli`)

Dos modos con la misma lógica (`Deployment`): ninguna operación pregunta nada; el menú (`Menu.cs`) pide los datos y
confirma, el modo directo (`Commands.cs`) los toma de las opciones. Un requisito que falta corta con
`CliException` (código estable + pista); una operación que falla devuelve `false`.

- **Interactivo:** `tecnm` o `tecnm menu`. Panel con estado (en línea, URL, días de certificado, último respaldo, IA),
  opciones por sección (Servicio, Respaldos, Instalación, Desarrollo), flechas ↑↓ + Enter, atajo por tecla, Esc
  vuelve; bajo la opción elegida muestra su comando directo. Mismas teclas que antes. Sin terminal (stdin
  redirigido) lista las opciones y lee la tecla como texto.
- **Directo:** `tecnm <comando> [opciones]`, sin preguntas. `--json`: nada en pantalla mientras trabaja (la salida
  de docker va a `messages`) y un solo objeto al final:
  `{"command","ok","exitCode","data","messages":[{"level","text"}],"error":{"code","message","hint"}|null}`.

| Código de salida | Significado | `error.code` |
| --- | --- | --- |
| 0 | Bien | — |
| 1 | La operación falló (o `status`: no responde) | `OPERATION_FAILED`, `UNHEALTHY`, `NO_CERTIFICATE`, `AI_UNHEALTHY` |
| 2 | Uso incorrecto: comando, opción o dato inválido o faltante | `USAGE`, `BACKUP_NOT_FOUND` |
| 3 | Operación que reemplaza datos o reinicia sin `--yes` | `CONFIRMATION_REQUIRED` |
| 4 | Falta un requisito | `DOCKER_UNAVAILABLE`, `NOT_INSTALLED`, `ENV_INVALID`, `ROOT_NOT_FOUND`, `CLI_NOT_PUBLISHED` |

| Menú | Comando directo | Qué hace |
| --- | --- | --- |
| 1 · 2 · 3 | `start` · `stop` · `restart` | `docker compose up -d` / `stop` / `restart` y espera `/api/health` |
| 4 | `status` · `logs [--tail N]` | Contenedores (`compose ps --format json`), salud, URL, certificado, último respaldo, IA; logs de api y web |
| 9 | `install [--host IP]` | Crea `database/.env.prod` con secretos aleatorios (no los muestra; host por omisión: la IP detectada), emite la CA propia y el certificado, levanta los contenedores y aplica `database.sql` + scripts del README (sin los de desarrollo). El menú ofrece crear el administrador al final |
| 5 | `update --yes` | Respaldo, `docker compose build`, `up -d`, scripts otra vez (idempotentes) y espera `/api/health`. Hacer `git pull` antes |
| 6 | `backup` · `backup list` | `pg_dump -Fc` + `tar` de documentos en `database/backups/` (no se versiona); conserva 14 |
| 7 | `restore --backup FECHA --yes` | Respaldo de seguridad, `pg_restore --single-transaction` y documentos. El menú pide escribir `RESTAURAR` |
| 8 | `admin --user U --email E [--first --last] --password-stdin` | Crea o reactiva un ADMIN; contraseña 12+ por stdin (por tubería, o escrita sin mostrarse), BCrypt, valores por variables de psql. Escribe directo en la BD: no queda en `audit_log` |
| C | `cert [status]` · `cert renew --yes` · `cert help` | Regenera el del servidor con la misma CA; instrucciones para instalar `ca.crt` (Windows, Android, iPhone) o usar uno de sistemas |
| P | `schedule-backup` | Respaldo diario 02:00 (`tecnm respaldar`): Programador de tareas (Windows) o crontab (Linux) |
| I | `ai status \| install [--from CARPETA] \| test \| disable` | Modelo local (ver abajo) |
| S | `sandbox` | En el equipo de desarrollo: BD de `database/docker-compose.yml`, `dotnet run` (5112) y vite (5173); Enter los detiene (sin terminal: Ctrl+C) |

Alias de antes: `respaldar`, `estado`, `iniciar`, `detener`, `reiniciar`, `actualizar`, `restaurar`, `ayuda`,
`ia instalar|estado|probar|desactivar`, `--desde`. La tarea programada sigue llamando `tecnm respaldar`.

Instalación: `database/instalar.ps1` (Windows) y `database/instalar.sh` (Linux) compilan el CLI en
`mcr.microsoft.com/dotnet/sdk:10.0` sobre una copia (los `obj/` del contenedor no tocan los del repo; en Linux con el
usuario actual), un solo archivo `win-x64` / `linux-x64` / `linux-arm64` en `backend/tools/TecnmCli/dist`, y lo
registran: Windows en el PATH del usuario + `TECNM_ROOT`; Linux con un lanzador en `/usr/local/bin` (o
`~/.local/bin`) que fija `TECNM_ROOT`. El CLI también encuentra la raíz subiendo desde su carpeta.

Se niega a operar si `.env.prod` falta o tiene secretos débiles (contraseña < 24, JWT < 64).
La CA no publica lista de revocación (igual que mkcert): los navegadores la aceptan; `curl` en
Windows necesita `--ssl-no-revoke`. `ca.key` no sale del servidor.

Verificado (local, 2026-11-09): instalación desde volúmenes vacíos, login con el admin creado
validando solo contra `ca.crt`, respaldo → cambio → restauración (vuelve al estado del respaldo),
cancelación sin `RESTAURAR`, actualización sobre BD existente, regenerar certificado (CA intacta),
reactivar admin inactivo, contraseñas cortas/distintas rechazadas, sandbox arranca y cierra sus
procesos, binario linux-x64 compilado en el contenedor del SDK.

## Asistente IA (modelo local)

Servicio `ia` del compose: `ghcr.io/ggml-org/llama.cpp:server-b11960` con `Qwen3.5-2B-Q4_K_M.gguf` y su módulo
de visión `mmproj-F16.gguf` (`unsloth/Qwen3.5-2B-GGUF` en Hugging Face, Apache 2.0; tamaños y SHA256 fijados en
`backend/tools/TecnmCli/AiModel.cs`). Lee texto e imágenes (oficios escaneados). Es opcional: perfil `ia`,
encendido con `COMPOSE_PROFILES=ia` en `.env.prod`.

- Sin puerto publicado y solo en la red `back` (interna, sin internet): únicamente la API le habla (`http://ia:8080`).
- `--ctx-size 8192 --parallel 2`: dos turnos de 4096 (el asistente responde aunque una carga masiva esté corriendo).
  `mem_limit: 3584m` (pico medido ~3.15 GB con ambos turnos trabajando y leyendo una imagen), caché KV en 8 bits y `--image-max-tokens 512`
  (misma precisión, transcribe en 23 s en lugar de 36 s). Las versiones IQ3 del 2B y el 0.8B se probaron: rompen la extracción. Hilos `IA_THREADS` (4 por omisión). Contenedor de solo lectura.
- El modelo vive en `database/models/` (no se versiona). `tecnm ai install` descarga cada archivo a `.part` y solo lo
  acepta si tamaño y SHA256 coinciden; `--from <carpeta>` hace lo mismo con archivos copiados (servidor sin internet).
  Borra los `.gguf` de modelos anteriores.
- `tecnm ai test` empareja encabezados de ejemplo con las columnas de la plantilla (JSON con esquema) y mide tokens/s.
- `tecnm ai disable` borra el contenedor y apaga el perfil; el modelo se conserva.
