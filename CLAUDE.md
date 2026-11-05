# CLAUDE.md — DigitatonGame (Passive Amnesia: UAO Edition)

Juego 2D pixel art (vista top-down 3/4) ambientado en la Universidad Autónoma de Occidente (UAO). Unity 6000.6, URP con Renderer 2D, Input System nuevo, Cinemachine y TextMeshPro. Estado actual: **greybox** (cubos de colores como placeholders) y flujo del guion con textos temporales en inglés.

## Reglas del proyecto
- Todo el código y los assets propios viven en `Assets/_Project`. El plano de referencia está en `Docs/mapa_uao.jpg` (no se importa a Assets).
- Textos de UI y del juego: **inglés**. Comentarios y nombres de objetos del greybox: español.
- Input: solo el **nuevo Input System** (`Active Input Handling = Input System Package (New)`). Se lee con `Keyboard.current`/`InputAction` en código. Si se cambia ese ajuste, **hay que reiniciar Unity** (si no, `Keyboard.current` es null y nada responde).
- Cámara estilo Pokémon Esmeralda: Pixel Perfect, 480x270 de referencia (30 casillas de ancho), 16 PPU, `PixelSnapping`, fija sobre Andrés (sin suavizado) y fondo negro fuera del mapa (las salas más chicas que la vista quedan centradas). **El zoom se cambia solo en `PixelCameraSettings`** (más resolución = más mapa visible, p. ej. 384x216 = zoom intermedio); `CameraRigBinder` lo aplica al iniciar cada escena. Orden de dibujo por eje Y (`Transparency Sort Mode = Custom Axis (0,1,0)`).
- Saltos de línea: LF (`.gitattributes`).

## Leyenda de colores (fija, ver `PlaceholderPalette`)
Blanco jugador · Azul aulas · Morado administrativos/servicios · Rojo salud · Naranja comida · Verde zonas verdes · Beige caminos · Gris oscuro calles/parqueaderos · Negro muros/bordes · Amarillo puertas/entradas · Rosado NPC · Cian objetos interactivos y retos.
Toda cosa del greybox lleva un `PlaceholderInfo` (nombre, descripción del asset final, tamaño en px, responsable). La tabla de todos está en `Docs/PLACEHOLDERS.md` (se regenera con el menú `DigitatonGame ▸ Generar PLACEHOLDERS.md`).

## Escenas (todas en el Build Profile, en `Assets/_Project/Scenes`)
`MainMenu`, `Campus`, `Aulas1`–`Aulas4`, `EdificioCentral`, `Sotanos`, `Bienestar`, `Cafeteria`, `SalaProfesores`, `TechoAulas3`, `MuseoLili`, `Tutorial`.
- No existen `Intro` ni `Agora`: el Intro ocurre en el "Intro classroom" de `Aulas2` y la Escena 5 en el Ágora del `Campus`.
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
  - Para agregar el atril sin reconstruir la escena: menú `DigitatonGame ▸ Agregar lección a Aulas2 (sin reconstruir)`.
- **CameraRig** (prefab): Cinemachine + Pixel Perfect + Confiner 2D; se conecta solo a Andrés y a `MapBounds`.
- **Colisiones**: el jugador y los NPC colisionan solo en los pies; edificios y palmas del campus, solo en su base (se puede pasar por detrás).

## Flujo del juego (guion, versión temporal)
Play → `Aulas2` "Intro classroom" ("Scene 1: Intro – class dialogue pending") → salir y caminar al Ágora → al entrar al círculo: fundido, "Your memory has been lost." (3 s) → `Bienestar` junto a la camilla → Lucía (preguntas 1–4, +4 recuerdos; antes la puerta dice "Lucía: You must rest a little more.") → campus abierto → retos 5 (candado, portería este), 6 (banda, Cafetería), 7 (canecas, junto a Aulas 1), 8 (puzzle, TechoAulas3), 9–11 (exámenes, SalaProfesores, +3), 12 (palmas del Ágora, solo con 11 recuerdos) → "Results screen (pending)" → volver al menú; Play vuelve a empezar de cero.

## Constructores de greybox (menú `DigitatonGame`)
Las escenas del greybox se generan por código para que sean reproducibles; **volver a ejecutarlas pisa las ediciones a mano** de esas escenas.
- `Construir campus completo` → `CampusBuilder` (escena Campus según el plano; 0,25 u por píxel del plano).
- `Construir interiores` → `InteriorsBuilder` (Aulas1–4, EdificioCentral, Sotanos, Bienestar, Cafeteria, SalaProfesores, TechoAulas3, MuseoLili).
- `Construir flujo de menús` → `MenuFlowBuilder` (prefabs de UI, MainMenu y Tutorial). `Agregar UI de historia al GameManager` → `StoryUiBuilder` (solo agrega lo que falta al prefab).
- `Construir base del greybox` → `GreyboxBuilder` (prefabs Andrés y CameraRig; solo crea las escenas que faltan).
- Convención de spawns: entradas a interiores `Aulas2_A`, `EC_Quincha`, `Cafeteria_Entrance`…; regresos al campus `Campus_Aulas2_A`, `Campus_EC_Quincha`, `Campus_Cafeteria`, `Campus_FromBienestar`…

## Cómo probar
- Recorrido completo: ver la sección "Cómo probar" del último commit o `Docs/` del equipo. Modo desarrollo: dar Play directo en cualquier escena del juego abre las puertas del campus.
- Pruebas por código en el editor: simular teclas con `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.D))` (sin `InputSystem.Update()`), esperar un frame y soltar con `new KeyboardState()`.
- Si Play se queda en el fotograma 1 (editor sin visibilidad), probar en modo edición con consultas de física.
