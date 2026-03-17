# Progreso - Programa 2 (Agente)

## Objetivo del Programa 2
- Recolectar metricas del host (Windows/Linux), empaquetarlas y enviarlas al Programa 1.
- Operar con `HostId` creado desde la UI de Programa 1 (`Agentes`).

## Estado actual real
- Agente implementado en `CentralMonitoring.Agent` como Worker .NET.
- Envio de ingesta operativo hacia `POST /api/v1/metrics/ingest` con `HostId` + `X-Api-Key`.
- Collectors reales implementados:
  - `agent_cpu_usage_pct` (CPU del proceso agente)
  - `cpu_usage_pct` (CPU total del sistema)
  - `load1`, `load5`, `load15` (solo Linux)
  - `mem_total_mb`, `mem_available_mb`, `mem_used_mb`, `mem_used_pct` (memoria sistema)
  - `disk_total_mb`, `disk_free_mb`, `disk_used_mb`, `disk_used_pct` (por unidad con labels)
  - `disk_read_bytes_per_sec`, `disk_write_bytes_per_sec`, `disk_queue_len` (I/O, Linux)
  - `net_rx_bytes_total`, `net_tx_bytes_total`, `net_rx_bytes_per_sec`, `net_tx_bytes_per_sec` (por interfaz)
  - `net_rx_errors`, `net_tx_errors`, `net_rx_drops`, `net_tx_drops` (por interfaz)
  - `proc_cpu_pct`, `proc_mem_mb` (top N procesos)
- `service_up` (servicios críticos Windows/systemd configurables)
- `service_up` queda sin servicios por defecto en configuracion base; solo debe activarse cuando se definan servicios criticos explicitamente
  - `uptime_seconds`
  - `agent_heartbeat`
- Programa 1 ya esta listo para recibir ingesta del agente por API.
- UI de Programa 1 ya soporta ciclo de vida de host/agente:
  - Crear, editar, eliminar host.
  - Copiar `HostId`.
  - Ver estado (`Created`, `Connected`, `Confirmed`).

## Prerequisitos minimos del servidor central (Programa 1)
- API activa y alcanzable desde el agente.
- Endpoint de ingesta disponible: `POST /api/v1/metrics/ingest`.
- API key valida (`X-Api-Key`).
- Host creado en UI (`Agentes`) y `HostId` copiado.

## Contrato de ingesta que cumple el agente
- DTO enviado:
  - `hostId: Guid` (obligatorio)
  - `timestampUtc: DateTime?` (UTC)
  - `metrics: List<{ key, value, labelsJson? }>`

## Config minima del agente (MVP actual)
- `ApiBaseUrl` (ej. `http://127.0.0.1:5188`)
- `ApiKey`
- `HostId`
- `FlushIntervalSeconds` (recomendado 15-30)
- `RequestTimeoutSeconds`
- `BufferFilePath`
- `MaxBufferedBatches`
- `BackoffInitialSeconds`, `BackoffMaxSeconds`, `BackoffMultiplier`, `BackoffJitterRatio`
- `TopProcessesCount`
- `CriticalWindowsServices`
- `CriticalLinuxSystemdUnits`

## Fases y tareas (estado)

### Fase 1: Nucleo de recoleccion y envio
- [x] Collectors base (CPU/memoria/disco/red/uptime).
- [x] Cliente HTTP con envio periodico a `POST /api/v1/metrics/ingest`.
- [x] Config por archivo/ENV.
- [x] Validacion de respuesta HTTP y log.

### Fase 2: Fiabilidad y resiliencia
- [x] Buffer local persistente en archivo JSON (`Agent:BufferFilePath`).
- [x] Backoff exponencial con jitter en errores de red/API y HTTP no exitoso.
- [x] Control de overflow: descarte del batch mas antiguo cuando se supera `MaxBufferedBatches`.

### Fase 3: Observabilidad del agente
- [x] Logging estructurado via `ILogger` con contexto de batch/intentos/pendientes.
- [x] Hardening base de configuracion (`AgentOptions.Validate()` al inicio).
- [x] Metricas internas del agente (logs operativos):
  - cola pendiente
  - reintentos
  - drops por overflow
  - ultimo envio exitoso

### Fase 4: Servicio del sistema
- [x] Soporte de ejecucion como `Windows Service` y `systemd` (host configurado para ambos).
- [x] Scripts base Windows:
  - `CentralMonitoring.Agent/scripts/install-windows-service.ps1`
  - `CentralMonitoring.Agent/scripts/uninstall-windows-service.ps1`

### Fase 5: Seguridad avanzada (post-MVP)
- [ ] mTLS cuando Programa 1 lo habilite.
- [ ] Rotacion de claves / firma de payload opcional.

## Como validar que el agente quedo bien integrado
- En Programa 1 (`Agentes`): estado debe pasar de `Created` a `Connected/Confirmed`.
- En `Metricas`: deben verse las metricas del `HostId`.
- En `Reglas/Alertas`: al forzar un umbral, debe generarse alerta.

## Endpoints relevantes del servidor
- Ingesta: `POST /api/v1/metrics/ingest`
- Hosts: `GET /api/v1/hosts`, `POST /api/v1/hosts`, `PATCH /api/v1/hosts/{id}`, `DELETE /api/v1/hosts/{id}`
- Verificacion: `GET /api/v1/metrics/latest?hostId=...`, `GET /api/v1/rules`, `GET /api/v1/alerts?resolved=false`

## Checklist rapido para el siguiente sprint del agente
- [x] Crear esqueleto del proceso agente + config.
- [x] Implementar collectors base (CPU, RAM, disco, red, uptime).
- [x] Implementar envio a `/metrics/ingest` con API key.
- [x] Implementar buffer local y reintentos con backoff.
- [x] Hardening base de operacion y validacion de config.
- [ ] Enriquecer labels (hostname, os, interfaz estandarizada).
- [ ] Crear smoke test: ingesta visible en UI + alerta de prueba.

## Referencia de arquitectura comercial (cloud + licencias + APK)
- Documento base: `ARQUITECTURA-CLOUD-LICENCIAS.md`
- Proximo hito recomendado:
  - construir `Control Plane` minimo (auth + activacion/licencia + cuotas),
  - luego integrar onboarding de licencia en Programa 1,
  - despues arrancar APK con consumo de resumen multi-central.
