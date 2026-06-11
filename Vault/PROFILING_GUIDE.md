# Guía de Profiling MVP1

## Objective
Cumplir:
- 60 FPS en build standalone
- Máximo 500 Draw Calls
- Máximo 10 warnings
- Sin errores de consola persistentes

## Herramientas

### 1. Unity Profiler
**Window > Analysis > Profiler**

#### CPU
- Frame Rate: ¿Estamos en 60 FPS?
- Script code: ¿Cuánto tiempo gastan Systems?
- Physics: ¿Está activada la física? (en MVP1, no la necesitamos)

#### GPU
- Frame time: ¿Está limitada por GPU?
- Batches: ¿Cuántos draw calls hay?

#### Memory
- Total Memory: ¿Usamos mucha memoria?
- GC Alloc: ¿Hay garbage collection frecuente?

### 2. Frame Debugger
**Window > Analysis > Frame Debugger**

- Ver exactamente qué se renderiza
- Contar draw calls reales
- Identificar batches ineficientes

### 3. Console
**Window > General > Console**

- Buscar warnings/errors
- Máximo 10 warnings permitidos
- Eliminar errores persistentes

## Profiling Workflow MVP1

### Paso 1: Baseline
1. Abrir Profiler
2. Play mode
3. Esperar 2-3 segundos estable
4. Notar FPS, draw calls, memoria

### Paso 2: CPU
1. Click en "CPU" en Profiler
2. Expandir "PlayerLoop"
3. Buscar spikes de GameManager.Update()
4. Notar si Systems.Tick() es lento

Optimización típica:
- Si GameManager.Update() es lento:
  - ¿Hay muchas colisiones a checkear?
  - ¿El WaveSystem pregunta demasiado?
  - Reducir la frecuencia de chequeos

### Paso 3: GPU
1. Click en "GPU" en Profiler
2. Notar "Batches"
3. Abrir Frame Debugger
4. Contar draw calls

Optimización típica MVP1:
- Batching estático: Ground, spawnpoints (si los renderizamos)
- Batching dinámico: Enemigos, proyectiles
- Usar solo 2-3 materiales en total

Draw calls objetivo:
```
Ground: 1 call
Player: 1 call
Enemigos: ~50 calls (si hay 50 enemigos)
Proyectiles: ~100 calls (si hay 100 proyectiles)
UI: 5-10 calls
Total: ~150-170 calls (bien dentro de 500)
```

### Paso 4: Memory
1. Click en "Memory"
2. Notar GC.Alloc
3. Si hay picos, ahí hay garbage collection

Optimización MVP1:
- ObjectPool ya recicla enemigos/proyectiles
- Verificar que no se crean listas innecesarias
- Usar `List.Clear()` en lugar de `new List()`

### Paso 5: Build
1. File > Build Settings
2. Standalong PC (Windows, Linux, Mac)
3. Development build (para profiling)
4. Build
5. Profiler > Select Window > Seleccionar exe

Verificar FPS en build (no editor).

## Checklist Profiling

- [ ] Profiler abierto
- [ ] FPS >= 60 (editor)
- [ ] FPS >= 60 (build)
- [ ] Draw calls <= 500
- [ ] GC Alloc bajo (< 1 KB/frame)
- [ ] Warnings <= 10
- [ ] Errors == 0 (persistentes)
- [ ] Frame Debugger muestra batching correcto
- [ ] Memory < 500 MB

## Optimizaciones Rápidas (si es necesario)

### Si FPS < 60

1. **Reducir enemigos simultáneos**
   - WaveConfig.EnemyCount: 10 -> 5

2. **Simplificar gráficos**
   - Enemigos: Sphere -> Cube
   - Texturas: None (solo color)
   - Shadows: Off

3. **Frustum culling**
   - Camera > Clipping planes: ajustar
   - Enemigos fuera de pantalla: desactivar

4. **Physics (si está activada)**
   - Physics > Gravity = 0
   - Sin Rigidbody

### Si Draw Calls > 500

1. **Batching estático**
   - Ground, decoraciones: Static

2. **Instancing**
   - Enemy material: Enable GPU instancing

3. **Reducir cantidad de objetos**
   - Menos enemigos
   - Menos proyectiles (pool más pequeño)

## Ejemplo de Sesión Profiling

```
1. Play
2. Esperar 5 segundos (escenas se cargan)
3. Abrir Profiler
4. Notar: 
   - FPS: 65
   - Draw calls: 180
   - GC.Alloc: 0.5 KB/frame
   - Memory: 120 MB
5. Frame Debugger:
   - Batches: 15
   - Calls: 180 (12 batches dinámicos)
6. Todo OK, cerrar Profiler
7. Build > Test en exe
8. ✅ Completo
```

## Debug Output Útil

Agregar en GameManager.Update() temporal:

```csharp
if (Input.GetKeyDown(KeyCode.P))
{
    Debug.Log($"Players: {_playerModel.Health}");
    Debug.Log($"Enemies alive: {_enemySystem.AliveCount}");
    Debug.Log($"Projectiles: {_projectileSystem.Projectiles.Count}");
    Debug.Log($"Wave: {_waveSystem.CurrentWave}/{_waveSystem.TotalWaves}");
}
```

Presionar P para ver estado del juego.

## Profiling Obligatorio

La consigna pide "profiling obligatorio". Esto significa:

1. Ejecutar sesión de Profiler
2. Verificar FPS >= 60
3. Verificar draw calls <= 500
4. Documentar resultados en un archivo:

```
PROFILING_RESULTS.txt

Date: 2026-06-06
Platform: Windows Standalone

CPU:
- FPS: 62
- Frame time: 16.1ms

GPU:
- Draw calls: 185
- Batches: 18
- Texture memory: 45 MB

Memory:
- Total: 125 MB
- GC.Alloc: 0.3 KB/frame
- Heap: 80 MB

Result: ✅ PASS
```

Guardar este archivo en la raíz del proyecto.

