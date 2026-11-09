# API de producción. Contexto: backend/ (docker compose -f database/docker-compose.prod.yml build).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props TecnmInventario.Api.csproj ./
RUN dotnet restore TecnmInventario.Api.csproj
COPY . .
RUN dotnet publish TecnmInventario.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# QuestPDF necesita fuentes del sistema en Linux.
RUN apt-get update \
 && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data/documents && chown app:app /data/documents
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    FileStorage__LocalPath=/data/documents
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TecnmInventario.Api.dll"]
