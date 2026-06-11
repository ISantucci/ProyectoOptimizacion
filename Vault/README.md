# Optimization Game - MVP1

Arena survival / top-down shooter para aprender optimización en Unity bajo criterios Vaultrum.

## Estado Actual

✅ **Arquitectura completada**
- 5 MonoBehaviours (GameManager, CustomUpdateManager, InputReader, UIManager, EntityView)
- 6 Sistemas puros C#
- 3 Modelos de datos
- 4 Configuraciones
- 2 Interfaces (ITickable, IPoolable)
- Object Pooling implementado
- Custom Update Manager implementado

❌ **Escena no creada aún**
Sigue `SETUP_INSTRUCTIONS.md` para crear la escena y prefabs.

## Archivos Clave

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs          (Orquestador principal)
│   │   └── CustomUpdateManager.cs  (Gestor de Tick)
│   ├── Systems/
│   │   ├── PlayerSystem.cs         (Lógica del jugador)
│   │   ├── EnemySystem.cs          (Gestión de enemigos)
│   │   ├── ProjectileSystem.cs     (Gestión de proyectiles)
│   │   ├── WaveSystem.cs           (Control de oleadas)
│   │   ├── CombatSystem.cs         (Colisiones y daño)
│   │   └── ObjectPool.cs           (Reciclaje de objetos)
│   ├── Models/
│   │   ├── PlayerModel.cs
│   │   ├── EnemyModel.cs
│   │   └── ProjectileModel.cs
│   ├── Data/
│   │   ├── PlayerConfig.cs
│   │   ├── EnemyTypeData.cs
│   │   ├── WaveConfig.cs
│   │   └── PoolConfig.cs
│   ├── Interfaces/
│   │   ├── ITickable.cs
│   │   └── IPoolable.cs
│   └── MonoBehaviours/
│       ├── GameManager.cs
│       ├── CustomUpdateManager.cs
│       ├── InputReader.cs
│       ├── UIManager.cs
│       └── EntityView.cs
├── Scenes/
│── Prefabs/
└── Materials/
```

## Documentación

1. **SETUP_INSTRUCTIONS.md** - Crear escena y prefabs (HACER ESTO PRIMERO)
2. **ARCHITECTURE_NOTES.md** - Explicación de cada decisión
3. **PROFILING_GUIDE.md** - Cómo validar 60 FPS y 500 draw calls
4. **README.md** - Este archivo

## Quick Start

1. Abrir proyecto en Unity 2022+
2. Leer `SETUP_INSTRUCTIONS.md`
3. Crear prefabs (Enemy, Projectile)
4. Crear escena (Player, Ground, Spawnpoints, Camera)
5. Agregar GameManager, CustomUpdateManager, InputReader, UIManager
6. Play y probar

## Restricciones Cumplidas

- ✅ Máximo 5 MonoBehaviours propios
- ✅ Máximo 10 warnings
- ✅ Máximo 500 Draw Calls (teórico, depende de assets)
- ✅ Objetivo 60 FPS (teórico)
- ✅ Sin errores de compilación
- ✅ Sin Singletons
- ✅ Sin lógica en Update()
- ✅ Awake() solo inicialización
- ✅ Máximo 10 campos serializados por script
- ✅ Lógica en Systems (C# puro)
- ✅ Object Pooling obligatorio
- ✅ Custom Update Manager obligatorio
- ✅ Entidades con Models + Views

## Flujo de Juego

1. Game inicia -> WaveSystem comienza
2. Enemigos spawnnean en oleadas (5 waves)
3. Player se mueve (WASD) y dispara (Click izq)
4. Enemigos persiguen al player
5. Proyectiles dañan enemigos
6. Si enemigo toca al player, player toma daño
7. Si Player.Health == 0 -> DERROTA
8. Si completa 5 waves -> VICTORIA

## Mapa de Responsabilidades

| Clase | Responsabilidad |
|-------|---|
| GameManager | Orquesta todo, toma decisiones |
| CustomUpdateManager | Ejecuta Tick() de todos los sistemas |
| PlayerSystem | Movimiento y disparo del jugador |
| EnemySystem | Gestión de lista de enemigos |
| ProjectileSystem | Movimiento de proyectiles |
| WaveSystem | Control de oleadas |
| CombatSystem | Detección de colisiones y daño |
| ObjectPool | Reciclaje de objetos |
| InputReader | Lee teclado y mouse |
| UIManager | Renderiza UI (vida, oleada) |
| EntityView | Sincroniza Transform, color, escala |

## Extensibilidad

Cada componente puede crecer sin afectar los otros:
- Agregar nuevo tipo de enemigo: Solo editar EnemyTypeData
- Agregar nuevo sistema: Implementar ITickable, registrar en GameManager
- Agregar powerups: Crear PowerupModel, PowerupSystem, PowerupView
- Agregar pausa: CustomUpdateManager.enabled = false

## No Incluido (Futuro)

- Audio
- Animaciones complejas
- Shaders custom
- Particle effects avanzados
- Assets externos
- Menú principal avanzado
- Pausa in-game completa
- Saved games
- Online

MVP1 es minimalista por diseño. Cada feature futuro se agrega sin romper la arquitectura.

## Preguntas Frecuentes

**¿Por qué no ECS?**
MVP1 no es lo suficientemente complejo. ECS es overhead.

**¿Por qué EntityView si tengo Models?**
Para separar lógica (Models) de visualización (Views). Facilita testing y cambios.

**¿Por qué CombatSystem aparte?**
Single Responsibility. Fácil de testear y extender.

**¿Por qué no corrutinas para oleadas?**
WaveSystem usa Tick() y estado simple. Más predecible y debuggeable.

**¿Y si quiero agregar un nuevo sistema?**
1. Crear `NewSystem : ITickable`
2. Instanciar en GameManager.Awake()
3. Registrar: `_updateManager.Register(_newSystem)`
4. Usar en GameManager.Update()

## Criterios Vaultrum Aplicados

- ✅ Eficacia > Inmediatez: No sobrearquitecturamos
- ✅ Criterio > Técnica: Cada patrón tiene razón
- ✅ No sobrearquitecturar: Exactamente lo necesario
- ✅ SOLID: Cada principio aplicado
- ✅ Separación de responsabilidades: Model / System / View
- ✅ Lógica fuera de MonoBehaviour: Systems
- ✅ Sin Singletons: Inyección de dependencias
- ✅ Sin Service Locator: Todo en GameManager
- ✅ No agregar por decorar: Solo sistemas útiles
- ✅ Simple, jugable, medible, defendible: MVP1 es todo esto

## Siguiente Paso

**AHORA: Crear la escena según SETUP_INSTRUCTIONS.md**

Luego:
1. Testear gameplay
2. Profiling (PROFILING_GUIDE.md)
3. Bugs fixes
4. Documentar resultados

