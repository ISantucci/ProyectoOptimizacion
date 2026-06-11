# MVP1 Setup Instructions

## Proyecto Unity
- Unity 2022 LTS o superior
- New 3D Project (URP opcional pero recomendado)

## Estructura de Carpetas
Está creada. Todos los scripts están en `Assets/Scripts/`

## MonoBehaviours (Máximo 5)
1. `GameManager` - Orquestador principal
2. `CustomUpdateManager` - Gestor de Tick personalizado
3. `InputReader` - Lectura de input
4. `UIManager` - Gestión de UI
5. `EntityView` - Vista genérica para entidades

## Pasos de Setup

### 1. Crear Prefabs

#### Enemy Prefab
- Crear GameObject con esfera (Mesh: Sphere)
- Escala: (2, 2, 2)
- Color: Rojo
- Agregar componente `EntityView`
- Guardar en `Assets/Prefabs/Enemy.prefab`

#### Projectile Prefab
- Crear GameObject con esfera pequeña (Mesh: Sphere)
- Escala: (0.3, 0.3, 0.3)
- Color: Amarillo
- Agregar componente `EntityView`
- Guardar en `Assets/Prefabs/Projectile.prefab`

### 2. Crear Escena

#### Plano Base
- Crear Plane en el centro (0, 0, 0)
- Escala: (20, 1, 20)
- Material: Gris

#### Cámara
- Posicionar en (0, 20, 0)
- Rotación: (90, 0, 0)
- Propiedad: Orthographic o Perspective con ajuste adecuado

#### Player
- Crear GameObject con cápsula (Mesh: Capsule)
- Posición: (0, 0, 0)
- Escala: (1, 1, 1)
- Color: Azul

#### Spawn Points (6 Transform)
```
EnemySpawnPoint_00: (0, 0, 12)
EnemySpawnPoint_01: (10, 0, 6)
EnemySpawnPoint_02: (10, 0, -6)
EnemySpawnPoint_03: (0, 0, -12)
EnemySpawnPoint_04: (-10, 0, -6)
EnemySpawnPoint_05: (-10, 0, 6)
```

### 3. Configurar GameManager
- Crear GameObject vacío llamado "GameManager"
- Agregar script `GameManager`
- Asignar referencias:
  - Player Transform: El GameObject del jugador
  - Spawn Points: Los 6 spawnpoints en orden
  - Enemy Prefab: Assets/Prefabs/Enemy.prefab
  - Projectile Prefab: Assets/Prefabs/Projectile.prefab
  - Update Manager: Asignar la instancia de CustomUpdateManager

### 4. Configurar CustomUpdateManager
- Crear GameObject vacío llamado "CustomUpdateManager"
- Agregar script `CustomUpdateManager`

### 5. Configurar InputReader
- Crear GameObject vacío llamado "InputReader"
- Agregar script `InputReader`
- Asignar referencias:
  - Game Manager: El GameManager
  - Main Camera: La cámara principal

### 6. Configurar UIManager
- Crear Canvas en la escena
- Crear TextMeshPro para Health (arriba a la izquierda)
- Crear Image para HealthBar (debajo del texto)
- Crear GameObject "UIManager"
- Agregar script `UIManager`
- Asignar referencias:
  - Game Manager: El GameManager
  - Health Text: El TextMeshPro
  - Health Bar: La Image

### 7. Tags y Layers
- Crear Tag "Enemy"
- Crear Tag "Projectile"
- Crear Tag "Player"

### 8. Input Manager
- Vertical y Horizontal ya están por defecto

## Flujo de Juego MVP1

1. Al iniciar, el GameManager inicia el WaveSystem
2. Los enemigos se spawnnean desde los 6 spawnpoints
3. El jugador se mueve con WASD
4. El jugador dispara con Mouse Click
5. Los enemigos persiguen al jugador
6. Si el jugador recibe daño por contacto, pierde vida
7. Si la vida llega a 0, GameOver
8. Si se completan todas las waves, Victoria

## Restricciones Cumplidas

- ✅ Máximo 5 MonoBehaviours propios
- ✅ Lógica en clases puras C# (Systems)
- ✅ Object Pooling obligatorio
- ✅ Custom Update Manager obligatorio
- ✅ Sin Singletons
- ✅ Separación de responsabilidades (Model-View)
- ✅ Arquitectura simple y defendible

## Testing Básico

1. Verificar que el jugador se mueve con WASD
2. Verificar que el jugador dispara hacia el mouse
3. Verificar que los enemigos aparecen y se mueven hacia el jugador
4. Verificar que los proyectiles impactan y dañan enemigos
5. Verificar que el jugador pierde vida por contacto
6. Verificar que muere si la vida llega a 0
7. Verificar que gana si completa 5 waves
