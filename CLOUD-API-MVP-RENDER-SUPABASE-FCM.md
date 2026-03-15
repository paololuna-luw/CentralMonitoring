# Cloud API MVP - Render + Supabase + FCM

## Objetivo

Definir un MVP cloud simple, barato y ejecutable para que:

- `Programa 1` local envie datos a la nube,
- un usuario pueda iniciar sesion,
- un usuario pueda ver varias centrales,
- un APK pueda consultar resumen y alertas,
- el sistema pueda enviar notificaciones push.

Este MVP se basa solo en:

- `Render`
- `Supabase`
- `Firebase Cloud Messaging`

## Stack decidido

### Backend cloud

- `CentralMonitoring.CloudApi`
- Hosting: `Render`

### Base de datos y autenticacion

- `Supabase Postgres`
- `Supabase Auth`

### Notificaciones moviles

- `Firebase Cloud Messaging (FCM)`

### Cliente movil

- APK Android
- recomendado: `Flutter`

## Responsabilidad de cada componente

### Render

Se usa para desplegar el backend:

- `CentralMonitoring.CloudApi`
- endpoints REST cloud
- validacion de usuarios
- sincronizacion de centrales
- consulta para APK
- emision de notificaciones push usando FCM

### Supabase

Se usa para:

- base de datos PostgreSQL cloud
- autenticacion de usuarios
- almacenamiento de organizaciones, centrales, snapshots y alertas

### FCM

Se usa para:

- enviar notificaciones push al celular
- avisar sobre alertas criticas o cambios relevantes

## Arquitectura final del MVP

```text
CentralMonitoring.Agent
        |
        v
Programa 1 local (Api + Worker + DB local)
        |
        | sync HTTPS
        v
CentralMonitoring.CloudApi en Render
        |
        +--> Supabase Postgres
        |
        +--> Supabase Auth
        |
        +--> FCM
                |
                v
               APK
```

## Flujo operativo

1. El `Agent` envia metricas a `Programa 1`.
2. `Programa 1` procesa reglas y alertas localmente.
3. `Programa 1` sincroniza a nube:
   - snapshot de estado,
   - alertas abiertas,
   - alertas resueltas,
   - inventario minimo.
4. `CloudApi` guarda informacion en `Supabase`.
5. El usuario inicia sesion.
6. El APK consulta `CloudApi`.
7. Si hay alerta critica, `CloudApi` envia push via `FCM`.

## Concepto multi-central

Cada `Programa 1` local debe tener:

- `InstanceId`
- `InstanceName`
- `OrganizationId`

Eso permite que:

- un usuario tenga una o varias centrales,
- cada central se diferencie correctamente,
- la nube no mezcle datos entre clientes.

## Modelo de datos minimo

### Tablas

- `organizations`
- `users`
- `organization_users`
- `central_instances`
- `central_user_access`
- `central_snapshots`
- `cloud_alerts`
- `mobile_device_tokens`

### Campos importantes

#### `central_instances`

- `id`
- `organization_id`
- `instance_id`
- `instance_name`
- `api_key_hash`
- `last_seen_utc`
- `is_active`

#### `central_snapshots`

- `id`
- `instance_id`
- `timestamp_utc`
- `hosts_total`
- `hosts_active`
- `alerts_open`
- `critical_alerts`
- `warning_alerts`
- `summary_json`

#### `cloud_alerts`

- `id`
- `instance_id`
- `source_alert_id`
- `host_id`
- `host_name`
- `metric_key`
- `severity`
- `status`
- `trigger_value`
- `threshold`
- `opened_at_utc`
- `resolved_at_utc`

#### `mobile_device_tokens`

- `id`
- `user_id`
- `platform`
- `device_token`
- `created_at_utc`
- `last_seen_utc`

## Endpoints minimos de Cloud API

### Auth

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`

### Usuario

- `GET /api/v1/me`
- `GET /api/v1/me/centrals`

### Registro de centrales

- `POST /api/v1/centrals/register`
- `POST /api/v1/centrals/{instanceId}/heartbeat`

### Sync desde Programa 1

- `POST /api/v1/centrals/{instanceId}/snapshots`
- `POST /api/v1/centrals/{instanceId}/alerts/sync`

### Consumo APK

- `GET /api/v1/mobile/centrals`
- `GET /api/v1/mobile/centrals/{instanceId}/summary`
- `GET /api/v1/mobile/alerts`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/ack`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/resolve`

### Tokens moviles

- `POST /api/v1/mobile/device-tokens`
- `DELETE /api/v1/mobile/device-tokens/{id}`

## Contratos minimos de sync

### Snapshot

```json
{
  "instanceId": "8c5d1a4e-0a58-4c18-b16c-1d7238dfb9c2",
  "instanceName": "Central Medellin",
  "timestampUtc": "2026-03-15T10:00:00Z",
  "hostsTotal": 12,
  "hostsActive": 11,
  "alertsOpen": 4,
  "criticalAlerts": 1,
  "warningAlerts": 3
}
```

### Alert sync

```json
{
  "instanceId": "8c5d1a4e-0a58-4c18-b16c-1d7238dfb9c2",
  "alerts": [
    {
      "alertId": "cb608f4b-4e90-4998-b47c-75ab6f4ab603",
      "hostId": "d2bb89a8-18c6-4a31-899e-48b2df58d78a",
      "hostName": "srv-app-01",
      "metricKey": "mem_used_pct",
      "severity": "Critical",
      "status": "Open",
      "triggerValue": 95.4,
      "threshold": 90,
      "openedAtUtc": "2026-03-15T10:01:00Z",
      "resolvedAtUtc": null
    }
  ]
}
```

## Cambios necesarios en Programa 1

`Programa 1` debe agregar configuracion cloud:

```json
{
  "Cloud": {
    "Enabled": true,
    "BaseUrl": "https://tu-cloud-api.onrender.com",
    "ApiKey": "secret-instance-key",
    "InstanceId": "8c5d1a4e-0a58-4c18-b16c-1d7238dfb9c2",
    "InstanceName": "Central Medellin",
    "SyncIntervalSeconds": 30
  }
}
```

Tambien debe agregar:

- generacion y persistencia de `InstanceId`
- `CloudSyncWorker`
- envio de snapshots
- envio de alertas

## Seguridad del MVP

### Programa 1 -> Cloud API

- autenticacion por API key por instancia
- validacion de `InstanceId`
- TLS obligatorio

### APK -> Cloud API

- login con `Supabase Auth`
- JWT para usuario
- acceso filtrado por organizacion y centrales habilitadas

## Push notifications

`FCM` se usa para:

- alertas criticas,
- cambios de estado importantes,
- notificaciones del sistema.

Flujo:

1. APK registra `device_token`.
2. `CloudApi` guarda ese token.
3. llega alerta critica desde una central.
4. `CloudApi` consulta usuarios con acceso a esa central.
5. `CloudApi` envia push via `FCM`.

## Ventajas del stack elegido

- muy bajo costo para empezar,
- rapido de montar,
- Postgres y Auth ya resueltos con Supabase,
- notificaciones push sin costo con FCM,
- backend cloud simple en Render.

## Limitaciones del MVP

- `Render Free` puede dormir el servicio por inactividad
- `Supabase Free` tiene limites de almacenamiento y uso
- no es la arquitectura final de produccion de alto trafico

Este stack sirve para:

- demo
- validacion tecnica
- piloto
- primeras pruebas con usuarios reales

## Roadmap de implementacion

### Fase 1

- crear `CentralMonitoring.CloudApi`
- crear proyecto en Supabase
- crear esquema minimo
- login basico
- `GET /me/centrals`

### Fase 2

- agregar `CloudSyncWorker` a `Programa 1`
- sync de snapshots
- sync de alertas
- registro de centrales

### Fase 3

- crear APK
- login
- lista de centrales
- resumen
- alertas

### Fase 4

- integrar FCM
- push de alertas criticas
- ack/resolve desde APK

## Criterio de exito del MVP cloud

Se considera completo cuando:

- una central local se registra en cloud,
- envia snapshots y alertas,
- un usuario inicia sesion,
- ve una o varias centrales,
- consulta alertas desde el APK,
- recibe push por FCM.
