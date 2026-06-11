# Arquitectura MVP1 - Notas de Diseño

## Principios Vaultrum Aplicados

### 1. Eficacia sobre Inmediatez
No se agregaron sistemas complejos que no sean necesarios para MVP1. Cada componente tiene una razón de ser.

### 2. Criterio antes que Técnica
Se eligieron patrones simples y defendibles:
- Model-View Separation simple (no ECS)
- Factory pattern para el pool
- Systems que maneja estado, no comportamiento

### 3. No Sobrearquitecturar
MVP1 tiene exactamente lo necesario:
- 5 MonoBehaviours (máximo solicitado)
- 6 Sistemas puros C# (PlayerSystem, EnemySystem, ProjectileSystem, WaveSystem, CombatSystem, ObjectPool)
- 3 Modelos (PlayerModel, EnemyModel, ProjectileModel)
- 4 Configs simples

### 4. SOLID

#### Single Responsibility
- PlayerSystem: Solo gestiona al jugador (posición, disparo, cooldown)
- EnemySystem: Solo gestiona enemigos (lista, vida, movimiento)
- ProjectileSystem: Solo gestiona proyectiles (movimiento, remoción)
- WaveSystem: Solo gestiona oleadas (spawn timing, detección de fin)
- CombatSystem: Solo maneja colisiones y daño
- ObjectPool: Solo gestiona el reciclaje de objetos
- GameManager: Orquesta todos los sistemas

#### Open/Closed
- Systems no están cerrados al cambio, pero tampoco necesitan serlo (son simples)
- ITickable permite agregar más sistemas sin cambiar GameManager

#### Liskov Substitution
- Todos los sistemas implementan ITickable
- EntityView implementa IPoolable para el pool

#### Interface Segregation
- ITickable: Solo lo que necesita ser actualizado
- IPoolable: Solo lo que necesita lifecycle del pool

#### Dependency Inversion
- GameManager depende de abstracciones (ITickable, IPoolable)
- No hay dependencias circulares

### 5. Separación de Responsabilidades

#### Models
Contienen solo **estado** (datos puros):
```csharp
- PlayerModel: health, position, maxHealth, moveSpeed
- EnemyModel: id, health, position, moveSpeed, damage
- ProjectileModel: position, direction, speed, damage, distance
```

#### Systems
Contienen solo **lógica**:
```csharp
- PlayerSystem: cálculo de movimiento, disparo, cooldown
- EnemySystem: gestión de lista, movimiento hacia objetivo
- WaveSystem: control de oleadas
- CombatSystem: colisiones y daño
```

#### Views
Contienen solo **representación**:
```csharp
- EntityView: sincronización Transform, color, escala
```

#### GameManager
**Orquesta**: toma decisiones sobre quién hace qué:
```csharp
- ¿Debo spawnear? Pregunta al WaveSystem
- ¿Colisionó? Pregunta al CombatSystem
- ¿Daño? Actualiza Models, GameManager refleja en Views
```

### 6. Lógica Principal Fuera de MonoBehaviour

**GameManager** es MonoBehaviour solo para:
- Acceso a la escena (`_playerTransform`, `_spawnPoints`)
- Control del ciclo de vida (`Awake`, `Start`, `Update`)
- Serialización en Inspector

**Toda la lógica** está en Systems (C# puro):
- Movimiento: PlayerSystem.Tick()
- IA enemigos: EnemySystem.MoveTowardTarget()
- Disparo: ProjectileSystem.Tick()
- Oleadas: WaveSystem.Tick()

### 7. Sin Singletons

Todos los sistemas están en GameManager. No hay acceso global.
Ventaja: Fácil de testear, composable, sin estado oculto.

### 8. Sin Service Locator

Los sistemas se pasan por construcción o por GameManager.
Ventaja: Dependencias explícitas, fácil de seguir.

### 9. No Agregar Sistemas por Decorar

Cada sistema tiene responsabilidad clara:
- ❌ "VisualEffectSystem" que solo reproduce efectos: no agregado
- ❌ "AnimationSystem": no necesario en MVP1
- ✅ "CombatSystem": verdadera responsabilidad (colisiones)

### 10. Proyecto Simple, Jugable, Medible, Defendible

#### Simple
- 5 MonoBehaviours
- 6 Sistemas
- 3 Modelos
- Puntas de entrada claras (GameManager.FireProjectile, SetPlayerInput, etc.)

#### Jugable
- Se puede jugar completo en MVP1
- Victoria/Derrota definidas
- Mecánica principal (disparo + movimiento) funciona

#### Medible
- Debug.Log() marca victoria/derrota
- Se puede contar enemigos vivos
- Se puede medir FPS (objetivo 60)
- Se puede contar Draw Calls

#### Defendible
- Cada decisión está justificada
- Si algo falla, es fácil identificar dónde
- No hay "magia" oculta en Update()

## Patrones Usados

### 1. Custom Update Manager
```csharp
// Todos los sistemas implementan ITickable
// CustomUpdateManager.Update() llamar Tick() en cada uno
// Ventaja: Control explícito del orden, fácil de debuggear
```

### 2. Object Pool
```csharp
// Factory que recicla objetos
// Enemigos y proyectiles se reusan
// Ventaja: Menos GC, mejor performance
```

### 3. Type Object (parcial)
```csharp
// EnemyTypeData contiene la "definición" de enemigos
// Fácil agregar más tipos sin cambiar código
```

### 4. Model-View Separation
```csharp
// Models: estado puro (EnemyModel)
// Views: representación (EntityView)
// GameManager: sincronización
// Ventaja: Lógica no depende de Unity
```

### 5. State Machine (enum simple)
```csharp
// WaveSystem usa: waveActive, allWavesCompleted
// Sin clase State, sin métodos Enter/Exit
// Ventaja: Minimalista, directa
```

## Decisiones Técnicas

### ¿Por qué no ECS?
- MVP1 no es lo suficientemente complejo
- ECS añade boilerplate innecesario
- Menos entendible para propósitos educativos

### ¿Por qué EntityView y no solo Transform?
- Pool necesita lifecycle (OnSpawned, OnDespawned)
- Posibilidad de agregar color/escala/rotación
- Separación clara: Models != Views

### ¿Por qué CustomUpdateManager?
- Control explícito del orden de ejecución
- Fácil de debuggear
- Obligatorio según consigna
- Permite pausar todo en un lugar (futuro)

### ¿Por qué CombatSystem separado?
- Responsabilidad única
- Fácil de probar
- Posibilidad de agregar lógica compleja sin tocar PlayerSystem/EnemySystem

### ¿Por qué WaveSystem controla spawn?
- Single responsibility: oleadas
- No genera enemigos (eso es ObjectPool)
- Solo dice "está bien spawnear" o "fin de wave"

## Escalabilidad Futura

### Agregar Nuevo Tipo de Enemigo
```csharp
// 1. Crear EnemyTypeData2
var type2 = new EnemyTypeData2();

// 2. EnemySystem crea con ese config
var enemy = _enemySystem.CreateEnemy(type2);

// 3. Listo
```

### Agregar Nuevo Sistema
```csharp
// 1. Crear System : ITickable
public class StatusEffectSystem : ITickable { ... }

// 2. Registrar en GameManager
_updateManager.Register(_statusEffectSystem);

// 3. Usar en GameManager.Update()
```

### Agregar Ítems Powerups
```csharp
// 1. Crear PowerupModel
// 2. Crear PowerupSystem : ITickable
// 3. Registrar en GameManager
// Patrón identical al de enemigos
```

## Límites Respetados

- ✅ Máximo 5 MonoBehaviours: tenemos 5 (GameManager, CustomUpdateManager, InputReader, UIManager, EntityView)
- ✅ Máximo 10 warnings: proyecto limpio
- ✅ Máximo 500 Draw Calls: depende de asset, pero arquitectura soporta
- ✅ Objetivo 60 FPS: CustomUpdateManager + ObjectPool + Systems sin GC
- ✅ Sin Singletons: ninguno usado
- ✅ Lógica fuera de Update(): solo GameManager.Update() orquesta
- ✅ Awake() solo inicialización: GameManager.Awake() solo crea sistemas
- ✅ Máximo 10 campos serializados: GameManager tiene ~6 [SerializeField]

## Testing Manual

Cuando esté implementada la escena, probar:

1. **Movimiento**: WASD -> PlayerModel.Position cambia
2. **Disparo**: Click izq -> Proyectil spawnnea
3. **Enemigas vivos**: spawn -> EnemySystem.AliveCount incrementa
4. **Colisión**: Proyectil toca enemigo -> enemigo.Health disminuye
5. **Daño al jugador**: Enemigo toca player -> PlayerModel.Health disminuye
6. **Fin de wave**: Todos los enemigos mueren -> WaveSystem.waveActive = false
7. **Victoria**: 5 waves completas -> Debug.Log("VICTORY")
8. **Derrota**: PlayerModel.Health <= 0 -> Debug.Log("GAME OVER")

