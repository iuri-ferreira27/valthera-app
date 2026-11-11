# syntax=docker/dockerfile:1

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY src/Valthera.Core/Valthera.Core.csproj src/Valthera.Core/
COPY src/Valthera.Data/Valthera.Data.csproj src/Valthera.Data/
COPY src/Valthera.Api/Valthera.Api.csproj src/Valthera.Api/
RUN dotnet restore src/Valthera.Api/Valthera.Api.csproj

COPY src/ src/
RUN dotnet publish src/Valthera.Api/Valthera.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Valthera__DataSource=Sample

EXPOSE 8080

# The aspnet image ships a non-root user ("app", uid 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "Valthera.Api.dll"]
