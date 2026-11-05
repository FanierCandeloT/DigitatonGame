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
        foreach (var scene in new[] { "Campus", "Aulas1", "Aulas2", "Aulas3", "Aulas4", "EdificioCentral", "Sotanos", "Bienestar",
                                      "Cafeteria", "SalaProfesores", "TechoAulas3", "MuseoLili" })
        {
            var path = $"{ScenesDir}{scene}.unity";
            if (!System.IO.File.Exists(path)) continue;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = scene == "Campus" ? DecorateCampus(sc, sprites) : DecorateInterior(sc, scene, sprites);
            if (n > 0) EditorSceneManager.SaveScene(sc);
            Debug.Log($"[Escuela] {scene}: {n} objetos con sprite.");
            changed += n;
        }

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
            var rects = provider.GetSpriteRects().Where(r => !r.name.StartsWith("tile_") && !r.name.StartsWith("door_")).ToList();

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

            // Los árboles se anclan por el pie del tronco (centro-abajo) para poder colocarlos en el suelo.
            if (sheet == "Outside")
            {
                foreach (var r in rects)
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

    static Sprite FindByName(string sheet, string name) => SpritesOf(sheet).FirstOrDefault(s => s.name == name);

    static Sprite FindByPoint(string sheet, int x, int yTop)
    {
        var p = new Vector2(x, SheetHeight[sheet] - yTop);
        return SpritesOf(sheet).Where(s => !s.name.StartsWith("tile_") && !s.name.StartsWith("door_") && s.rect.Contains(p))
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
    }

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
        foreach (var sr in Renderers(sc))
        {
            string name = sr.gameObject.name;
            var before = sr.sprite;

            if (IsDoor(sr)) DoorSprite(sr, sp);
            else if (name == "Zonas verdes del campus") Tile(sr, sp["tile_grass"]);
            else if (name.StartsWith("Camino") || name == "Plaza de la portería oeste") Tile(sr, sp["tile_cobble"]);
            else if (name.StartsWith("Palma ") && sr.transform.parent != null && sr.transform.parent.name == "Palmas del Ágora")
                ReplacePalm(sr, sp["tree"]);

            if (sr.sprite != before) n++;
        }
        return n;
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
        var box = sr.TryGetComponent(out BoxCollider2D col) ? col.size : sr.size;
        Prop(sr, box.x >= box.y ? sp["door_double_red"] : sp["door_single_window"]);
    }

    /// <summary>Cambia el cilindro verde por un árbol de ~3 u de alto, con el pie del tronco en el punto de colisión.</summary>
    static void ReplacePalm(SpriteRenderer sr, Sprite tree)
    {
        if (tree == null) return;
        float scale = 3f / tree.bounds.size.y;
        sr.transform.localScale = Vector3.one * scale;
        Prop(sr, tree);
        if (sr.TryGetComponent(out CircleCollider2D col))
        {
            col.radius = 0.22f / scale;
            col.offset = new Vector2(0f, col.radius);
        }
    }
}
