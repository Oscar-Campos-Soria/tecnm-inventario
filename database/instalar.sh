#!/usr/bin/env bash
# Instala TecNM Inventario en Linux y deja el comando "tecnm" disponible en cualquier terminal.
#   1. Revisa Docker y Docker Compose.
#   2. Compila tecnm dentro de un contenedor del SDK de .NET (el servidor solo necesita Docker y Git).
#   3. Crea el lanzador /usr/local/bin/tecnm (o ~/.local/bin/tecnm sin sudo) con TECNM_ROOT.
#   4. Ejecuta "tecnm install" (y, si hay una persona, ofrece crear el primer administrador).
# Se puede volver a ejecutar: recompila el CLI y la instalación se salta lo que ya existe.
#
# Uso: database/instalar.sh [--host IP] [--solo-cli]
#   --host IP     IP o nombre con el que entrarán los usuarios (por omisión, la IP detectada)
#   --solo-cli    solo compila y registra el comando tecnm (actualizar el CLI sin tocar el sistema)
set -euo pipefail

# Mismos códigos de salida que el CLI: 1 falló, 2 uso incorrecto, 4 falta un requisito.
ok()   { printf '\033[32m✓ %s\033[0m\n' "$1"; }
step() { printf '\n\033[36m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31m✗ %s\033[0m\n' "$1"; exit "${2:-1}"; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$ROOT/backend/tools/TecnmCli/dist"
EXE="$DIST/tecnm"
HOST_NAME=""
SOLO_CLI=0

while [ $# -gt 0 ]; do
  case "$1" in
    --host) [ $# -ge 2 ] || fail "--host necesita un valor." 2; HOST_NAME="$2"; shift 2 ;;
    --solo-cli) SOLO_CLI=1; shift ;;
    -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
    *) fail "Opción desconocida: $1 (usa --help)." 2 ;;
  esac
done

step "Revisando Docker"
docker info --format '{{.ServerVersion}}' >/dev/null 2>&1 \
  || fail "Docker no responde. Instálalo o inícialo (usuario en el grupo docker o usa sudo) y vuelve a ejecutar." 4
docker compose version >/dev/null 2>&1 || fail "Falta Docker Compose v2 (paquete docker-compose-plugin)." 4
ok "Docker y Compose responden"

case "$(uname -m)" in
  x86_64|amd64) RID=linux-x64 ;;
  aarch64|arm64) RID=linux-arm64 ;;
  *) fail "Arquitectura no soportada: $(uname -m)." 4 ;;
esac

step "Compilando el comando tecnm (contenedor del SDK de .NET, la primera vez descarga ~800 MB)"
rm -rf "$DIST"
# Copia de trabajo: los obj/ del contenedor no se mezclan con los del repositorio. Con el usuario actual,
# para que dist/ no quede a nombre de root.
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -v "$ROOT":/src \
  mcr.microsoft.com/dotnet/sdk:10.0 sh -c "mkdir -p /tmp/w/tools && cp -r /src/backend/tools/TecnmCli /tmp/w/tools/ \
    && cp /src/backend/Directory.Packages.props /tmp/w/ \
    && rm -rf /tmp/w/tools/TecnmCli/bin /tmp/w/tools/TecnmCli/obj /tmp/w/tools/TecnmCli/dist \
    && dotnet publish /tmp/w/tools/TecnmCli/TecnmCli.csproj -c Release -r $RID --self-contained \
       -p:PublishSingleFile=true -o /src/backend/tools/TecnmCli/dist"
[ -x "$EXE" ] || fail "No se pudo compilar tecnm (revisa la salida de arriba)."
ok "tecnm en $DIST"

step "Registrando el comando tecnm"
LAUNCHER="#!/bin/sh
TECNM_ROOT=\"$ROOT\" exec \"$EXE\" \"\$@\"
"
if [ -w /usr/local/bin ]; then
  TARGET=/usr/local/bin/tecnm; printf '%s' "$LAUNCHER" > "$TARGET"; chmod 755 "$TARGET"
elif command -v sudo >/dev/null 2>&1 && sudo -n true 2>/dev/null; then
  TARGET=/usr/local/bin/tecnm; printf '%s' "$LAUNCHER" | sudo tee "$TARGET" >/dev/null; sudo chmod 755 "$TARGET"
else
  mkdir -p "$HOME/.local/bin"; TARGET="$HOME/.local/bin/tecnm"; printf '%s' "$LAUNCHER" > "$TARGET"; chmod 755 "$TARGET"
  case ":$PATH:" in *":$HOME/.local/bin:"*) ;; *) echo "! Agrega ~/.local/bin a tu PATH (por ejemplo en ~/.profile)." ;; esac
fi
ok "Lanzador en $TARGET"

if [ "$SOLO_CLI" -eq 1 ]; then ok "Listo: solo se actualizó el CLI."; exit 0; fi

step "Instalando el sistema"
if [ -n "$HOST_NAME" ]; then "$TARGET" install --host "$HOST_NAME"; else "$TARGET" install; fi

if [ -t 0 ] && [ -t 1 ]; then
  read -r -p "¿Crear ahora el primer administrador? (s/n) [s]: " answer
  if [ -z "$answer" ] || [ "${answer#[sS]}" != "$answer" ]; then
    read -r -p "Usuario [admin]: " user; user="${user:-admin}"
    read -r -p "Correo: " email
    # La contraseña la pide tecnm sin mostrarla; nunca pasa como argumento.
    "$TARGET" admin --user "$user" --email "$email" --password-stdin
  fi
else
  echo "Crea el administrador con: echo '<contraseña>' | tecnm admin --user <usuario> --email <correo> --password-stdin"
fi

step "Listo"
echo 'Escribe "tecnm" para abrir el menú, o "tecnm status" para ver el estado.'
