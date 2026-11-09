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
- [ ] CLI `tecnm` (instalar, respaldos, certificado con CA propia, sandbox con `npm run dev`)
