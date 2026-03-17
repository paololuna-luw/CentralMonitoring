# Handoff - APK

## Objetivo

Construir el cliente movil que consume `CentralMonitoring.CloudApi` ya desplegado en cloud.

El APK no habla con `Programa 1` local.

Flujo correcto:

- `Programa 1 -> CloudApi -> Supabase`
- `APK -> CloudApi`

## Estado actual ya validado

- `CloudApi` desplegado en `Render`
- URL publica:
  - `https://centralmonitoring-cloudap.onrender.com`
- `Supabase Auth` con Google funcionando
- JWT validado dentro de `CloudApi`
- `Programa 1` sincronizando:
  - `heartbeat`
  - `snapshots`
  - `alerts`
- endpoints moviles funcionando desde internet
- flujo de alertas revisado extremo a extremo:
  - `AlertEvent local -> CloudSyncWorker -> CloudApi -> /mobile/alerts`
  - alertas activas viajan con contexto persistido (`labelsJson`)
  - el `CloudApi` devuelve `reason` calculado para UI movil

## Central de prueba actual

- `InstanceId`: `f2f0f0c7-2a48-45f4-a067-6bd6f3a4fd2f`
- `InstanceName`: `Programa 1 Local Paolo`
- `Organization`: `Org Paolo Dev`
- usuario owner de prueba:
  - `paolo.luna.dev@gmail.com`

### Estado operativo revisado de la central de prueba

- hosts registrados:
  - `Pruebita` (`127.0.0.1`, Windows)
  - `AP-EPIS-N-P02` (`10.3.23.32`, Network)
  - `AP-EPIS-N-P03` (`10.3.23.38`, Network)
  - `AP-EPIS-N-P04` (`10.3.23.34`, Network)
- alertas activas locales vistas al revisar:
  - `Pruebita`:
    - `mem_used_pct` critica
    - `cpu_usage_pct` critica
- equipos sin alertas activas actuales:
  - `AP-EPIS-N-P02`
  - `AP-EPIS-N-P03`
  - `AP-EPIS-N-P04`

Interpretacion recomendada para el APK:

- si un dispositivo no aparece en alertas activas cloud:
  - tratarlo como `sin alertas activas`
  - no mostrarlo como fallo
- `mobile/alerts` representa problemas actuales cloud, no historial cerrado

## Arquitectura minima que el APK debe asumir

- auth: `Google -> Supabase Auth -> access_token`
- backend principal: `CloudApi`
- datos:
  - resumen por central
  - alertas
  - centrales asignadas al usuario
- push futuro:
  - `FCM`

## Endpoints que el APK ya puede consumir

### Auth

- `POST /api/v1/auth/login`
- `GET /api/v1/me`
- `GET /api/v1/me/centrals`

### Movil

- `GET /api/v1/mobile/dashboard`
- `GET /api/v1/mobile/centrals`
- `GET /api/v1/mobile/centrals/{instanceId}/summary`
- `GET /api/v1/mobile/alerts`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/ack`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/resolve`
- `POST /api/v1/mobile/device-tokens`
- `DELETE /api/v1/mobile/device-tokens/{id}`

Base URL:

- `https://centralmonitoring-cloudap.onrender.com`

## Auth del APK

### Opcion MVP simple

- usar `Supabase Auth` con Google
- obtener `access_token`
- enviar header:
  - `Authorization: Bearer <access_token>`

### Endpoints ya probados con token real

- `GET /api/v1/me`
- `GET /api/v1/me/centrals`
- `GET /api/v1/mobile/dashboard`

## Respuestas utiles para UI

### `GET /api/v1/me`

Devuelve:

- `id`
- `email`
- `fullName`
- `isActive`
- `createdAtUtc`

### `GET /api/v1/me/centrals`

Cada item devuelve:

- `userId`
- `email`
- `centralId`
- `instanceId`
- `instanceName`
- `organizationId`
- `role`
- `lastSeenUtc`
- `isActive`

### `GET /api/v1/mobile/dashboard`

Devuelve:

- `user`
- `centrals`
- `openAlertsTop`

Cada central trae resumen compacto:

- `centralId`
- `instanceId`
- `instanceName`
- `organizationId`
- `role`
- `lastSeenUtc`
- `isActive`
- `hostsTotal`
- `hostsActive`
- `alertsOpen`
- `criticalAlerts`
- `warningAlerts`
- `snapshotTimestampUtc`

### `GET /api/v1/mobile/alerts`

Cada alerta movil ahora trae tambien:

- `metricDisplayName`
- `sourceType`
- `labelsJson`
- `reason`

Importante:

- `GET /api/v1/mobile/alerts` devuelve solo alertas `Open` o `Acked`
- no devuelve alertas `Resolved`
- para estado actual del sistema, este endpoint es la referencia operativa
- el `CloudApi` filtra abiertas estancadas/viejas para no saturar el APK con basura historica
- el `CloudApi` deduplica por contexto para no enviar el mismo problema varias veces

El backend ahora persiste contexto propio de cada alerta:

- `contextKey`
- `labelsJson`

Eso evita mezclar en una sola alerta cosas distintas del mismo host, por ejemplo:

- discos distintos con `disk_used_pct`
- servicios distintos con `service_up`
- interfaces distintas con metricas `snmp_if*`

`labelsJson` puede incluir, segun el origen:

- SNMP:
  - `snmp_ip`
  - `oid`
  - `if_index`
  - `host_ip`
  - `failure_reason` para `snmp_poll_failure`
- Agente:
  - `service`
  - `kind`
  - `drive`
  - `iface`
  - `process`
  - `pid`
  - `host_ip`
  - `host_type`

Uso recomendado en UI:

- titulo:
  - `metricDisplayName`
- subtitulo:
  - `reason`
- chips secundarios:
  - `sourceType`
  - `service`
  - `iface`
  - `drive`
  - `snmp_ip`
  - `oid`

Regla de interpretacion para el APK:

- usar `reason` como texto principal de la incidencia
- usar `metricDisplayName` como titulo corto
- usar `labelsJson` solo para detalle/metadata
- no intentar reconstruir la razon solo con `metricKey` si `reason` ya viene informado

Ejemplos reales de razon esperada:

- `mem_used_pct`
  - `Memory usage 90.09% exceeds threshold 90% on Pruebita.`
- `cpu_usage_pct`
  - `CPU usage 97.31% exceeds threshold 85% on Pruebita.`
- `snmp_poll_failure`
  - `SNMP polling failed for 10.3.23.32. status X ... Consecutive failures: Y.`
- `service_up`
  - `Critical service 'Spooler' is reported as down on HostName.`

## Pantallas MVP recomendadas

1. Login
2. Mis centrales
3. Dashboard de central
4. Alertas
5. Detalle basico de alerta

## Flujo de UI recomendado

1. login Google
2. guardar `access_token`
3. llamar `GET /api/v1/me`
4. llamar `GET /api/v1/me/centrals`
5. mostrar lista de centrales
6. al entrar a una central:
   - llamar `GET /api/v1/mobile/dashboard`
   - o `GET /api/v1/mobile/centrals/{instanceId}/summary`
7. alertas:
   - `GET /api/v1/mobile/alerts`
   - `ack/resolve`

## Pendientes para el bloque APK

- elegir stack del cliente movil
- implementar login Google/Supabase en el cliente
- persistencia segura de sesion/token
- cliente HTTP base con bearer token
- manejo de expiracion de token
- luego integrar `FCM`

## Requisito tecnico nuevo para alertas con contexto

Se agrego migracion local para persistir contexto real de cada alerta:

- `20260317193000_AddAlertContextFields`

Antes de validar completamente el bloque cloud/apk con contexto, aplicar:

```powershell
dotnet ef database update --project CentralMonitoring.Infrastructure --startup-project CentralMonitoring.Api
```

Luego:

1. reiniciar `CentralMonitoring.Api`
2. reiniciar `CentralMonitoring.Worker`
3. volver a sincronizar hacia `CloudApi`
4. redeploy de `CloudApi` si quieres que `Render` use el ajuste de `reason` para `snmp_poll_failure`

## Recomendacion tecnica

Si el objetivo es velocidad de entrega:

- usar `Flutter`

Minimo de modulos:

- auth
- api client
- session storage
- centrals
- dashboard
- alerts

## Documentos que debes llevarte junto con este

- `PROGRESO-CLOUD-API.md`
- `PROGRESO.md`
- `APK-HANDOFF.md`

Con esos tres hay contexto suficiente para arrancar el APK sin perder el panorama general.
