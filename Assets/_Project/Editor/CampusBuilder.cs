using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye el campus completo de la UAO en la escena Campus a partir del plano Docs/mapa_uao.jpg.
/// Todo se coloca en "píxeles del plano" y se convierte con ESCALA unidades por píxel, así que se ve igual que la imagen.
/// Menú: DigitatonGame ▸ Construir campus completo (reconstruye la escena Campus completa).
/// </summary>
public static class CampusBuilder
{
    const string Root = "Assets/_Project";
    const string GameManagerPrefabPath = Root + "/Resources/GameManager.prefab";

    // Plano de referencia: 1024x572 px. Campus = rectángulo (40,10)-(990,435).
    // Con 0,25 u/px el campus mide ~238 x 106 unidades: cruzarlo a 5 u/s toma ~48 s.
    const float S = 0.25f;
    const float CX = 515f, CY = 222.5f;
    const float WallThickness = 2f;
    const float BaseDepth = 2f;      // alto (en unidades) de la colisión en la base de edificios y palmas

    const float MapLeft = 40f, MapTop = 10f, MapRight = 990f, MapBottom = 435f;

    static Sprite square, squareBase, circle, circleBase;

    static Vector2 W(float px, float py) => new Vector2((px - CX) * S, (CY - py) * S);

    // ───────────────────────── Menú ─────────────────────────

    [MenuItem("DigitatonGame/Construir campus completo")]
    public static void BuildCampus()
    {
        EnsureSprites();
        EnsureToast();

        var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Campus.unity", OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

        GreyboxBuilder.AddGlobalLight();

        var inner = new Vector2((MapRight - MapLeft) * S, (MapBottom - MapTop) * S);
        GreyboxBuilder.AddBounds(inner + Vector2.one * (WallThickness * 2f));

        BuildGround();
        BuildBorders(inner);
        BuildPaths();
        BuildParking();
        BuildCentralBuilding();
        BuildAgora();
        BuildClassrooms();
        BuildWellbeingAndCafeteria();
        BuildGatesAndEntrances();
        BuildEastCourtyard();
        BuildStoryObjects();

        // Puntos de aparición: el de por defecto en la portería oeste; el de Bienestar frente a su puerta (se conserva el id).
        var def = W(75f, 296f);
        MakeSpawn("Campus_Default", def, true);
        MakeSpawn("Campus_Agora", W(410f, 250f), false);
        MakeSpawn("Campus_FromBienestar", BienestarSpawn, false);

        GreyboxBuilder.AddPlayerAndCamera(def);

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Campus.unity");
        GreyboxBuilder.RegisterScenesInBuild();
        Debug.Log("Campus construido.");
    }

    // ───────────────────────── Sprites ─────────────────────────

    public static void EnsureSprites()
    {
        square = MakeSprite("Square", false, SpriteAlignment.Center);
        squareBase = MakeSprite("SquareBase", false, SpriteAlignment.BottomCenter);
        circle = MakeSprite("Circle", true, SpriteAlignment.Center);
        circleBase = MakeSprite("CircleBase", true, SpriteAlignment.BottomCenter);
    }

    static Sprite MakeSprite(string name, bool round, SpriteAlignment align)
    {
        var path = $"{Root}/Art/Placeholders/{name}.png";
        if (!File.Exists(path))
        {
            int n = round ? 128 : 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float r = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool inside = !round || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) <= r;
                    px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)align;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ───────────────────────── Toast "Coming soon" en el prefab del GameManager ─────────────────────────

    public static void EnsureToast()
    {
        if (!File.Exists(GameManagerPrefabPath)) return;
        var root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);
        if (root.GetComponentInChildren<ToastMessage>(true) == null)
        {
            var canvasGo = MenuFlowBuilder.MakeCanvas("ToastCanvas", 600, raycaster: false);
            canvasGo.transform.SetParent(root.transform, false);
            var group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var panel = MenuFlowBuilder.NewImage("Panel", canvasGo.transform, new Color(0f, 0f, 0f, 0.8f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(620, 80);
            prt.anchoredPosition = new Vector2(0, 150);
            panel.raycastTarget = false;

            var label = MenuFlowBuilder.NewText("Label", panel.transform, "Coming soon", 36, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuFlowBuilder.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16, 6);
            label.rectTransform.offsetMax = new Vector2(-16, -6);

            var toast = canvasGo.AddComponent<ToastMessage>();
            toast.label = label;
            toast.group = group;
            group.alpha = 0f;
        }
        PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ───────────────────────── Helpers de objetos ─────────────────────────

    static void Tag(GameObject go, PlaceholderKind kind, string name, string description, Vector2 sizeUnits,
        bool label = true, float font = 6f, bool useKindColor = true)
    {
        var info = go.AddComponent<PlaceholderInfo>();
        info.areaName = name;
        info.kind = kind;
        info.finalAssetDescription = description;
        info.suggestedSizePx = new Vector2Int(Mathf.RoundToInt(sizeUnits.x * 16f), Mathf.RoundToInt(sizeUnits.y * 16f));
        info.owner = PlaceholderOwner.Integrante1;
        info.showLabel = label;
        info.labelFontSize = font;
        info.applyKindColor = useKindColor;
    }

    static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>Rectángulo plano (suelo, camino, parqueadero): sin colisión. Coordenadas del plano en px.</summary>
    static GameObject Ground(string name, PlaceholderKind kind, float x0, float y0, float x1, float y1, int order,
        string description, bool label = false, float font = 6f)
    {
        var go = new GameObject(name);
        go.transform.position = W((x0 + x1) / 2f, (y0 + y1) / 2f);
        var size = new Vector2((x1 - x0) * S, (y1 - y0) * S);
        var sr = AddSprite(go, square, PlaceholderPalette.GetColor(kind), order);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        Tag(go, kind, name, description, size, label, font);
        return go;
    }

    /// <summary>Franja rotada entre dos puntos del plano (caminos diagonales).</summary>
    static GameObject PathLine(string name, float ax, float ay, float bx, float by, float widthPx, int order, string description)
    {
        var a = W(ax, ay);
        var b = W(bx, by);
        var go = new GameObject(name);
        go.transform.position = (a + b) / 2f;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        var size = new Vector2(Vector2.Distance(a, b), widthPx * S);
        var sr = AddSprite(go, square, PlaceholderPalette.GetColor(PlaceholderKind.Walkway), order);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        Tag(go, PlaceholderKind.Walkway, name, description, size, false);
        return go;
    }

    static GameObject Ellipse(string name, PlaceholderKind kind, float cxPx, float cyPx, float wPx, float hPx, int order,
        string description, bool label = true, Color? color = null)
    {
        var go = new GameObject(name);
        go.transform.position = W(cxPx, cyPx);
        go.transform.localScale = new Vector3(wPx * S / 8f, hPx * S / 8f, 1f);   // el sprite mide 8 u
        var sr = AddSprite(go, circle, color ?? PlaceholderPalette.GetColor(kind), order);
        Tag(go, kind, name, description, new Vector2(wPx * S, hPx * S), label, 6f, color == null);
        return go;
    }

    /// <summary>
    /// Edificio: el objeto está en el centro de su base (borde sur) y el sprite crece hacia el norte.
    /// Solo la franja de la base tiene colisión, así Andrés puede pasar por detrás (vista 3/4).
    /// </summary>
    static GameObject Building(string name, PlaceholderKind kind, float x0, float y0, float x1, float y1, string description,
        Color? color = null, float font = 7f)
    {
        var go = new GameObject(name);
        go.transform.position = W((x0 + x1) / 2f, y1);
        var size = new Vector2((x1 - x0) * S, (y1 - y0) * S);
        var sr = AddSprite(go, squareBase, color ?? PlaceholderPalette.GetColor(kind), 0);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(size.x, BaseDepth);
        col.offset = new Vector2(0f, BaseDepth / 2f);

        Tag(go, kind, name, description, size, true, font, color == null);
        return go;
    }

    static GameObject Wall(string name, Vector2 center, Vector2 size, string description)
    {
        var go = new GameObject(name);
        go.transform.position = center;
        var sr = AddSprite(go, square, PlaceholderPalette.GetColor(PlaceholderKind.Wall), 0);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        Tag(go, PlaceholderKind.Wall, name, description, size, true, 8f);
        return go;
    }

    /// <summary>Puerta amarilla. Si targetScene está vacío, muestra "Coming soon".</summary>
    static GameObject Door(string name, Vector2 pos, Vector2 size, string description, string targetScene = "", string targetSpawn = "",
        bool lockUntilBienestar = false)
    {
        var go = GreyboxBuilder.MakeDoor(name, pos, size, square, targetScene, targetSpawn);
        var info = go.GetComponent<PlaceholderInfo>();
        info.finalAssetDescription = description;
        info.labelFontSize = 4f;
        if (lockUntilBienestar)
        {
            // Los edificios se abren después de Bienestar (preguntas 1-4); en modo desarrollo se abren solos.
            var door = go.GetComponent<Door>();
            door.requiredFlag = GameManager.FlagBienestarDone;
            door.lockedMessage = "Locked for now.";
            door.devBypass = true;
        }
        return go;
    }

    /// <summary>
    /// Puerta frontal de un edificio: en el borde sur de su base. Si returnSpawn no es nulo, crea ese SpawnPoint
    /// frente a la puerta (a donde vuelve Andrés al salir del interior).
    /// </summary>
    static GameObject FrontDoor(string name, float xPx, float basePx, string description, string targetScene = "", string targetSpawn = "",
        string returnSpawn = null, bool locked = true)
    {
        var b = W(xPx, basePx);
        if (!string.IsNullOrEmpty(returnSpawn)) MakeSpawn(returnSpawn, new Vector2(b.x, b.y - 2.2f), false);
        return Door(name, new Vector2(b.x, b.y - 0.5f), new Vector2(3f, 1.5f), description, targetScene, targetSpawn, locked && !string.IsNullOrEmpty(targetScene));
    }

    static void MakeSpawn(string id, Vector2 pos, bool isDefault) => GreyboxBuilder.MakeSpawn(id, pos, isDefault);

    static Vector2 BienestarSpawn;

    // ───────────────────────── Secciones del campus ─────────────────────────

    static void BuildGround()
    {
        Ground("Zonas verdes del campus", PlaceholderKind.Green, MapLeft, MapTop, MapRight, MapBottom, -100,
            "Césped, jardines y árboles que rodean los edificios (tileset de pasto con variaciones y árboles sueltos).");
    }

    static void BuildBorders(Vector2 inner)
    {
        float t = WallThickness;
        var top = W(0, MapTop).y; var bottom = W(0, MapBottom).y;
        var left = W(MapLeft, 0).x; var right = W(MapRight, 0).x;
        Wall("Carrera 115 (borde norte)", new Vector2(0, top + t / 2), new Vector2(inner.x + 2 * t, t),
            "Borde norte del campus: Carrera 115. Muro/reja que no se puede atravesar.");
        Wall("Carrera 116 (borde sur)", new Vector2(0, bottom - t / 2), new Vector2(inner.x + 2 * t, t),
            "Borde sur del campus: Carrera 116. Muro/reja que no se puede atravesar.");
        Wall("Autopista Cali-Jamundí (borde oeste)", new Vector2(left - t / 2, 0), new Vector2(t, inner.y),
            "Borde oeste del campus: Autopista Cali-Jamundí. Muro/reja que no se puede atravesar.");
        Wall("Calle 42 (borde este)", new Vector2(right + t / 2, 0), new Vector2(t, inner.y),
            "Borde este del campus: Calle 42. Muro/reja que no se puede atravesar.");
    }

    static void BuildPaths()
    {
        const string d = "Camino peatonal de ladrillo/adoquín (tileset de camino).";
        Ground("Camino recto: portería oeste - Ágora", PlaceholderKind.Walkway, 40, 280, 358, 312, -90,
            "Camino peatonal recto desde la portería peatonal oeste, entre las dos alas del Edificio Central, hasta el Ágora.", true, 6f);
        Ground("Plaza de la portería oeste", PlaceholderKind.Walkway, 40, 235, 100, 312, -90, "Plazoleta de entrada junto a la portería peatonal oeste.");
        Ground("Camino norte-sur central", PlaceholderKind.Walkway, 388, 10, 404, 435, -90, d);
        Ground("Camino Ágora - sur", PlaceholderKind.Walkway, 403, 300, 417, 390, -90, d);
        PathLine("Camino diagonal Ágora - Aulas", 440, 232, 700, 60, 12, -90, "Camino diagonal que sube desde el Ágora hacia Aulas 2, 3 y 4.");
        Ground("Camino sur (frente a Aulas 1 y Bienestar)", PlaceholderKind.Walkway, 392, 387, 985, 412, -90, d);
        Ground("Camino frente a Aulas 2", PlaceholderKind.Walkway, 505, 270, 730, 284, -90, d);
        Ground("Camino frente a Aulas 3", PlaceholderKind.Walkway, 600, 185, 830, 199, -90, d);
        Ground("Camino frente a Aulas 4", PlaceholderKind.Walkway, 740, 110, 950, 124, -90, d);
        Ground("Camino este (Aulas 2 - sur)", PlaceholderKind.Walkway, 740, 250, 754, 400, -90, d);
        Ground("Camino este (Aulas 3 - Cafetería)", PlaceholderKind.Walkway, 832, 110, 844, 260, -90, d);
        Ground("Camino a la Cafetería", PlaceholderKind.Walkway, 754, 244, 832, 258, -90, d);
        Ground("Camino de la portería este", PlaceholderKind.Walkway, 754, 143, 985, 157, -90, d);
        Ground("Camino norte (frente a Calle 115)", PlaceholderKind.Walkway, 392, 14, 985, 28, -90, d);
    }

    static void BuildParking()
    {
        const string d = "Parqueadero: piso de asfalto con líneas de parqueo y algunos carros (tileset de calle/parqueadero).";
        Ellipse("Parqueadero norte", PlaceholderKind.Street, 205, 92, 290, 90, -85, d);
        Ellipse("Parqueadero sur", PlaceholderKind.Street, 220, 408, 210, 40, -85, d);
        Ground("Vía de acceso norte", PlaceholderKind.Street, 198, 10, 212, 48, -85, "Vía de acceso vehicular desde la portería norte.");
        Ground("Vía de acceso sur", PlaceholderKind.Street, 213, 426, 227, 435, -85, "Vía de acceso vehicular desde la portería sur.");
    }

    static void BuildCentralBuilding()
    {
        var north = Building("Edificio Central - ala norte", PlaceholderKind.AdminBuilding, 100, 140, 305, 205,
            "Ala norte del Edificio Central: Recepción e información y bloque D2. Fachada de ladrillo con techo verde azulado, vista 3/4.");
        FrontDoor("Puerta Recepción e información", 140, 205, "Puerta de la Recepción e información (Edificio Central, ala norte).", "EdificioCentral", "EC_Recepcion", "Campus_EC_Recepcion");
        FrontDoor("Puerta bloque D2", 217, 205, "Puerta del bloque D2 (Edificio Central, ala norte).", "EdificioCentral", "EC_D2", "Campus_EC_D2");

        Building("Edificio Central - centro (Salón Lili y Auditorio Quincha)", PlaceholderKind.AdminBuilding, 100, 217, 200, 258,
            "Centro del Edificio Central: Salón Lili y Auditorio Quincha. Fachada de ladrillo, vista 3/4.");
        FrontDoor("Puerta Salón Lili", 125, 258, "Puerta del Salón Lili.", "EdificioCentral", "EC_SalonLili", "Campus_EC_SalonLili");
        FrontDoor("Puerta Auditorio Quincha", 175, 258, "Puerta del Auditorio Quincha.", "EdificioCentral", "EC_Quincha", "Campus_EC_Quincha");

        // Domo blanco al lado este del centro
        var domePos = W(280, 275);
        var dome = new GameObject("Domo del Edificio Central");
        dome.transform.position = domePos;
        dome.transform.localScale = new Vector3(95 * S / 8f, 62 * S / 8f, 1f);
        var sr = AddSprite(dome, circleBase, Color.white, 0);
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        var col = dome.AddComponent<BoxCollider2D>();
        col.size = new Vector2(8f * 0.6f, BaseDepth / (62 * S / 8f));    // colisión solo en la base (en espacio local)
        col.offset = new Vector2(0f, col.size.y / 2f);
        Tag(dome, PlaceholderKind.AdminBuilding, "Domo (Edificio Central)",
            "Domo blanco con forma de cúpula acanalada, al lado este del centro del Edificio Central.", new Vector2(95 * S, 62 * S), true, 7f, false);
        FrontDoor("Entrada al Domo", 280, 275, "Entrada al domo.", "EdificioCentral", "EC_Domo", "Campus_EC_Domo");

        Building("Edificio Central - ala sur", PlaceholderKind.AdminBuilding, 100, 318, 305, 372,
            "Ala sur del Edificio Central: Biblioteca y Auditorio Yquinde. Fachada de ladrillo con techo verde azulado, vista 3/4.");
        FrontDoor("Puerta Biblioteca", 140, 372, "Puerta de la Biblioteca (Edificio Central, ala sur).", "EdificioCentral", "EC_Biblioteca", "Campus_EC_Biblioteca");
        FrontDoor("Puerta Auditorio Yquinde", 250, 372, "Puerta del Auditorio Yquinde (Edificio Central, ala sur).", "EdificioCentral", "EC_Yquinde", "Campus_EC_Yquinde");
    }

    static void BuildAgora()
    {
        Ellipse("Borde del Ágora", PlaceholderKind.Walkway, 410, 250, 104, 104, -80,
            "Borde de ladrillo/adoquín del Ágora.", false);
        Ellipse("Ágora", PlaceholderKind.Green, 410, 250, 84, 84, -75,
            "Plaza circular verde con borde de adoquín, al este del Edificio Central. Área clave del guion.", true);

        // 8 palmas en arco al noreste del Ágora (cilindros verde oscuro, con colisión solo en la base)
        var group = new GameObject("Palmas del Ágora");
        group.transform.position = W(410, 250 - 70);
        Tag(group, PlaceholderKind.Green, "Palmas del Ágora (8)",
            "Ocho palmas en arco al noreste del Ágora (sprite de palma, vista 3/4).", new Vector2(40, 40), true, 5f);
        var palmColor = new Color(0.04f, 0.30f, 0.10f);
        for (int i = 0; i < 8; i++)
        {
            float angle = (15f + i * (90f / 7f)) * Mathf.Deg2Rad;
            var p = W(410 + 70f * Mathf.Cos(angle), 250 - 70f * Mathf.Sin(angle));
            var go = new GameObject($"Palma {i + 1}");
            go.transform.SetParent(group.transform, true);
            go.transform.position = p;
            go.transform.localScale = Vector3.one * (3f / 8f);
            var sr = AddSprite(go, circleBase, palmColor, 0);
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.22f / (3f / 8f) * 1f;     // ~0,22 u de radio: solo el tronco
            col.offset = new Vector2(0f, col.radius);
            Tag(go, PlaceholderKind.Green, $"Palma {i + 1}", "Palma del Ágora (sprite de palma de ~48 px de alto).", new Vector2(3, 3), false, 4f, false);
        }
    }

    static void BuildClassrooms()
    {
        const string aulas = "Edificio de aulas: techo verde azulado con paneles solares, fachada de ladrillo y torre de escaleras, vista 3/4.";

        Building("Aulas 1", PlaceholderKind.ClassroomBuilding, 478, 315, 640, 395, aulas);
        FrontDoor("Puerta Aulas 1 (A)", 520, 395, "Puerta de Aulas 1.", "Aulas1", "Aulas1_A", "Campus_Aulas1_A");
        FrontDoor("Puerta Aulas 1 (B)", 600, 395, "Puerta de Aulas 1.", "Aulas1", "Aulas1_B", "Campus_Aulas1_B");
        Building("Museo Lili", PlaceholderKind.AdminBuilding, 445, 318, 478, 395,
            "Museo Lili, en el extremo oeste de Aulas 1. Fachada de ladrillo con torre, vista 3/4.");
        FrontDoor("Puerta Museo Lili", 461, 395, "Puerta del Museo Lili.", "MuseoLili", "MuseoLili_Entrance", "Campus_MuseoLili");

        Building("Aulas 2", PlaceholderKind.ClassroomBuilding, 505, 200, 730, 270, aulas);
        FrontDoor("Puerta Aulas 2 (A)", 560, 270, "Puerta de Aulas 2.", "Aulas2", "Aulas2_A", "Campus_Aulas2_A", false);
        FrontDoor("Puerta Aulas 2 (B)", 680, 270, "Puerta de Aulas 2.", "Aulas2", "Aulas2_B", "Campus_Aulas2_B", false);

        Building("Aulas 3", PlaceholderKind.ClassroomBuilding, 600, 115, 830, 185, aulas);
        FrontDoor("Puerta Aulas 3 (A)", 655, 185, "Puerta de Aulas 3.", "Aulas3", "Aulas3_A", "Campus_Aulas3_A");
        FrontDoor("Puerta Aulas 3 (B)", 775, 185, "Puerta de Aulas 3.", "Aulas3", "Aulas3_B", "Campus_Aulas3_B");

        Building("Aulas 4", PlaceholderKind.ClassroomBuilding, 740, 40, 950, 110, aulas);
        FrontDoor("Puerta Aulas 4 (A)", 795, 110, "Puerta de Aulas 4.", "Aulas4", "Aulas4_A", "Campus_Aulas4_A");
        FrontDoor("Puerta Aulas 4 (B)", 895, 110, "Puerta de Aulas 4.", "Aulas4", "Aulas4_B", "Campus_Aulas4_B");
    }

    static void BuildWellbeingAndCafeteria()
    {
        // Edificio en L (esquina sureste): Cafetería (barra vertical) + Bienestar (barra horizontal)
        Building("Cafetería UAO", PlaceholderKind.Food, 845, 195, 960, 305,
            "Cafetería UAO: barra vertical del edificio en L, techo verde azulado con paneles solares, vista 3/4.");
        var cafeDoorPos = W(842, 250);
        Door("Puerta Cafetería", cafeDoorPos, new Vector2(1.5f, 3f), "Puerta de la Cafetería UAO (lado oeste).",
            "Cafeteria", "Cafeteria_Entrance", true);
        MakeSpawn("Campus_Cafeteria", new Vector2(cafeDoorPos.x - 1.9f, cafeDoorPos.y), false);   // frente a la puerta, al oeste

        Building("Bienestar Universitario", PlaceholderKind.Health, 780, 305, 960, 385,
            "Bienestar Universitario y enfermería: barra horizontal del edificio en L, vista 3/4. Su puerta lleva a la escena Bienestar.");
        var b = W(870, 385);
        Door("Puerta a Bienestar", new Vector2(b.x, b.y - 0.5f), new Vector2(3f, 1.5f),
            "Puerta de Bienestar Universitario. Lleva a la escena Bienestar.", "Bienestar", "Bienestar_Entrance", true);
        // Aparece frente a la puerta (hacia el sur), dentro del alcance de "Press E"
        BienestarSpawn = new Vector2(b.x, b.y - 2.2f);
    }

    static void BuildGatesAndEntrances()
    {
        Door("Portería peatonal oeste", W(44, 296), new Vector2(1.5f, 3f),
            "Entrada peatonal desde la Autopista Cali-Jamundí (puerta/reja con caseta).");
        Door("Portería vehicular norte", W(205, 14), new Vector2(3f, 1.5f),
            "Entrada vehicular norte hacia el parqueadero norte (barrera y caseta).");
        Door("Portería vehicular sur", W(220, 431), new Vector2(3f, 1.5f),
            "Entrada vehicular sur hacia el parqueadero sur (barrera y caseta).");
        var sot = W(345, 350);
        Door("Entrada a sótanos", sot, new Vector2(3f, 1.5f),
            "Rampa de entrada a los sótanos, al este del Edificio Central.", "Sotanos", "Sotanos_FromCampus", true);
        MakeSpawn("Campus_Sotanos", new Vector2(sot.x, sot.y - 2.2f), false);
        Door("Entrada a almacén", W(590, 421), new Vector2(3f, 1.5f),
            "Entrada al almacén, al sur, cerca de Aulas 1.");
    }

    static void BuildEastCourtyard()
    {
        Ground("Patio verde", PlaceholderKind.Green, 880, 112, 980, 190, -95,
            "Patio verde con árboles y bancas junto a la portería peatonal este.", true);

        Door("Portería peatonal este", W(986, 150), new Vector2(1.5f, 3f),
            "Entrada peatonal sobre la Calle 42 (puerta/reja con caseta).");

        // Don Jairo (NPC rosado): sólido y con diálogo corto
        var jairo = new GameObject("Don Jairo");
        jairo.transform.position = W(966, 168);
        var sr = AddSprite(jairo, square, PlaceholderPalette.GetColor(PlaceholderKind.NPC), 0);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(1f, 2f);
        var col = jairo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);
        col.offset = new Vector2(0f, -0.5f);
        var toast = jairo.AddComponent<ToastInteractable>();
        toast.message = "Don Jairo: Welcome to UAO!";
        Tag(jairo, PlaceholderKind.NPC, "Don Jairo",
            "Sprite de Don Jairo, el portero de la entrada este (NPC, vista 3/4, 16x32 px).", new Vector2(1, 2), true, 5f);
    }

    /// <summary>Objetos del guion en el campus: retos de las preguntas 5, 7 y 12.</summary>
    static void BuildStoryObjects()
    {
        // Pregunta 5: candado en la portería este, junto a Don Jairo
        InteriorsBuilder.Challenge("Candado (Question 5)", W(956, 160), new Vector2(1.2f, 1.2f), "q5", "Question 5 – Padlock", 1,
            "Candado de la portería este, junto a Don Jairo (sprite de candado, vista 3/4). Reto de la pregunta 5.");

        // Pregunta 7: canecas junto a Aulas 1, con Valentina
        InteriorsBuilder.Challenge("Canecas (Question 7)", W(653, 371), new Vector2(2f, 1.4f), "q7", "Question 7 – Trash bins", 1,
            "Canecas de reciclaje junto a Aulas 1 (sprite de canecas de colores, vista 3/4). Reto de la pregunta 7.");
        InteriorsBuilder.Npc("Valentina (canecas)", W(666, 377), "Valentina: (dialogue pending)",
            "Sprite de Valentina junto a las canecas de Aulas 1 (NPC, vista 3/4, 16x32 px).");

        // Pregunta 12: las 8 palmas del Ágora (solo se activa con 11 recuerdos)
        InteriorsBuilder.Challenge("Palmas del Ágora (Question 12)", W(436, 205), new Vector2(1.6f, 1.6f), "q12", "Question 12 – Agora palms", 1,
            "Marcador de las 8 palmas del Ágora (se activa con 11 recuerdos). Reto final de la pregunta 12.",
            required: 11, locked: "Something is still missing... (needs 11 memories)", ends: true);
    }
}
