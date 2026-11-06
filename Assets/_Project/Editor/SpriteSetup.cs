using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Preparación de los sprites reales del campus: importación a 48 PPU, corte de hojas (TILE SET, Outside, edificios),
/// tiles isométricos y textura de pasto. No tiene menús: se llama desde código y NO regenera escenas.
/// </summary>
public static class SpriteSetup
{
    public const int Ppu = 48;
    public const string Root = "Assets/_Project";
    public const string Esc = Root + "/Art/Sprites Escuela";
    public const string TilesDir = Root + "/Art/Tiles";
    public const string GrassPath = Root + "/Art/Terreno/GrassTile.png";

    // ───────────────────────── 1. Importación general ─────────────────────────

    /// <summary>Todos los sprites del proyecto: 48 PPU, Filter Point, Compression None.</summary>
    public static string ApplyImportDefaults()
    {
        int changed = 0, total = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || imp.textureType != TextureImporterType.Sprite) continue;
            total++;
            bool dirty = false;
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, Ppu)) { imp.spritePixelsPerUnit = Ppu; dirty = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (dirty) { imp.SaveAndReimport(); changed++; }
        }
        return $"sprites={total}, reimportados={changed}";
    }

    /// <summary>Mantiene el tamaño de Andrés (1x2) tras pasar su hoja de 100 a 48 PPU.</summary>
    public static string FixAndresScale()
    {
        const string path = Root + "/Prefabs/Andres.prefab";
        var go = PrefabUtility.LoadPrefabContents(path);
        var sr = go.GetComponent<SpriteRenderer>();
        var before = go.transform.localScale;
        // Las hojas de Andrés estaban a 100 PPU: al pasar a 48 PPU el sprite crece 100/48 veces.
        go.transform.localScale = new Vector3(before.x * 48f / 100f, before.y * 48f / 100f, 1f);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        var size = sr.bounds.size;
        PrefabUtility.UnloadPrefabContents(go);
        return $"escala {before} -> {before.x * 0.48f:F3},{before.y * 0.48f:F3}; tamaño en mundo {size.x:F2}x{size.y:F2}";
    }

    // ───────────────────────── 2. Texturas de placeholders a la escala 48x48 ─────────────────────────

    public static void RegeneratePlaceholderTextures()
    {
        Write($"{Root}/Art/Placeholders/Square.png", 48, false);
        Write($"{Root}/Art/Placeholders/SquareBase.png", 48, false);
        Write($"{Root}/Art/Placeholders/Circle.png", 384, true);        // 384 px / 48 PPU = 8 u (igual que antes)
        Write($"{Root}/Art/Placeholders/CircleBase.png", 384, true);
        AssetDatabase.Refresh();
    }

    static void Write(string path, int n, bool round)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        float r = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool inside = !round || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) <= r;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    // ───────────────────────── 3. Corte de hojas ─────────────────────────

    static Texture2D LoadReadable(string path)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    static void SetRects(string path, List<SpriteRect> rects)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = Ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());
        provider.Apply();
        imp.SaveAndReimport();
    }

    static SpriteRect Rect(string name, float x, float yBottom, float w, float h, Vector2 pivot) => new SpriteRect
    {
        name = name,
        rect = new Rect(x, yBottom, w, h),
        alignment = SpriteAlignment.Custom,
        pivot = pivot,
        spriteID = GUID.Generate(),
    };

    /// <summary>TILE SET: 6 filas x 5 columnas de rombos isométricos 2:1. Pivote en el centro de la cara superior.</summary>
    public static string SliceTileSet()
    {
        var path = $"{Esc}/TILE SET.png";
        var tex = LoadReadable(path);
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        bool Op(int x, int yTop) => px[(h - 1 - yTop) * w + x].a > 40;

        // Filas: tramos de y con píxeles opacos
        var rowOpaque = new bool[h];
        int minX = w, maxX = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Op(x, y)) { rowOpaque[y] = true; if (x < minX) minX = x; if (x > maxX) maxX = x; }
        var rows = new List<(int top, int bottom)>();
        int start = -1;
        for (int y = 0; y < h; y++)
        {
            if (rowOpaque[y] && start < 0) start = y;
            if ((!rowOpaque[y] || y == h - 1) && start >= 0) { if (y - start > 40) rows.Add((start, y - 1)); start = -1; }
        }
        const int cols = 5;
        float pitch = (maxX - minX + 1) / (float)cols;

        var rects = new List<SpriteRect>();
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < cols; c++)
            {
                float x = minX + c * pitch;
                int top = rows[r].top, bottom = rows[r].bottom;
                float hh = bottom - top + 1;
                float yBottom = h - 1 - bottom;
                // Cara superior = rombo 2:1 de ancho "pitch"; su centro está pitch/4 bajo el vértice superior.
                float pivotY = 1f - (pitch / 4f) / hh;
                rects.Add(Rect($"TILE_{r}_{c}", Mathf.Round(x), yBottom, Mathf.Round(pitch), hh, new Vector2(0.5f, pivotY)));
            }
        Object.DestroyImmediate(tex);
        SetRects(path, rects);
        return $"filas={rows.Count}, tile {pitch:F1}px de ancho -> celda {pitch / Ppu:F2} x {pitch / Ppu / 2f:F2} u";
    }

    /// <summary>Árboles y vegetación de Outside, cada uno con el pivote en la base del tronco.</summary>
    public static string SliceOutside()
    {
        var path = $"{Esc}/Outside.png";
        var tex = LoadReadable(path);
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        bool Op(int x, int yTop) => px[(h - 1 - yTop) * w + x].a > 40;

        // nombre, región (x, yTop, ancho, alto) desde arriba-izquierda, pivote (x normalizado, px sobre la base del sprite)
        var regions = new (string name, int x, int y, int rw, int rh, float pivX, float baseOffsetPx)[]
        {
            ("tree_green_a",   0,   0,   170, 236, 0.50f, 28),
            ("tree_pink",      180, 0,   180, 240, 0.44f, 30),
            ("tree_olive",     0,   240, 175, 199, 0.37f, 3),
            ("tree_green_b",   180, 244, 180, 195, 0.41f, 3),
            ("bush_small",     380, 35,  55,  75,  0.50f, 0),
            ("vine_a",         435, 35,  60,  75,  0.50f, 0),
            ("vine_tall",      380, 140, 60,  110, 0.50f, 0),
            ("bush_purple",    435, 160, 60,  45,  0.50f, 0),
            ("flower",         440, 255, 35,  40,  0.50f, 0),
            ("ground_dirt",    520, 35,  110, 110, 0.50f, 0),
            ("ground_cobble",  520, 180, 110, 105, 0.50f, 0),
        };

        var rects = new List<SpriteRect>();
        var log = new List<string>();
        foreach (var rg in regions)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = rg.y; y < Mathf.Min(h, rg.y + rg.rh); y++)
                for (int x = rg.x; x < Mathf.Min(w, rg.x + rg.rw); x++)
                    if (Op(x, y)) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) { log.Add(rg.name + ": vacío"); continue; }
            float sw = x1 - x0 + 1, sh = y1 - y0 + 1;
            rects.Add(Rect(rg.name, x0, h - 1 - y1, sw, sh, new Vector2(rg.pivX, Mathf.Clamp01(rg.baseOffsetPx / sh))));
            log.Add($"{rg.name} {sw}x{sh}");
        }
        Object.DestroyImmediate(tex);
        SetRects(path, rects);
        return string.Join(", ", log);
    }

    // Edificios: nombre de archivo -> rebanadas verticales (x0, x1) con su punto de orden (pivote) en píxeles desde arriba-izquierda.
    public struct Slice { public string name; public int x0, x1; public Vector2 pivotPx; }

    public static readonly Dictionary<string, Slice[]> Buildings = new Dictionary<string, Slice[]>
    {
        // Aulas (isométrico): 3 cortes para que Andrés se ordene bien delante/detrás a lo largo de la fachada diagonal
        ["Aulas1"] = AulasSlices("Aulas1"),
        ["Aulas2"] = AulasSlices("Aulas2"),
        ["Aulas3"] = AulasSlices("Aulas3"),
        ["Aulas4"] = AulasSlices("Aulas4"),
        // El Ágora y el Edificio Central (vista cenital) van como decorado de suelo: un solo corte, pivote en la base
        ["Agora"] = new[] { new Slice { name = "Agora", x0 = 0, x1 = 1536, pivotPx = new Vector2(768, 1024) } },
        ["ARCO UAO"] = new[] { new Slice { name = "ARCO_UAO", x0 = 0, x1 = 1448, pivotPx = new Vector2(724, 1086) } },
        // Cafetería + Bienestar: pivote en el centro de la fachada frontal
        ["CAF y BIENESTAR"] = new[] { new Slice { name = "CAF_BIENESTAR", x0 = 0, x1 = 1536, pivotPx = new Vector2(883, 890) } },
    };

    static Slice[] AulasSlices(string n) => new[]
    {
        new Slice { name = n + "_izq", x0 = 0,    x1 = 600,  pivotPx = new Vector2(300, 880) },
        new Slice { name = n + "_med", x0 = 600,  x1 = 1050, pivotPx = new Vector2(820, 830) },
        new Slice { name = n + "_der", x0 = 1050, x1 = 1450, pivotPx = new Vector2(1220, 650) },
    };

    public static string SliceBuildings()
    {
        var log = new List<string>();
        foreach (var kv in Buildings)
        {
            var path = $"{Esc}/{kv.Key}.png";
            var tex = LoadReadable(path);
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);

            var rects = new List<SpriteRect>();
            foreach (var s in kv.Value)
            {
                float rw = Mathf.Min(s.x1, w) - s.x0;
                var pivot = new Vector2((s.pivotPx.x - s.x0) / rw, (h - s.pivotPx.y) / h);   // pivote normalizado dentro del corte
                rects.Add(Rect(s.name, s.x0, 0, rw, h, pivot));
            }
            SetRects(path, rects);
            log.Add($"{kv.Key} {w}x{h} ({kv.Value.Length} corte/s)");
        }
        return string.Join(", ", log);
    }

    // ───────────────────────── 4. Tiles isométricos y pasto ─────────────────────────

    /// <summary>Crea un asset Tile por cada rombo de TILE SET.</summary>
    public static string CreateTileAssets()
    {
        if (!AssetDatabase.IsValidFolder(TilesDir)) AssetDatabase.CreateFolder(Root + "/Art", "Tiles");
        int n = 0;
        foreach (var s in AssetDatabase.LoadAllAssetsAtPath($"{Esc}/TILE SET.png").OfType<Sprite>())
        {
            var path = $"{TilesDir}/{s.name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = s;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            n++;
        }
        AssetDatabase.SaveAssets();
        return $"tiles creados/actualizados: {n}";
    }

    /// <summary>Textura de pasto repetible de 8x8 u, hecha con los verdes reales del TILE SET.</summary>
    public static string CreateGrassTexture()
    {
        var dir = Root + "/Art/Terreno";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "/Art", "Terreno");

        var src = LoadReadable($"{Esc}/TILE SET.png");
        var greens = new List<Color32>();
        foreach (var p in src.GetPixels32())
            if (p.a > 200 && p.g > p.r + 25 && p.g > p.b + 25) greens.Add(p);
        Object.DestroyImmediate(src);
        if (greens.Count < 100) greens.Add(new Color32(70, 160, 50, 255));

        const int n = 384, block = 4;
        var rnd = new System.Random(48);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int by = 0; by < n / block; by++)
            for (int bx = 0; bx < n / block; bx++)
            {
                var c = greens[rnd.Next(greens.Count)];
                for (int y = 0; y < block; y++)
                    for (int x = 0; x < block; x++)
                        px[(by * block + y) * n + bx * block + x] = c;
            }
        tex.SetPixels32(px);
        File.WriteAllBytes(GrassPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GrassPath, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(GrassPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = Ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Repeat;
        var s2 = new TextureImporterSettings();
        imp.ReadTextureSettings(s2);
        s2.spriteMeshType = SpriteMeshType.FullRect;
        s2.spriteAlignment = (int)SpriteAlignment.Center;
        imp.SetTextureSettings(s2);
        imp.SaveAndReimport();
        return $"pasto generado con {greens.Count} colores de muestra";
    }

    // ───────────────────────── 5. Cámara 640x360 a 48 PPU ─────────────────────────

    /// <summary>Aplica PixelCameraSettings (640x360, 48 PPU) al prefab CameraRig y a las cámaras de las escenas.</summary>
    public static string ApplyCameraEverywhere()
    {
        int n = 0;
        var rigPath = Root + "/Prefabs/CameraRig.prefab";
        var rig = PrefabUtility.LoadPrefabContents(rigPath);
        foreach (var ppc in rig.GetComponentsInChildren<PixelPerfectCamera>(true))
        {
            PixelCameraSettings.Apply(ppc.GetComponent<Camera>(), ppc);
            n++;
        }
        foreach (var vcam in rig.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
        {
            var lens = vcam.Lens;
            lens.OrthographicSize = PixelCameraSettings.OrthoSize;
            vcam.Lens = lens;
        }
        PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
        PrefabUtility.UnloadPrefabContents(rig);

        foreach (var s in EditorBuildSettings.scenes)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
            bool dirty = false;
            foreach (var ppc in Object.FindObjectsByType<PixelPerfectCamera>(FindObjectsInactive.Include))
            {
                PixelCameraSettings.Apply(ppc.GetComponent<Camera>(), ppc);
                EditorUtility.SetDirty(ppc);
                dirty = true; n++;
            }
            foreach (var vcam in Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include))
            {
                var lens = vcam.Lens;
                lens.OrthographicSize = PixelCameraSettings.OrthoSize;
                vcam.Lens = lens;
                EditorUtility.SetDirty(vcam);
                dirty = true;
            }
            if (dirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        return $"cámaras PixelPerfect actualizadas: {n} (ref {PixelCameraSettings.ViewWidth}x{PixelCameraSettings.ViewHeight}, {PixelCameraSettings.Ppu} PPU)";
    }
}
