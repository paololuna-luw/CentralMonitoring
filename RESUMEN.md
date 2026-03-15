# Estado actual (Programa 1)

## Que esta funcionando
- API v1 con endpoints de hosts, ingest de metricas, consultas (`latest`/`range`) y alertas (GET/PATCH).
- Seguridad basica por API Key (`X-Api-Key`), Swagger accesible y health check en `/health` con verificacion de DB.
- Logging con Serilog en API y Worker (consola + archivos `logs/api-*.log`, `logs/worker-*.log`).
- SNMP:
  - Targets gestionables via `POST/GET/PATCH/DELETE /api/v1/snmp/targets`.
  - Polling cada 15s (v2c) con metricas de uptime, estado/admin de interfaz, trafico, errores y CPU.
  - Catalogo global en `GET /api/v1/metrics/catalog` (configurado en API y Worker).
  - Auto-regla "interface down" al crear un target (`Snmp:AutoRules:InterfaceDown`).
  - Tracking de fallos por target (`GraceEnabled=false` por defecto).
- Reglas genericas (`/api/v1/rules`) evaluadas por Worker, ademas de la regla hardcodeada `cpu_usage > 90`.
- UI Angular funcional para:
  - Targets: listado, detalle, edicion, delete, toggles por OID y acciones masivas enableAll/disableAll.
  - Metricas: consulta latest por `snmpIp` con `freshMinutes`.
  - Alertas: listar y resolver.
  - Reglas: crear y editar con PATCH.

## Pendientes principales
- Configurar `Alerting:WebhookUrl` para despachar alertas.
- Evaluar endurecimiento de seguridad (remover bypass dev o migrar a mTLS).
- Mejoras de UX en metricas (selector desde targets, auto-refresh, filtros avanzados).

## Como probar rapido
1. API: `dotnet run --project CentralMonitoring.Api` y abrir `http://localhost:5110/health`.
2. Worker: `dotnet run --project CentralMonitoring.Worker` y revisar `logs/worker-*.log`.
3. UI: `cd ui && npm start` y abrir `http://localhost:4200`.
4. Validar datos SNMP: `GET /api/v1/metrics/latest?snmpIp=<ip>&freshMinutes=2`.
