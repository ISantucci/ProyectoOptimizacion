# ProyectoOptimizacion — instrucciones para agentes

Arena survivor 3D cenital, inspirado en Brotato (en el menú dice *Arena Survivor*). Unity **6000.3.12f1**, URP 17.3, Input System, TextMesh Pro. Destino: **build Web en itch.io**. Rama de trabajo: **`preproduction`**.

## Dónde vive la producción

El juego está en este repo; **la producción vive en Vaultrum**:

```txt
C:\Users\ControlEquipos\Documents\GitHub\Vaultrum\06_Proyectos\ProyectoOptimizacion\
  ProyectoOptimizacion.md      el cuaderno: estado, decisiones, pendientes. Leelo primero.
  01_Produccion/               TL y RQ (qué hay que hacer y su criterio de cierre)
  05_Programacion/             SOL y EJ (cómo se resolvió y qué se ejecutó)
  06_Calidad/ 08_Metricas/ ... el resto de las áreas, si existen
```

- Antes de tocar código, leé el cuaderno y el `RQ` que nombra la orden.
- Cada trabajo deja su artefacto según la skill del área (`Vaultrum\02_Agencia\Area <área>\Skills\<skill>\SKILL.md`): programación → `SOL` + `EJ`; calidad → `QA`.
- **Nunca escribas en las capas de sistema del vault (`01_` a `05_`).** Solo en `06_Proyectos\ProyectoOptimizacion\`.

## Tablero

Jira **PO** (https://santuccignacio.atlassian.net/jira/software/projects/PO/boards/34), con la forma de FHZ. Épica `PO-1` = `TL-001`, `PO-2` = `TL-002`; tareas por área (etiquetas `programacion`, `uiux`, `arte`, `calidad`, `metricas`, `gamedesign`, `produccion`, `owner`); errores como `BUG-NNN`. Lo etiquetado `owner` no lo hace un agente.

Estados: Por hacer → En curso → En revisión → Finalizada. Para moverlos usá el **id** de transición (los nombres quedaron cruzados): `11` Por hacer · `21` En curso · `31` En revisión · `41` Finalizada. Una tarea de agente termina en **En revisión**; Finalizada la pone el owner o el `QA`. Si no tenés las herramientas de Atlassian, cerrá tu respuesta con `Tablero: PO-n → <estado>`.

## Reglas de arquitectura (del TP, no se negocian)

- Como máximo **5 MonoBehaviour** (hoy 4: `GameManager`, `CustomUpdateManager`, `UIManager`, `AudioManager`).
- Como máximo **10 `[SerializeField]`** por script (agrupá en clases serializables, como `ConfigReferences`).
- **Sin singletons.** Un solo `Update`: `CustomUpdateManager` hace tick a sistemas de C# puro (`ITickable`).
- `ObjectPool` para todo lo que se instancia seguido; configuración en `ScriptableObject`; eventos del `GameManager` con Observer.

## Unity y el MCP

- MCP `ai-game-developer` en `http://localhost:29060` (Window > AI Game Developer, modo **Custom**). Necesita **Unity abierto** con este proyecto.
- Después de tocar código: esperá la compilación y leé `console-get-logs`. **Cero errores** antes de seguir.
- Después de tocar escena o prefab: guardá (`scene-save` / `assets-prefab-save`). La escena es una sola: `Assets/Scenes/samplescene.unity`.
- **No lances procesos externos desde el Editor** (git, instaladores, shells por `script-execute`): el Editor espera al proceso y se congela. Git va por la terminal.

## Git

- Trabajá en `preproduction`, commits chicos con mensaje en castellano.
- **No** `push`, `reset`, `checkout`/`switch` de otras ramas, `rebase`, `merge`, `stash` ni borrar archivos. Lo que sobra se mueve a `_to_delete/`.
- `.mcp.json`, `.claude/settings.local.json` y `.claude/skills/` son de esta máquina: no se versionan.

## Telemetría

`Assets/Data/Telemetry/TelemetryConfig.asset`: no mandes eventos desde el Editor (`_sendFromEditor: 0`) y no toques la URL ni el token de la planilla.

## Cómo cerrar cada orden

Al final, en pocas líneas: qué hiciste · archivos tocados · cómo lo verificaste (con números) · qué quedó pendiente · `Tablero: PO-n → <estado>`. Si algo no se pudo, decilo como **FALLO** con el motivo, no como nota al pie.
