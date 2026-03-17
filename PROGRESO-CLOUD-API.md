# Progreso - Cloud API

## Objetivo

Construir una capa cloud separada del monitoreo local para:

- login de usuarios,
- acceso multi-central,
- sincronizacion de snapshots y alertas desde `Programa 1`,
- consumo desde APK,
- notificaciones push.

## Stack definido para MVP

- Backend: `CentralMonitoring.CloudApi`
- Hosting: `Render`
- DB/Auth: `Supabase`
- Push: `Firebase Cloud Messaging (FCM)`

## Estado actual

- Estado: `fundacion iniciada`
- Estado: `fundacion + sync base iniciados`
- Estado: `auth Google/Supabase validado; falta automatizar app_users`
- Estado: `JWT Supabase integrado en CloudApi`
- Estado: `sync base Programa 1 -> Cloud validado`
- Estado: `dashboard movil compacto agregado`
- Estado: `backend movil cerrado para APK MVP`
- Estado: `administracion de usuarios por central agregada`
- Estado: `CloudApi desplegado en Render y respondiendo Healthy`
- Arquitectura definida.
- Stack del MVP definido.
- Flujo principal definido:
  - `Programa 1 -> Cloud API -> APK`
- Proyecto `CentralMonitoring.CloudApi` creado y agregado a la solucion.
- `CloudApi` base compila y ya expone endpoints iniciales reales para:
  - registro de central
  - heartbeat
  - sync de snapshots
  - sync de alertas
  - consulta basica de centrales/alertas por `userId`
- Esquema SQL inicial aplicado en Supabase.
- Login Google probado con exito en `Supabase Auth`.
- Trigger `auth.users -> public.app_users` agregado y probado.
- `CloudApi` ya valida JWT de `Supabase` y expone endpoints autenticados base.
- `Programa 1` ya sincroniza `heartbeat`, `snapshots` y `alerts` hacia cloud.
- `CloudApi` ya expone endpoint compacto para dashboard movil autenticado.
- `CloudApi` ya expone acciones moviles de `ack/resolve` y registro de `device-tokens`.
- `CloudApi` ya expone endpoints admin para asignar/listar/quitar usuarios por central.
- `appsettings.json` versionados ya fueron saneados para GitHub; los valores reales quedan en `appsettings.Development.json` local y luego en variables de entorno en `Render`.
- `Dockerfile` y `.dockerignore` agregados para desplegar `CentralMonitoring.CloudApi` en `Render` usando el repo completo.
- `CloudApi` ya esta publicado en `Render` con URL publica: `https://centralmonitoring-cloudap.onrender.com`
- alertas moviles enriquecidas con:
  - `metricDisplayName`
  - `sourceType`
  - `labelsJson`
  - `reason`
- revision del flujo de alertas completada:
  - contexto real persistido por alerta
  - sync cloud usando `labelsJson` propio de la alerta
  - `CloudApi` devolviendo razon explicita para consumo movil

## Linea de progreso actual

- `Arquitectura definida + auth cerrada + sync base validado + backend movil/admin listo + deploy Render cerrado`
- siguiente bloque: `sync Programa 1 -> Render + APK + FCM`

## Estado funcional actual

Hoy el `CloudApi` ya permite:

- login Google via `Supabase Auth`
- validacion JWT dentro de `CloudApi`
- consulta del usuario autenticado
- consulta de centrales asignadas al usuario
- dashboard movil compacto
- consulta de alertas moviles
- consulta de alertas moviles con contexto SNMP/agente
- `ack/resolve` de alertas cloud
- registro/eliminacion de `device-tokens`
- administracion de usuarios por central
- sincronizacion real desde `Programa 1`:
  - `heartbeat`
  - `snapshots`
  - `alerts`
- alertas moviles con contexto tecnico suficiente para explicar por que disparo:
  - `service`
  - `drive`
  - `iface`
  - `snmp_ip`
  - `oid`
  - `failure_reason`

## Central de prueba actual

- `InstanceId`: `f2f0f0c7-2a48-45f4-a067-6bd6f3a4fd2f`
- `InstanceName`: `Programa 1 Local Paolo`
- `Organization`: `Org Paolo Dev`
- `CloudApi URL`: `https://centralmonitoring-cloudap.onrender.com`
- usuario actual con acceso:
  - `paolo.luna.dev@gmail.com`
  - rol: `owner`

## Documentos base

- `CLOUD-API-ARQUITECTURA.md`
- `CLOUD-API-MVP-RENDER-SUPABASE-FCM.md`
- `ARQUITECTURA-CLOUD-LICENCIAS.md`
- `APK-HANDOFF.md`

## Alcance del MVP cloud

### Debe permitir

- registrar una central (`Programa 1`)
- identificar cada central con `InstanceId`
- asociar centrales a una organizacion
- permitir login de usuario
- permitir que un usuario vea una o varias centrales
- recibir snapshots y alertas desde `Programa 1`
- entregar resumen y alertas al APK
- enviar notificaciones push

### No entra en este primer bloque

- billing real
- cuotas/licencias completas
- relays complejos de comandos remotos
- analitica pesada

## Fases de implementacion

### Fase 1 - Fundacion Cloud API

- [x] Crear proyecto `CentralMonitoring.CloudApi`
- [x] Configurar estructura base ASP.NET Core
- [x] Definir configuracion cloud base (`Supabase`, `Fcm`, `CloudApi`)
- [x] Definir contratos DTO iniciales

### Fase 2 - Supabase

- [x] Crear proyecto Supabase
- [x] Definir esquema SQL inicial
- [x] Crear tablas base:
  - `organizations`
  - `users`
  - `organization_users`
  - `central_instances`
  - `central_user_access`
  - `central_snapshots`
  - `cloud_alerts`
  - `mobile_device_tokens`
- [x] Configurar autenticacion base
- [x] Crear trigger `auth.users -> public.app_users`

### Fase 3 - Endpoints MVP

 - [x] `POST /api/v1/auth/login`
 - [x] `GET /api/v1/me`
 - [x] `GET /api/v1/me/centrals`
- [x] `GET /api/v1/admin/centrals/{instanceId}/users`
- [x] `POST /api/v1/admin/centrals/{instanceId}/users`
- [x] `DELETE /api/v1/admin/centrals/{instanceId}/users/{targetUserId}`
- [x] `POST /api/v1/centrals/register`
- [x] `POST /api/v1/centrals/{instanceId}/heartbeat`
- [x] `POST /api/v1/centrals/{instanceId}/snapshots`
- [x] `POST /api/v1/centrals/{instanceId}/alerts/sync`
- [x] `GET /api/v1/mobile/centrals`
- [x] `GET /api/v1/mobile/centrals/{instanceId}/summary`
- [x] `GET /api/v1/mobile/alerts`
- [x] `GET /api/v1/mobile/dashboard`
- [x] `POST /api/v1/mobile/alerts/{cloudAlertId}/ack`
- [x] `POST /api/v1/mobile/alerts/{cloudAlertId}/resolve`
- [x] `POST /api/v1/mobile/device-tokens`
- [x] `DELETE /api/v1/mobile/device-tokens/{id}`

## Endpoints actuales

### Auth y usuario

- `POST /api/v1/auth/login`
- `GET /api/v1/me`
- `GET /api/v1/me/centrals`

### Sync de centrales

- `POST /api/v1/centrals/register`
- `POST /api/v1/centrals/{instanceId}/heartbeat`
- `POST /api/v1/centrals/{instanceId}/snapshots`
- `POST /api/v1/centrals/{instanceId}/alerts/sync`

### Movil / APK

- `GET /api/v1/mobile/centrals`
- `GET /api/v1/mobile/centrals/{instanceId}/summary`
- `GET /api/v1/mobile/alerts`
- `GET /api/v1/mobile/dashboard`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/ack`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/resolve`
- `POST /api/v1/mobile/device-tokens`
- `DELETE /api/v1/mobile/device-tokens/{id}`

### Administracion por central

- `GET /api/v1/admin/centrals/{instanceId}/users`
- `POST /api/v1/admin/centrals/{instanceId}/users`
- `DELETE /api/v1/admin/centrals/{instanceId}/users/{targetUserId}`

## Como probarlo

### 1. Obtener token

- abrir login de `Supabase/Google`
- copiar `access_token`
- usar header:
  - `Authorization: Bearer TU_ACCESS_TOKEN`

### 2. Probar usuario autenticado

- `GET /api/v1/me`
- respuesta esperada:
  - `id`
  - `email`
  - `fullName`
  - `isActive`
  - `createdAtUtc`

### 3. Probar centrales del usuario

- `GET /api/v1/me/centrals`
- respuesta esperada:
  - `centralId`
  - `instanceId`
  - `instanceName`
  - `organizationId`
  - `role`
  - `lastSeenUtc`
  - `isActive`

### 4. Probar dashboard movil

- `GET /api/v1/mobile/dashboard`
- respuesta esperada:
  - `user`
  - `centrals`
  - `openAlertsTop`

### 5. Probar alertas moviles

- `GET /api/v1/mobile/alerts`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/ack`
- `POST /api/v1/mobile/alerts/{cloudAlertId}/resolve`

Campos utiles nuevos en alertas moviles:

- `metricDisplayName`
- `sourceType` (`agent` o `snmp`)
- `labelsJson`
- `reason`

Comportamiento operativo actual de `GET /api/v1/mobile/alerts`:

- devuelve solo alertas `Open` o `Acked`
- no devuelve alertas `Resolved`
- es el endpoint correcto para saber el estado actual de una central
- si un dispositivo no aparece ahi, desde el punto de vista de alertas cloud esta sin alertas activas

El flujo correcto ahora es:

1. `Programa 1` crea alerta local
2. la alerta guarda `ContextKey` y `LabelsJson`
3. `CloudSyncWorker` sincroniza ese contexto al cloud
4. `CloudApi` devuelve `reason` y `labelsJson` al movil

Con eso se evita mezclar:

- varios discos del mismo host
- varios servicios del mismo host
- varias interfaces SNMP del mismo host

Ejemplos de uso en APK:

- si `sourceType = snmp`, mostrar `snmp_ip`, `oid`, `if_index`
- si `metricKey = service_up`, mostrar `service`
- si `metricKey = disk_used_pct`, mostrar `drive`
- si `metricKey = net_rx_errors`, mostrar `iface`

Ejemplos de razon que el backend ya entrega:

- `Memory usage 90.09% exceeds threshold 90% on Pruebita.`
- `CPU usage 97.31% exceeds threshold 85% on Pruebita.`
- `Critical service 'Spooler' is reported as down on HostName.`
- `SNMP polling failed for 10.3.23.32. <failure_reason>. Consecutive failures: Y.`

### 6. Probar device token

- `POST /api/v1/mobile/device-tokens`
- body:

```json
{
  "platform": "android",
  "deviceToken": "demo-token-001"
}
```

- `DELETE /api/v1/mobile/device-tokens/{id}`

### 7. Probar administracion de usuarios por central

- listar usuarios:
  - `GET /api/v1/admin/centrals/{instanceId}/users`
- asignar usuario:
  - `POST /api/v1/admin/centrals/{instanceId}/users`
- quitar usuario:
  - `DELETE /api/v1/admin/centrals/{instanceId}/users/{targetUserId}`

Regla importante:

- solo `owner` o `admin` pueden administrar usuarios de una central
- el usuario destino debe haber iniciado sesion al menos una vez para existir en `app_users`

### Body para asignar usuario a una central

```json
{
  "email": "usuario@ejemplo.com",
  "role": "readonly"
}
```

Roles permitidos:

- `owner`
- `admin`
- `operator`
- `readonly`

## Flujo recomendado para una central nueva

1. desplegar `CloudApi`
2. iniciar `Programa 1` con `Cloud:BaseUrl` apuntando a cloud
3. dejar que `Programa 1` registre la central y sincronice
4. hacer login del usuario en cloud
5. asignar usuario a la central via endpoint admin
6. consumir `me/centrals`, `dashboard` y `alerts` desde APK

## Pendientes importantes

- cargar en `Render` las variables de entorno reales (`Supabase`, `ConnectionStrings`, `ApiKey`)
- validar sync real de `Programa 1` contra la URL publica de `Render`
- cambiar `Programa 1` a URL publica
- integrar `FCM`
- crear APK

## Ajuste tecnico aplicado para contexto de alertas

Se agrego migracion local:

- `20260317193000_AddAlertContextFields`

Objetivo:

- guardar `ContextKey`
- guardar `LabelsJson`
- deduplicar alertas por contexto real y no solo por `metricKey`

Aplicar:

```powershell
dotnet ef database update --project CentralMonitoring.Infrastructure --startup-project CentralMonitoring.Api
```

Luego:

1. reiniciar `CentralMonitoring.Api`
2. reiniciar `CentralMonitoring.Worker`
3. para cloud publico, hacer `git push` y redeploy de `Render`

### Fase 4 - Integracion con Programa 1

- [ ] Generar/persistir `InstanceId`
- [ ] Agregar configuracion `Cloud:*` en `Programa 1`
- [ ] Crear `CloudSyncWorker`
- [ ] Enviar snapshots periodicos
- [ ] Enviar alertas abiertas/resueltas
- [x] Validar sync real `heartbeat/snapshots/alerts` hacia cloud

### Fase 5 - APK

- [ ] Login
- [ ] Lista de centrales
- [ ] Resumen por central
- [ ] Alertas
- [ ] Vista movil basica
- [ ] Consumir `dashboard`
- [ ] Registrar `device_token`

### Fase 6 - Push notifications

- [ ] Configurar Firebase
- [ ] Registrar `device_token`
- [ ] Enviar push por alertas criticas

## Criterio de exito del MVP cloud

Se considera cumplido cuando:

- existe `CloudApi` desplegado en `Render`,
- existe DB/Auth en `Supabase`,
- una central local se registra con `InstanceId`,
- `Programa 1` sincroniza snapshots y alertas,
- un usuario inicia sesion,
- un usuario puede ver varias centrales,
- el APK puede consultar resumen y alertas,
- `FCM` envia push de alertas criticas.

## Siguiente paso inmediato recomendado

- Reiniciar `Worker` apuntando a `https://centralmonitoring-cloudap.onrender.com`
- Validar `heartbeat/snapshots/alerts` contra `Render`
- Luego iniciar APK y `FCM`
