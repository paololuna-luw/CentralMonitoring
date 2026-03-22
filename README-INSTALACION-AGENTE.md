# Instalacion CentralMonitoring Agent

Esta guia usa el mismo modelo:

- `publish`: compila y deja artefactos en `dist/...`
- `install`: primera instalacion en la ruta final del agente
- `update`: actualizacion posterior conservando `appsettings.Production.json`

## Modelo operativo

Control central estricto:

1. primero se crea el host en la central
2. la central emite el `HostId`
3. luego ese `HostId` se configura en el agente
4. el agente no se auto-registra

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
./scripts/publish-agent-linux.sh linux-x64
```

Install:

```bash
./scripts/install-agent-linux.sh
```

Update:

```bash
./scripts/update-agent-linux.sh
```

Ruta final sugerida:

- `/opt/centralmonitoring/agent`

## Windows

Publish:

```powershell
.\scripts\publish-agent-win.ps1 -Runtime win-x64
```

Install:

```powershell
.\scripts\install-agent-win.ps1
```

Update:

```powershell
.\scripts\update-agent-win.ps1
```

Ruta final sugerida:

- `C:\CentralMonitoring\Agent`

## Datos requeridos para install

El instalador del agente pide:

- `ApiBaseUrl`
- `ApiKey`
- `HostId`

Ese `HostId` debe existir previamente en la central.

## Update sin perder configuracion

Los scripts `update`:

- reemplazan binarios
- conservan `appsettings.Production.json`

Por tanto una actualizacion normal no debe cambiar:

- `ApiBaseUrl`
- `ApiKey`
- `HostId`

## Siguiente paso despues de install

1. prueba manual del agente
2. validacion de ingesta en la central
3. servicio `systemd` o servicio Windows
