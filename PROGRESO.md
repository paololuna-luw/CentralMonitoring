# Estado de Progreso - Programa 1

## Estado general
- Estado actual: `MVP funcional cerrado`.
- Cobertura: API + Worker + UI operando en conjunto para monitoreo por `Agente` y por `SNMP`.
- Resultado: ya se puede operar desde la UI sin recargar navegador manualmente.
- Programa 2 (`CentralMonitoring.Agent`) ya envia metricas reales al central.

## Componentes completados

### Backend (API)
- Hosts:
  - `POST /api/v1/hosts`
  - `GET /api/v1/hosts`
  - `GET /api/v1/hosts/{id}`
  - `PATCH /api/v1/hosts/{id}`
  - `DELETE /api/v1/hosts/{id}`
- Metrics:
  - `POST /api/v1/metrics/ingest`
  - `GET /api/v1/metrics/latest` (por `hostId` o `snmpIp`)
  - `GET /api/v1/metrics/range` (por `hostId` o `snmpIp`)
- Alerts:
  - `GET /api/v1/alerts`
  - `PATCH /api/v1/alerts/{id}/resolve`
- Rules:
  - `GET /api/v1/rules`
  - `POST /api/v1/rules`
  - `PATCH /api/v1/rules/{id}`
- SNMP targets:
  - `POST /api/v1/snmp/targets`
  - `GET /api/v1/snmp/targets`
  - `GET /api/v1/snmp/targets/{id}`
  - `PATCH /api/v1/snmp/targets/{id}`
  - `DELETE /api/v1/snmp/targets/{id}`
  - `POST /api/v1/snmp/targets/{id}/metrics/enableAll`
  - `POST /api/v1/snmp/targets/{id}/metrics/disableAll`
- Catalogo:
  - `GET /api/v1/metrics/catalog`
- Salud:
  - `GET /health`

### Worker
- Polling SNMP v2c operativo.
- Insercion de metricas SNMP en `MetricSamples`.
- Evaluacion de reglas genericas por ventana (`windowMinutes` + operador + threshold).
- Generacion de alertas por reglas con consolidacion:
  - una sola alerta abierta por `hostId + metricKey`
  - si el problema persiste, se actualizan `lastTriggerAtUtc`, `lastTriggerValue` y `occurrences`
  - ya no se crean filas nuevas por cada re-disparo del mismo problema abierto.
- Seguimiento de estado SNMP por target:
  - `ConsecutiveFailures`
  - `LastSuccessUtc`
  - `LastFailureUtc`
- Alertas por falla de polling SNMP:
  - metrica `snmp_poll_failure` (se refleja en modulo Alertas UI)
  - auto-resolucion cuando el target vuelve a responder.
- Retention job configurable (desactivado por defecto).

### UI
- Modulos operativos: `Resumen`, `Targets`, `Agentes`, `Metricas`, `Alertas`, `Reglas`.
- CRUD de Agentes y Targets desde interfaz.
- Targets con separacion explicita: `Nombre`, `IP`, `HostId`, `HostName`.
- Alertas con resumen por dispositivo y detalle de `Nombre equipo`, `HostId`, `IP host`.
- Alertas separadas en:
  - `Alertas activas`
  - `Historial agrupado`
- Historial agrupado por `host + metricKey` para evitar listados eternos del mismo problema.
- Auto-refresh sin F5:
  - Targets: 8s
  - Agentes: 9s
  - Alertas: 8s
  - Reglas: 10s (pausa si hay cambios sin guardar)
- UX: modales internos (no dialogs del navegador), menu de acciones `...`, toasts/skeleton/empty states.

## Seguridad y observabilidad
- Seguridad actual: API Key por `X-Api-Key` (MVP).
- Pendiente: mTLS de extremo a extremo.
- Observabilidad: Serilog en API/Worker + health check DB.

## Pendientes para etapa produccion (no bloquean MVP)
- Endurecimiento de seguridad (mTLS, rotacion de claves).
- Empaquetado y despliegue como servicios (Windows/Linux).
- Suite de pruebas automatizadas (integracion/E2E/carga).
- Alert dispatch externo (webhook/canales) con politicas definitivas.
- Agente: hardening avanzado de labels/tags y seguridad mTLS (base de buffer/backoff/servicio ya implementada en `CentralMonitoring.Agent`).

## Infra local (dev)
- PostgreSQL local:
  - `Host=localhost;Port=5432;Database=central_monitoring;Username=central_app;Password=Central123!`
- Migraciones:
  - `dotnet ef database update --project CentralMonitoring.Infrastructure --startup-project CentralMonitoring.Api`
