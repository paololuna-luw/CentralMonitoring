# Progreso - Frontend (UI)

## Estado actual (implementado)
- Stack base creado en `ui/` con Angular standalone + Tailwind.
- Layout principal con sidebar y header.
- Soporte de tema claro/oscuro con toggle y persistencia en `localStorage` (`cm-theme`).
- Interceptor de `X-Api-Key` integrado para consumo de API.
- Servicios API creados:
  - `snmp-targets.service.ts`
  - `metrics.service.ts`
  - `alerts.service.ts`
  - `rules.service.ts`
- Vistas base creadas y navegables:
  - Targets
  - Metricas
  - Alertas
  - Reglas
- Targets: detalle/edicion por target (`GET/PATCH`), delete, toggles por OID y acciones masivas `enableAll/disableAll`.
- Metricas: selector de target + auto refresh configurable (off/15s/30s/60s) + estado de frescura por `freshMinutes`.
- Reglas: edicion inline por fila (`PATCH`) incluyendo `operator`, `threshold`, `windowMinutes`, `severity`, `snmpIp`, `enabled`.
- Alertas: filtros por severidad/fecha, busqueda por `metricKey`/`hostId` y feedback visual al resolver.
- Targets UX operativa: `lastSuccessUtc`, `lastFailureUtc`, `consecutiveFailures`, estado visual reforzado y confirmacion de delete con modal.
- Reglas UX: validaciones en create/edit (`operator`, `windowMinutes > 0`, `severity`), estado dirty por fila y accion de cancelar cambios.
- Pulido visual: toasts globales, skeleton loaders reutilizables y empty states mejorados en vistas principales.
- Rediseño visual aplicado: paleta carbon/cobre, tipografia editorial (display + sans), fondos atmosfericos y nueva portada `Resumen` como dashboard principal.
- Targets: formulario de creacion directo en UI (`POST /api/v1/snmp/targets`) y columna `HostId` mapeada por IP desde `GET /api/v1/hosts`.
- Metricas: filtro por fuente (`SNMP IP` o `HostId` de agente) usando `GET /api/v1/metrics/latest` con `snmpIp` o `hostId`.
- Alertas: resumen por dispositivo (host/ip) con severidad maxima, conteo y drill-down a detalle filtrado.
- Targets SNMP alineados con Agentes: campos separados `Nombre`, `IP` y `HostId` en crear/listar/editar; alta permite seleccionar `HostId` (autocompleta IP por backend) o capturar IP manual.
- Targets listado/detalle: columna/campos `HostName` visibles para trazabilidad de dispositivo.
- Agentes (`/hosts`): accion de eliminar host habilitada en UI + endpoint `DELETE /api/v1/hosts/{id}`.
- Agentes (`/hosts`): edicion habilitada via menu de acciones (3 puntos) + endpoint `PATCH /api/v1/hosts/{id}`.
- Estilo de scroll refinado para dark mode (thumb delgado, track sutil, hover coherente con paleta).
- Alertas: cada tarjeta y resumen por dispositivo muestran `Nombre equipo`, `HostId` e `IP host`.
- Refresco automatico sin recargar navegador:
  - Targets cada `8s`,
  - Agentes cada `9s`,
  - Alertas cada `8s`,
  - Reglas cada `10s` (con pausa si hay cambios no guardados).
- Build de frontend validado (`npm run build` OK).

## Endpoints en uso
- Targets: `GET /api/v1/snmp/targets?ip=`
- Targets detail/edit: `GET /api/v1/snmp/targets/{id}`, `PATCH /api/v1/snmp/targets/{id}`, `DELETE /api/v1/snmp/targets/{id}`
- Targets toggles masivos: `POST /api/v1/snmp/targets/{id}/metrics/enableAll`, `POST /api/v1/snmp/targets/{id}/metrics/disableAll`
- Metricas: `GET /api/v1/metrics/latest?snmpIp=&freshMinutes=`
- Alertas: `GET /api/v1/alerts?resolved=false`, `PATCH /api/v1/alerts/{id}/resolve`
- Reglas: `GET /api/v1/rules`, `POST /api/v1/rules`, `PATCH /api/v1/rules/{id}`
- Catalogo: `GET /api/v1/metrics/catalog` (conectado en UI de Targets)

## Roadmap siguiente (prioridad)
1. Siguiente iteracion UX
- Historial de toasts (opcional) y variantes por accion.
- Skeletons especificos por tabla/card para mayor fidelidad.
- Microinteracciones de transicion en filtros y tablas.

## Riesgos y bloqueos actuales
- En algunos entornos Windows hay bloqueo de bind en `localhost` con puertos especificos.
- Mitigacion actual: correr API con `--urls "http://127.0.0.1:<puerto>"` y alinear `ui/src/app/env.ts`.

## Convencion de trabajo
- Toda funcionalidad nueva se registra en este archivo al cerrar cada bloque.
- Cualquier cambio de UX/tema tambien se anota aqui y en `DESIGN-UI.md` si afecta lineamientos de diseno.
