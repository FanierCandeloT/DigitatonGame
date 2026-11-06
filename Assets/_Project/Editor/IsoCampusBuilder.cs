using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Reemplaza el greybox del Campus por los sprites reales sobre un suelo isométrico, EN SU LUGAR:
/// conserva las puertas, los puntos de aparición, los NPC, los objetos de retos y el trigger del Ágora (solo los recoloca).
/// No es un menú "Construir": se llama desde código una sola vez.
/// </summary>
public static class IsoCampusBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Campus.unity";
    const float U = SpriteSetup.Ppu;

    // Rectángulo del campus (mundo, en unidades). Cruzarlo de oeste a este: ~190 u = ~38 s a 5 u/s.
    const float XMin = -92f, XMax = 100f, YMin = -52f, YMax = 50f;
    const float WallThickness = 2f;

    class Bld { public string key; public float W, H; public Vector2 root; public Rect extent; }

    static Scene scene;
    static Grid grid;
    static Tilemap tilemap;
    static Tile[] pathTiles;
    static readonly Dictionary<string, Bld> blds = new Dictionary<string, Bld>();
    static readonly HashSet<Vector3Int> paved = new HashSet<Vector3Int>();
    static readonly List<Rect> noTree = new List<Rect>();
    static System.Random rnd;

    static GameObject Find(string name) => scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

    static Vector2 WorldOf(Bld b, float px, float py) => b.root + new Vector2((px - b.W / 2f) / U, (b.H - py) / U);

    // ───────────────────────── Entrada principal ─────────────────────────

    public static string Rebuild()
    {
        rnd = new System.Random(2024);
        blds.Clear(); paved.Clear(); noTree.Clear();
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var log = new List<string>();
        log.Add($"borrados: {RemoveGreybox()}");

        // Suelo
        BuildGround();

        // Bordes + MapBounds (conservan sus nombres y colisión)
        PlaceBorders();

        // Edificios y decorados con sprite
        PlaceBuildings();

        // Puertas, spawns, NPC, retos, trigger
        PlaceDoorsAndSpawns();
        PlaceStoryObjects();

        // Caminos sobre el tilemap isométrico y vegetación
        BuildPaths();
        log.Add($"celdas de camino: {paved.Count}");
        log.Add($"árboles: {PlaceTrees()}");

        // Andrés al punto de aparición por defecto
        var def = Find("Spawn_Campus_Default");
        var player = Find("Andres");
        if (def != null && player != null) player.transform.position = def.transform.position;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return string.Join(" | ", log);
    }

    // ───────────────────────── Limpieza del greybox ─────────────────────────

    static readonly HashSet<string> RemoveNames = new HashSet<string>
    {
        "Zonas verdes del campus", "Plaza de la portería oeste", "Parqueadero sur", "Vía de acceso sur", "Patio verde",
        "Edificio Central - ala norte", "Edificio Central - centro (Salón Lili y Auditorio Quincha)", "Edificio Central - ala sur",
        "Domo del Edificio Central", "Borde del Ágora", "Ágora", "Palmas del Ágora",
        "Aulas 1", "Aulas 2", "Aulas 3", "Aulas 4", "Museo Lili", "Cafetería UAO", "Bienestar Universitario",
        // Lo que crea este mismo constructor (así se puede volver a correr sin duplicar)
        "Pasto (suelo)", "Grid isométrico", "Edificio Central (ARCO UAO)", "Cafetería y Bienestar (edificio en L)", "Vegetación (Outside)",
    };

    static int RemoveGreybox()
    {
        int n = 0;
        foreach (var g in scene.GetRootGameObjects())
        {
            if (RemoveNames.Contains(g.name) || g.name.StartsWith("Camino"))
            {
                Object.DestroyImmediate(g);
                n++;
            }
        }
        return n;
    }

    // ───────────────────────── Suelo: pasto + tilemap isométrico ─────────────────────────

    static void BuildGround()
    {
        var grass = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteSetup.GrassPath);
        var g = new GameObject("Pasto (suelo)");
        g.transform.position = new Vector3((XMin + XMax) / 2f, (YMin + YMax) / 2f, 0);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = grass;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(XMax - XMin + 2 * WallThickness, YMax - YMin + 2 * WallThickness);
        sr.sortingOrder = -100;

        // Tiles isométricos 2:1 de TILE SET: celdas de ~5 x 2,5 u
        var sample = AssetDatabase.LoadAllAssetsAtPath($"{SpriteSetup.Esc}/TILE SET.png").OfType<Sprite>().First();
        float cw = sample.rect.width / U;
        var gridGo = new GameObject("Grid isométrico");
        grid = gridGo.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(cw, cw / 2f, 1f);

        var tmGo = new GameObject("Suelo isométrico (caminos)");
        tmGo.transform.SetParent(gridGo.transform, false);
        tilemap = tmGo.AddComponent<Tilemap>();
        var tr = tmGo.AddComponent<TilemapRenderer>();
        tr.sortingOrder = -80;

        pathTiles = new Tile[12];
        int k = 0;
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 3; c++)
                pathTiles[k++] = AssetDatabase.LoadAssetAtPath<Tile>($"{SpriteSetup.TilesDir}/TILE_{r}_{c}.asset");   // rombos de camino (cruz de ladrillo)
    }

    // ───────────────────────── Bordes y límites ─────────────────────────

    static void SetBox(GameObject go, Vector2 pos, Vector2 size)
    {
        if (go == null) return;
        go.transform.position = pos;
        if (go.TryGetComponent(out SpriteRenderer sr) && sr.drawMode != SpriteDrawMode.Simple) sr.size = size;
        if (go.TryGetComponent(out BoxCollider2D box)) box.size = size;
    }

    static void PlaceBorders()
    {
        float t = WallThickness, w = XMax - XMin, h = YMax - YMin, cx = (XMin + XMax) / 2f, cy = (YMin + YMax) / 2f;
        SetBox(Find("Carrera 115 (borde norte)"), new Vector2(cx, YMax + t / 2), new Vector2(w + 2 * t, t));
        SetBox(Find("Carrera 116 (borde sur)"), new Vector2(cx, YMin - t / 2), new Vector2(w + 2 * t, t));
        SetBox(Find("Autopista Cali-Jamundí (borde oeste)"), new Vector2(XMin - t / 2, cy), new Vector2(t, h));
        SetBox(Find("Calle 42 (borde este)"), new Vector2(XMax + t / 2, cy), new Vector2(t, h));

        var mb = Find("MapBounds");
        if (mb != null)
        {
            mb.transform.position = new Vector3(cx, cy, 0);
            mb.GetComponent<BoxCollider2D>().size = new Vector2(w + 2 * t, h + 2 * t);
        }

        // Parqueadero norte (greybox gris oscuro) y su vía: se conservan, más compactos
        var park = Find("Parqueadero norte");
        if (park != null)
        {
            park.transform.position = new Vector3(-28f, 32f, 0);
            park.transform.localScale = new Vector3(52f / 8f, 20f / 8f, 1f);
            park.GetComponent<SpriteRenderer>().sortingOrder = -70;
            noTree.Add(new Rect(-56f, 20f, 56f, 24f));
        }
        var via = Find("Vía de acceso norte");
        if (via != null)
        {
            SetBox(via, new Vector2(-28f, 46.5f), new Vector2(3f, 7f));
            via.GetComponent<SpriteRenderer>().sortingOrder = -70;
        }
    }

    // ───────────────────────── Edificios ─────────────────────────

    // Huella en el suelo (px desde arriba-izquierda): solo la base del edificio, para poder pasar por detrás.
    static readonly Dictionary<string, Vector2[][]> Footprints = new Dictionary<string, Vector2[][]>
    {
        ["Aulas"] = new[] { new[] {
            new Vector2(150, 870), new Vector2(430, 885), new Vector2(700, 865), new Vector2(870, 812), new Vector2(1390, 578),
            new Vector2(1390, 330), new Vector2(870, 565), new Vector2(700, 615), new Vector2(430, 635), new Vector2(150, 620) } },
        ["CAF y BIENESTAR"] = new[] { new[] {
            new Vector2(300, 790), new Vector2(440, 890), new Vector2(1330, 890), new Vector2(1410, 780),
            new Vector2(1410, 600), new Vector2(1330, 640), new Vector2(440, 640), new Vector2(300, 590) } },
        // Edificio Central (vista cenital): alas norte y sur, bloque central y domo
        ["ARCO UAO"] = new[]
        {
            new[] { new Vector2(285, 85), new Vector2(1090, 85), new Vector2(1090, 375), new Vector2(285, 375) },
            new[] { new Vector2(540, 200), new Vector2(730, 200), new Vector2(730, 660), new Vector2(540, 660) },
            new[] { new Vector2(665, 285), new Vector2(1150, 285), new Vector2(1150, 650), new Vector2(665, 650) },
            new[] { new Vector2(255, 610), new Vector2(1100, 610), new Vector2(1100, 985), new Vector2(255, 985) },
        },
    };

    static Bld Place(string key, string goName, Vector2 anchorPx, Vector2 anchorWorld, int order, string footprintKey)
    {
        var path = $"{SpriteSetup.Esc}/{key}.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        var slices = SpriteSetup.Buildings[key];
        var tex = sprites.Values.First().texture;
        var b = new Bld { key = key, W = tex.width, H = tex.height };
        b.root = anchorWorld - new Vector2((anchorPx.x - b.W / 2f) / U, (b.H - anchorPx.y) / U);
        b.extent = new Rect(b.root.x - b.W / 2f / U, b.root.y, b.W / U, b.H / U);

        var root = new GameObject(goName);
        root.transform.position = b.root;
        foreach (var s in slices)
        {
            var child = new GameObject(s.name);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3((s.pivotPx.x - b.W / 2f) / U, (b.H - s.pivotPx.y) / U, 0);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[s.name];
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        if (footprintKey != null && Footprints.TryGetValue(footprintKey, out var paths))
        {
            var col = root.AddComponent<PolygonCollider2D>();
            col.pathCount = paths.Length;
            for (int i = 0; i < paths.Length; i++)
                col.SetPath(i, paths[i].Select(p => new Vector2((p.x - b.W / 2f) / U, (b.H - p.y) / U)).ToArray());
        }

        var ex = b.extent;
        noTree.Add(new Rect(ex.x - 1.5f, ex.y - 1.5f, ex.width + 3f, ex.height + 3f));
        blds[goName] = b;
        return b;
    }

    static void PlaceBuildings()
    {
        // Distribución según mapa_uao_2.png: Edificio Central y Ágora al oeste, las 4 aulas en diagonal hacia el noreste,
        // Cafetería y Bienestar (edificio en L) al sureste.
        var arch = new Vector2(730, 540);
        Place("ARCO UAO", "Edificio Central (ARCO UAO)", arch, new Vector2(-52f, -6f), -60, "ARCO UAO");
        Place("Agora", "Ágora", new Vector2(805, 640), new Vector2(-12f, -6f), -40, null);

        var aulas = new Vector2(716, 536);
        Place("Aulas1", "Aulas 1", aulas, new Vector2(4f, -36f), 0, "Aulas");
        Place("Aulas2", "Aulas 2", aulas, new Vector2(24f, -15f), 0, "Aulas");
        Place("Aulas3", "Aulas 3", aulas, new Vector2(44f, 7f), 0, "Aulas");
        Place("Aulas4", "Aulas 4", aulas, new Vector2(64f, 27f), 0, "Aulas");
        Place("CAF y BIENESTAR", "Cafetería y Bienestar (edificio en L)", new Vector2(780, 500), new Vector2(68f, -22f), 0, "CAF y BIENESTAR");

        // Troncos de las palmas del Ágora: colisión solo en la base
        var ag = blds["Ágora"];
        var root = Find("Ágora");
        foreach (var t in new[] { new Vector2(335, 545), new Vector2(320, 750), new Vector2(520, 450), new Vector2(765, 395), new Vector2(1050, 465), new Vector2(1280, 670) })
        {
            var go = new GameObject("Tronco palma");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3((t.x - ag.W / 2f) / U, (ag.H - t.y) / U, 0);
            var cc = go.AddComponent<CircleCollider2D>();
            cc.radius = 0.4f;
        }
    }

    // ───────────────────────── Puertas y puntos de aparición ─────────────────────────

    static void Door(string doorName, string spawnName, Bld b, Vector2 doorPx, Vector2 spawnOffsetPx, bool vertical = false)
    {
        var d = Find(doorName);
        if (d == null) { Debug.LogWarning("Falta la puerta " + doorName); return; }
        var pos = WorldOf(b, doorPx.x, doorPx.y);
        d.transform.position = pos;
        if (d.TryGetComponent(out BoxCollider2D col)) col.size = vertical ? new Vector2(1.5f, 3f) : new Vector2(3f, 1.5f);
        var sp = Find(spawnName);
        if (sp != null) sp.transform.position = pos + new Vector2(spawnOffsetPx.x / U, -spawnOffsetPx.y / U);
    }

    static void Gate(string name, Vector2 pos, bool vertical)
    {
        var d = Find(name);
        if (d == null) return;
        d.transform.position = pos;
        if (d.TryGetComponent(out BoxCollider2D col)) col.size = vertical ? new Vector2(1.5f, 3f) : new Vector2(3f, 1.5f);
    }

    static void PlaceDoorsAndSpawns()
    {
        var ec = blds["Edificio Central (ARCO UAO)"];
        // Entradas visibles (px del sprite cenital); las laterales miran al oeste/este
        Door("Puerta Recepción e información", "Spawn_Campus_EC_Recepcion", ec, new Vector2(330, 392), new Vector2(0, 105));
        Door("Puerta bloque D2", "Spawn_Campus_EC_D2", ec, new Vector2(1110, 260), new Vector2(110, 0), true);
        Door("Puerta Salón Lili", "Spawn_Campus_EC_SalonLili", ec, new Vector2(525, 445), new Vector2(-105, 0), true);
        Door("Puerta Auditorio Quincha", "Spawn_Campus_EC_Quincha", ec, new Vector2(532, 590), new Vector2(-105, 0), true);
        Door("Entrada al Domo", "Spawn_Campus_EC_Domo", ec, new Vector2(1262, 450), new Vector2(105, 0), true);
        Door("Puerta Biblioteca", "Spawn_Campus_EC_Biblioteca", ec, new Vector2(520, 1010), new Vector2(0, 105));
        Door("Puerta Auditorio Yquinde", "Spawn_Campus_EC_Yquinde", ec, new Vector2(920, 1010), new Vector2(0, 105));

        var arch = new Vector2(255, 830);   // arco de la torre redonda (oeste del edificio)
        var wing = new Vector2(1000, 745);  // pie del ala larga (este)
        var down = new Vector2(0, 105);
        var a1 = blds["Aulas 1"];
        Door("Puerta Museo Lili", "Spawn_Campus_MuseoLili", a1, arch, down);
        Door("Puerta Aulas 1 (A)", "Spawn_Campus_Aulas1_A", a1, new Vector2(520, 872), down);   // base de la torre de vidrio
        Door("Puerta Aulas 1 (B)", "Spawn_Campus_Aulas1_B", a1, wing, down);
        for (int n = 2; n <= 4; n++)
        {
            var b = blds["Aulas " + n];
            Door($"Puerta Aulas {n} (A)", $"Spawn_Campus_Aulas{n}_A", b, arch, down);
            Door($"Puerta Aulas {n} (B)", $"Spawn_Campus_Aulas{n}_B", b, wing, down);
        }

        var caf = blds["Cafetería y Bienestar (edificio en L)"];
        Door("Puerta a Bienestar", "Spawn_Campus_FromBienestar", caf, new Vector2(883, 880), down);
        Door("Puerta Cafetería", "Spawn_Campus_Cafeteria", caf, new Vector2(480, 880), down);

        // Porterías y entradas sin interior (siguen con "Coming soon")
        Gate("Portería peatonal oeste", new Vector2(XMin + 1.6f, -6f), true);
        Gate("Portería vehicular norte", new Vector2(-28f, YMax - 0.8f), false);
        Gate("Portería vehicular sur", new Vector2(-30f, YMin + 0.8f), false);
        Gate("Portería peatonal este", new Vector2(XMax - 1.6f, 14f), true);
        Gate("Entrada a sótanos", new Vector2(-34f, -25f), false);
        Gate("Entrada a almacén", new Vector2(32f, YMin + 3.5f), false);
        var sot = Find("Spawn_Campus_Sotanos");
        if (sot != null) sot.transform.position = new Vector3(-34f, -22.6f, 0);   // sobre el camino sur, frente a la rampa
    }

    // ───────────────────────── NPC, retos, trigger ─────────────────────────

    static void MoveTo(string name, Vector2 pos)
    {
        var g = Find(name);
        if (g != null) g.transform.position = pos;
        else Debug.LogWarning("Falta " + name);
    }

    static void PlaceStoryObjects()
    {
        // Ágora (centro de la plaza) y escena del accidente
        MoveTo("Disparador del Ágora", new Vector2(-12f, -6f));
        var trig = Find("Disparador del Ágora");
        if (trig != null && trig.TryGetComponent(out CircleCollider2D cc)) cc.radius = 6f;
        MoveTo("Spawn_Campus_Agora", new Vector2(-12f, -15f));   // fuera del círculo para no disparar la escena
        MoveTo("Palmas del Ágora (Question 12)", new Vector2(-8f, -2.8f));

        // Portería este: Don Jairo y el candado (pregunta 5)
        MoveTo("Don Jairo", new Vector2(XMax - 5.5f, 11.2f));
        MoveTo("Candado (Question 5)", new Vector2(XMax - 5f, 16.5f));

        // Aulas 1: canecas (pregunta 7) con Valentina
        MoveTo("Canecas (Question 7)", new Vector2(26.5f, -40f));
        MoveTo("Valentina (canecas)", new Vector2(29.5f, -38.5f));

        MoveTo("Spawn_Campus_Default", new Vector2(XMin + 4f, -6f));
    }

    // ───────────────────────── Caminos (tilemap isométrico) ─────────────────────────

    static Vector2 SpawnPos(string name) => Find(name).transform.position;

    static void Paint(Vector3Int cell)
    {
        // Nada fuera de los muros del campus
        var c = grid.GetCellCenterWorld(cell);
        if (c.x < XMin + 2.5f || c.x > XMax - 2.5f || c.y < YMin + 1.3f || c.y > YMax - 1.3f) return;
        if (!paved.Add(cell)) return;
        tilemap.SetTile(cell, pathTiles[rnd.Next(pathTiles.Length)]);
    }

    /// <summary>Camino en "L" sobre los ejes del rombo (se ve como dos diagonales).</summary>
    static void Connect(Vector2 a, Vector2 b)
    {
        var ca = grid.WorldToCell(new Vector3(a.x, a.y, 0));
        var cb = grid.WorldToCell(new Vector3(b.x, b.y, 0));
        int i = ca.x, j = ca.y;
        Paint(new Vector3Int(i, j, 0));
        while (i != cb.x) { i += (int)Mathf.Sign(cb.x - i); Paint(new Vector3Int(i, j, 0)); }
        while (j != cb.y) { j += (int)Mathf.Sign(cb.y - j); Paint(new Vector3Int(i, j, 0)); }
    }

    static void BuildPaths()
    {
        var gw = new Vector2(XMin + 4f, -6f);
        var n1 = new Vector2(XMin + 4f, 13f);
        var n2 = new Vector2(-24f, 13f);
        var s1 = new Vector2(XMin + 4f, -22f);
        var s2 = new Vector2(-24f, -22f);
        Vector2 agN = new Vector2(-12f, -2f), agS = new Vector2(-12f, -10f), agW = new Vector2(-19.5f, -6f), agE = new Vector2(-4.5f, -6f);

        // Anillo alrededor del Edificio Central y acceso al Ágora
        Connect(gw, n1); Connect(n1, n2); Connect(n2, agN);
        Connect(gw, s1); Connect(s1, s2); Connect(s2, agS);

        // Entradas del Edificio Central
        var rec = SpawnPos("Spawn_Campus_EC_Recepcion");
        Connect(rec, new Vector2(rec.x, 13f));
        var d2 = SpawnPos("Spawn_Campus_EC_D2");
        Connect(d2, new Vector2(d2.x, 13f));
        Connect(SpawnPos("Spawn_Campus_EC_SalonLili"), gw);
        Connect(SpawnPos("Spawn_Campus_EC_Quincha"), gw);
        Connect(SpawnPos("Spawn_Campus_EC_Domo"), agW);
        var bib = SpawnPos("Spawn_Campus_EC_Biblioteca");
        Connect(bib, new Vector2(bib.x, -22f));
        var yq = SpawnPos("Spawn_Campus_EC_Yquinde");
        Connect(yq, new Vector2(yq.x, -22f));
        Connect(SpawnPos("Spawn_Campus_Sotanos"), new Vector2(-34f, -22f));

        // Cadena de aulas hacia el noreste
        Connect(agE, SpawnPos("Spawn_Campus_Aulas2_A"));
        Connect(SpawnPos("Spawn_Campus_Aulas2_A"), SpawnPos("Spawn_Campus_Aulas3_A"));
        Connect(SpawnPos("Spawn_Campus_Aulas3_A"), SpawnPos("Spawn_Campus_Aulas4_A"));
        Connect(agS, SpawnPos("Spawn_Campus_MuseoLili"));
        Connect(SpawnPos("Spawn_Campus_MuseoLili"), SpawnPos("Spawn_Campus_Aulas1_A"));
        Connect(SpawnPos("Spawn_Campus_Aulas1_A"), SpawnPos("Spawn_Campus_Aulas1_B"));
        Connect(SpawnPos("Spawn_Campus_Aulas1_B"), SpawnPos("Spawn_Campus_Aulas2_B"));
        Connect(SpawnPos("Spawn_Campus_Aulas2_B"), SpawnPos("Spawn_Campus_Aulas3_B"));
        Connect(SpawnPos("Spawn_Campus_Aulas3_B"), SpawnPos("Spawn_Campus_Aulas4_B"));

        // Cafetería, Bienestar y portería este
        var bien = SpawnPos("Spawn_Campus_FromBienestar");
        Connect(SpawnPos("Spawn_Campus_Aulas1_B"), bien);
        Connect(bien, SpawnPos("Spawn_Campus_Cafeteria"));
        Connect(bien, new Vector2(XMax - 8f, 14f));

        // Almacén y porterías vehiculares
        Connect(new Vector2(32f, YMin + 5.5f), SpawnPos("Spawn_Campus_Aulas1_B"));
        Connect(new Vector2(-28f, YMax - 2.5f), new Vector2(-28f, 13f));
        Connect(new Vector2(-30f, YMin + 2.5f), new Vector2(-30f, -22f));

        foreach (var c in paved)
        {
            var p = grid.GetCellCenterWorld(c);
            noTree.Add(new Rect(p.x - 3.5f, p.y - 2.4f, 7f, 4.8f));
        }
    }

    // ───────────────────────── Árboles y vegetación ─────────────────────────

    static int PlaceTrees()
    {
        var outside = AssetDatabase.LoadAllAssetsAtPath($"{SpriteSetup.Esc}/Outside.png").OfType<Sprite>().ToDictionary(s => s.name);
        var trees = new[] { "tree_green_a", "tree_pink", "tree_olive", "tree_green_b" }.Where(outside.ContainsKey).Select(n => outside[n]).ToArray();
        var plants = new[] { "bush_small", "vine_a", "bush_purple", "flower" }.Where(outside.ContainsKey).Select(n => outside[n]).ToArray();

        // Zonas libres alrededor de puertas, spawns, NPC y retos
        var points = new List<Vector2>();
        foreach (var g in scene.GetRootGameObjects())
            if (g.GetComponent<Door>() != null || g.GetComponent<SpawnPoint>() != null || g.GetComponent<ToastInteractable>() != null
                || g.GetComponent<ChallengeInteractable>() != null)
                points.Add(g.transform.position);

        var holder = new GameObject("Vegetación (Outside)");
        var placed = new List<Vector2>();
        int count = 0;
        for (int attempt = 0; attempt < 6000 && count < 95; attempt++)
        {
            var p = new Vector2(Mathf.Lerp(XMin + 3f, XMax - 3f, (float)rnd.NextDouble()), Mathf.Lerp(YMin + 2f, YMax - 2f, (float)rnd.NextDouble()));
            if (noTree.Any(r => r.Contains(p))) continue;
            if (points.Any(q => (q - p).sqrMagnitude < 16f)) continue;
            if (placed.Any(q => (q - p).sqrMagnitude < 42f)) continue;

            bool tree = trees.Length > 0 && rnd.NextDouble() < 0.75;
            var sprite = tree ? trees[rnd.Next(trees.Length)] : plants[rnd.Next(plants.Length)];
            var go = new GameObject(tree ? "Árbol" : "Planta");
            go.transform.SetParent(holder.transform, false);
            go.transform.position = p;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (tree)
            {
                var cc = go.AddComponent<CircleCollider2D>();   // colisión solo en el tronco
                cc.radius = 0.4f;
                cc.offset = new Vector2(0f, 0.35f);
            }
            placed.Add(p);
            count++;
        }
        return count;
    }
}
