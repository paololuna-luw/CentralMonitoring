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

## Central de prueba actual

- `InstanceId`: `f2f0f0c7-2a48-45f4-a067-6bd6f3a4fd2f`
- `InstanceName`: `Programa 1 Local Paolo`
- `Organization`: `Org Paolo Dev`
- usuario owner de prueba:
  - `paolo.luna.dev@gmail.com`

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

`labelsJson` puede incluir, segun el origen:

- SNMP:
  - `snmp_ip`
  - `oid`
  - `if_index`
  - `host_ip`
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
