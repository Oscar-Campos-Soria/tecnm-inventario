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

Menú en consola para el administrador del servidor; sin argumentos abre el menú, y
`tecnm respaldar | estado | iniciar | detener` sirve para tareas programadas.

Compilar en el servidor sin instalar .NET (solo Docker), desde la raíz del repo:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0   dotnet publish backend/tools/TecnmCli/TecnmCli.csproj -c Release -r linux-x64   --self-contained -p:PublishSingleFile=true -o backend/tools/TecnmCli/dist
backend/tools/TecnmCli/dist/tecnm
```

En Windows: `dotnet publish backend/tools/TecnmCli/TecnmCli.csproj -c Release -o backend/tools/TecnmCli/dist`.
El CLI busca la raíz del repo subiendo desde su carpeta (o `TECNM_ROOT`).

| Opción | Qué hace |
| --- | --- |
| 9 Instalar | Crea `database/.env.prod` con secretos aleatorios (no los muestra), emite la CA propia y el certificado para el host, levanta los contenedores, aplica `database.sql` + scripts del README (sin los de desarrollo) y crea el primer administrador |
| 5 Actualizar | Respaldo, `docker compose build`, `up -d`, scripts otra vez (idempotentes) y espera `/api/health`. Hacer `git pull` antes |
| 6 Respaldar | `pg_dump -Fc` + `tar` de documentos en `database/backups/` (no se versiona); conserva 14 |
| 7 Restaurar | Elige respaldo, pide escribir `RESTAURAR`, saca respaldo de seguridad, `pg_restore --single-transaction` y documentos |
| 8 Administrador | Crea o reactiva un ADMIN; contraseña oculta (12+), BCrypt, valores por variables de psql. Escribe directo en la BD: no queda en `audit_log` |
| C Certificado | Regenera el del servidor con la misma CA; instrucciones para instalar `ca.crt` (Windows, Android, iPhone) o usar uno de sistemas |
| P Programar | Respaldo diario 02:00: Programador de tareas (Windows) o crontab (Linux) |
| I Asistente IA | Modelo local de la carga masiva (ver abajo). También `tecnm ia instalar [--desde archivo.gguf] \| estado \| probar \| desactivar` |
| S Sandbox | En el equipo de desarrollo: BD de `database/docker-compose.yml`, `dotnet run` (5112) y vite (5173); Enter los detiene |

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
- El modelo vive en `database/models/` (no se versiona). `tecnm ia instalar` descarga cada archivo a `.part` y solo lo
  acepta si tamaño y SHA256 coinciden; `--desde <carpeta>` hace lo mismo con archivos copiados (servidor sin internet).
  Borra los `.gguf` de modelos anteriores.
- `tecnm ia probar` empareja encabezados de ejemplo con las columnas de la plantilla (JSON con esquema) y mide tokens/s.
- `tecnm ia desactivar` borra el contenedor y apaga el perfil; el modelo se conserva.
