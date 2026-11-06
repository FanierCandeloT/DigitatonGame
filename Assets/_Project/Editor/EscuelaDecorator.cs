using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reemplaza piezas del greybox (pisos, paredes, muebles, caminos, palmas) por sprites de "Sprites Escuela".
/// Solo toca lo que tiene un sprite adecuado; lo demás queda como greybox para buscarle sprite después.
/// No toca a Andrés, NPC, puertas ni retos. Es repetible. Si se vuelven a correr los constructores
/// del greybox (CampusBuilder / InteriorsBuilder) hay que volver a ejecutar este menú.
/// Menú: DigitatonGame ▸ Decorar escenas con Sprites Escuela.
/// </summary>
public static class EscuelaDecorator
{
    const string Dir = "Assets/_Project/Art/Sprites Escuela/";
    const string ScenesDir = "Assets/_Project/Scenes/";
    const float Ppu = 48f;   // 1 tile de 48 px = 1 unidad de mundo (Andrés mide ~1x2)

    // Tiles de 48x48 que se agregan como sprites nuevos: nombre, hoja, x, y (desde arriba), ancho, alto.
    static readonly (string name, string sheet, int x, int yTop, int w, int h)[] Tiles =
    {
        ("tile_floor_beige",   "A5", 0,   48,  48, 48),
        ("tile_floor_light",   "A5", 48,  48,  48, 48),
        ("tile_floor_grey",    "A5", 96,  96,  48, 48),
        ("tile_floor_dark",    "A5", 336, 0,   48, 48),
        ("tile_floor_wood",    "A5", 48,  240, 48, 48),
        ("tile_floor_checker", "A5", 96,  192, 48, 48),
        ("tile_carpet_purple", "A5", 96,  144, 48, 48),
        ("tile_grass",         "A5", 0,   384, 48, 48),
        ("tile_cobble",        "A5", 48,  384, 48, 48),
        ("tile_wall_beige",    "A4", 310, 392, 48, 48),
        ("tile_wall_brick",    "A4", 24,  496, 48, 48),
        // Puertas (Inside_E, parte inferior de la hoja)
        ("door_double_red",    "Inside_E", 3,   482, 123, 95),
        ("door_single_window", "Inside_E", 505, 634, 48,  86),
    };

    // Texturas de fachada y techo con el pivote ABAJO (para edificios, que crecen hacia el norte desde su base).
    public static readonly (string name, string sheet, int x, int yTop, int w, int h)[] BottomTiles =
    {
        ("face_brick",      "A4", 20,  400, 48, 48),   // ladrillo rojo
        ("face_brick_grey", "A4", 24,  496, 48, 48),   // ladrillo gris
        ("face_beige",      "A4", 310, 392, 48, 48),   // pared beige lisa
        ("roof_green",      "A4", 200, 392, 48, 48),   // franja verde azulada
        ("roof_blue",       "A4", 200, 490, 48, 48),   // franja azul grisácea
    };

    // Objetos de decoración (sprites sueltos de las hojas) re-cortados con el pivote abajo: nombre → hoja, x, y (desde arriba).
    // Cada punto debe caer dentro de un sprite de la hoja; si no, se avisa y ese objeto simplemente no se usa.
    public static readonly Dictionary<string, (string sheet, int x, int y)> Deco = new Dictionary<string, (string, int, int)>
    {
        // Ventanas y pared
        ["win_beige"] = ("Inside_C", 47, 47), ["win_yellow"] = ("Inside_C", 143, 47), ["win_dark"] = ("Inside_C", 237, 47),
        ["win_pair_blue"] = ("Inside_C", 23, 140), ["win_pair_yellow"] = ("Inside_C", 70, 140), ["win_big"] = ("Inside_C", 190, 140),
        ["win_glass"] = ("Inside_C", 205, 235), ["win_arch"] = ("Inside_C", 650, 25), ["win_arch_curtain"] = ("Inside_C", 745, 25),
        // Muebles y objetos
        ["wardrobe"] = ("Inside_C", 480, 50), ["cabinet"] = ("Inside_C", 480, 210),
        ["drawers_a"] = ("Inside_C", 617, 168), ["drawers_b"] = ("Inside_C", 720, 168),
        ["clock"] = ("Inside_C", 22, 315), ["bin_purple"] = ("Inside_C", 68, 360), ["bin_green"] = ("Inside_C", 118, 360), ["bin_grey"] = ("Inside_C", 160, 360),
        ["bench_red"] = ("Inside_C", 300, 322), ["statue"] = ("Inside_C", 718, 385),
        ["plant_a"] = ("Inside_C", 312, 430), ["plant_b"] = ("Inside_C", 357, 445), ["plant_c"] = ("Inside_C", 400, 450),
        ["plant_d"] = ("Inside_C", 448, 440), ["plant_e"] = ("Inside_C", 498, 450), ["plant_f"] = ("Inside_C", 312, 520), ["plant_big"] = ("Inside_C", 415, 358),
        ["cork"] = ("Inside_C", 718, 505), ["frame_a"] = ("Inside_C", 597, 548), ["frame_b"] = ("Inside_C", 652, 548),
        ["pc_desk"] = ("Inside_C", 73, 495), ["books_color"] = ("Inside_C", 23, 550),
        ["shelf_a"] = ("Inside_C", 23, 720), ["shelf_b"] = ("Inside_C", 68, 720), ["shelf_c"] = ("Inside_C", 125, 720), ["shelf_d"] = ("Inside_C", 173, 715),
        ["chair_a"] = ("Inside_C", 217, 560), ["chair_b"] = ("Inside_C", 168, 560), ["stool"] = ("Inside_C", 268, 548),
        ["bed_red"] = ("Inside_C", 430, 540), ["table_low"] = ("Inside_C", 430, 742),
        // Pasillos, baños, cafetería
        ["vend_green"] = ("Inside_D", 287, 145), ["vend_purple"] = ("Inside_D", 382, 145), ["vend_green_s"] = ("Inside_D", 290, 240), ["vend_purple_s"] = ("Inside_D", 382, 240),
        ["fridge_a"] = ("Inside_D", 452, 250), ["fridge_b"] = ("Inside_D", 505, 250),
        ["locker_s1"] = ("Inside_D", 360, 315), ["locker_s2"] = ("Inside_D", 410, 310), ["locker_s3"] = ("Inside_D", 455, 315),
        ["locker_big1"] = ("Inside_D", 528, 335), ["locker_big2"] = ("Inside_D", 630, 335), ["locker_big3"] = ("Inside_D", 718, 335),
        ["bench_wood"] = ("Inside_D", 288, 365), ["bench_metal1"] = ("Inside_D", 288, 418), ["bench_metal2"] = ("Inside_D", 288, 467),
        ["shoe_rack1"] = ("Inside_D", 390, 490), ["shoe_rack2"] = ("Inside_D", 484, 490), ["umbrellas"] = ("Inside_D", 655, 500),
        ["board_a"] = ("Inside_D", 508, 55), ["board_b"] = ("Inside_D", 550, 55), ["board_c"] = ("Inside_D", 650, 55), ["board_d"] = ("Inside_D", 703, 55),
        ["lamp"] = ("Inside_D", 22, 385), ["sink"] = ("Inside_D", 45, 210), ["frame_d"] = ("Inside_D", 311, 607), ["plant_g"] = ("Inside_D", 354, 625),
        ["curtain_purple"] = ("Inside_D", 24, 595), ["curtain_red"] = ("Inside_D", 70, 595), ["curtain_violet"] = ("Inside_D", 117, 595),
        ["curtain_blue"] = ("Inside_D", 165, 595), ["curtain_grey"] = ("Inside_D", 215, 595),
        // Exteriores (Outside)
        ["tree_a"] = ("Outside", 75, 100), ["tree_b"] = ("Outside", 75, 330),
        ["bush_leaf"] = ("Outside", 407, 72), ["bush_vine"] = ("Outside", 455, 72), ["bush_small"] = ("Outside", 458, 182), ["flower_red"] = ("Outside", 458, 270),
    };

    // Sprites sueltos que se buscan por un punto dentro de ellos: hoja, x, y (desde arriba).
    static readonly Dictionary<string, (string sheet, int x, int y)> Props = new Dictionary<string, (string, int, int)>
    {
        ["desk"]     = ("Inside_C", 240, 650),
        ["board"]    = ("Inside_C", 597, 470),
        ["shelf"]    = ("Inside_C", 23,  720),
        ["chair"]    = ("Inside_C", 217, 560),
        ["stage"]    = ("Inside_C", 610, 625),
        ["counter"]  = ("Inside_C", 620, 720),
        ["table"]    = ("Inside_C", 430, 742),
        ["workdesk"] = ("Inside_C", 430, 612),
        ["bed"]      = ("Inside_C", 430, 540),
        ["cabinet"]  = ("Inside_C", 610, 170),
        ["medcab"]   = ("Inside_D", 455, 318),
        ["tree"]     = ("Outside",  75,  330),
    };

    static readonly Dictionary<string, int> SheetHeight = new Dictionary<string, int>
    {
        ["A4"] = 720, ["A5"] = 768, ["Inside_C"] = 768, ["Inside_D"] = 768, ["Inside_E"] = 768, ["Outside"] = 439,
    };

    [MenuItem("DigitatonGame/Decorar escenas con Sprites Escuela")]
    public static void Run()
    {
        // Si la escena abierta tiene cambios sin guardar, OpenScene preguntaría y bloquearía: se guardan antes (no se pierde nada).
        if (SceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.SaveOpenScenes();

        PrepareSheets();

        var sprites = new Dictionary<string, Sprite>();
        foreach (var t in Tiles) sprites[t.name] = FindByName(t.sheet, t.name);
        foreach (var p in Props)
        {
            var s = FindByPoint(p.Value.sheet, p.Value.x, p.Value.y);
            if (s == null) Debug.LogWarning($"[Escuela] No hay sprite en {p.Value.sheet} ({p.Value.x},{p.Value.y}) para '{p.Key}': se deja el greybox.");
            sprites[p.Key] = s;
        }

        var originalScene = SceneManager.GetActiveScene().path;
        int changed = 0;
        foreach (var scene in new[] { "Campus", "Aulas1", "Aulas2", "Aulas3", "Aulas4", "Sotanos", "Bienestar",
                                      "Cafeteria", "SalaProfesores", "TechoAulas3", "MuseoLili" })
        {
            var path = $"{ScenesDir}{scene}.unity";
            if (!System.IO.File.Exists(path)) continue;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = scene == "Campus" ? DecorateCampus(sc, sprites) : DecorateInterior(sc, scene, sprites);
            // Se guarda siempre: TileChunked cambia el objeto sin cambiar su sprite, así que n no lo cuenta.
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            Debug.Log($"[Escuela] {scene}: {n} objetos con sprite.");
            changed += n;
        }

        // Segunda parte: ambientación (fachadas, árboles, estados descuidados, mobiliario) y Lucía.
        EscuelaDecorExtra.Run();

        if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Escuela] Listo: {changed} objetos decorados.");
    }

    // ───────────────────────── Importación de las hojas ─────────────────────────

    static void PrepareSheets()
    {
        foreach (var sheet in SheetHeight.Keys)
        {
            var path = $"{Dir}{sheet}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // necesario para el modo Tiled
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects().Where(r => !IsGenerated(r.name)).ToList();
            var originals = rects.ToList();

            foreach (var t in BottomTiles.Where(t => t.sheet == sheet))
            {
                rects.Add(new SpriteRect
                {
                    name = t.name,
                    rect = new Rect(t.x, SheetHeight[sheet] - t.yTop - t.h, t.w, t.h),
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    spriteID = GUID.Generate(),
                });
            }

            // Copias con el pivote abajo de los objetos de decoración (se buscan por un punto dentro del sprite original).
            foreach (var d in Deco.Where(d => d.Value.sheet == sheet))
            {
                var p = new Vector2(d.Value.x, SheetHeight[sheet] - d.Value.y);
                var src = originals.Where(r => r.rect.Contains(p)).OrderBy(r => r.rect.width * r.rect.height).FirstOrDefault();
                if (src == null)
                {
                    Debug.LogWarning($"[Escuela] '{d.Key}': no hay sprite en {sheet} ({d.Value.x},{d.Value.y}); no se usará.");
                    continue;
                }
                rects.Add(new SpriteRect
                {
                    name = "deco_" + d.Key,
                    rect = src.rect,
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    spriteID = GUID.Generate(),
                });
            }

            foreach (var t in Tiles.Where(t => t.sheet == sheet))
            {
                rects.Add(new SpriteRect
                {
                    name = t.name,
                    rect = new Rect(t.x, SheetHeight[sheet] - t.yTop - t.h, t.w, t.h),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate(),
                });
            }

            // Los árboles y plantas se anclan por el pie (centro-abajo) para poder colocarlos en el suelo.
            if (sheet == "Outside")
            {
                foreach (var r in rects.Where(r => !r.name.StartsWith("deco_")))
                {
                    r.alignment = SpriteAlignment.Custom;
                    r.pivot = new Vector2(0.5f, 0.02f);
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
            importer.SaveAndReimport();
        }
    }

    static Sprite[] SpritesOf(string sheet) => AssetDatabase.LoadAllAssetsAtPath($"{Dir}{sheet}.png").OfType<Sprite>().ToArray();

    /// <summary>Sprites que genera esta herramienta (no son los cortes originales de la hoja).</summary>
    static bool IsGenerated(string name) =>
        name.StartsWith("tile_") || name.StartsWith("door_") || name.StartsWith("face_") || name.StartsWith("roof_") || name.StartsWith("deco_");

    public static Sprite FindByName(string sheet, string name) => SpritesOf(sheet).FirstOrDefault(s => s.name == name);

    static Sprite FindByPoint(string sheet, int x, int yTop)
    {
        var p = new Vector2(x, SheetHeight[sheet] - yTop);
        return SpritesOf(sheet).Where(s => !IsGenerated(s.name) && s.rect.Contains(p))
            .OrderBy(s => s.rect.width * s.rect.height).FirstOrDefault();
    }

    // ───────────────────────── Aplicar sprites ─────────────────────────

    static IEnumerable<SpriteRenderer> Renderers(Scene sc) =>
        sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true));

    static void Clean(SpriteRenderer sr)
    {
        sr.color = Color.white;
        if (sr.TryGetComponent(out PlaceholderInfo info))
        {
            info.applyKindColor = false;
            info.showLabel = false;
        }
        EditorUtility.SetDirty(sr);
    }

    /// <summary>Repite el sprite para cubrir el tamaño actual del objeto (pisos, paredes).</summary>
    static void Tile(SpriteRenderer sr, Sprite s)
    {
        if (s == null) return;
        var size = sr.size;
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        Clean(sr);
    }

    /// <summary>
    /// Igual que Tile, pero para superficies enormes (el pasto del campus): un solo SpriteRenderer en modo Tiled no puede
    /// pasar de ~16 000 casillas (límite de vértices de Unity), así que se parte en trozos de 40x40 u como hijos.
    /// </summary>
    static void TileChunked(SpriteRenderer sr, Sprite s, Vector2 size, Vector2 center, float chunk = 40f)
    {
        if (s == null) return;
        // El tamaño y el centro vienen de los límites del mapa (MapBounds), no del objeto: el pasto original podía venir
        // como un sprite de 1 u estirado por la escala del transform, y su tamaño ya no es confiable.
        var chunks = sr.transform.Cast<Transform>().Where(t => t.name == "Trozo de pasto").ToArray();
        sr.transform.localScale = Vector3.one;
        sr.transform.position = new Vector3(center.x, center.y, sr.transform.position.z);
        foreach (var old in chunks) Object.DestroyImmediate(old.gameObject);

        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Simple;   // el objeto original deja de dibujar; los trozos lo reemplazan
        sr.enabled = false;
        Clean(sr);

        int nx = Mathf.CeilToInt(size.x / chunk), ny = Mathf.CeilToInt(size.y / chunk);
        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float w = Mathf.Min(chunk, size.x - ix * chunk), h = Mathf.Min(chunk, size.y - iy * chunk);
                var go = new GameObject("Trozo de pasto");
                go.transform.SetParent(sr.transform, false);
                go.transform.localPosition = new Vector3(-size.x / 2f + ix * chunk + w / 2f, -size.y / 2f + iy * chunk + h / 2f, 0f);
                var c = go.AddComponent<SpriteRenderer>();
                c.sprite = s;
                c.drawMode = SpriteDrawMode.Tiled;
                c.tileMode = SpriteTileMode.Continuous;
                c.size = new Vector2(w, h);
                c.sortingOrder = sr.sortingOrder;
            }
    }

    /// <summary>Repite el sprite a lo ancho y le da su alto natural (estanterías, tableros, mostradores).</summary>
    static void TileAcross(SpriteRenderer sr, Sprite s)
    {
        if (s == null) return;
        var width = sr.size.x;
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, s.bounds.size.y);
        Clean(sr);
    }

    /// <summary>Un solo sprite a su tamaño natural.</summary>
    static void Prop(SpriteRenderer sr, Sprite s)
    {
        if (s == null) return;
        sr.sprite = s;
        sr.drawMode = SpriteDrawMode.Simple;
        Clean(sr);

        // El placeholder venía estirado por la escala del transform (muebles de 2-3,8x, o aplastados a 0,8x en un eje).
        // Un sprite estirado desigual se ve raro junto a Andrés: se deja uniforme y como mucho a 1,5x el tamaño del sprite.
        var ls = sr.transform.localScale;
        float u = Mathf.Clamp(Mathf.Sqrt(Mathf.Abs(ls.x * ls.y)), 1f, MaxPropScale);
        sr.transform.localScale = new Vector3(u, u, ls.z);
    }

    /// <summary>Tope de ampliación de un mueble con sprite (más que eso se ve pixelado y desproporcionado).</summary>
    const float MaxPropScale = 1.5f;

    static int DecorateInterior(Scene sc, string scene, Dictionary<string, Sprite> sp)
    {
        int n = 0;
        string wall = scene == "Sotanos" || scene == "TechoAulas3" ? "tile_wall_brick" : "tile_wall_beige";
        foreach (var sr in Renderers(sc))
        {
            string name = sr.gameObject.name;
            bool Is(string prefix) => name.StartsWith(prefix);
            var before = sr.sprite;

            if (IsDoor(sr)) DoorSprite(sr, sp);
            else if (Is("Muro Norte") || Is("Muro Sur") || Is("Muro Este") || Is("Muro Oeste") || name == "Muro interior") Tile(sr, sp[wall]);
            else if (FloorFor(scene, name) is string floor) Tile(sr, sp[floor]);
            else if (Is("Pupitre")) Prop(sr, sp["desk"]);
            else if (Is("Tablero")) Prop(sr, sp["board"]);
            else if (Is("Estantería")) TileAcross(sr, sp["shelf"]);
            else if (Is("Butaca")) Prop(sr, sp["chair"]);
            else if (Is("Escenario")) Tile(sr, sp["stage"]);
            else if (Is("Mostrador")) TileAcross(sr, sp["counter"]);
            else if (Is("Mesa ")) Prop(sr, sp["table"]);
            else if (name == "Escritorio de Lucía" || name == "Escritorio principal") TileAcross(sr, sp["workdesk"]);
            else if (Is("Escritorio de profesor")) Prop(sr, sp["workdesk"]);
            else if (Is("Camilla")) Prop(sr, sp["bed"]);
            else if (Is("Botiquín")) Prop(sr, sp["medcab"]);
            else if (Is("Vitrina")) Prop(sr, sp["cabinet"]);

            if (sr.sprite != before) n++;
        }
        return n;
    }

    /// <summary>Tile de piso para un objeto de piso de esa escena, o null si el objeto no es un piso.</summary>
    static string FloorFor(string scene, string name)
    {
        if (name.StartsWith("Piso ")) return scene switch
        {
            "Sotanos" => "tile_floor_dark",
            "TechoAulas3" => "tile_floor_grey",
            "Bienestar" => "tile_floor_light",
            "Cafeteria" => "tile_floor_checker",
            "SalaProfesores" or "MuseoLili" => "tile_floor_wood",
            _ when scene.StartsWith("Aulas") => "tile_floor_beige",
            _ => "tile_floor_grey",
        };
        if (name.StartsWith("Pasillo")) return scene == "Sotanos" ? "tile_floor_dark" : "tile_floor_light";
        if (name == "Biblioteca") return "tile_floor_wood";
        if (name.StartsWith("Auditorio")) return "tile_carpet_purple";
        if (name == "Enfermería" || name == "Recepción") return "tile_floor_light";
        if (name.StartsWith("Aulas ") && name.Contains("Salón") || name == "Intro classroom" ||
            name == "Recepción e información" || name == "Salón Lili" || name == "Bloque D2") return "tile_floor_beige";
        return null;
    }

    static int DecorateCampus(Scene sc, Dictionary<string, Sprite> sp)
    {
        int n = 0;

        // Límites del mapa: el pasto de fondo los cubre completos (con 1 u de margen)
        var mapBounds = sc.GetRootGameObjects().Select(g => g.GetComponentInChildren<MapBounds>(true)).FirstOrDefault(m => m != null);
        var mb = mapBounds != null ? mapBounds.GetComponent<BoxCollider2D>().bounds : new Bounds(Vector3.zero, new Vector3(238f, 106f, 0f));

        // Copia fija: TileChunked destruye y crea hijos mientras se recorre
        foreach (var sr in Renderers(sc).ToArray())
        {
            if (sr == null) continue;
            string name = sr.gameObject.name;
            var before = sr.sprite;

            if (IsDoor(sr)) DoorSprite(sr, sp);
            else if (name == "Zonas verdes del campus") TileChunked(sr, sp["tile_grass"], (Vector2)mb.size + Vector2.one * 2f, mb.center);
            else if (name.StartsWith("Camino") || name == "Plaza de la portería oeste") { NormalizeScale(sr); Tile(sr, sp["tile_cobble"]); }
            else if (name.StartsWith("Palma ") && sr.transform.parent != null && sr.transform.parent.name == "Palmas del Ágora")
                ReplacePalm(sr, sp["tree"]);

            if (sr.sprite != before) n++;
        }
        return n;
    }

    /// <summary>
    /// Los caminos del campus pueden estar estirados con la escala del transform (un sprite pequeño × escala enorme), lo que
    /// emborrona la textura al pasar a modo Tiled. Se deja la escala en 1 y el tamaño real queda en 'size' (mismo tamaño en el mundo).
    /// </summary>
    static void NormalizeScale(SpriteRenderer sr)
    {
        var ls = sr.transform.localScale;
        if (Mathf.Abs(ls.x - 1f) < 0.01f && Mathf.Abs(ls.y - 1f) < 0.01f) return;
        var world = new Vector2(sr.size.x * Mathf.Abs(ls.x), sr.size.y * Mathf.Abs(ls.y));
        sr.transform.localScale = new Vector3(1f, 1f, ls.z);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = world;
    }

    /// <summary>Puertas de entrada/salida. Las escaleras (Escalera, Bajada) no llevan sprite de puerta.</summary>
    static bool IsDoor(SpriteRenderer sr)
    {
        if (!sr.TryGetComponent(out Door _)) return false;
        string n = sr.gameObject.name;
        return n.StartsWith("Salida") || n.StartsWith("Puerta") || n.StartsWith("Entrada");
    }

    /// <summary>Puerta doble si el hueco es horizontal (pared norte/sur), puerta sencilla si es vertical (pared este/oeste).</summary>
    static void DoorSprite(SpriteRenderer sr, Dictionary<string, Sprite> sp)
    {
        // Zona de interacción en el mundo (no cambia: solo cambia lo que se ve)
        bool hasBox = sr.TryGetComponent(out BoxCollider2D col);
        var lossy = sr.transform.lossyScale;
        var worldBox = hasBox ? new Vector2(col.size.x * Mathf.Abs(lossy.x), col.size.y * Mathf.Abs(lossy.y)) : sr.size;
        bool horizontal = worldBox.x >= worldBox.y;

        sr.sprite = horizontal ? sp["door_double_red"] : sp["door_single_window"];
        sr.drawMode = SpriteDrawMode.Simple;
        Clean(sr);

        // Escala UNIFORME (antes la puerta doble estaba aplastada a 3 x 1,5 u: más baja que Andrés, que mide ~2,2 u).
        // Doble: 3,2 x 2,5 u. Sencilla: 1,7 x 3 u.
        float scale = horizontal ? DoubleDoorScale : SingleDoorScale;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        if (hasBox) col.size = new Vector2(worldBox.x / scale, worldBox.y / scale);
    }

    const float DoubleDoorScale = 1.25f, SingleDoorScale = 1.67f;

    /// <summary>Cambia el cilindro verde por un árbol de ~3 u de alto, con el pie del tronco en el punto de colisión.</summary>
    static void ReplacePalm(SpriteRenderer sr, Sprite tree)
    {
        if (tree == null) return;
        float scale = 3f / tree.bounds.size.y;
        Prop(sr, tree);
        sr.transform.localScale = Vector3.one * scale;   // después de Prop: Prop deja la escala en 1 o más
        if (sr.TryGetComponent(out CircleCollider2D col))
        {
            col.radius = 0.22f / scale;
            col.offset = new Vector2(0f, col.radius);
        }
    }
}
