# Passive Amnesia — Historia conectada (guion canónico)

> Este documento es la **fuente de verdad** de cómo se conectan la historia, los retos y los diálogos. El código la sigue en
> `Scripts/Core/StoryFlow.cs` (orden, introducción y objetivos). Todos los textos del juego van en **inglés**; este documento está en español.
> Duración objetivo: **9–10 minutos** · Nivel: B1 · Temas: Passive Voice, ODS 8 (trabajo decente) y ODS 12 (producción y consumo responsables).

## 1. La idea que lo une todo

En este país todos hablan en pasiva, así que **nadie dice quién hizo las cosas** ("The bottles were thrown", "The wages were reduced").
Andrés perdió justamente eso: no sabe quién fue ni quién es. **Recuperar la identidad es recuperar al agente** (`by + agent`).
El juego termina cuando Andrés dice, por primera vez, una frase con agente y se nombra a sí mismo: *"The workers will be heard by me."*

Tono: serio y formal; lo absurdo sale solo de la premisa (los NPC narran todo en pasiva: "Lunch is being eaten by me").

## 2. Arco emocional

confusión (habla en español entrecortado) → recuperación técnica (mezcla español/inglés) → recuperación de propósito (inglés fluido) → **acción** (nombrarse como agente).

## 3. Orden y conexión de los retos

| # | Momento | Escena | Cómo se activa | Mecánica | Practica | ODS | Marca (`done:`) | Recuerdos | Habla al terminar | Estado |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Introducción: el accidente | Negro con textos (se salta con una tecla) | Play en el menú | — | — | — | `intro_done` | 0 | Confusa | **Hecho** |
| 1 | Hospital | `Bienestar` | Hablar con Lucía | Conectar neuronas + diálogo | La forma: `be` + participio | Mixto | `q1_4` | +4 → 4 | **Mezcla** | **Hecho** |
| 2 | Campus (Aulas 1) | `Campus` | Estudiante con "!" | La frase mueve el mundo | Significado y agente (`by`) | 12 | `r2_litter` | +2 → 6 | Mezcla | **Hecho** |
| 3 | Sala de profesores | `SalaProfesores` (por Aulas 3) | Escritorio con los exámenes | Corregir exámenes con bloques + libro que se completa | Transformar activa a pasiva | 8 y 12 | `r3_exams` | +2 → 8 | **Fluida** | Pendiente |
| 4 | Recuerdo de clase | `Aulas2` (atril) | Después del reto 3 | "Atrapa el error" (flashback) | Reconocer errores | 8 y 12 | `class1` | +2 → 10 | Fluida | **Hecho** |
| 5 | La obra | Obra del campus | Pista del reto 4 | La confrontación (preguntas con bloques) | Preguntas en pasiva y agente | 8 y 12 | `r5_confrontation` | +2 → 12 | Fluida | Pendiente |
| 6 | Cierre | Resultados | Al terminar el reto 5 | Pantalla de resultados | — | — | — | — | Fluida | Pendiente |

**Condiciones que conectan los pasos**
- Andrés empieza en `Bienestar` (cama) y **no puede salir** hasta completar el reto 1 (la puerta dice "Lucía: You must rest a little more.").
- Al completar el reto 1 se abren los edificios del campus (`done:q1_4`).
- El HUD muestra el **objetivo actual** (`StoryFlow`): el primer reto construido que falta. Un paso pendiente se salta hasta que se construya.
- Pendiente de implementar con el reto 3: que el atril del reto 4 pida `done:r3_exams`.

## 4. Guion de diálogos

### 0. Introducción (pantallas sobre negro, ~4,6 s cada una; se saltan con cualquier tecla)
1. "Andrés taught English at the Universidad Autónoma de Occidente."
2. "In this country, everybody speaks in the passive voice. Things are done. Nobody says who did them."
3. "Andrés believed that every action has a doer."
4. "Then, one day, an accident happened."
5. "A scaffold was left unsecured. By whom? Nobody knows."
6. "Your memory has been lost."

### 1. Hospital (hecho)
*Andrés despierta junto a la camilla. Lucía está junto a su escritorio. Andrés habla en español entrecortado.*
- **Andrés** (confuso): "Todo está en blanco... Quizás hablar con la gente me ayude a recuperar mis recuerdos, y a saber quién soy."
- **Lucía:** "You were found near the campus yesterday. You were brought here by a student."
- **Lucía:** "Your memories can be repaired. The pieces must be connected, one by one."
- **Andrés:** "Está bien... vamos a intentarlo."
- *Minijuego de neuronas (3 frases).* Si se equivoca: panel rojo con la regla.
- **Lucía:** "The pieces were connected. Your first memories have been restored!" + repaso de errores (frase, regla, frase correcta).
- **Andrés** (ya mezcla, con 4 recuerdos): "Ahora recuerdo... *I'm a teacher*. *English was taught* en mi salón."
- **Lucía:** "The campus can be explored now. Your other memories are waiting there."

### 2. Campus: la basura (hecho)
- **Valentina:** "The bottles are being thrown. The bin is being ignored. It is just... happening."
- **Andrés:** "Eso no está bien. Hay que limpiar el campus. Déjame decirlo bien." (mezcla: "Eso no está *right*. El campus *should be cleaned*...")
- *Constructor de bloques:* sujeto + verbo en pasiva + agente. **Sin agente nadie actúa:** "Collected... by whom? Nobody is named, so nobody moves."
- **Valentina** (al acertar): "The bottles were collected by me! The campus is being cleaned, thanks to one sentence."
- **Andrés:** "When the doer is named, things happen. I remember teaching this."

### 3. Sala de profesores: los exámenes (pendiente)
*Andrés encuentra una pila de exámenes sin calificar. El libro de consulta (tecla C) está borroso.*
- **Andrés:** "These are my students' exams. Their sentences are all in the active voice..."
- **Andrés:** "In this country, they must be passive. Let me fix them."
- *Mecánica:* cada examen muestra una frase activa; el jugador la reescribe con bloques (objeto al inicio, `be` + participio, agente). Cada vez que consulta el libro y luego acierta, **una página se aclara**.
- Frases de ejemplo: "The company pays the workers fairly." → "The workers are paid fairly by the company." · "Students recycle plastic bottles." → "Plastic bottles are recycled by students." · "The factory wastes energy." → "Energy is wasted by the factory."
- **Andrés** (fluido, al terminar): "I taught them this. Decent work. Responsible production. I remember why."

### 4. Recuerdo de clase (hecho; falta el marco de flashback)
*Recuerdo en tono sepia: Andrés enseña su última clase antes del accidente (habla fluido: es el Andrés de antes).*
- **Andrés:** "Good morning, class! Today we are practicing the passive voice."
- **Valentina:** "Professor, the sentences will be read by us, right?"
- **Andrés:** "Exactly! Listen carefully. If a sentence has a mistake, you must stop it."
- *Atrapa el error.* Si no aprueba: reporte con guía por tipo de error y reintento.
- **Valentina** (al aprobar): "Great class, professor! The mistakes were found by everyone."
- **Pista (pendiente):** al final, una estudiante lee su ensayo: "The workers on the building site are not paid fairly." → lleva a la obra.

### 5. La obra: la confrontación (pendiente)
*El contratista solo habla en pasiva, sin nombrar a nadie.*
- **Contratista:** "The wages were reduced." → **Andrés** arma: "By whom were the wages reduced?"
- **Contratista:** "The helmets were removed." → "By whom were the helmets removed?"
- **Contratista:** "The scaffold was left unsecured." → "By whom was the scaffold left unsecured?"
- Cada pregunta bien hecha suma una pieza al tablero de evidencias (ODS 8: salarios y cascos; ODS 12: material desperdiciado).
- **Contratista:** "...It was decided by the company. And the scaffold was left by... by me."
- **Andrés** (cierre, con bloques): "The workers will be heard by me."

### 6. Cierre
- Captions: "Andrés remembered who he was." · "Not because things were done to him..." · "...but because he named himself as the doer."
- Pantalla de resultados: precisión (errores de la clase y de cada reto), impacto en ODS 8 y 12 y comparación de errores del primer reto contra el último.

## 5. Recursos que se conectan
- **Habla de Andrés** (`SpeechRecovery`): 0–3 recuerdos confusa · 4–7 mezcla · 8+ fluida. Cada línea de Andrés se escribe en 3 versiones.
- **Objetivo en pantalla** (`StoryFlow` + `MemoryHud`): le dice al jugador qué sigue.
- **Libro de consulta** (cuaderno, tecla C): se va completando con cada reto (pendiente).
- **Claridad Mental** (pendiente): sube con aciertos y baja con errores seguidos; con claridad baja los bloques se ven borrosos pero hay **más** ayuda, no menos.
- **Peatones del campus** (`AmbientCrowd`): ambiente; solo están activos los que se ven en cámara.

## 6. Tiempo estimado (aproximado, no medido)
| Tramo | Tiempo |
|---|---|
| Introducción | 0:35 |
| Reto 1 (hospital) | 2:00 |
| Reto 2 (campus) | 1:30 |
| Reto 3 (exámenes) | 1:30 |
| Reto 4 (recuerdo de clase) | 1:15 |
| Reto 5 (confrontación) | 1:30 |
| Cierre y resultados | 0:45 |
| **Total** | **~9:05** (más caminata y errores) |
