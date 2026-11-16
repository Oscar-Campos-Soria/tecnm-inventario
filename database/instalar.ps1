<#
.SYNOPSIS
    Instala TecNM Inventario en Windows y deja el comando "tecnm" disponible en cualquier terminal.

.DESCRIPTION
    1. Revisa Docker y Docker Compose.
    2. Compila tecnm.exe dentro de un contenedor del SDK de .NET (el servidor solo necesita Docker y Git).
    3. Agrega el CLI al PATH del usuario y define TECNM_ROOT.
    4. Ejecuta "tecnm install" (y, si hay una persona, ofrece crear el primer administrador).
    Se puede volver a ejecutar: recompila el CLI y la instalación se salta lo que ya existe.

.PARAMETER HostName
    IP o nombre con el que entrarán los usuarios (sin https:// ni puerto). Por omisión, la IP detectada.

.PARAMETER SoloCli
    Solo compila y registra el comando tecnm (para actualizar el CLI sin tocar el sistema).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File database\instalar.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File database\instalar.ps1 -HostName 10.0.0.5
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File database\instalar.ps1 -SoloCli
#>
[CmdletBinding()]
param(
    [string]$HostName,
    [switch]$SoloCli
)

$ErrorActionPreference = 'Stop'
# Mismos códigos de salida que el CLI: 1 falló, 4 falta un requisito.
function Ok([string]$Text) { Write-Host "✓ $Text" -ForegroundColor Green }
function Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Fail([string]$Text, [int]$Code = 1) { Write-Host "✗ $Text" -ForegroundColor Red; exit $Code }

$Root = Split-Path -Parent $PSScriptRoot
$Dist = Join-Path $Root 'backend\tools\TecnmCli\dist'
$Exe = Join-Path $Dist 'tecnm.exe'

Step 'Revisando Docker'
docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { Fail 'Docker no responde. Instala o inicia Docker Desktop (con WSL2) y vuelve a ejecutar.' 4 }
docker compose version *> $null
if ($LASTEXITCODE -ne 0) { Fail 'Falta Docker Compose v2 (viene con Docker Desktop).' 4 }
Ok 'Docker y Compose responden'

Step 'Compilando el comando tecnm (contenedor del SDK de .NET, la primera vez descarga ~800 MB)'
if (Test-Path $Dist) {
    try { Remove-Item -Recurse -Force $Dist }
    catch { Fail 'tecnm está abierto en otra terminal: sal del menú (0) y vuelve a ejecutar.' 1 }
}
# Se compila sobre una copia: los obj/ del contenedor (rutas Linux) no se mezclan con los del repositorio.
$build = 'mkdir -p /w/tools && cp -r /src/backend/tools/TecnmCli /w/tools/ && cp /src/backend/Directory.Packages.props /w/ ' +
         '&& rm -rf /w/tools/TecnmCli/bin /w/tools/TecnmCli/obj /w/tools/TecnmCli/dist ' +
         '&& dotnet publish /w/tools/TecnmCli/TecnmCli.csproj -c Release -r win-x64 --self-contained ' +
         '-p:PublishSingleFile=true -o /src/backend/tools/TecnmCli/dist'
docker run --rm -v "${Root}:/src" mcr.microsoft.com/dotnet/sdk:10.0 sh -c $build
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Exe)) { Fail 'No se pudo compilar tecnm.exe (revisa la salida de arriba).' }
Ok "tecnm.exe en $Dist"

Step 'Registrando el comando tecnm'
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not $userPath) { $userPath = '' }
if (-not (($userPath -split ';') -contains $Dist)) {
    [Environment]::SetEnvironmentVariable('Path', ($userPath.TrimEnd(';') + ';' + $Dist).TrimStart(';'), 'User')
}
[Environment]::SetEnvironmentVariable('TECNM_ROOT', $Root, 'User')
$env:Path = "$env:Path;$Dist"
$env:TECNM_ROOT = $Root
Ok 'tecnm queda en el PATH (abre una terminal nueva para usarlo desde cualquier carpeta)'

if ($SoloCli) { Ok 'Listo: solo se actualizó el CLI.'; exit 0 }

Step 'Instalando el sistema'
$installArgs = @('install')
if ($HostName) { $installArgs += @('--host', $HostName) }
& $Exe @installArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$interactive = [Environment]::UserInteractive -and -not [Console]::IsInputRedirected
if ($interactive) {
    $answer = Read-Host '¿Crear ahora el primer administrador? (s/n) [s]'
    if ($answer -eq '' -or $answer -match '^[sS]') {
        $user = Read-Host 'Usuario [admin]'
        if (-not $user) { $user = 'admin' }
        $email = Read-Host 'Correo'
        # La contraseña la pide tecnm sin mostrarla; nunca pasa como argumento.
        & $Exe admin --user $user --email $email --password-stdin
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
else {
    Write-Host 'Crea el administrador con: tecnm admin --user <usuario> --email <correo> --password-stdin'
}

Step 'Listo'
Write-Host 'Escribe "tecnm" en una terminal nueva para abrir el menú, o "tecnm status" para ver el estado.'
exit 0
