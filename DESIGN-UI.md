# Diseño UI (Programa 1)

## Organización del código
- Frontend Angular en `ui/`, separado de proyectos .NET.
- API base configurable desde `ui/src/app/env.ts`.
- Header de autenticación: `X-Api-Key`.

## Dirección visual actual
- Estilo editorial/operativo: base carbón, acento cobre, contraste alto y superficies profundas.
- Tipografía dual:
  - Display: `Cormorant Garamond` (títulos/hero/secciones).
  - UI text: `Manrope` (tablas, formularios, controles).
- Fondo con atmósfera:
  - Gradientes radiales sutiles.
  - Superficies translúcidas con blur ligero.
- Componentes:
  - Bordes suaves, sombras profundas y jerarquía clara.
  - Estados visuales consistentes (ok/warn/danger).

## Estructura de experiencia
- Sidebar izquierda con navegación principal:
  - `Resumen`, `Targets`, `Métricas`, `Alertas`, `Reglas`.
- Header superior con contexto operativo.
- Página principal `Resumen` como dashboard de entrada:
  - Hero de contexto.
  - KPIs clave (targets habilitados, en falla, alertas críticas, reglas activas).
  - Listas resumidas de targets y alertas recientes.

## Patrones UX implementados
- Toast global reutilizable para feedback de acciones.
- Skeleton loaders reutilizables para estados de carga.
- Empty states con copy claro y orientación de siguiente acción.
- Confirmación modal para acciones destructivas (delete target).

## Módulos y enfoque
- Targets:
  - Gestión operativa completa (detalle, edición, delete, toggles de OIDs).
  - Telemetría de salud visible (`consecutiveFailures`, `lastSuccessUtc`, `lastFailureUtc`).
- Métricas:
  - Selector por target SNMP.
  - Auto-refresh configurable y señal de frescura por métrica.
- Alertas:
  - Filtros por severidad/fecha y búsqueda.
  - Resolución con feedback visual.
- Reglas:
  - Edición inline con validaciones, estado dirty y cancelación por fila.

## Lineamientos para próximas iteraciones
1. Mantener consistencia carbón/cobre en nuevos módulos.
2. Priorizar jerarquía visual y aire entre bloques (espacio como componente de diseño).
3. Evitar UI genérica; preferir títulos display + bloques narrativos de operación.
4. Reforzar microinteracciones (transiciones y feedback contextual sin ruido).
