# CLAUDE.md — DigitatonGame (Passive Amnesia: UAO Edition)

Juego 2D pixel art (vista top-down 3/4) ambientado en la Universidad Autónoma de Occidente (UAO). Unity 6000.6, URP con Renderer 2D, Input System nuevo, Cinemachine y TextMeshPro. Estado actual: el **Campus ya usa sprites reales** (suelo isométrico, edificios, Ágora, árboles); los **interiores siguen en greybox** (con algunos muebles/pisos con sprite) y el flujo del guion usa textos temporales en inglés.

## Reglas del proyecto
- Todo el código y los assets propios viven en `Assets/_Project`. Planos de referencia: `Docs/mapa_uao.jpg` (greybox original) y `Assets/_Project/Docs/mapa_uao_2.png` (distribución real con caminos, base del Campus actual).
- **Escala: todos los sprites a 48 PPU** (1 casilla = 48x48 px = 1 unidad), Filter Point, Compression None. Andrés mide 1x2 casillas (su prefab lleva `localScale` porque su hoja es de alta resolución).
- Textos de UI y del juego: **inglés**. Comentarios y nombres de objetos del greybox: español.
- Input: solo el **nuevo Input System** (`Active Input Handling = Input System Package (New)`). Se lee con `Keyboard.current`/`InputAction` en código. Si se cambia ese ajuste, **hay que reiniciar Unity** (si no, `Keyboard.current` es null y nada responde).
- Cámara: Pixel Perfect, **640x360 de referencia a 48 PPU** (se ven 13,3 x 7,5 casillas), `PixelSnapping`, fija sobre Andrés (sin suavizado) y fondo negro fuera del mapa (las salas más chicas que la vista quedan centradas). **El zoom se cambia solo en `PixelCameraSettings`** (más resolución = más mapa visible); `CameraRigBinder` lo aplica al iniciar cada escena. Orden de dibujo por eje Y (`Transparency Sort Mode = Custom Axis (0,1,0)`).
- Saltos de línea: LF (`.gitattributes`).

## Leyenda de colores (fija, ver `PlaceholderPalette`)
Blanco jugador · Azul aulas · Morado administrativos/servicios · Rojo salud · Naranja comida · Verde zonas verdes · Beige caminos · Gris oscuro calles/parqueaderos · Negro muros/bordes · Amarillo puertas/entradas · Rosado NPC · Cian objetos interactivos y retos.
Toda cosa del greybox lleva un `PlaceholderInfo` (nombre, descripción del asset final, tamaño en px, responsable). La tabla de todos está en `Docs/PLACEHOLDERS.md` (se regenera con el menú `DigitatonGame ▸ Generar PLACEHOLDERS.md`).

## Escenas (todas en el Build Profile, en `Assets/_Project/Scenes`)
`MainMenu`, `Campus`, `Aulas1`–`Aulas4`, `EdificioCentral`, `Sotanos`, `Bienestar`, `Cafeteria`, `SalaProfesores`, `TechoAulas3`, `MuseoLili`, `Tutorial`.
- No existen `Intro` ni `Agora`: la introducción (el accidente) son pantallas de texto sobre negro (`StoryFlow.IntroCaptions`, saltables con una tecla) y el juego empieza en `Bienestar`. El atril de `Aulas2` es el reto 4. El antiguo `AgoraTrigger` quedó obsoleto (`StartStory` marca `agora_done`).
- El campus quedó al **60 %** del tamaño original: las coordenadas viejas se multiplican por 0,6.
- Cada interior tiene su puerta de salida que devuelve a Andrés al `Campus` frente a la puerta por la que entró.

## Arquitectura (scripts en `Assets/_Project/Scripts`)
- **GameManager** (`Core`): singleton persistente, creado solo desde `Resources/GameManager.prefab` antes de cargar cualquier escena. Guarda recuerdos (0–12), marcas del guion (`flags`, p. ej. `done:q5`, `agora_done`), último spawn, pausa y resultados. Hace los cambios de escena con fundido (`TransitionTo`, opcionalmente con texto sobre negro). `ResetProgress()` borra todo (lo llama Play).
- **SceneTransition.Go(escena, spawnId)**: fachada que usan puertas y menús.
- **SpawnPoint(id)**: dónde aparece Andrés. `PlayerController` lo busca al cargar (`SpawnPoint.Resolve`); si no hay id pendiente usa el `isDefault`.
- **Door**: lleva a `targetScene`/`targetSpawnId`. Sin escena destino → "Coming soon". `requiredFlag` bloquea la puerta con `lockedMessage` hasta que exista esa marca (`devBypass` la abre sola si se da Play directo en una escena del juego).
- **Interactable / PlayerInteractor**: "Press E" cerca de objetos con `Collider2D`. Subclases: `Door`, `ToastInteractable` (NPC con mensaje), `ChallengeInteractable` (retos del guion: suma recuerdos, no se repite, muestra "(pending)"), `DebugInteractable`.
- **UI** (prefab del GameManager): fundido y texto sobre negro (`ScreenFader`), `ToastMessage`, `PauseMenu` (Esc), `NotebookPanel` (C), `MemoryHud` ("Memories: X/12", solo en gameplay), `ResultsPanel`.
- **Minijuegos y diálogos** (`Scripts/Minigames`, toda la UI se arma por código con `PixelUi`; bloquean al jugador con `GameManager.MinigameOpen` / `DialogueOpen`):
  - `DialoguePanel` (diálogo con máquina de escribir; `DialogueLine(speaker, text)`), `NpcChallengeInteractable` (Lucía: diálogo → `MemoryReconstructionGame` de neuronas → diálogo de cierre con repaso de errores).
  - `ClassErrorHunt` = lección **"Atrapa el error"** en el atril de `Aulas2` (`ClassLessonInteractable`, marca `done:class1`). 8 frases de estudiantes (5 con error): STOP o dejar pasar con tiempo límite → ubicar la pieza mala → elegir la corrección. Puntaje 18; se aprueba con el 70 % (`passFraction`). Si no aprueba: guía por tipo de error (sin dar la respuesta; nivel 2 de la guía y +25 % de tiempo en cada reintento) y reintento con frases nuevas que refuerzan lo que falló. Frases, tipos de error y guías: `ErrorHuntContent`. Puntaje y intentos quedan en `GameManager.SetStat("class1:score" ...)`.
  - **Habla de Andrés** (`SpeechRecovery`): con 0–3 recuerdos habla un español entrecortado, con 4–7 mezcla (palabras en inglés resaltadas en cian) y con 8+ habla inglés fluido (`MixedAt` / `FluentAt`). Las líneas de Andrés se escriben en 3 versiones con `new DialogueLine("Andrés", inglés, mezcla con {palabras recuperadas}, español)`; `fluent = true` lo deja siempre fluido (clase antes del accidente). El HUD muestra "SPEECH x% - etapa" y avisa cuando mejora (`MemoryHud`). Las líneas por defecto de Lucía viven en código (`NpcChallengeInteractable.DefaultIntro`), no en la escena, para que un merge no las pise.
  - **Bloques de palabras** (`SentenceBuilder`): panel inferior reutilizable (`SentenceBuilder.Open(instrucción, new[]{ new BuilderSlot(...) }, onSubmit)`; `Hide()/Show()/Reload()/SetFeedback()/Close()`); solo puede haber uno abierto. `CameraRigBinder` sube la cámara mientras está abierto.
  - **Reto 2 – la frase mueve el mundo** (`LitterChallenge`, prefab `Prefabs/Reto2_Basura.prefab`, marca `done:r2_litter`, +2): Valentina (con "!") junto a Aulas 1; la frase decide qué pasa (sin agente nadie se mueve). Menú `DigitatonGame ▸ Agregar reto 2 (basura) al Campus` (reemplaza la caneca provisional).
  - **Reto 3 – los exámenes** (`ExamChallenge` en la pila de exámenes de `SalaProfesores`, marca `done:r3_exams`, +2): 3 frases activas → pasiva con bloques; el "libro de consulta" (arriba a la izquierda) se va aclarando con cada examen aprobado. Si falla: mensaje rojo + guía (`ErrorHuntContent.Hint`; al 3.er fallo explica la regla). El atril del reto 4 pide `done:r3_exams` (`ClassLessonInteractable.requiredChallenge`; no bloquea en modo desarrollo). Menú `DigitatonGame ▸ Agregar reto 3 (exámenes) a SalaProfesores`.
  - Para agregar el atril sin reconstruir la escena: menú `DigitatonGame ▸ Agregar lección a Aulas2 (sin reconstruir)`.
- **CameraRig** (prefab): Cinemachine + Pixel Perfect + Confiner 2D; se conecta solo a Andrés y a `MapBounds`.
- **Colisiones**: el jugador y los NPC colisionan solo en los pies; edificios y palmas del campus, solo en su base (se puede pasar por detrás).

## Estándar de TODO reto (obligatorio)
1. **Andrés queda congelado mientras se juega**: `var lock = GameManager.Instance.LockInput();` al abrir y `lock.Release()` al cerrar **y** en `OnDestroy` (idempotente; cada reto tiene su propio bloqueo, nada de bools compartidos; `ResetProgress` invalida los anteriores). Se toma **antes** de armar la UI.
2. **Feedback cuando se equivoca**: (a) visible al instante y dice **qué** pasó, (b) **guía** sin la respuesta (`ErrorHuntContent.Hint(tipo, nivel)`; el nivel sube desde el 3.er intento), (c) permite **reintentar** sin perder lo hecho.

## Flujo del juego (la historia completa está en `Docs/HISTORIA.md`)
Idea central: en este país todos hablan en pasiva y nadie nombra al responsable; Andrés recupera su identidad al **recuperar al agente** (`by + agent`) y termina diciendo *"The workers will be heard by me."*
Play (menú → `GameManager.StartStory()`) → introducción del accidente (6 pantallas) → despierta en `Bienestar` → **R1** Lucía (neuronas, +4) → campus abierto (`done:q1_4`) → **R2** Valentina con "!" junto a Aulas 1 (+2) → **R3** exámenes en `SalaProfesores` (por Aulas 3, +2) → **R4** recuerdo de clase en `Aulas2` (+2) → **R5** confrontación en la obra (pendiente) → resultados (pendiente).
- Orden, introducción y **objetivo en pantalla**: `Scripts/Core/StoryFlow.cs` (`Steps`; `Built = true` al construir un reto). `GameManager.ProgressChanged` actualiza el HUD. Habla de Andrés: `SpeechRecovery` (0–3 confusa, 4–7 mezcla, 8+ fluida).
- **Sprites de personajes** (`Art/Sprites Personajes`): `NpcSpriteSlicer` (menú `Recortar sprites de NPC`) corta las 40 hojas en 3 columnas x 4 filas de 20x32 px (filas abajo/izquierda/derecha/arriba; columna 1 = quieto), pivote en los pies, 16 PPU (mide 2 u, igual que Andrés), Point y sin compresión; `NpcSpriteSlicer.Load(n)` devuelve los 12 cuadros. `NpcSpriteSetup` (menú `Poner sprites a los NPC`) los asigna a Valentina, Santiago, Estudiante (Aulas2), Don Jairo (Campus) y Doña Marta (Cafetería). Lucía usa `Enfermera.png` con `NpcSpriteAnimator` (idle y hablar).
- **Peatones de ambiente** (`AmbientCrowd` + `AmbientWalker`, prefab `Peatones_Campus`, 11 en el Campus, rutas al 60 %, con sprites que miran hacia donde caminan): solo están activos los que caen en cámara. Menú `Agregar peatones al Campus`.

## Rendimiento y WebGL
- Ajustes aplicados (`optimize-web`): build target WebGL, Gzip + Decompression Fallback (GitHub Pages no manda `Content-Encoding`), Managed Stripping High, IL2CPP Optimize Size, Quality Low sin vSync/MSAA, Input solo nuevo (`activeInputHandler = 1`). Compilación de prueba: 13 MB, carga y se mueve en el navegador. `Builds/` está ignorada por git.
- `PlaceholderInfo` solo crea rótulos en Editor/Development build y se apaga en la versión final.
- Pruebas automáticas: `DialoguePanel.SimulatedAdvance = true` avanza un diálogo; `PlayerController.SimulatedInput` simula movimiento (el teclado real no llega al juego si el Editor no tiene foco).

## Campus con sprites reales (isométrico)
- **No usar los menús "Construir ..." de `DigitatonGame`**: regenerarían el greybox y borrarían el Campus con sprites y los interiores reescalados (y las ediciones de todo el equipo). Para cambios en esas escenas, editarlas directamente.
- Suelo: pasto repetible generado con los verdes del TILE SET (`Art/Terreno/GrassTile.png`) + un **Tilemap isométrico** (Grid `Isometric`, celda ~5 x 2,5 u) con los rombos de `TILE SET` (assets `Art/Tiles/TILE_f_c.asset`) para los caminos.
- Edificios (`Aulas1–4`, `CAF y BIENESTAR`): sprites isométricos con **pivote en la base** (en Aulas, 3 cortes para ordenar bien a Andrés a lo largo de la fachada) y `PolygonCollider2D` solo en la huella del suelo, así que se puede pasar por detrás. `ARCO UAO` (Edificio Central) es una vista cenital: va como decorado de suelo con colisión en sus bloques. El Ágora es decorado de suelo con colisión solo en los troncos de las palmas. Árboles de `Outside` con colisión solo en el tronco.
- Las puertas amarillas, spawns, NPC, retos y el trigger del Ágora son los mismos objetos del greybox, recolocados en la entrada visible de cada edificio.
- Herramientas (sin menú, se llaman desde código; son de un solo uso): `SpriteSetup` (importación a 48 PPU, corte de hojas, tiles, pasto, cámara), `IsoCampusBuilder` (rearma el Campus en su lugar; repetible) e `InteriorRescale` (**no repetir**: escala los interiores una vez, 0,65). `EscuelaDecorator` (A4/A5/Inside) sigue siendo independiente.

## Constructores de greybox (menú `DigitatonGame`) — OBSOLETOS para Campus e interiores
Las escenas del greybox se generaban por código para que fueran reproducibles; **volver a ejecutarlas pisa las ediciones a mano** de esas escenas (y ya no reproducen el estado actual).
- `Construir campus completo` → `CampusBuilder` (escena Campus según el plano; 0,25 u por píxel del plano).
- `Construir interiores` → `InteriorsBuilder` (Aulas1–4, EdificioCentral, Sotanos, Bienestar, Cafeteria, SalaProfesores, TechoAulas3, MuseoLili).
- `Construir flujo de menús` → `MenuFlowBuilder` (prefabs de UI, MainMenu y Tutorial). `Agregar UI de historia al GameManager` → `StoryUiBuilder` (solo agrega lo que falta al prefab).
- `Construir base del greybox` → `GreyboxBuilder` (prefabs Andrés y CameraRig; solo crea las escenas que faltan).
- Convención de spawns: entradas a interiores `Aulas2_A`, `EC_Quincha`, `Cafeteria_Entrance`…; regresos al campus `Campus_Aulas2_A`, `Campus_EC_Quincha`, `Campus_Cafeteria`, `Campus_FromBienestar`…

## Cómo probar
- Recorrido completo: ver la sección "Cómo probar" del último commit o `Docs/` del equipo. Modo desarrollo: dar Play directo en cualquier escena del juego abre las puertas del campus.
- Pruebas por código en el editor: simular teclas con `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.D))` (sin `InputSystem.Update()`), esperar un frame y soltar con `new KeyboardState()`.
- Si Play se queda en el fotograma 1 (editor sin visibilidad), probar en modo edición con consultas de física.
