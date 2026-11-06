# Prompts para la cinemática de introducción (pixel art)

> Objetivo: reemplazar las pantallas de texto sobre negro (`StoryFlow.IntroCaptions`) por una cinemática que narra el accidente
> hasta que Andrés despierta en `Bienestar`. Los prompts están en **inglés** porque los generadores de imágenes responden mejor así.
> Las instrucciones y los nombres de archivo van en español.

## 1. Cómo usar estos prompts

1. **Primero crea la referencia de estilo** (Prompt 0) y genera 3–4 variantes. Elige la que mejor se vea y guárdala como `REF_estilo.png`.
2. **Luego crea la referencia de personajes** (Prompts A y B). Si el generador acepta imágenes de referencia, sube también
   el sprite existente de Andrés (`Assets/_Project/Art/Sprites Personajes/...`) y el de Lucía (`Enfermera.png`) para que se parezcan.
3. **Genera cada toma** (Prompts 1–10) usando la referencia de estilo como imagen de entrada cuando el generador lo permita.
   Mantén la misma paleta y el mismo número de colores en todas las tomas.
4. **Descarga en PNG**, sin fondo si la toma tiene personajes sueltos. Usa el nombre de archivo de la tabla de la sección 4.

### Reglas para que todo se vea del mismo juego
- Pide siempre **pixel art** con **paleta limitada** (16–32 colores), **sin antialiasing** y **sin degradados suaves**.
- Resolución de generación: **1024 x 576** (16:9). Después, reduce cada imagen a **480 x 270** con vecino más cercano
  (en Photoshop/GIMP: "Nearest Neighbor" o "Sin interpolación") y amplía al tamaño de pantalla con la misma técnica.
  Así los pixeles quedan limpios y no se ven borrosos. Si el generador no da pixel art real, haz la reducción igual.
- Encuadre cinematográfico: planos anchos y medios, pocos primeros planos.
- Mantén la **vista lateral o de 3/4** del juego y la misma altura de cámara.
- Evita texto escrito dentro de la imagen (el texto lo pone Unity; ver la sección 5).

---

## 2. Prompts base

### Prompt 0 — Estilo general (genera primero)
```
Pixel art cinematic still, 16-bit style, limited palette of 24 colors, no anti-aliasing, crisp pixels,
top-down three-quarter view of a modern Latin American university campus, red brick and cream walls,
warm daylight, green trees, sidewalks, cinematic 16:9 composition, muted realistic tones with a
melancholic, quiet mood, clean silhouettes, no text, no watermark
```

### Prompt A — Andrés (personaje, referencia)
```
Pixel art character sprite reference sheet, adult male English teacher, around 35 years old, short dark hair,
light blue button shirt with rolled sleeves, dark trousers, brown leather shoes, carrying a lanyard with an
ID card, neutral pose, front view, full body, plain light gray background, 16-bit style, limited palette,
no anti-aliasing, crisp outline
```

### Prompt B — Lucía (enfermera, referencia)
```
Pixel art character reference, adult female nurse, white uniform with a light blue trim, hair tied back,
small campus name badge, calm expression, standing front view, full body, plain light gray background,
16-bit style, limited palette, no anti-aliasing
```

### Prompt C — Estudiantes y ambiente (referencia)
```
Pixel art reference sheet of three young university students, backpacks, casual clothes, one holding a phone
with the screen glowing, front and back views, plain background, 16-bit style, limited palette, no anti-aliasing
```

---

## 3. Tomas de la cinemática (una imagen por toma)

Cada toma corresponde a una pantalla o a un momento de la narración. Son 10 tomas que cubren desde la clase
hasta que Andrés despierta.

### Toma 01 — La clase (memoria antes del accidente)
```
Pixel art cinematic still, interior of a classroom at a Latin American university, a male English teacher in a
light blue shirt pointing at a whiteboard with a sentence written in chalk (no readable text), students seated at
desks watching, warm yellow light from the windows, nostalgic tone, 16-bit style, limited palette,
no anti-aliasing, wide shot, 16:9
```
*Narración (caption):* "Andrés taught English at the Universidad Autónoma de Occidente."

### Toma 02 — El campus en un día normal
```
Pixel art cinematic still, a busy university campus in daylight, students walking on stone paths, a few plastic
bottles on the grass near a bin, green trees, red brick buildings, a sunny sky, wide establishing shot,
16-bit style, limited palette, no anti-aliasing, 16:9
```
*Narración:* "In this country, everybody speaks in the passive voice. Things are done. Nobody says who did them."

### Toma 03 — Andrés piensa en quién hace las cosas
```
Pixel art cinematic still, a teacher standing on a campus path looking thoughtfully at a group of workers in the
distance, soft warm light, long shadows, a thought-bubble-free composition, 16-bit style, limited palette,
no anti-aliasing, medium shot, 16:9
```
*Narración:* "Andrés believed that every action has a doer."

### Toma 04 — La obra y el andamio sin asegurar
```
Pixel art cinematic still, a building under construction at a university campus, a metal scaffold on the
facade with loose planks and no safety netting, a few yellow helmets on the ground, empty site, late afternoon,
16-bit style, limited palette, no anti-aliasing, wide shot, 16:9
```
*Narración:* "Then, one day, an accident happened. A scaffold was left unsecured."

### Toma 05 — El andamio cae
```
Pixel art cinematic still, a scaffold collapsing on a building, planks falling diagonally, dust clouds, motion
lines drawn with pixels, a teacher below looking up in shock, dramatic but readable silhouette, orange and gray
palette, 16-bit style, limited palette, no anti-aliasing, 16:9
```
*Narración:* "By whom? Nobody knows."

### Toma 06 — Andrés en el suelo, la vista se pierde
```
Pixel art cinematic still, a teacher lying on the ground next to scattered planks, the edges of the screen dark
and blurred with a pixelated vignette, faint light in the center, dizzy mood, 16-bit style, limited palette,
no anti-aliasing, close medium shot, 16:9
```
*Narración:* "Your memory has been lost."

### Toma 07 — Los estudiantes llaman ayuda
```
Pixel art cinematic still, students gathering around a fallen man on a campus lawn, one student holding a phone
with the light glowing, others pointing and calling, night-blue tint with warm light from the phone, 16-bit style,
limited palette, no anti-aliasing, wide shot, 16:9
```
*Narración:* "And with it, his English. Only his mother tongue, Spanish, remains."

### Toma 08 — La ambulancia llega
```
Pixel art cinematic still, an ambulance with flashing red and blue lights driving on a campus road, trees and
brick buildings around, rain-free dusk sky, motion streaks, 16-bit style, limited palette, no anti-aliasing,
wide shot, 16:9
```
*Narración:* "Every memory he recovers will bring his English back."

### Toma 09 — La habitación de Bienestar (despertar, plano vacío)
```
Pixel art cinematic still, a small clean room with a single white bed, a window with soft light, a bedside table
with a glass of water, a wooden desk with a lamp, calm and empty, 16-bit style, limited palette, no anti-aliasing,
medium wide shot, 16:9
```
*Sin narración: se ve la habitación en borroso, antes de que aparezca Andrés.*

### Toma 10 — Andrés despierta en Bienestar
```
Pixel art cinematic still, a man with short dark hair lying in a white bed, eyes half open, a nurse in a white
uniform sitting at a desk beside him, soft window light, slightly blurred edges, hopeful but confused mood,
16-bit style, limited palette, no anti-aliasing, medium shot, 16:9
```
*Narración final (transición al juego):* "Andrés remembered who he was." → se pasa a `Bienestar`, donde Lucía empieza el reto 1.

---

## 4. Nombres de archivo y carpeta en el proyecto

Carpeta: `Assets/_Project/Art/Cinematica/` (crear la carpeta; Unity la importará sola).

| Archivo | Qué es |
|---|---|
| `REF_estilo.png` | Referencia de estilo (Prompt 0). No se usa en el juego. |
| `REF_andres.png`, `REF_lucia.png`, `REF_estudiantes.png` | Referencias de personajes. No se usan en el juego. |
| `C01_clase.png` | Toma 01 |
| `C02_campus.png` | Toma 02 |
| `C03_pensando.png` | Toma 03 |
| `C04_obra.png` | Toma 04 |
| `C05_caida.png` | Toma 05 |
| `C06_suelo.png` | Toma 06 |
| `C07_ayuda.png` | Toma 07 |
| `C08_ambulancia.png` | Toma 08 |
| `C09_habitacion.png` | Toma 09 |
| `C10_despertar.png` | Toma 10 |

Ajustes de importación al llegar a Unity (para todas): **Pixels Per Unit = 48**, **Filter Mode = Point (no filter)**,
**Compression = None**, igual que el resto del arte. Si las imágenes son de 480 x 270, eso mide 10 x 5,6 unidades.

---

## 5. Lo que no va en la imagen

- **Texto**: los captions se escriben en Unity (en inglés, con la misma fuente Pixelify Sans que el menú) para poder cambiarlos sin regenerar imágenes.
- **Personajes con movimiento**: si quieres que Andrés o Lucía se muevan dentro de la toma, pide **un personaje por imagen sobre fondo transparente** (como las referencias A y B) y anímalo después en Unity.

## 6. Prompt negativo (si el generador lo acepta)
```
photo, realistic, 3D render, smooth gradients, anti-aliasing, blur, text, watermark, logo, signature, extra fingers,
deformed hands, modern brand names, readable words, cropped faces
```
