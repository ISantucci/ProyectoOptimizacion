# MVP1 — Checklist de Configuración en Unity Editor

**Versión:** 1.0  
**Objetivo:** Configurar spawns, enemigos y oleadas para MVP1  
**Criterio:** Sin MonoBehaviours nuevos, sin refactor, solo configuración  

---

## PARTE 1: GAMEOBJECTS EN ESCENA

### 1.1 Player Spawn

**Paso 1:** En Scene (SampleScene.unity), búsca el GameObject llamado `PlayerSpawn`.

- ✓ Si existe → salta a Paso 3
- ✗ Si NO existe → Paso 2

**Paso 2 (si no existe):**
- Clic derecho en Hierarchy → Create Empty
- Renombra a `PlayerSpawn`
- Position: X=0, Y=0, Z=0
- Guardá la escena (Ctrl+S)

**Paso 3:** Verifica que existe un GameObject `GameBootstrapper` en la escena.

**Paso 4:** En GameBootstrapper, en el Inspector:
- Campo: `_playerSpawnPoint`
- Asigná (drag) el `PlayerSpawn` desde Hierarchy

**Confirmación:** `_playerSpawnPoint` NO debe estar en rojo ni vacío.

---

### 1.2 Enemy Spawn Points

**Paso 1:** Crea un container vacío para ordenarlos.
- Clic derecho en Hierarchy → Create Empty
- Renombra a `SpawnPoints`
- Position: X=0, Y=0, Z=0

**Paso 2:** Dentro de `SpawnPoints`, crea 6 GameObjects vacíos con estos nombres y posiciones exactas:

| Nombre | X | Y | Z |
|---|---|---|---|
| EnemySpawnPoint_00 | 0 | 0 | 12 |
| EnemySpawnPoint_01 | 10 | 0 | 6 |
| EnemySpawnPoint_02 | 10 | 0 | -6 |
| EnemySpawnPoint_03 | 0 | 0 | -12 |
| EnemySpawnPoint_04 | -10 | 0 | -6 |
| EnemySpawnPoint_05 | -10 | 0 | 6 |

**Cómo:** Para cada uno:
- Clic derecho en `SpawnPoints` → Create Empty
- Renombra (ej: EnemySpawnPoint_00)
- En Inspector, Position exacta como tabla arriba

**Paso 3:** Guardá la escena (Ctrl+S).

**Paso 4:** En GameBootstrapper, Inspector:
- Campo: `_enemySpawnPoints` (verás un array)
- Size: 6
- Element 0: Arrastra `EnemySpawnPoint_00` desde Hierarchy
- Element 1: Arrastra `EnemySpawnPoint_01`
- Element 2: Arrastra `EnemySpawnPoint_02`
- Element 3: Arrastra `EnemySpawnPoint_03`
- Element 4: Arrastra `EnemySpawnPoint_04`
- Element 5: Arrastra `EnemySpawnPoint_05`

**Confirmación:** Todos 6 elementos deben tener referencias (no en rojo).

---

## PARTE 2: PREFABS MÍNIMOS

### 2.1 Crear estructura de carpetas

En Assets/ (dentro del Project):
1. Clic derecho → New Folder → `Prefabs`
2. Dentro de Prefabs → New Folder → `MVP1`

Estructura final: `Assets/Prefabs/MVP1/`

### 2.2 Enemy Prefab

**Paso 1:** En Assets/Prefabs/MVP1/
- Clic derecho → Create → Capsule
- Renombra el objeto a `EnemyCapsule`

**Paso 2:** Configura visualmente:
- Transform:
  - Position: 0, 0, 0
  - Scale: 1, 1, 1
- Add Component → Material (asigna cualquier color visible, ej. red)

**Paso 3:** Agrega EntityView:
- En el Inspector, "Add Component"
- Busca `EntityView`
- Selecciona `EntityView` (debe estar en TopDownShooter.Views)
- **IMPORTANTE:** Asigna en los campos de EntityView:
  - `_renderers[]`: Size 1, Element 0 = el Renderer de la cápsula

**Paso 4:** Implementa IPoolable:
- EntityView ya implementa IPoolable (verifica que aparece en script)

**Paso 5:** Haz prefab:
- Arrastra `EnemyCapsule` (GameObject) a `Assets/Prefabs/MVP1/`
- Unity preguntará → Select "Prefab"
- El GameObject en Hierarchy se volverá azul (es prefab)

**Confirmación:** Debe existir `Assets/Prefabs/MVP1/EnemyCapsule.prefab`

### 2.3 Projectile Prefab

**Paso 1:** En Assets/Prefabs/MVP1/
- Clic derecho → Create → Sphere
- Renombra a `ProjectileCapsule`

**Paso 2:** Configura:
- Transform:
  - Scale: 0.2, 0.2, 0.2 (más chico que enemigo)
  - Position: 0, 0, 0
- Add Component → Material (color visible, ej. yellow)

**Paso 3:** Agrega EntityView:
- Add Component → EntityView
- En `_renderers[]`: Size 1, Element 0 = Renderer de esfera

**Paso 4:** Haz prefab:
- Arrastra `ProjectileCapsule` a `Assets/Prefabs/MVP1/`

**Confirmación:** Debe existir `Assets/Prefabs/MVP1/ProjectileCapsule.prefab`

### 2.4 HitVFX Prefab

**Paso 1:** En Assets/Prefabs/MVP1/
- Clic derecho → Create → Cube
- Renombra a `HitVFX`

**Paso 2:** Configura:
- Transform:
  - Scale: 0.1, 0.1, 0.1 (muy chico, es un efecto)
  - Position: 0, 0, 0
- Add Component → Material (color visible, ej. white)

**Paso 3:** Agrega EntityView:
- Add Component → EntityView
- En `_renderers[]`: Size 1, Element 0 = Renderer del cube

**Paso 4:** Haz prefab:
- Arrastra `HitVFX` a `Assets/Prefabs/MVP1/`

**Confirmación:** Debe existir `Assets/Prefabs/MVP1/HitVFX.prefab`

---

## PARTE 3: ASIGNAR PREFABS AL POOL

### 3.1 Configurar PoolConfig

**Paso 1:** En Assets/SO/ (o donde esté), abre `PoolConfig.asset`

**Paso 2:** En Inspector, verifica campos:
- `EnemyViewPrefab`: Está vacío → Arrastra `EnemyCapsule.prefab` aquí
- `ProjectileViewPrefab`: Está vacío → Arrastra `ProjectileCapsule.prefab` aquí
- `HitVFXPrefab`: Está vacío → Arrastra `HitVFX.prefab` aquí

**Confirmación:** Los tres campos NO deben estar en rojo.

---

## PARTE 4: CONFIGURAR WAVES

### 4.1 Verificar EnemyData existentes

**Paso 1:** En Assets/SO/
- Abre `EnemyDataMelee.asset`
- Verifica que tiene valores (no está vacío)
- Si está vacío, asigna stats sugeridos:
  - MaxHealth: 30
  - MoveSpeed: 2.5
  - AttackDamage: 10
  - AttackRange: 1.5
  - AttackCooldown: 1.2
  - DetectionRange: 30
  - AttackType: Melee
  - ProjectileSpeed: 8
  - ScoreValue: 10

**Paso 2:** Abre `EnemyDataRanged.asset`
- Verifica que tiene valores
- Si está vacío, asigna:
  - MaxHealth: 20
  - MoveSpeed: 3.5
  - AttackDamage: 5
  - AttackRange: 5
  - AttackCooldown: 1.5
  - DetectionRange: 30
  - AttackType: Ranged
  - ProjectileSpeed: 8
  - ScoreValue: 15

### 4.2 Configurar WaveConfig

**Paso 1:** En Assets/SO/, abre `WaveConfig.asset`

**Paso 2:** Verifica o modifica para tener 3 waves:

**Wave 0:**
- Enemies: 1 entrada
  - EnemyData: `EnemyDataMelee`
  - Count: 3
- SpawnInterval: 0.75
- PreWaveDelay: 1

**Wave 1:**
- Enemies: 2 entradas
  - Entrada 1: EnemyData = `EnemyDataMelee`, Count = 4
  - Entrada 2: EnemyData = `EnemyDataRanged`, Count = 2
- SpawnInterval: 0.65
- PreWaveDelay: 2

**Wave 2:**
- Enemies: 2 entradas
  - Entrada 1: EnemyData = `EnemyDataMelee`, Count = 6
  - Entrada 2: EnemyData = `EnemyDataRanged`, Count = 4
- SpawnInterval: 0.5
- PreWaveDelay: 2

**DelayBetweenWaves:** 3

**Cómo:**
- En Inspector de WaveConfig:
- "Waves" → Size: 3
- Para cada Wave:
  - Expand Wave
  - Enemies → Size: (1 o 2 según arriba)
  - Para cada Enemy entry:
    - Drag EnemyData asset al campo
    - Set Count

**Confirmación:** WaveConfig debe tener 3 waves completas sin campos vacíos.

---

## PARTE 5: VALIDAR REFERENCIAS EN GAMBOOTSTRAPPER

Abre `GameBootstrapper` en escena, Inspector. Verifica:

| Campo | Debe tener | Tipo |
|---|---|---|
| `_updateManager` | UpdateManager del GameObject | MonoBehaviour |
| `_uiController` | UIController del GameObject | MonoBehaviour |
| `_inputReader` | InputReader del GameObject | MonoBehaviour |
| `_waveConfig` | WaveConfig.asset | ScriptableObject |
| `_playerConfig` | PlayerConfig.asset | ScriptableObject |
| `_poolConfig` | PoolConfig.asset | ScriptableObject |
| `_playerSpawnPoint` | PlayerSpawn (GameObject Transform) | Transform |
| `_enemySpawnPoints[0-5]` | Cada spawnpoint | Transform array (6 elementos) |
| `_playerVisual` | PlayerVisual (GameObject) | Transform |

Si alguno está vacío (rojo), asignalo.

---

## PARTE 6: TEST BÁSICO

### 6.1 Entra a Play Mode

- Clic Play (o Ctrl+P)

### 6.2 Verifica

- ✓ El juego no crashea
- ✓ No hay errores en Console
- ✓ Los enemigos aparecen en la escena
- ✓ Están en posiciones de los spawnpoints
- ✓ Avanzan hacia el centro (jugador)
- ✓ Aparecen primeros 3 enemigos (Wave 0)
- ✓ Cuando mueren, aparecen más (Wave 1)
- ✓ Console no está rojo

### 6.3 Si hay errores

- Copia el error exacto
- Reportá en qué línea y en qué script

---

## PARTE 7: GUARDAR Y CONFIRMAR

**Paso 1:** Exit Play Mode

**Paso 2:** Guardá todo (Ctrl+S)

**Paso 3:** Confirmá que sigues en 5/5 MonoBehaviours:
- GameBootstrapper ✓
- UpdateManager ✓
- EntityView ✓
- InputReader ✓
- UIController ✓

**Paso 4:** Confirmá que NO creaste MonoBehaviours nuevos.

---

## CHECKLIST FINAL

Antes de reportar éxito:

- [ ] PlayerSpawn existe y está asignado
- [ ] 6 EnemySpawnPoints creados y asignados (array de 6)
- [ ] EnemyCapsule.prefab creado
- [ ] ProjectileCapsule.prefab creado
- [ ] HitVFX.prefab creado
- [ ] Prefabs asignados en PoolConfig
- [ ] EnemyDataMelee tiene valores
- [ ] EnemyDataRanged tiene valores
- [ ] WaveConfig tiene 3 waves
- [ ] GameBootstrapper está wireado (todas referencias)
- [ ] Play Mode: Sin crashes, sin errores
- [ ] Play Mode: Enemigos spawnean y se mueven
- [ ] Sigues en 5/5 MonoBehaviours
- [ ] Ningún MonoBehaviour nuevo fue creado

---

## SI ALGO NO SE PUEDE HACER

Si encuentras que algo no se puede asignar o no funciona, reportá:

1. **Qué intentaste hacer**
2. **Qué error ves en Console**
3. **En qué línea del script**
4. **Qué referencia falta**

No inventes que funcionó si no funcionó.

---

## PRÓXIMO PASO

Una vez que MVP1 está configurado y funcionando:

1. Reportá el estado exacto
2. Listaá qué quedó hecho
3. Listaá qué problemas quedaron
4. Indicá si podés avanzar a siguiente iteración

No avances a UI, pausa, profiling ni polish sin completar esto.

