# Docker Central

Este stack Docker es solo para la central:

- `CentralMonitoring.Api`
- `CentralMonitoring.Worker`
- `ui`
- `postgres`

No toca el `Dockerfile` actual del `CloudApi` usado por Render.

## Archivos

- `docker-compose.central.yml`
- `docker/central/api.Dockerfile`
- `docker/central/worker.Dockerfile`
- `docker/central/ui.Dockerfile`
- `docker/central/nginx.conf`
- `.env.central.example`

## Flujo

1. Clona el repo
2. copia `.env.central.example` a `.env.central`
3. ajusta credenciales y datos de cloud
4. levanta el stack

## Preparacion

```bash
cp .env.central.example .env.central
```

Ajusta al menos:

- `POSTGRES_PASSWORD`
- `CENTRAL_API_KEY`
- `CLOUD_BASE_URL`
- `CLOUD_INSTANCE_ID`
- `CLOUD_INSTANCE_NAME`
- `CLOUD_ORGANIZATION_NAME`
- `CLOUD_ORGANIZATION_SLUG`

Si no quieres cloud al inicio:

- `CLOUD_ENABLED=false`

## Levantar

```bash
docker compose --env-file .env.central -f docker-compose.central.yml up -d --build
```

## Migraciones

La API aplica migraciones automaticamente al arrancar, así que el stack Docker ya no requiere ejecutar `dotnet ef` manualmente para una instalacion normal.

## Puertos

- UI: `http://localhost:8080`
- API directa: `http://localhost:5188`
- PostgreSQL: `localhost:5432`

La UI usa el mismo origen y `nginx` hace proxy a `/api`, `/health` y `/swagger`, así evitamos el problema de CORS del código actual.

## Update

```bash
docker compose --env-file .env.central -f docker-compose.central.yml up -d --build
```

Si no cambiaste variables, el stack reutiliza el volumen persistente de PostgreSQL.

## Datos persistentes

PostgreSQL usa el volumen:

- `central_postgres_data`

No lo borres si no quieres perder datos.
