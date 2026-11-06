using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

/// <summary>
/// Segunda parte de la decoración: fachadas de los edificios, árboles y vegetación, zonas descuidadas
/// (parqueaderos, patio, almacén, sótanos; ODS 8 y 12), mobiliario de interiores y el sprite animado de Lucía.
/// Se ejecuta desde <see cref="EscuelaDecorator.Run"/>. Todo lo que crea cuelga de un objeto
/// "Decoración (Escuela)" o "Fachada (Escuela)", que se borra y se rehace en cada corrida (es repetible).
///
/// El mapa puede estar reducido con <see cref="MapScaleTool"/>: las posiciones de los interiores se escriben en las
/// coordenadas del greybox original y aquí se convierten al tamaño actual (K); las zonas del campus se calculan a partir
/// de objetos que ya existen en la escena. Lo que quedaría encima de otra cosa se descarta (<see cref="Place"/>).
/// </summary>
public static class EscuelaDecorExtra
{
    const string Pack = "Assets/_Project/Art/Sprites Escuela/PostApocalypse_AssetPack_v1.1.2/";
    const string ScenesDir = "Assets/_Project/Scenes/";
    const string RootName = "Decoración (Escuela)";
    const string FacadeName = "Fachada (Escuela)";
    const float PackPpu = 10f;   // el pack es de 16 px por casilla: a 10 PPU un auto mide ~2,5 x 3,7 u (Andrés es 1x2)

    // Órdenes de dibujo: pisos (-90) < calcomanías de suelo < calcomanías de pared < objetos (0, ordenados por Y).
    const int GroundDecal = -60, WallDecal = -50;

    static readonly Dictionary<string, Sprite> packCache = new Dictionary<string, Sprite>();
    static Random rng;
    static float K = 1f;                                  // escala actual del mapa respecto al greybox original
    static readonly List<Rect> occupied = new List<Rect>();   // cosas sólidas que no se deben tapar (huellas en el suelo)

    // ───────────────────────── Entrada ─────────────────────────

    public static void Run()
    {
        packCache.Clear();
        EnsureGarbageSlices();

        foreach (var scene in new[] { "Campus", "Aulas1", "Aulas2", "Aulas3", "Aulas4", "Sotanos", "Bienestar",
                                      "Cafeteria", "SalaProfesores", "TechoAulas3", "MuseoLili" })
        {
            var path = $"{ScenesDir}{scene}.unity";
            if (!System.IO.File.Exists(path)) continue;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            rng = new Random(scene.GetHashCode() ^ 7919);
            K = MapScaleTool.Read(sc);

            ClearPrevious(sc);
            CollectOccupied(sc);
            int n = scene == "Campus" ? DecorateCampus(sc) : DecorateInterior(sc, scene);
            if (scene == "Bienestar") n += SetupLucia(sc);
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            Debug.Log($"[Escuela] {scene} (x{K:0.##}): {n} objetos de ambientación.");
        }
    }

    public static void ClearPrevious(Scene sc)
    {
        foreach (var root in sc.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) UnityEngine.Object.DestroyImmediate(root);
        foreach (var t in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t != null && t.name == FacadeName).ToArray())
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
    }

    // ───────────────────────── Sprites ─────────────────────────

    /// <summary>Sprite del pack (ruta relativa a la carpeta del pack). Lo deja con pivote abajo y 10 PPU.</summary>
    static Sprite Pk(string rel)
    {
        if (packCache.TryGetValue(rel, out var cached)) return cached;
        var path = Pack + rel;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        Sprite result = null;
        if (imp == null) Debug.LogWarning($"[Escuela] No existe el sprite del pack: {rel}");
        else
        {
            bool dirty = false;
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, PackPpu)) { imp.spritePixelsPerUnit = PackPpu; dirty = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }

            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            if (st.spriteAlignment != (int)SpriteAlignment.BottomCenter || st.spriteMeshType != SpriteMeshType.FullRect)
            {
                st.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                st.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(st);
                dirty = true;
            }
            if (imp.spritePivot != new Vector2(0.5f, 0f)) { imp.spritePivot = new Vector2(0.5f, 0f); dirty = true; }
            if (dirty) imp.SaveAndReimport();

            result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result == null) Debug.LogWarning($"[Escuela] El pack no dio un sprite en: {rel}");
        }
        packCache[rel] = result;
        return result;
    }

    /// <summary>Sprite re-cortado de las hojas de la escuela (pivote abajo), por su clave en EscuelaDecorator.Deco.</summary>
    static Sprite Sc(string key)
    {
        if (!EscuelaDecorator.Deco.TryGetValue(key, out var d)) { Debug.LogWarning($"[Escuela] Clave de decoración desconocida: {key}"); return null; }
        var s = EscuelaDecorator.FindByName(d.sheet, "deco_" + key);
        if (s == null) Debug.LogWarning($"[Escuela] Falta el sprite 'deco_{key}' ({d.sheet}).");
        return s;
    }

    static Sprite Tile(string name, string sheet) => EscuelaDecorator.FindByName(sheet, name);

    /// <summary>La hoja de basura trae dos montones: se cortan como sprites sueltos (pivote abajo, 10 PPU).</summary>
    static void EnsureGarbageSlices()
    {
        var path = Pack + "Tiles/Garbage_TileSet.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = PackPpu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(new[]
        {
            new SpriteRect { name = "garbage_a", rect = new Rect(0, 0, 64, 64), alignment = SpriteAlignment.BottomCenter, pivot = new Vector2(0.5f, 0f), spriteID = GUID.Generate() },
            new SpriteRect { name = "garbage_b", rect = new Rect(64, 0, 64, 64), alignment = SpriteAlignment.BottomCenter, pivot = new Vector2(0.5f, 0f), spriteID = GUID.Generate() },
        });
        provider.Apply();
        imp.SaveAndReimport();
    }

    static Sprite Garbage(int i) =>
        AssetDatabase.LoadAllAssetsAtPath(Pack + "Tiles/Garbage_TileSet.png").OfType<Sprite>().FirstOrDefault(s => s.name == (i == 0 ? "garbage_a" : "garbage_b"));

    // Rutas del pack usadas varias veces
    const string N = "Objects/Nature/";
    static string[] CleanTrees => new[]
    {
        N + "Green/Tree_5_Big_Green.png", N + "Green/Tree_7_Birch_Green.png", N + "Green/Tree_9_Small-oak_Green.png",
        N + "Green/Tree_10_Small-oak_Green.png", N + "Green/Tree_3_Normal_Green.png", N + "Green/Tree_6_Pine_Big_Green.png",
    };
    static string[] WildTrees => new[]
    {
        N + "Dark-Green/Tree_5_Big_Dark-Green.png", N + "Dark-Green/Tree_6_Big-pine_Dark-Green.png", N + "Dark-Green/Tree_3_Normal_Dark-Green.png",
        N + "Dark-Green/Tree_10_Small-oak_Dark-Green.png", N + "Bleak-Yellow/Tree_5_Big_Bleak-Yellow.png", N + "Bleak-Yellow/Tree_9_Small-oak_Bleak-Yellow.png",
        N + "Bleak-Yellow/Tree_7_Birch_Bleak-Yellow.png", N + "Orange/Tree_5_Big_Orange.png", N + "Flowers_Mashrooms_Other-nature-stuff/Tree_4_Dry_Green.png",
    };
    static string[] CleanBushes => new[] { N + "Green/Bush_1_Green.png", N + "Green/Bush_2_Green.png" };
    static string[] WildBushes => new[]
    {
        N + "Dark-Green/Bush_1_Dark-Green.png", N + "Dark-Green/Bush_2_Dark-Green.png", N + "Bleak-Yellow/Bush_1_Bleak-Yellow.png", N + "Bleak-Yellow/Bush_2_Bleak-Yellow.png",
    };
    static string[] CleanGrass => new[]
    {
        N + "Green/Grass_1_Green.png", N + "Green/Grass_2_Green.png", N + "Green/Grass_3_Green.png", N + "Green/Grass_4_Green.png", N + "Green/Grass_5_Green.png",
    };
    static string[] WildGrass => new[]
    {
        N + "Bleak-Yellow/Grass_3_Bleak-Yellow.png", N + "Bleak-Yellow/Grass_4_Bleak-Yellow.png", N + "Bleak-Yellow/Grass_5_Bleak-Yellow.png",
        N + "Dark-Green/Grass_3_Dark-Green.png", N + "Dark-Green/Grass_4_Dark-Green.png", N + "Dark-Green/Grass_5_Dark-Green.png",
    };
    static string[] Flowers()
    {
        var f = N + "Flowers_Mashrooms_Other-nature-stuff/";
        var list = new List<string>();
        foreach (var c in new[] { "red", "yellow", "blue", "purple" })
        {
            list.Add($"{f}Flowers_1_{c}.png"); list.Add($"{f}Flowers_2_{c}.png"); list.Add($"{f}Flowers_3_{c}.png"); list.Add($"{f}Flower_1_{c}.png");
        }
        return list.ToArray();
    }
    static string[] Rocks => Enumerable.Range(1, 7).Select(i => $"{N}Flowers_Mashrooms_Other-nature-stuff/Rocks/Rock_{i}.png").ToArray();
    static string[] Stumps => new[]
    {
        N + "Flowers_Mashrooms_Other-nature-stuff/Stump_1.png", N + "Flowers_Mashrooms_Other-nature-stuff/Stump_2_Mushrooms.png",
        N + "Flowers_Mashrooms_Other-nature-stuff/Tree-trunk_1_Green.png", N + "Flowers_Mashrooms_Other-nature-stuff/Mushrooms_2_Red.png",
    };

    const string V = "Objects/Vehicles/";
    static string[] RustCars => new[]
    {
        V + "Rust/Car_1_Rust/Car_1_Rust_Blue.png", V + "Rust/Car_1_Rust/Car_1_Rust_Green.png", V + "Rust/Car_1_Rust/Car_1_Rust_Orange.png",
        V + "Rust/Car_1_Rust/Car_1_Rust_Red.png", V + "Rust/Car_1_Rust/Car_1_Rust_Yellow.png", V + "Rust/Car_1_Rust/Car_1_Rust_Dark-Blue.png",
        V + "Overgrown/Car_1_Overgrown/Dark-Green/Car_1_Overgrown_Dark-Green_Gray.png", V + "Overgrown/Car_1_Overgrown/Green/Car_1_Overgrown_Green_Red.png",
        V + "Overgrown/Car_1_Overgrown/Bleak-Yellow/Car_1_Overgrown_Bleak-Yellow_Blue.png",
        V + "Rust/Car_2_Rust_Upsidedown/Car_2_Rust_Orange_Upsidedown.png", V + "Rust/Car_2_Rust_Upsidedown/Car_2_Rust_Red_Upsidedown.png",
    };

    const string C = "Objects/Container/";

    // ───────────────────────── Colocación ─────────────────────────

    static GameObject Put(Transform parent, Sprite s, Vector2 foot, string name, int order = 0, bool solid = false, float scale = 1f, bool flip = false)
    {
        if (s == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(foot.x, foot.y, 0f);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        sr.flipX = flip;
        if (solid)
        {
            var col = go.AddComponent<BoxCollider2D>();
            float w = Mathf.Min(s.bounds.size.x * 0.8f, 3.5f);
            float h = Mathf.Min(0.6f, s.bounds.size.y);
            col.size = new Vector2(w / Mathf.Max(scale, 0.01f), h / Mathf.Max(scale, 0.01f));
            col.offset = new Vector2(0f, col.size.y / 2f);
        }
        return go;
    }

    /// <summary>
    /// Ajuste de proporción respecto a Andrés (mide ~2,2 u de alto). Los sprites del pack son de 16 px por casilla y vienen
    /// muy pequeños: un auto medía 2,5 x 3,7 u (más bajo que una persona en largo). Se multiplica el tamaño de cada familia:
    /// vehículos y contenedores x1,5; farolas x1,8 (antes más bajas que Andrés); canecas x1,3; basura x0,45 (los montones medían ~6 u).
    /// </summary>
    static float ScaleFor(Sprite s)
    {
        string n = s.name;
        if (n.StartsWith("Car_") || n.StartsWith("Container_")) return 1.5f;
        if (n == "garbage_a" || n == "garbage_b") return 0.45f;
        if (n == "deco_lamp") return 1.8f;
        if (n.StartsWith("deco_bin")) return 1.3f;
        return 1f;
    }

    /// <summary>Huella en el suelo de un objeto de pie: ancho del sprite y una franja de ~0,3 u desde su base.</summary>
    static Rect Footprint(Sprite s, Vector2 foot, float scale)
    {
        float w = Mathf.Min(s.bounds.size.x * scale * 0.8f, 3.5f);
        return new Rect(foot.x - w / 2f, foot.y, w, Mathf.Min(0.35f, s.bounds.size.y * scale));
    }

    /// <summary>
    /// Pone un objeto SOLO si no queda encima de algo sólido, de una puerta/spawn ni de otro objeto ya puesto.
    /// Las calcomanías (suelo y pared) no se revisan: no estorban el paso.
    /// </summary>
    static GameObject Place(Transform parent, Sprite s, Vector2 foot, string name, int order = 0, bool solid = false, float scale = 1f, bool flip = false)
    {
        if (s == null) return null;
        scale *= ScaleFor(s);
        bool standing = order >= 0;
        Rect fp = default;
        if (standing)
        {
            fp = Footprint(s, foot, scale);
            if (occupied.Any(o => o.Overlaps(fp))) return null;
        }
        var go = Put(parent, s, foot, name, order, solid, scale, flip);
        if (go != null && standing) occupied.Add(fp);
        return go;
    }

    /// <summary>Huellas de todo lo sólido o importante que ya existe en la escena (sin muros ni bordes: contra ellos sí se apoya).</summary>
    static void CollectOccupied(Scene sc)
    {
        occupied.Clear();
        foreach (var g in sc.GetRootGameObjects())
        {
            foreach (var col in g.GetComponentsInChildren<Collider2D>(true))
            {
                if (!col.enabled || col.GetComponent<MapBounds>() != null) continue;
                var b = col.bounds;
                if (b.size.x > 40f || b.size.y > 40f) continue;                                      // bordes y muros largos
                string n = col.name;
                if (n.StartsWith("Muro") || n.StartsWith("Carrera") || n.StartsWith("Calle") || n.StartsWith("Autopista")) continue;
                bool building = col.GetComponent<SpriteRenderer>() != null && col.GetComponent<SpriteRenderer>().sortingOrder == 0 && b.size.y <= 2.1f && b.size.x > 6f;
                if (building) continue;                                                              // los edificios se tratan aparte
                occupied.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
            }
            foreach (var sp in g.GetComponentsInChildren<SpawnPoint>(true))
                occupied.Add(new Rect(sp.transform.position.x - 1.2f, sp.transform.position.y - 1.2f, 2.4f, 2.4f));
        }
    }

    static Transform NewRoot(Scene sc)
    {
        var go = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(go, sc);
        return go.transform;
    }

    static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

    // ───────────────────────── Interiores ─────────────────────────

    // Mitad del interior (x, y) de cada escena en el greybox original: las posiciones se escriben en ese espacio.
    static readonly Dictionary<string, Vector2> InnerHalf = new Dictionary<string, Vector2>
    {
        ["Aulas1"] = new Vector2(26f, 10f), ["Aulas2"] = new Vector2(26f, 10f), ["Aulas3"] = new Vector2(26f, 10f), ["Aulas4"] = new Vector2(26f, 10f),
        ["EdificioCentral"] = new Vector2(35f, 20f), ["Sotanos"] = new Vector2(30f, 7f), ["Bienestar"] = new Vector2(22f, 10f),
        ["Cafeteria"] = new Vector2(20f, 9f), ["SalaProfesores"] = new Vector2(15f, 8f), ["TechoAulas3"] = new Vector2(25f, 12f),
        ["MuseoLili"] = new Vector2(12f, 6f),
    };

    /// <summary>
    /// Convierte un eje de coordenadas del greybox original al tamaño actual. Lo que está a menos de 2,6 u del muro exterior
    /// conserva esa distancia (así lo pegado a la pared sigue pegado); el resto se escala con K, igual que el greybox.
    /// </summary>
    static float Axis(float v, float half)
    {
        if (K >= 0.999f) return v;
        float d = half - Mathf.Abs(v);
        return d <= 2.6f ? Mathf.Sign(v) * (half * K - d) : v * K;
    }

    static Vector2 MapPt(string scene, float x, float y, float interiorWallY)
    {
        if (K >= 0.999f || !InnerHalf.TryGetValue(scene, out var h)) return new Vector2(x, y);
        // Objetos pegados a un muro interior (relojes, cuadros): conservan su distancia a ese muro
        if (!float.IsNaN(interiorWallY) && Mathf.Abs(y - interiorWallY) <= 2.6f) return new Vector2(Axis(x, h.x), interiorWallY * K + (y - interiorWallY));
        return new Vector2(Axis(x, h.x), Axis(y, h.y));
    }

    // (clave, x, y, orden, sólido, escala). Prefijo "p:" = sprite del pack (ruta relativa); "g:" = basura (0/1); el resto, hojas de la escuela.
    static (string key, float x, float y, int order, bool solid, float scale) It(string key, float x, float y, int order = 0, bool solid = false, float scale = 1f) =>
        (key, x, y, order, solid, scale);

    static int DecorateInterior(Scene sc, string scene)
    {
        var root = NewRoot(sc);
        var items = new List<(string key, float x, float y, int order, bool solid, float scale)>();

        switch (scene)
        {
            case "Aulas1": case "Aulas2": case "Aulas3": case "Aulas4": AulasItems(items, scene); break;
            case "EdificioCentral": CentralItems(items); break;
            case "Bienestar": BienestarItems(items); break;
            case "Cafeteria": CafeteriaItems(items); break;
            case "SalaProfesores": SalaProfesoresItems(items); break;
            case "TechoAulas3": TechoItems(items); break;
            case "Sotanos": SotanosItems(items); break;
            case "MuseoLili": MuseoItems(items); break;
        }

        int n = 0, dropped = 0;
        foreach (var it in items)
        {
            var s = ResolveSprite(it.key);
            // En Edificio Central las calcomanías de los salones del sur cuelgan del muro interior y = -6
            float wall = scene == "EdificioCentral" && it.order == WallDecal && it.y < -6.5f && it.y > -9.5f ? -6f : float.NaN;
            var p = MapPt(scene, it.x, it.y, wall);
            var go = Place(root, s, p, it.key.Replace("p:", "").Split('/').Last().Replace(".png", ""), it.order, it.solid, it.scale);
            if (go != null) n++; else if (s != null) dropped++;
        }
        if (dropped > 0) Debug.Log($"[Escuela] {scene}: {dropped} objetos descartados por quedar encima de otros.");
        return n;
    }

    static Sprite ResolveSprite(string key)
    {
        if (key.StartsWith("p:")) return Pk(key.Substring(2));
        if (key.StartsWith("g:")) return Garbage(int.Parse(key.Substring(2)));
        return Sc(key);
    }

    static void AulasItems(List<(string key, float x, float y, int order, bool solid, float scale)> l, string scene)
    {
        float[] cx = { -17.4f, 0f, 17.4f };
        foreach (var c in cx)
        {
            // Ventanas en la pared norte de cada salón (a los lados del tablero), armario y planta
            foreach (var (dx, k) in new[] { (-6.1f, "win_beige"), (-3.9f, "win_yellow"), (3.9f, "win_yellow"), (6.1f, "win_beige") })
                l.Add(It(k, c + dx, 8.1f, WallDecal, false, 1.3f));
            l.Add(It("wardrobe", c - 7.3f, 8.4f, 0, true));
            l.Add(It("plant_c", c + 7.4f, 8.4f, 0, true));
            l.Add(It("bin_grey", c + 7.5f, -2.3f, 0, false));
            l.Add(It("plant_a", c - 7.5f, -2.2f, 0, false));
        }

        // Pasillo: casilleros contra la pared (con huecos en las puertas), máquinas, bancas, plantas, relojes
        string[] lockers = { "locker_big1", "locker_big2", "locker_big3" };
        int li = 0;
        for (float x = -24.2f; x <= 24.2f; x += 2.9f)
        {
            if (cx.Any(g => Mathf.Abs(x - g) < 3.1f) || Mathf.Abs(x + 9f) < 1.6f || Mathf.Abs(x - 9f) < 1.6f) continue;
            l.Add(It(lockers[li++ % 3], x, -4.55f, 0, true));
        }
        l.Add(It("vend_green", -9f, -4.55f, 0, true));
        l.Add(It("vend_purple", 9f, -4.55f, 0, true));
        l.Add(It("bench_wood", -13f, -8.7f, 0, false));
        l.Add(It("bench_wood", 13f, -8.7f, 0, false));
        l.Add(It("board_c", -5.5f, -8.2f, 0, true));
        l.Add(It("board_d", 5.5f, -8.2f, 0, true));
        l.Add(It("plant_big", -23.2f, -9.3f, 0, true));
        l.Add(It("plant_big", 23.2f, -9.3f, 0, true));
        l.Add(It("bin_green", -19f, -9.2f, 0, false));
        l.Add(It("bin_purple", 19f, -9.2f, 0, false));
        l.Add(It("clock", -8.75f, -5.9f, WallDecal));
        l.Add(It("clock", 8.75f, -5.9f, WallDecal));

        if (scene == "Aulas4")
        {
            // Aulas 4 está en la zona más vieja del campus: un par de detalles de abandono
            l.Add(It("p:Objects/Buildings/layered-posters_1_For-ground-and-walls.png", -4f, -5.9f, WallDecal));
            l.Add(It("p:Objects/Buildings/layered-posters_2_For-ground-and-walls.png", 4f, -5.9f, WallDecal));
            l.Add(It("g:1", 22.5f, -8.2f, 0, false, 0.6f));
        }
    }

    static void CentralItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        // Biblioteca
        foreach (var x in new[] { -31f, -28f, -16f, -13f }) l.Add(It("win_big", x, 18.2f, WallDecal, false, 0.9f));
        l.Add(It("plant_big", -33.4f, 1.2f, 0, true)); l.Add(It("plant_big", -10.6f, 1.2f, 0, true));
        l.Add(It("bin_green", -33.4f, 19f, 0, false)); l.Add(It("clock", -18.5f, 19.2f, WallDecal));
        // Auditorios: barra de cortinas (un solo sprite con 5 cortinas) junto a cada escenario, ventanas en la pared norte
        l.Add(It("curtain_purple", 9f, 18.2f, WallDecal - 5, false, 0.9f));
        foreach (var x in new[] { -6f, -3f, 0f }) l.Add(It("win_big", x, 18.2f, WallDecal, false, 0.9f));
        l.Add(It("curtain_purple", 26f, 17.3f, WallDecal - 5, false, 1.2f));
        foreach (var x in new[] { 18f, 33.5f }) l.Add(It("win_big", x, 18.2f, WallDecal, false, 0.9f));
        // Recepción, Salón Lili y D2: relojes, cuadros, tablón, plantas, bancas
        foreach (var (k, x) in new[] { ("frame_a", -30f), ("clock", -23.5f), ("cork", -17f), ("frame_b", -6f), ("clock", -2f), ("frame_a", 3f), ("cork", 14f), ("clock", 21.5f), ("frame_b", 29f) })
            l.Add(It(k, x, -7.7f, WallDecal));
        l.Add(It("plant_c", -33.5f, -8.2f, 0, true)); l.Add(It("plant_c", -13.5f, -8.2f, 0, true));
        l.Add(It("bench_red", -31f, -10.5f, 0, false)); l.Add(It("bench_red", -16f, -10.5f, 0, false));
        l.Add(It("plant_a", -10.8f, -8.2f, 0, true)); l.Add(It("plant_a", 6.8f, -8.2f, 0, true));
        l.Add(It("plant_a", 9.6f, -8.2f, 0, true)); l.Add(It("plant_a", 33.5f, -8.2f, 0, true));
        l.Add(It("wardrobe", 33f, -17.5f, 0, true));
        // Pasillo central
        float[] lockerX = { -32f, -29f, -26f, -18f, -15f, -12f, 8f, 11f, 14.5f, 19f, 31f, 33.2f };
        string[] lockers = { "locker_big1", "locker_big2", "locker_big3" };
        for (int i = 0; i < lockerX.Length; i++) l.Add(It(lockers[i % 3], lockerX[i], -0.6f, 0, true));
        l.Add(It("vend_green", -7f, -0.6f, 0, true)); l.Add(It("vend_purple", -4.6f, -0.6f, 0, true));
        l.Add(It("bench_wood", -12f, -5.5f)); l.Add(It("bench_wood", 10f, -5.5f)); l.Add(It("bench_wood", 30f, -5.5f));
        l.Add(It("plant_big", -34f, -5.3f, 0, true)); l.Add(It("plant_big", 25f, -5.3f, 0, true));
        l.Add(It("bin_green", -9f, -5.3f)); l.Add(It("bin_purple", 14f, -5.3f));
    }

    static void BienestarItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        // Recepción
        foreach (var x in new[] { -18f, -15f, -7f, -4f }) l.Add(It("win_beige", x, 8.1f, WallDecal, false, 1.3f));
        l.Add(It("clock", -11f, 8.6f, WallDecal));
        l.Add(It("bench_red", -17f, -6.4f)); l.Add(It("bench_red", -5.2f, -6.4f));
        l.Add(It("plant_big", -20.6f, 8.8f, 0, true)); l.Add(It("plant_big", -1.6f, 8.8f, 0, true));
        l.Add(It("bin_green", -20.8f, 1.8f)); l.Add(It("frame_a", -20f, -1.5f, WallDecal)); l.Add(It("cork", -2f, 2.5f, 0, true));
        // Enfermería
        foreach (var x in new[] { 9.5f, 13.5f, 17.5f }) l.Add(It("win_beige", x, 8.1f, WallDecal, false, 1.3f));
        l.Add(It("fridge_a", 2.6f, 8.9f, 0, true)); l.Add(It("cabinet", 5.6f, 8.9f, 0, true));
        l.Add(It("curtain_purple", 15f, 7.4f, WallDecal - 5, false, 1f));   // barra de cortinas detrás de las camillas
        l.Add(It("plant_c", 21f, 0.6f, 0, true)); l.Add(It("bin_grey", 21f, -8.2f)); l.Add(It("frame_b", 14f, 8.4f, WallDecal));
        l.Add(It("shoe_rack1", 3.2f, -8.3f, 0, true));
    }

    static void CafeteriaItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        foreach (var x in new[] { -9f, -6.5f, 6.5f, 9f }) l.Add(It("win_big", x, 7.8f, WallDecal, false, 0.8f));
        foreach (var y in new[] { -2f, -6f })
            foreach (var x in new[] { -12f, -4f, 4f })
            {
                l.Add(It("chair_a", x - 2.2f, y - 0.5f)); l.Add(It("chair_b", x + 2.2f, y - 0.5f));
            }
        l.Add(It("vend_green", -17.2f, 8.4f, 0, true)); l.Add(It("vend_purple", -14.8f, 8.4f, 0, true));
        l.Add(It("fridge_a", 16.2f, 8.4f, 0, true)); l.Add(It("fridge_b", 18.2f, 8.4f, 0, true));
        l.Add(It("plant_c", -18.6f, -7.4f, 0, true)); l.Add(It("plant_c", 18.8f, -7.4f, 0, true));
        l.Add(It("bin_green", -18.6f, 3.8f)); l.Add(It("bin_purple", 19f, 3.4f)); l.Add(It("bin_grey", 19f, -4.6f));
        l.Add(It("clock", 0f, 8.4f, WallDecal)); l.Add(It("lamp", -7.6f, -8.2f, 0, true)); l.Add(It("lamp", 7.6f, -8.2f, 0, true));
    }

    static void SalaProfesoresItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        l.Add(It("shelf_a", -12.6f, 7.6f, 0, true)); l.Add(It("shelf_b", -10.4f, 7.6f, 0, true)); l.Add(It("shelf_c", -8.2f, 7.6f, 0, true));
        l.Add(It("shelf_d", 8.2f, 7.6f, 0, true)); l.Add(It("cabinet", 10.4f, 7.6f, 0, true)); l.Add(It("shelf_a", 12.6f, 7.6f, 0, true));
        foreach (var x in new[] { -2.6f, 2.6f }) l.Add(It("win_beige", x, 6.4f, WallDecal, false, 1.3f));
        l.Add(It("frame_a", -5.5f, 6.9f, WallDecal)); l.Add(It("clock", 5.8f, 6.9f, WallDecal));
        foreach (var (x, y) in new[] { (-14f, 5.4f), (14f, 5.4f), (-14f, -6.6f), (14f, -6.6f) }) l.Add(It("plant_c", x, y, 0, true));
        l.Add(It("bin_grey", 12.6f, -6.8f)); l.Add(It("bin_green", -12.6f, -6.8f)); l.Add(It("cork", -14.6f, 1.8f, WallDecal));
        l.Add(It("pc_desk", -9f, 1.6f, 1)); l.Add(It("pc_desk", 9f, 1.6f, 1));
    }

    static void TechoItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        const string B = "Objects/Buildings/";
        l.Add(It("p:" + B + "HVAC.png", -23f, -9.5f, 0, true)); l.Add(It("p:" + B + "HVAC_Overgrown_Green.png", 22.5f, -9.5f, 0, true));
        l.Add(It("p:" + B + "HVAC_Overgrown_Dark-Green.png", 22.8f, 2.6f, 0, true));
        l.Add(It("p:" + B + "Air-vent_2_rusty.png", -23.5f, 8.8f, 0, true)); l.Add(It("p:" + B + "Air-vent_4_rusty.png", 24f, 10.2f, 0, true));
        l.Add(It("p:" + B + "Antenna_1.png", -24f, 10.8f, 0, true)); l.Add(It("p:" + B + "Antenna_2.png", 24.2f, 4.6f, 0, true));
        l.Add(It("p:" + B + "Hatch_1_Closed.png", -17f, -2.6f, GroundDecal)); l.Add(It("p:" + B + "Roof-hole_1_Gray.png", 14f, -2.6f, GroundDecal));
        l.Add(It("p:" + B + "Duct_1_side.png", -20f, 11.2f, 0, true));
        l.Add(It("g:0", 12f, -3.6f, 0, false, 0.7f)); l.Add(It("g:1", -22f, -3.2f, 0, false, 0.6f));
        // Hierba seca y hongos que se cuelan por las grietas
        for (int i = 0; i < 26; i++)
        {
            float x = (float)(rng.NextDouble() * 48 - 24), y = (float)(rng.NextDouble() * 22 - 11);
            if (Mathf.Abs(x) < 4.5f && Mathf.Abs(y) < 4.5f) continue;   // el puzzle del centro queda limpio
            l.Add(It("p:" + Pick(WildGrass), x, y, GroundDecal));
        }
        l.Add(It("p:" + N + "Flowers_Mashrooms_Other-nature-stuff/Mushrooms_1_Yellow.png", -12f, 8f, GroundDecal));
        l.Add(It("p:" + N + "Flowers_Mashrooms_Other-nature-stuff/Mushrooms_2_Red.png", 17f, 8.4f, GroundDecal));
    }

    static void SotanosItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        const string B = "Objects/Buildings/";
        // 0,67 x 1,5 (el ajuste general de contenedores): en el sótano, que es bajito, se quedan a su tamaño del pack
        l.Add(It("p:" + C + "Container_3_Gray_Horizontal.png", -12f, 4.2f, 0, true, 0.67f)); l.Add(It("p:" + C + "Container_7_Red_Horizontal.png", 12f, 4.2f, 0, true, 0.67f));
        l.Add(It("g:0", -20f, -5.6f, 0, false, 0.8f)); l.Add(It("g:1", -3f, 5.4f, 0, false, 0.7f));
        l.Add(It("g:1", 16f, -5.8f, 0, false, 0.8f)); l.Add(It("g:0", 26f, 5.4f, 0, false, 0.7f));
        l.Add(It("p:" + B + "Air-vent_2_rusty.png", -4f, 6.6f, WallDecal)); l.Add(It("p:" + B + "Air-vent_4_rusty.png", 20f, 6.6f, WallDecal));
        l.Add(It("p:" + B + "layered-posters_1_For-ground-and-walls.png", -26f, 6.6f, WallDecal));
        l.Add(It("p:" + B + "layered-posters_2_For-ground-and-walls.png", 6f, 6.6f, WallDecal));
        l.Add(It("p:" + B + "Hatch_1_Closed.png", -8f, -3f, GroundDecal));
        l.Add(It("lamp", -20f, -1.5f, 0, true)); l.Add(It("lamp", 0f, -1.5f, 0, true)); l.Add(It("lamp", 20f, -1.5f, 0, true));
        l.Add(It("p:" + B + "Brick-wall_1_Lying.png", 5f, -5f, 0, false)); l.Add(It("p:" + B + "Brick-wall_2_Lying.png", 22f, -4.6f, 0, false));
        l.Add(It("p:Objects/Pickable/Canned-food.png", 3.4f, 3.9f, GroundDecal));
    }

    static void MuseoItems(List<(string key, float x, float y, int order, bool solid, float scale)> l)
    {
        l.Add(It("statue", 0f, 3.6f, 0, true));
        foreach (var x in new[] { -5.5f, 5.5f }) l.Add(It("win_arch", x, 3.7f, WallDecal, false, 1f));
        l.Add(It("frame_a", -9.6f, 4.4f, WallDecal)); l.Add(It("frame_b", 9.6f, 4.4f, WallDecal));
        l.Add(It("plant_c", -11f, 4.6f, 0, true)); l.Add(It("plant_c", 11f, 4.6f, 0, true));
        l.Add(It("bench_red", 0f, -1.4f)); l.Add(It("lamp", -10.8f, -3.6f, 0, true)); l.Add(It("lamp", 10.8f, -3.6f, 0, true));
        l.Add(It("curtain_red", -2.6f, 4.2f, WallDecal - 5)); l.Add(It("curtain_red", 2.6f, 4.2f, WallDecal - 5));
    }

    // ───────────────────────── Lucía ─────────────────────────

    static int SetupLucia(Scene sc)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites Personajes/Enfermera.png").OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
        if (all.Length < 24) { Debug.LogWarning("[Escuela] Enfermera.png no tiene los 28 cuadros esperados."); return 0; }

        var lucia = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
            .FirstOrDefault(g => g.name == "Lucía" && g.GetComponent<NpcChallengeInteractable>() != null);
        if (lucia == null) { Debug.LogWarning("[Escuela] No se encontró a Lucía en Bienestar."); return 0; }

        var sr = lucia.GetComponent<SpriteRenderer>();
        lucia.transform.localScale = Vector3.one;   // el placeholder se estiraba con la escala: el sprite debe verse a su proporción real
        sr.sprite = all[0];
        sr.drawMode = SpriteDrawMode.Simple;
        sr.color = Color.white;
        if (lucia.TryGetComponent(out PlaceholderInfo info)) { info.applyKindColor = false; info.showLabel = false; }

        // No usar '??': los componentes de Unity sobrecargan == y '??' lo ignora.
        var anim = lucia.GetComponent<NpcSpriteAnimator>();
        if (anim == null) anim = lucia.AddComponent<NpcSpriteAnimator>();
        anim.idle = all.Take(4).ToArray();            // fila 1: idle mirando hacia abajo
        anim.talk = all.Skip(20).Take(4).ToArray();   // fila 6: hablando
        anim.idleFps = 3f; anim.talkFps = 6f;
        EditorUtility.SetDirty(lucia);
        return 1;
    }

    // ───────────────────────── Campus ─────────────────────────

    // Zonas "descuidadas" (ODS 8 y 12). Se calculan a partir de objetos de la escena, así siguen valiendo si el mapa cambia de tamaño.
    static Rect parkN, parkS, patio, storage, basement, mapArea;

    static bool InNeglected(Vector2 p) =>
        Inflate(parkN, 4f).Contains(p) || Inflate(parkS, 2.5f).Contains(p) || Inflate(patio, 1f).Contains(p) ||
        Inflate(storage, 1f).Contains(p) || basement.Contains(p);

    static Rect Inflate(Rect r, float m) => r.width <= 0 ? r : new Rect(r.xMin - m, r.yMin - m, r.width + 2 * m, r.height + 2 * m);

    /// <summary>Rectángulo que ocupa un óvalo del greybox (sprite Circle de 8 u estirado por la escala del transform).</summary>
    static Rect EllipseRect(GameObject go)
    {
        if (go == null) return Rect.zero;
        var s = go.transform.lossyScale;
        var size = new Vector2(8f * Mathf.Abs(s.x), 8f * Mathf.Abs(s.y));
        return new Rect((Vector2)go.transform.position - size / 2f, size);
    }

    /// <summary>Punto dentro de un rectángulo por fracciones (0-1).</summary>
    static Vector2 At(Rect r, float u, float v) => new Vector2(r.xMin + r.width * u, r.yMin + r.height * v);

    static int DecorateCampus(Scene sc)
    {
        var rootT = NewRoot(sc);
        int n = 0;

        var blocked = CollectBlocked(sc);
        var all = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        GameObject Find(string name) => all.FirstOrDefault(t => t != null && t.name == name)?.gameObject;

        var mb = Find("MapBounds")?.GetComponent<BoxCollider2D>();
        var mbBounds = mb != null ? mb.bounds : new Bounds(Vector3.zero, new Vector3(238f, 106f, 0f) * K);
        mapArea = new Rect(mbBounds.min.x + 2f, mbBounds.min.y + 2f, mbBounds.size.x - 4f, mbBounds.size.y - 4f);

        parkN = EllipseRect(Find("Parqueadero norte"));
        parkS = EllipseRect(Find("Parqueadero sur"));
        var patioGo = Find("Patio verde");
        patio = patioGo != null && patioGo.GetComponent<SpriteRenderer>() != null ? RectOf(patioGo.GetComponent<SpriteRenderer>().bounds) : Rect.zero;
        var almacen = Find("Entrada a almacén");
        storage = almacen != null ? new Rect(almacen.transform.position.x - 12f * K - 3f, mapArea.yMin - 1f, 24f * K + 6f, 5f) : Rect.zero;
        var sotanos = Find("Entrada a sótanos");
        basement = sotanos != null ? new Rect(sotanos.transform.position.x - 10.5f * K, sotanos.transform.position.y - 3.6f * K, 20f * K, 14f * K) : Rect.zero;

        n += Borders(Find);
        n += Parking(rootT, Find);
        n += Facades(Find, all);
        n += SpecialZones(rootT, Find);
        n += StreetFurniture(rootT, Find, all);
        n += Vegetation(rootT, blocked);
        return n;
    }

    static Rect RectOf(Bounds b) => new Rect(b.min.x, b.min.y, b.size.x, b.size.y);

    /// <summary>Rectángulos donde NO se esparce vegetación: edificios, caminos, parqueaderos, NPC, puertas, spawns.</summary>
    static List<Rect> CollectBlocked(Scene sc)
    {
        var list = new List<Rect>();
        foreach (var g in sc.GetRootGameObjects())
        {
            foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sortingOrder <= -95) continue;       // pasto de fondo y patio verde
                var b = sr.bounds;
                float pad = sr.sortingOrder >= 0 ? 1.2f : 0.5f;

                // Franjas en diagonal (caminos): su caja alineada a los ejes taparía media escena; se aproximan con cuadros a lo largo
                float angle = Mathf.Abs(Mathf.DeltaAngle(sr.transform.eulerAngles.z, 0f)) % 90f;
                if (angle > 1f && angle < 89f && sr.drawMode != SpriteDrawMode.Simple)
                {
                    var dir = sr.transform.right;
                    float len = sr.size.x * sr.transform.lossyScale.x, wid = sr.size.y * sr.transform.lossyScale.y;
                    for (float t = -len / 2f; t <= len / 2f; t += 2f)
                    {
                        var c = sr.transform.position + dir * t;
                        float r = wid / 2f + pad;
                        list.Add(new Rect(c.x - r, c.y - r, 2 * r, 2 * r));
                    }
                    continue;
                }
                list.Add(new Rect(b.min.x - pad, b.min.y - pad, b.size.x + 2 * pad, b.size.y + 2 * pad));
            }
            foreach (var col in g.GetComponentsInChildren<Collider2D>(true))
            {
                var b = col.bounds;
                // MapBounds (cubre todo el mapa) y los bordes largos no deben bloquear la vegetación
                if (col.GetComponent<MapBounds>() != null || b.size.x > 100f * K || b.size.y > 60f * K) continue;
                list.Add(new Rect(b.min.x - 1f, b.min.y - 1f, b.size.x + 2f, b.size.y + 2f));
            }
            foreach (var sp in g.GetComponentsInChildren<SpawnPoint>(true))
                list.Add(new Rect(sp.transform.position.x - 3f, sp.transform.position.y - 3f, 6f, 6f));
        }
        return list;
    }

    // ── Bordes: ladrillo gris al norte y este; cerca de madera (más descuidada) al sur y oeste ──

    static int Borders(Func<string, GameObject> find)
    {
        int n = 0;
        foreach (var (name, horizontal, brick) in new[]
        {
            ("Carrera 115 (borde norte)", true, true), ("Calle 42 (borde este)", false, true),
            ("Carrera 116 (borde sur)", true, false), ("Autopista Cali-Jamundí (borde oeste)", false, false),
        })
        {
            var go = find(name);
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.size;
            sr.enabled = false;
            if (go.TryGetComponent(out PlaceholderInfo info)) info.showLabel = false;

            var fence = new GameObject(FacadeName);
            fence.transform.SetParent(go.transform, false);
            var fsr = fence.AddComponent<SpriteRenderer>();
            fsr.drawMode = SpriteDrawMode.Tiled;
            fsr.tileMode = SpriteTileMode.Continuous;

            if (brick)
            {
                fsr.sprite = Tile("face_brick_grey", "A4");
                fsr.size = horizontal ? new Vector2(size.x, 2f) : new Vector2(2f, size.y);
                fence.transform.localPosition = horizontal ? new Vector3(0, -1f, 0) : new Vector3(0, -size.y / 2f, 0);
            }
            else if (horizontal)
            {
                fsr.sprite = Pk("Objects/Buildable/Wooden/Wooden-wall_Horizontal.png");
                fsr.size = new Vector2(size.x, 1.4f);
                fence.transform.localPosition = new Vector3(0, -0.7f, 0);
            }
            else
            {
                fsr.sprite = Pk("Objects/Buildable/Wooden/Wooden-wall_Vertical.png");
                fsr.size = new Vector2(0.8f, size.y);
                fence.transform.localPosition = new Vector3(0, -size.y / 2f, 0);
            }
            fsr.sortingOrder = 0;
            n++;
        }
        return n;
    }

    // ── Parqueaderos: asfalto, líneas, autos oxidados / cubiertos de maleza ──

    static int Parking(Transform root, Func<string, GameObject> find)
    {
        int n = 0;
        var asphalt = Tile("tile_floor_dark", "A5");

        foreach (var (name, rect) in new[] { ("Parqueadero norte", parkN), ("Parqueadero sur", parkS) })
        {
            var go = find(name);
            if (go == null || rect.width <= 0) continue;
            go.GetComponent<SpriteRenderer>().enabled = false;
            if (go.TryGetComponent(out PlaceholderInfo info)) info.showLabel = false;

            var floor = new GameObject("Asfalto");
            floor.transform.SetParent(root, false);
            floor.transform.position = rect.center;
            var sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = asphalt;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = rect.size;
            sr.sortingOrder = -85;
            n++;
        }
        foreach (var name in new[] { "Vía de acceso norte", "Vía de acceso sur" })
        {
            var go = find(name);
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            var size = sr.size;
            sr.sprite = asphalt; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.size = size; sr.color = Color.white;
            if (go.TryGetComponent(out PlaceholderInfo info)) { info.applyKindColor = false; info.showLabel = false; }
            n++;
        }

        // Parqueadero norte: una o dos filas de cabinas (autos de frente), muchas vacías, algunas volcadas
        if (parkN.width > 0)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Placeholders/Square.png");
            var rustCars = RustCars;
            var access = find("Vía de acceso norte");
            float accessX = access != null ? access.transform.position.x : float.NaN;

            // Un auto mide ~5,6 u de largo (x1,5): dos filas caben si el parqueadero mide al menos ~13 u de alto
            var rows = parkN.height >= 13f ? new[] { parkN.yMin + 0.5f, parkN.yMax - 5.9f } : new[] { parkN.center.y - 2.8f };
            foreach (float rowY in rows)
            {
                for (float x = parkN.xMin + 2.8f; x <= parkN.xMax - 2.8f; x += 5.4f)
                {
                    bool nearAccess = !float.IsNaN(accessX) && Mathf.Abs(x - accessX) < 3.5f && rowY > parkN.center.y;
                    if (nearAccess || rng.NextDouble() < 0.4) continue;
                    var car = Place(root, Pk(Pick(rustCars)), new Vector2(x + (float)(rng.NextDouble() - 0.5) * 0.6f, rowY), "Auto abandonado", 0, true);
                    if (car != null) n++;
                }
                for (float x = parkN.xMin + 0.2f; x <= parkN.xMax - 0.1f; x += 5.4f)
                {
                    var line = Put(root, white, new Vector2(x, rowY - 0.2f), "Línea de parqueo", -84, false);
                    if (line == null) continue;
                    var sr = line.GetComponent<SpriteRenderer>();
                    sr.drawMode = SpriteDrawMode.Sliced; sr.size = new Vector2(0.16f, 5.8f); sr.color = new Color(1f, 1f, 1f, 0.55f);
                }
            }
            n += Place(root, Garbage(0), At(parkN, 0.07f, 0.82f), "Basura", 0, false, 0.9f) != null ? 1 : 0;
            n += Place(root, Garbage(1), At(parkN, 0.80f, 0.08f), "Basura", 0, false, 0.8f) != null ? 1 : 0;
            n += Place(root, Pk(C + "Container_3_Gray_Horizontal.png"), At(parkN, 0.93f, 0.55f), "Contenedor", 0, true) != null ? 1 : 0;
        }

        // Parqueadero sur: buses y camiones fuera de servicio con maleza
        if (parkS.width > 0)
        {
            n += Place(root, Pk(V + "Overgrown/Car_8_Overgrown_Bus/Dark-Green/Car_8_Overgrown_Dark-Green_Orange_Bus.png"), At(parkS, 0.22f, 0.12f), "Bus abandonado", 0, true) != null ? 1 : 0;
            n += Place(root, Pk(V + "Overgrown/Car_8_Overgrown_Bus/Green/Car_8_Overgrown_Green_Blue_Bus.png"), At(parkS, 0.78f, 0.12f), "Bus abandonado", 0, true) != null ? 1 : 0;
            n += Place(root, Pk(RustCars[1]), At(parkS, 0.50f, 0.12f), "Auto abandonado", 0, true) != null ? 1 : 0;
            n += Place(root, Garbage(1), At(parkS, 0.95f, 0.15f), "Basura", 0, false, 0.8f) != null ? 1 : 0;
        }
        return n;
    }

    // ── Fachadas ──

    static int Facades(Func<string, GameObject> find, Transform[] all)
    {
        int n = 0;
        var doors = all.Where(t => t != null && t.GetComponent<Door>() != null).Select(t => t.gameObject).ToArray();
        // nombre, cara, techo, toldos, ¿envejecida?
        var specs = new[]
        {
            ("Edificio Central - ala norte", "face_brick", "roof_green", "blue", false),
            ("Edificio Central - centro (Salón Lili y Auditorio Quincha)", "face_brick", "roof_green", "blue", false),
            ("Edificio Central - ala sur", "face_brick", "roof_green", "blue", false),
            ("Aulas 1", "face_brick", "roof_green", "blue", false),
            ("Museo Lili", "face_brick", "roof_green", "blue", false),
            ("Aulas 2", "face_brick", "roof_green", "blue", false),
            ("Aulas 3", "face_brick", "roof_green", "blue", false),
            ("Aulas 4", "face_brick", "roof_green", "blue", true),
            ("Cafetería UAO", "face_beige", "roof_blue", "orange", false),
            ("Bienestar Universitario", "face_beige", "roof_blue", "blue", false),
        };

        foreach (var (name, face, roof, awning, aged) in specs)
        {
            var b = find(name);
            if (b == null) continue;
            var sr = b.GetComponent<SpriteRenderer>();
            float W = sr.size.x, H = sr.size.y;
            Vector2 basePos = b.transform.position;

            sr.sprite = Tile(face, "A4");
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(W, H);
            sr.color = Color.white;
            if (b.TryGetComponent(out PlaceholderInfo info)) { info.applyKindColor = false; info.showLabel = false; }

            var fx = new GameObject(FacadeName);
            fx.transform.SetParent(b.transform, false);
            var t = fx.transform;

            // Techo
            float roofH = Mathf.Min(3f, H * 0.3f);
            var roofGo = new GameObject("Techo");
            roofGo.transform.SetParent(t, false);
            roofGo.transform.localPosition = new Vector3(0, H - roofH, 0);
            var rsr = roofGo.AddComponent<SpriteRenderer>();
            rsr.sprite = Tile(roof, "A4"); rsr.drawMode = SpriteDrawMode.Tiled; rsr.tileMode = SpriteTileMode.Continuous;
            rsr.size = new Vector2(W + 0.4f, roofH); rsr.sortingOrder = 1;
            n++;

            // Ventanas por filas (entre la base y el techo)
            int rows = Mathf.Max(1, Mathf.FloorToInt((H - roofH - 2.4f) / 3.3f));
            float step = 4.3f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((W - 4f) / step) + 1);
            float x0 = -(cols - 1) * step / 2f;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    Sprite ws; float scale = 1.35f;
                    if (aged && rng.NextDouble() < 0.4) { ws = Pk(rng.NextDouble() < 0.5 ? "Objects/Windows/Window_17_Boarded-up_Beige.png" : "Objects/Windows/Window_13_Broken_Beige.png"); scale = 1.1f; }
                    else ws = Sc((r + c) % 2 == 0 ? "win_beige" : "win_yellow");
                    n += PutLocal(t, ws, new Vector2(x0 + c * step, 2.5f + r * 3.3f), "Ventana", 2, scale) ? 1 : 0;
                }

            // Equipos en la azotea
            string[] roofProps = aged
                ? new[] { "Objects/Buildings/HVAC_Overgrown_Green.png", "Objects/Buildings/Air-vent_2_rusty.png", "Objects/Buildings/Antenna_2.png" }
                : new[] { "Objects/Buildings/HVAC.png", "Objects/Buildings/Air-vent_1.png", "Objects/Buildings/Antenna_1.png" };
            int props = Mathf.Max(2, Mathf.RoundToInt(W / 14f));
            for (int i = 0; i < props; i++)
                n += PutLocal(t, Pk(roofProps[i % roofProps.Length]), new Vector2(-W / 2f + 3f + Mathf.Max(W - 6f, 0f) * (i + 0.5f) / props, H - 0.2f), "Azotea", 2, 1f) ? 1 : 0;

            // Toldos sobre cada puerta del edificio
            foreach (var d in doors)
            {
                var dp = (Vector2)d.transform.position;
                if (!d.name.StartsWith("Puerta") || dp.x < basePos.x - W / 2f || dp.x > basePos.x + W / 2f || Mathf.Abs(dp.y - basePos.y) > 2.5f) continue;
                var aw = Pk($"Objects/Buildings/Awning_{awning}_1.png");
                n += PutLocal(t, aw, new Vector2(dp.x - basePos.x, dp.y - basePos.y + 1.45f), "Toldo", 2, 1f) ? 1 : 0;   // sobre la puerta (que ahora mide 2,5 u)
            }
        }

        // Portería: reja con portón del pack
        foreach (var name in new[] { "Portería peatonal oeste", "Portería peatonal este", "Portería vehicular norte", "Portería vehicular sur" })
        {
            var go = find(name);
            if (go == null) continue;
            var gate = Pk("Tiles/Wire-Fence/Wire-Fence_Gate.png");
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = gate; sr.drawMode = SpriteDrawMode.Simple; sr.color = Color.white;
            go.transform.localScale = Vector3.one * 1.5f;
            if (go.TryGetComponent(out PlaceholderInfo info)) { info.applyKindColor = false; info.showLabel = false; }
            n++;
        }
        return n;
    }

    /// <summary>Pone un sprite como hijo con su base en una posición local (respecto al edificio).</summary>
    static bool PutLocal(Transform parent, Sprite s, Vector2 localFoot, string name, int order, float scale)
    {
        if (s == null) return false;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localFoot.x, localFoot.y, 0f);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingOrder = order;
        return true;
    }

    // ── Zonas especiales descuidadas ──

    static int SpecialZones(Transform root, Func<string, GameObject> find)
    {
        int n = 0;
        int Add(string rel, Vector2 foot, bool solid = true, int order = 0, float scale = 1f) =>
            Place(root, Pk(rel), foot, rel.Split('/').Last().Replace(".png", ""), order, solid, scale) != null ? 1 : 0;
        int AddG(int i, Vector2 foot, float scale = 1f) => Place(root, Garbage(i), foot, "Basura", 0, false, scale) != null ? 1 : 0;

        // Patio verde (este): vehículos y contenedores abandonados
        if (patio.width > 0)
        {
            n += Add(V + "Overgrown/Car_8_Overgrown_Bus/Bleak-Yellow/Car_8_Overgrown_Bleak-Yellow_Green_Bus.png", At(patio, 0.34f, 0.55f));
            n += Add(V + "Overgrown/Car_5_Overgrown_Tractor/Dark-Green/Car_5_Overgrown_Dark-Green_Tractor_Side.png", At(patio, 0.72f, 0.72f));
            n += Add(C + "Container_4_Gray_Horizontal_Overgrown_Dark-Green.png", At(patio, 0.25f, 0.12f));
            n += Add(C + "Container_6_Red_Vertical_Overgrown_Bleak-Yellow.png", At(patio, 0.62f, 0.10f));
            n += Add(C + "Container_10_Green_Vertical_Overgrown_Green.png", At(patio, 0.08f, 0.50f));
            n += AddG(0, At(patio, 0.10f, 0.88f), 0.9f); n += AddG(1, At(patio, 0.92f, 0.60f), 0.8f); n += AddG(0, At(patio, 0.90f, 0.12f), 0.7f);
            n += Add("Objects/Buildable/Wooden/Wooden-wall_Horizontal.png", At(patio, 0.45f, 0.95f));
            n += Add("Objects/Buildable/Wooden/Wooden-wall_Horizontal.png", At(patio, 0.55f, 0.95f));
        }

        // Almacén (sur): patio de contenedores y escombros
        if (storage.width > 0)
        {
            n += Add(C + "Container_5_Red_Vertical.png", At(storage, 0.10f, 0.05f));
            n += Add(C + "Container_3_Gray_Horizontal.png", At(storage, 0.26f, 0.05f));
            n += Add(C + "Container_11_Green_Horizontal.png", At(storage, 0.74f, 0.05f));
            n += Add(C + "Container_8_Red_Horizontal_Overgrown_Dark-Green.png", At(storage, 0.92f, 0.05f));
            n += AddG(1, At(storage, 0.40f, 0.60f), 0.8f); n += AddG(0, At(storage, 0.62f, 0.60f), 0.7f);
            n += Add("Objects/Buildings/Brick-wall_1_Lying.png", At(storage, 0.35f, 0.9f), false);
            n += Add("Objects/Buildings/Brick-wall_2_Lying.png", At(storage, 0.68f, 0.9f), false);
        }

        // Entrada a sótanos: escotilla, ductos y basura
        if (basement.width > 0)
        {
            n += Add("Objects/Buildings/Hatch_1_Closed.png", At(basement, 0.20f, 0.30f), false, -60);
            n += Add("Objects/Buildings/HVAC_Overgrown_Dark-Green.png", At(basement, 0.85f, 0.45f));
            n += Add("Objects/Buildings/Air-vent_2_rusty.png", At(basement, 0.05f, 0.62f));
            n += AddG(1, At(basement, 0.25f, 0.55f), 0.7f); n += AddG(0, At(basement, 0.80f, 0.12f), 0.6f);
        }

        // Las puertas de servicio muestran su entrada abandonada
        foreach (var (door, rel) in new[] { ("Entrada a sótanos", "Objects/Buildings/Enterance_Dark-Green.png"), ("Entrada a almacén", "Objects/Buildings/Enterance_Green.png") })
        {
            var go = find(door);
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            bool alreadyAdjusted = go.transform.Find("[Base ajustada]") != null || (sr.sprite != null && sr.sprite.name.StartsWith("Enterance"));

            // Escala uniforme (venía aplastada a 3,6 x 2 u) sin cambiar la zona de interacción en el mundo
            bool hasBox = go.TryGetComponent(out BoxCollider2D box);
            var lossy = go.transform.lossyScale;
            var worldBox = hasBox ? new Vector2(box.size.x * Mathf.Abs(lossy.x), box.size.y * Mathf.Abs(lossy.y)) : Vector2.zero;
            go.transform.localScale = Vector3.one * 1.15f;
            if (hasBox) box.size = worldBox / 1.15f;

            sr.sprite = Pk(rel); sr.drawMode = SpriteDrawMode.Simple; sr.color = Color.white;
            if (go.TryGetComponent(out PlaceholderInfo info)) { info.applyKindColor = false; info.showLabel = false; }
            // el sprite tiene el pivote abajo: se baja para que su base quede en la base de la zona de la puerta (solo la primera vez)
            if (go.transform.Find("[Base ajustada]") == null)
            {
                if (!alreadyAdjusted) go.transform.position += new Vector3(0f, -0.9f, 0f);
                var mark = new GameObject("[Base ajustada]");
                mark.transform.SetParent(go.transform, false);
            }
            n++;
        }
        return n;
    }

    // ── Mobiliario urbano: farolas, bancas y canecas ──

    static int StreetFurniture(Transform root, Func<string, GameObject> find, Transform[] all)
    {
        int n = 0;

        // Farolas a lo largo del borde norte de los caminos horizontales largos (sin rotación)
        foreach (var t in all)
        {
            if (t == null || !t.name.StartsWith("Camino")) continue;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled || sr.sortingOrder > -80 || Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.z, 0f)) > 1f) continue;
            var b = sr.bounds;
            if (b.size.x < 14f || b.size.y > 8f) continue;
            for (float x = b.min.x + 3f; x <= b.max.x - 3f; x += 11f)
                n += Place(root, Sc("lamp"), new Vector2(x, b.max.y + 0.4f), "Farola", 0, true) != null ? 1 : 0;
        }

        // Bancas con caneca alrededor del Ágora (lado sur)
        var agora = find("Borde del Ágora");
        if (agora != null && agora.GetComponent<SpriteRenderer>() != null)
        {
            var b = agora.GetComponent<SpriteRenderer>().bounds;
            float radius = b.extents.x + 1.2f;
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (215f + i * 18f);
                var p = (Vector2)b.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                n += Place(root, Sc(i % 2 == 0 ? "bench_wood" : "bench_metal1"), p, "Banca", 0, false) != null ? 1 : 0;
            }
            n += Place(root, Sc("bin_green"), (Vector2)b.center + new Vector2(radius * 0.55f, -radius * 0.95f), "Caneca", 0, false) != null ? 1 : 0;
            n += Place(root, Sc("bin_purple"), (Vector2)b.center + new Vector2(-radius * 0.55f, -radius * 0.95f), "Caneca", 0, false) != null ? 1 : 0;
        }

        // Una banca junto a cada puerta de los edificios de aulas
        foreach (var t in all)
        {
            if (t == null || !t.name.StartsWith("Puerta Aulas")) continue;
            n += Place(root, Sc("bench_wood"), (Vector2)t.position + new Vector2(3f, -2.4f), "Banca", 0, false) != null ? 1 : 0;
        }
        return n;
    }

    // ── Vegetación esparcida ──

    static int Vegetation(Transform root, List<Rect> blocked)
    {
        int n = 0;
        var area = mapArea;

        bool Free(Vector2 p) => area.Contains(p) && !blocked.Any(r => r.Contains(p)) && !occupied.Any(o => o.Contains(p));
        void Claim(Vector2 p, float r) => blocked.Add(new Rect(p.x - r, p.y - r, 2 * r, 2 * r));

        void Scatter(float cell, double prob, bool neglected, Func<Sprite> pick, string name, int order, bool solid, float claim, float scale = 1f)
        {
            for (float x = area.xMin; x < area.xMax; x += cell)
                for (float y = area.yMin; y < area.yMax; y += cell)
                {
                    if (rng.NextDouble() > prob) continue;
                    var p = new Vector2(x + (float)rng.NextDouble() * cell, y + (float)rng.NextDouble() * cell);
                    if (InNeglected(p) != neglected || !Free(p)) continue;
                    var s = pick();
                    if (Put(root, s, p, name, order, solid, scale) == null) continue;
                    if (claim > 0f) Claim(p, claim);
                    n++;
                }
        }

        var cleanTrees = CleanTrees.Select(Pk).Concat(new[] { Sc("tree_a"), Sc("tree_b") }).Where(s => s != null).ToArray();
        var wildTrees = WildTrees.Select(Pk).Where(s => s != null).ToArray();
        var cleanBushes = CleanBushes.Select(Pk).Concat(new[] { Sc("bush_leaf"), Sc("bush_vine"), Sc("bush_small") }).Where(s => s != null).ToArray();
        var wildBushes = WildBushes.Select(Pk).Where(s => s != null).ToArray();
        var flowers = Flowers().Select(Pk).Where(s => s != null).ToArray();
        var cleanGrass = CleanGrass.Select(Pk).Where(s => s != null).ToArray();
        var wildGrass = WildGrass.Select(Pk).Where(s => s != null).ToArray();
        var rocks = Rocks.Select(Pk).Where(s => s != null).ToArray();
        var stumps = Stumps.Select(Pk).Where(s => s != null).ToArray();

        // Árboles primero (ocupan más), luego arbustos, flores, maleza y piedras. En un mapa más chico, árboles más separados.
        float treeCell = 8f / Mathf.Sqrt(K);
        Scatter(treeCell, 0.55, false, () => Pick(cleanTrees), "Árbol", 0, true, 2.6f);
        Scatter(treeCell * 0.9f, 0.5, true, () => Pick(wildTrees), "Árbol", 0, true, 2.6f);
        Scatter(5f, 0.45, false, () => Pick(cleanBushes), "Arbusto", 0, false, 1.2f);
        Scatter(5f, 0.5, true, () => Pick(wildBushes), "Arbusto", 0, false, 1.2f);
        Scatter(4.5f, 0.4, false, () => Pick(flowers), "Flores", GroundDecal, false, 0f);
        Scatter(6f, 0.3, true, () => Pick(stumps), "Tronco", 0, false, 1f);
        Scatter(2.6f, 0.45, false, () => Pick(cleanGrass), "Pasto", GroundDecal - 1, false, 0f);
        Scatter(2.2f, 0.6, true, () => Pick(wildGrass), "Maleza", GroundDecal - 1, false, 0f);
        Scatter(7f, 0.25, false, () => Pick(rocks), "Piedra", 0, false, 0f);
        Scatter(5f, 0.35, true, () => Pick(rocks), "Piedra", 0, false, 0f);
        return n;
    }
}
