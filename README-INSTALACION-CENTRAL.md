# Instalacion CentralMonitoring Central

Esta guia refleja el flujo correcto del repo:

- `publish`: compila y deja artefactos en `dist/...`
- `install`: primera instalacion en rutas finales del servidor
- `update`: actualizacion posterior conservando `appsettings.Production.json`

## Flujo correcto

Primera instalacion:

1. `publish`
2. `install`

Actualizacion posterior:

1. `publish`
2. `update`

## Linux

Publish:

```bash
./scripts/publish-central-linux.sh linux-x64
```

Si quieres que la UI quede compilada con una URL/API key concreta:

```bash
UI_API_BASE=http://tu-api:5188 UI_API_KEY=tu_api_key ./scripts/publish-central-linux.sh linux-x64
```

Install:

```bash
./scripts/install-central-linux.sh
```

Update:

```bash
./scripts/update-central-linux.sh
```

Rutas finales sugeridas:

- `/opt/centralmonitoring/api`
- `/opt/centralmonitoring/worker`
- `/var/www/centralmonitoring-ui`

## Windows

Publish:

```powershell
.\scripts\publish-central-win.ps1 -Runtime win-x64
```

Con UI parametrizada:

```powershell
.\scripts\publish-central-win.ps1 -Runtime win-x64 -UiApiBase http://tu-api:5188 -UiApiKey tu_api_key
```

Install:

```powershell
.\scripts\install-central-win.ps1
```

Update:

```powershell
.\scripts\update-central-win.ps1
```

Rutas finales sugeridas:

- `C:\CentralMonitoring\Api`
- `C:\CentralMonitoring\Worker`
- `C:\CentralMonitoring\Ui`

## Base de datos

La API y el Worker comparten la misma base PostgreSQL.

Aplica migraciones apuntando a la conexion real:

```powershell
dotnet ef database update --project CentralMonitoring.Infrastructure --startup-project CentralMonitoring.Api
```

## Cloud

Cloud es opcional para que la central funcione localmente.

- si `Cloud.Enabled = false`, la central opera localmente
- si `Cloud.Enabled = true`, el Worker sincroniza con tu CloudApi en Render

## Update sin perder credenciales

Los scripts `update`:

- reemplazan binarios publicados
- conservan `appsettings.Production.json`

Eso significa que una actualizacion normal no debe cambiar:

- `ConnectionStrings:Default`
- `ApiKey`
- `InstanceId`
- configuracion cloud productiva

## Siguiente paso despues de install

1. aplicar migraciones
2. probar manualmente API y Worker
3. crear `systemd` o servicio Windows
4. validar `/health`
