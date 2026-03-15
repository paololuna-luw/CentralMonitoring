# Central Monitoring – Visión General

## 1) Programa 1: Servidor Central (Windows Server + .NET)
**Objetivo**: Recibir métricas de agentes y SNMP, evaluarlas contra reglas, generar alertas y exponer API segura para ingestión/consulta.

**Componentes**
- API Web (ASP.NET Core)
  - Autenticación/autorización: JWT o ideal mTLS con agentes.
  - Endpoints: ingestión de métricas (push), consultas para UI/reportes, gestión de agentes, reglas/umbrales, targets SNMP.
- Worker / Scheduler (Windows Service)
  - Jobs: evaluación de reglas (OK/WARN/CRIT), generación de alertas, polling SNMP, enriquecimiento (tags, agregados, compactación).
- Base de datos (PostgreSQL + TimescaleDB recomendado)
  - Series de tiempo para métricas; tablas relacionales para inventario, reglas, usuarios, auditoría.
- Alerting / Notificaciones
  - Canales: email (SMTP), Telegram/Discord webhook, etc.
  - Dedupe y re-notificación (ej. si sigue crítico 10 min).

**Modelo de datos mínimo**
- agents (id, hostname, os, version, last_seen, status)
- metrics (time, agent_id, metric_name, value, labels)
- snmp_targets (id, ip, community/v3_creds, perfil, enabled)
- rules (metric, condición, threshold, ventana, severidad)
- alerts (rule_id, target, estado, opened_at, closed_at, ack_by)
- audit_logs (quién cambió qué y cuándo)

**Flujos**
- Push (agentes → central): agente junta métricas, firma/autentica, manda lote; API valida/persiste; Worker evalúa y alerta.
- Pull SNMP (central → red): Worker programa polling, consulta OIDs, normaliza, persiste series y evalúa reglas (interfaces, CPU, etc.).

**Puertos y comunicaciones**
- API: HTTPS 443
- PostgreSQL: 5432 solo interno
- SNMP: UDP 161 (desde central); traps opcional UDP 162

**Seguridad**
- Agentes no exponen puertos entrantes; solo llaman a la API.
- Central solo expone 443.
- Preferir SNMPv3 (authPriv); si v2c, communities por zona y ACL por IP.

**Tamaño objetivo (~15 servers + red)**
- 1 socket / 4 vCPU; 16 GB RAM; SSD 250 GB (Timescale para retención).

---

## Helper rápido para frontend (endpoints principales)
- Auth: header `X-Api-Key` (hoy `dev-api-key`).
- SNMP targets:
  - `GET /api/v1/snmp/targets`
  - `GET /api/v1/snmp/targets/{id}`
  - `POST /api/v1/snmp/targets`
  - `PATCH /api/v1/snmp/targets/{id}` con `metrics` (lista `{ key, oid, enabled }`); si se omite `metrics`, usa la lista global.
- Métricas:
  - Últimas por IP SNMP: `GET /api/v1/metrics/latest?snmpIp=<ip>`
  - Rango: `GET /api/v1/metrics/range?snmpIp=<ip>&from=...&to=...&metricKey=...`
  - Ingesta agentes: `POST /api/v1/metrics/ingest` (hostId + metrics).
- Hosts (agentes): `GET /api/v1/hosts`, `POST /api/v1/hosts`
- Reglas: `GET /api/v1/rules`, `POST /api/v1/rules`, `PATCH /api/v1/rules/{id}`
  - Bootstrap base agente por host: `POST /api/v1/rules/bootstrap/agent/{hostId}`
- Alertas: `GET /api/v1/alerts?resolved=false`, `PATCH /api/v1/alerts/{id}/resolve`

Notas: polling SNMP actual 15 s (`Snmp:PollIntervalSeconds`); las métricas SNMP llevan `labelsJson` con `"snmp_ip":"<ip>"`.

Dev local actual:
- API: `http://127.0.0.1:5188`
- UI: `http://localhost:4200` (`ui/src/app/env.ts` debe apuntar al mismo puerto de API)

## 2) Programa 2: Agente de Monitoreo (Windows/Linux, .NET)
**Objetivo**: Recolectar métricas locales, bufferizar brevemente y enviarlas al servidor central de forma segura y eficiente.

**Módulos**
- Collectors: CPU/RAM/Disk, network (rx/tx), servicios críticos (Windows Services / systemd), logs opcional.
- Buffer local: cola corta (5–30 min) si el central cae; reintentos.
- Sender: batch + compresión (gzip), retries con backoff, TLS/mTLS.

**Frecuencia y carga**
- Frecuencia típica: 10–60 s según métrica.
- Batch para reducir requests; impacto <1% CPU en uso normal.

**Instalación**
- Windows: servicio (sc.exe / NSSM / MSI).
- Linux: binario + unit systemd.

**Alcance (qué NO hace)**
- No decide alertas.
- No guarda histórico largo.
- No abre puertos entrantes.

---

## Estado actual del repo (Feb 17, 2026)
- `CentralMonitoring.Api`: ASP.NET Core con ingestión de métricas, hosts, alertas, reglas y targets SNMP; usa PostgreSQL.
  - Al crear host nuevo, auto-crea reglas base de agente (configurables en `Agent:AutoRules`), incluyendo `service_up` separado por tipo de host (`CriticalWindowsServices` vs `CriticalLinuxSystemdUnits`).
- `CentralMonitoring.Worker`: BackgroundService con polling SNMP, reglas genéricas, cooldown, retention y dispatch webhook (configurable).
- `CentralMonitoring.Agent`: Worker de agente con collectors reales (CPU proceso, memoria, disco, red, uptime), buffer local persistente, backoff exponencial y soporte de ejecucion como Windows Service/systemd.
- `CentralMonitoring.Infrastructure`: EF Core + migraciones (PostgreSQL/Timescale pendiente de habilitar).
- `CentralMonitoring.Domain`: Entidades `Host`, `MetricSample`, `AlertEvent`, `Rule`, `SnmpTarget`.
- `CentralMonitoring.Shared`: DTOs/enums compartidos para API y Worker.
