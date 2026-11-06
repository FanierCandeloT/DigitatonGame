using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Arma la escena Tutorial con los sprites de la Profe Dalia y del libro (Art/SpriteDalia). Sin menús: se llama desde código
/// y es repetible (reimporta los sprites, regenera la extensión del salón y no pisa el texto del libro si ya existe).
/// Solo toca la escena Tutorial y sus propios assets; no modifica prefabs compartidos ni otras escenas.
/// </summary>
public static class TutorialSetup
{
    const string Dir = "Assets/_Project/Art/SpriteDalia";
    const string Scene = "Assets/_Project/Scenes/Tutorial.unity";
    const string AnimDir = Dir + "/Animacion";
    const string BookPath = "Assets/_Project/Data/TutorialBook.asset";
    const string DialoguePath = DialogueSetup.Dir + "/Dalia_Tutorial.asset";
    const string Esc = "Assets/_Project/Art/Sprites Escuela";

    // Cuadros de las tiras y pie de la Profe (en pixeles desde abajo): medidos con la transparencia de cada hoja
    const int IdleFrames = 8, IdleW = 192, IdleH = 1024;
    const float IdleFeet = 337f, IdleBodyH = 343f;
    const int PointFrames = 8, PointW = 256, PointH = 768;
    const float PointFeet = 141f, PointBodyH = 506f;
    const float DaliaHeightUnits = 2.0f;   // misma altura visible que Andrés (~2 u, 1x2 casillas) y los NPC adultos

    // ───────────── 1. Sprites ─────────────

    public static string ImportSprites()
    {
        var sb = new StringBuilder();
        Single("LibroTuto1");
        Single("LibroTutoDrch");
        Single("LibroTutoIzq");
        // Los botones traen mucho margen transparente: se recortan al botón para que midan lo que se ve
        CropOne("BotonCerrarTuto", new Rect(312, 108, 1072, 714));
        CropOne("BotonTutoDrch", new Rect(193, 95, 636, 360));
        CropOne("BotonTutoIzq", new Rect(170, 85, 1358, 730));
        var idleReport = NormalizeIdle();
        AssetDatabase.SaveAssets();

        sb.AppendLine(idleReport);
        foreach (var n in new[] { "LibroTuto1", "LibroTutoDrch", "LibroTutoIzq", "BotonCerrarTuto", "BotonTutoDrch", "BotonTutoIzq" })
        {
            var s = LoadSprites(n);
            sb.AppendLine(n + ": " + s.Length + " sprite(s), " + (s.Length > 0 ? s[0].rect.width + "x" + s[0].rect.height + " px" : "?"));
        }
        return sb.ToString();
    }

    const string IdleNorm = "DaliaIdleNorm";

    /// <summary>
    /// DaliaIdle.png NO es una cuadrícula regular: los 8 dibujos miden entre 172 y 189 px de ancho y los huecos entre ellos
    /// entre 8 y 18 px (no 192 fijos). Cortarla cada 192 px dejaba pedazos del dibujo vecino junto a la cabeza y la mano, y
    /// además el cuerpo se movía de cuadro a cuadro. Aquí se genera DaliaIdleNorm.png: cada dibujo se copia SOLO con sus
    /// propias columnas, alineado por el centro del torso, en celdas del mismo tamaño. El original no se modifica.
    /// </summary>
    public static string NormalizeIdle()
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes($"{Dir}/DaliaIdle.png"));
        var px = src.GetPixels32();
        int W = src.width, H = src.height;

        // Columnas con dibujo -> un tramo por cuadro
        var col = new int[W];
        for (int x = 0; x < W; x++)
        {
            int n = 0;
            for (int y = 0; y < H; y++) if (px[y * W + x].a > 40) n++;
            col[x] = n;
        }
        var segs = new List<Vector2Int>();
        int s = -1;
        for (int x = 0; x <= W; x++)
        {
            bool filled = x < W && col[x] > 3;
            if (filled && s < 0) s = x;
            if (!filled && s >= 0) { if (x - s > 40) segs.Add(new Vector2Int(s, x - 1)); s = -1; }
        }
        if (segs.Count != IdleFrames) throw new System.Exception($"Se esperaban {IdleFrames} dibujos y se encontraron {segs.Count}.");

        // Cada dibujo se queda con las columnas hasta la mitad del hueco con el vecino
        var from = new int[IdleFrames];
        var to = new int[IdleFrames];
        var anchor = new int[IdleFrames];
        int left = 0, right = 0;
        for (int f = 0; f < IdleFrames; f++)
        {
            from[f] = f == 0 ? 0 : (segs[f - 1].y + segs[f].x) / 2 + 1;
            to[f] = f == IdleFrames - 1 ? W - 1 : (segs[f].y + segs[f + 1].x) / 2;
            // ancla horizontal: centro del torso/cadera (sin el pelo, que cambia más de un cuadro a otro)
            double sum = 0; int count = 0;
            for (int y = 400; y <= 520; y++)
                for (int x = segs[f].x; x <= segs[f].y; x++)
                    if (px[y * W + x].a > 128) { sum += x; count++; }
            anchor[f] = Mathf.RoundToInt((float)(sum / count));
            left = Mathf.Max(left, anchor[f] - from[f]);
            right = Mathf.Max(right, to[f] - anchor[f]);
        }
        int cell = left + right + 1;   // ancho común de todas las celdas
        var dst = new Color32[cell * IdleFrames * H];
        for (int f = 0; f < IdleFrames; f++)
            for (int x = from[f]; x <= to[f]; x++)
            {
                int dx = f * cell + (left + x - anchor[f]);
                for (int y = 0; y < H; y++) dst[y * cell * IdleFrames + dx] = px[y * W + x];
            }

        var outTex = new Texture2D(cell * IdleFrames, H, TextureFormat.RGBA32, false);
        outTex.SetPixels32(dst);
        File.WriteAllBytes($"{Dir}/{IdleNorm}.png", outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        Object.DestroyImmediate(src);
        AssetDatabase.ImportAsset($"{Dir}/{IdleNorm}.png", ImportAssetOptions.ForceSynchronousImport);

        // Mismo tamaño en los 8 cuadros y pivote en los pies (X = torso, Y = suela)
        Strip(IdleNorm, IdleFrames, cell, H, new Vector2((float)left / cell, IdleFeet / H));
        return $"DaliaIdleNorm: {IdleFrames} cuadros de {cell}x{H} px (pivote x={left}, y={IdleFeet}); tramos originales: " +
               string.Join(", ", segs.Select(v => (v.y - v.x + 1) + " px"));
    }

    static TextureImporter Configure(string name, SpriteImportMode mode)
    {
        var path = $"{Dir}/{name}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = mode;
        imp.spritePixelsPerUnit = 48f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 2048;
        return imp;
    }

    static void Single(string name)
    {
        var imp = Configure(name, SpriteImportMode.Single);
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    static void CropOne(string name, Rect rect) => Slice(name, new List<SpriteRect> { Rect(name + "_0", rect, new Vector2(0.5f, 0.5f)) });

    static void Strip(string name, int frames, int w, int h, Vector2 pivot)
    {
        var list = new List<SpriteRect>();
        for (int i = 0; i < frames; i++) list.Add(Rect($"{name}_{i}", new Rect(i * w, 0, w, h), pivot));
        Slice(name, list);
    }

    static SpriteRect Rect(string name, Rect r, Vector2 pivot) => new SpriteRect
    {
        name = name,
        rect = r,
        alignment = SpriteAlignment.Custom,
        pivot = pivot,
        spriteID = GUID.Generate(),
    };

    static void Slice(string name, List<SpriteRect> rects)
    {
        var imp = Configure(name, SpriteImportMode.Multiple);
        imp.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());
        provider.Apply();
        imp.SaveAndReimport();
    }

    static Sprite[] LoadSprites(string name) =>
        AssetDatabase.LoadAllAssetsAtPath($"{Dir}/{name}.png").OfType<Sprite>().OrderBy(s => s.name, new NaturalCompare()).ToArray();

    class NaturalCompare : IComparer<string>
    {
        public int Compare(string a, string b)
        {
            int ia = a.LastIndexOf('_'), ib = b.LastIndexOf('_');
            if (ia >= 0 && ib >= 0 && a.Substring(0, ia) == b.Substring(0, ib)
                && int.TryParse(a.Substring(ia + 1), out var na) && int.TryParse(b.Substring(ib + 1), out var nb)) return na.CompareTo(nb);
            return string.CompareOrdinal(a, b);
        }
    }

    // ───────────── 2. Assets: diálogo, libro y animación ─────────────

    public static string CreateAssets()
    {
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data")) AssetDatabase.CreateFolder("Assets/_Project", "Data");
        if (!AssetDatabase.IsValidFolder(DialogueSetup.Dir)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogues");

        // Diálogo de Dalia (no pisa el texto si ya existe)
        var dialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DialoguePath);
        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<DialogueAsset>();
            dialogue.lines = new[]
            {
                new DialogueLine("Professor Dalia", "Welcome to UAO! I am Professor Dalia."),
                new DialogueLine("Professor Dalia", "Here, everyone speaks in the Passive Voice. Don't worry, it is learned step by step."),
                new DialogueLine("Professor Dalia", "This book was written for you. Read it carefully!"),
            };
            AssetDatabase.CreateAsset(dialogue, DialoguePath);
            log.Add("Dalia_Tutorial creado");
        }

        // Libro: el texto no se pisa si ya existe; los sprites sí se vuelven a enlazar
        var book = AssetDatabase.LoadAssetAtPath<TutorialBookAsset>(BookPath);
        if (book == null)
        {
            book = ScriptableObject.CreateInstance<TutorialBookAsset>();
            book.pages = BookPages();
            AssetDatabase.CreateAsset(book, BookPath);
            log.Add("TutorialBook creado (" + book.pages.Length + " hojas)");
        }
        book.book = LoadSprites("LibroTuto1")[0];
        book.turnNext = LoadSprites("LibroTutoDrch")[0];
        book.turnPrevious = LoadSprites("LibroTutoIzq")[0];
        book.nextButton = LoadSprites("BotonTutoDrch")[0];
        book.previousButton = LoadSprites("BotonTutoIzq")[0];
        book.closeButton = LoadSprites("BotonCerrarTuto")[0];
        EditorUtility.SetDirty(book);

        // Animación: solo DaliaIdle en loop
        if (!AssetDatabase.IsValidFolder(AnimDir)) AssetDatabase.CreateFolder(Dir, "Animacion");
        var idle = MakeClip("DaliaIdle", LoadSprites(IdleNorm), 6f, IdleBodyH);
        AssetDatabase.DeleteAsset(AnimDir + "/DaliaPoint.anim");   // Dalia solo usa DaliaIdle
        var controllerPath = AnimDir + "/Dalia.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var sIdle = machine.AddState("Idle");
        sIdle.motion = idle;
        machine.defaultState = sIdle;
        EditorUtility.SetDirty(controller);

        AssetDatabase.SaveAssets();
        log.Add("Animator Dalia.controller (solo DaliaIdle en loop) creado");
        return string.Join(" | ", log);
    }

    static void Transition(AnimatorState from, AnimatorState to, float exitTime)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = 0f;
    }

    /// <summary>Clip en loop con los cuadros y la escala que hace medir a la Profe lo mismo en las dos tiras (cada hoja trae un tamaño de dibujo distinto).</summary>
    static AnimationClip MakeClip(string name, Sprite[] frames, float fps, float bodyPixels)
    {
        var path = $"{AnimDir}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        float length = frames.Length / fps;
        float scale = DaliaHeightUnits / (bodyPixels / 48f);
        foreach (var axis in new[] { "x", "y", "z" })
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale." + axis), AnimationCurve.Constant(0f, length, scale));

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static BookPage[] BookPages() => new[]
    {
        new BookPage
        {
            leftTitle = "What is the Passive Voice?",
            leftBody = "We use it when the action is more important than who does it.",
            rightBody = "<b>Active:</b>\n\"The students recycle paper.\"\n\n<b>Passive:</b>\n\"Paper is recycled by the students.\"",
        },
        new BookPage
        {
            leftTitle = "The formula",
            leftBody = "Object + BE + past participle (+ by + who does it)",
            rightBody = "\"Solar panels are installed by engineers.\"\n\nBE changes with the tense.\nThe participle never changes.",
        },
        new BookPage
        {
            leftTitle = "Tenses (1)",
            leftBody = "<b>Present:</b> is/are + PP\n-> \"Classes are taught here.\"\n\n<b>Past:</b> was/were + PP\n-> \"The panels were installed.\"",
            rightTitle = "Tenses (2)",
            rightBody = "<b>Present continuous:</b> is/are being + PP\n\n<b>Present perfect:</b> has/have been + PP\n\n<b>Future:</b> will be + PP\n\n<b>Modals:</b> can/must be + PP",
        },
        new BookPage
        {
            leftTitle = "Active to Passive in 3 steps",
            leftBody = "1. Move the object to the front.\n2. Add BE in the same tense.\n3. Use the past participle.",
            rightBody = "\"The university recycles paper.\"\n->\n\"Paper is recycled by the university.\"",
        },
        new BookPage
        {
            leftTitle = "Common mistakes",
            leftBody = "\"The bins was emptied.\"\n-> \"were emptied\"\n\n\"Workers will paid.\"\n-> \"will be paid\"\n\n\"teached\"\n-> \"taught\"",
            rightTitle = "Ready?",
            rightBody = "You can read this again anytime in your Professor's Notebook. Press C during the game!",
        },
    };

    // ───────────── 3. Escena ─────────────

    const string ContainerName = "Salon con sprites";

    public static string BuildScene()
    {
        var log = new StringBuilder();

        // Receta de los interiores: se lee de Aulas2 (modo de dibujo del piso y de los muros)
        var ref2 = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Aulas2.unity", OpenSceneMode.Single);
        SpriteTileMode floorTile = SpriteTileMode.Continuous, wallTile = SpriteTileMode.Continuous;
        float floorThreshold = 0.5f, wallThreshold = 0.5f;
        Material lit = null;
        foreach (var g in ref2.GetRootGameObjects())
        {
            var r = g.GetComponent<SpriteRenderer>();
            if (r == null) continue;
            if (g.name == "Piso Aulas2") { floorTile = r.tileMode; floorThreshold = r.adaptiveModeThreshold; lit = r.sharedMaterial; }
            if (g.name == "Muro Norte") { wallTile = r.tileMode; wallThreshold = r.adaptiveModeThreshold; }
        }

        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        var S = new Sprites();
        // Los sprites se cargan DESPUÉS de abrir la escena (al cambiar de escena se descargan los assets sueltos)
        var floorSprite = S.Get("A5", "tile_floor_beige");
        var wallSprite = S.Get("A4", "tile_wall_beige");

        var roots = new Dictionary<string, GameObject>();
        foreach (var g in scene.GetRootGameObjects()) roots[g.name] = g;
        // repetible: los objetos que se renombran se encuentran también con su nombre nuevo
        void Alias(string nuevo, string viejo) { if (!roots.ContainsKey(viejo) && roots.ContainsKey(nuevo)) roots[viejo] = roots[nuevo]; }
        Alias("Piso del salon", "Tutorial Floor");
        Alias("Muro central izquierdo", "Muro Norte");
        foreach (var g in scene.GetRootGameObjects()) if (g.name == ContainerName) Object.DestroyImmediate(g);
        var container = new GameObject(ContainerName);

        // ── Piso y muros (los objetos existentes se reutilizan: así no se pierden sus referencias) ──
        var floor = roots["Tutorial Floor"];
        Tiled(floor, floorSprite, floorTile, floorThreshold, lit, new Vector2(0f, 3f), new Vector2(17.2f, 12.7f), -100, false);
        floor.name = "Piso del salon";

        Tiled(roots["Muro Sur"], wallSprite, wallTile, wallThreshold, lit, new Vector2(0f, -3.5f), new Vector2(17f, 0.5f), 0, true);
        Tiled(roots["Muro Oeste"], wallSprite, wallTile, wallThreshold, lit, new Vector2(-8.25f, 3f), new Vector2(0.5f, 13.5f), 0, true);
        Tiled(roots["Muro Este"], wallSprite, wallTile, wallThreshold, lit, new Vector2(8.25f, 3f), new Vector2(0.5f, 13.5f), 0, true);
        // El muro norte del salón pasa a ser el muro central, con una abertura de 3 casillas hacia la extensión
        var wallLeft = roots["Muro Norte"];
        Tiled(wallLeft, wallSprite, wallTile, wallThreshold, lit, new Vector2(-5.0f, 3.5f), new Vector2(7f, 0.5f), 0, true);
        wallLeft.name = "Muro central izquierdo";
        var wallRight = Object.Instantiate(wallLeft, container.transform);
        wallRight.name = "Muro central derecho";
        Tiled(wallRight, wallSprite, wallTile, wallThreshold, lit, new Vector2(5.0f, 3.5f), new Vector2(7f, 0.5f), 0, true);
        var wallNorth = Object.Instantiate(wallLeft, container.transform);
        wallNorth.name = "Muro norte (extension)";
        Tiled(wallNorth, wallSprite, wallTile, wallThreshold, lit, new Vector2(0f, 9.5f), new Vector2(17f, 0.5f), 0, true);
        Tiled(roots["Practice Wall"], wallSprite, wallTile, wallThreshold, lit, roots["Practice Wall"].transform.position, new Vector2(0.5f, 3.9f), 0, true);
        foreach (var n in new[] { "Muro Sur", "Muro Oeste", "Muro Este", "Practice Wall" }) StripPlaceholder(roots[n]);
        StripPlaceholder(wallLeft); StripPlaceholder(wallRight); StripPlaceholder(wallNorth);

        // ── Puerta de salida: sprite de puerta como las demás (solo después de leer el libro) ──
        var exit = roots["Exit to Main Menu"];
        var doorSr = exit.GetComponent<SpriteRenderer>();
        doorSr.sprite = S.Get("Inside_E", "door_single_window");
        doorSr.color = Color.white;
        doorSr.sortingOrder = 1;
        exit.transform.position = new Vector3(4.55f, -2.3f, 0f);
        exit.transform.localScale = new Vector3(1.5f, 1.67f, 1f);
        var exitCol = exit.GetComponent<BoxCollider2D>();
        exitCol.size = new Vector2(1f, 0.9f);
        exitCol.offset = new Vector2(0f, -0.2f);
        var door = exit.GetComponent<Door>();
        door.requiredFlag = "tutorial_book_read";
        door.lockedMessage = "Talk to Professor Dalia and read her book first.";
        door.devBypass = false;
        StripPlaceholder(exit);

        // ── Decoración del salón (los mismos sprites que Aulas2) ──
        Deco(container, S, "Inside_C", "deco_plant_a", "Planta", new Vector2(-7.5f, 2.7f), lit, 0, new Vector2(0.5f, 0.4f), new Vector2(0f, -0.35f));
        Deco(container, S, "Inside_C", "deco_plant_c", "Planta", new Vector2(7.5f, 2.2f), lit, 0, new Vector2(0.5f, 0.4f), new Vector2(0f, -0.65f));
        Deco(container, S, "Inside_C", "deco_bin_grey", "Papelera", new Vector2(-7.5f, -2.6f), lit, 0, new Vector2(0.4f, 0.3f), new Vector2(0f, -0.2f));
        Deco(container, S, "Inside_C", "deco_win_beige", "Ventana", new Vector2(-4.2f, 3.15f), lit, -50, Vector2.zero, Vector2.zero, 1.3f);
        Deco(container, S, "Inside_C", "deco_win_yellow", "Ventana", new Vector2(4.2f, 3.15f), lit, -50, Vector2.zero, Vector2.zero, 1.3f);
        Deco(container, S, "Inside_C", "deco_clock", "Reloj", new Vector2(0f, 3.1f), lit, -50, Vector2.zero, Vector2.zero);
        Deco(container, S, "Inside_D", "deco_locker_big1", "Casillero", new Vector2(0.5f, -2.4f), lit, 0, new Vector2(1.3f, 0.7f), new Vector2(0f, -0.6f));
        Deco(container, S, "Inside_D", "deco_locker_big2", "Casillero", new Vector2(2.0f, -2.4f), lit, 0, new Vector2(1.3f, 0.7f), new Vector2(0f, -0.6f));

        // ── Extensión: tablero, escritorio de la profesora y pupitres ──
        Deco(container, S, "Inside_C", "Inside_C_85", "Tablero", new Vector2(1.0f, 9.05f), lit, 1, Vector2.zero, Vector2.zero, 1f, new Vector3(1.84f, 0.6f, 1f));
        Deco(container, S, "Inside_C", "deco_win_beige", "Ventana", new Vector2(-5.4f, 8.6f), lit, -50, Vector2.zero, Vector2.zero, 1.3f);
        Deco(container, S, "Inside_C", "deco_win_yellow", "Ventana", new Vector2(6.4f, 8.6f), lit, -50, Vector2.zero, Vector2.zero, 1.3f);
        Deco(container, S, "Inside_C", "deco_plant_c", "Planta", new Vector2(-7.6f, 8.3f), lit, 0, new Vector2(0.5f, 0.4f), new Vector2(0f, -0.65f));
        Deco(container, S, "Inside_C", "deco_wardrobe", "Armario", new Vector2(7.2f, 7.4f), lit, 0, new Vector2(1.4f, 0.8f), new Vector2(0f, -0.6f));
        // Escritorio de la profesora (el mismo pupitre, más ancho)
        Deco(container, S, "Inside_C", "Inside_C_119", "Escritorio de la profesora", new Vector2(3.0f, 8.0f), lit, 0, new Vector2(1.4f, 1.0f), Vector2.zero, 1f, new Vector3(2.0f, 0.95f, 1f));
        foreach (var x in new[] { -5.2f, -3.4f })
            foreach (var y in new[] { 5.0f, 6.4f })
                Deco(container, S, "Inside_C", "Inside_C_119", "Pupitre", new Vector2(x, y), lit, 0, new Vector2(1.4f, 1.0f), Vector2.zero, 1f, new Vector3(1.2f, 0.79f, 1f));
        foreach (var x in new[] { 3.4f, 5.2f })
            foreach (var y in new[] { 5.0f, 6.4f })
                Deco(container, S, "Inside_C", "Inside_C_119", "Pupitre", new Vector2(x, y), lit, 0, new Vector2(1.4f, 1.0f), Vector2.zero, 1f, new Vector3(1.2f, 0.79f, 1f));

        // ── Profe Dalia ──
        BuildDalia(container, lit);

        // ── Límites del mapa, Andrés y spawn ──
        var bounds = roots["MapBounds"];
        bounds.transform.position = new Vector3(0f, 3f, 0f);
        bounds.GetComponent<BoxCollider2D>().size = new Vector2(17f, 13.5f);

        var spawn = roots["Spawn_Tutorial_Default"];
        var andres = roots["Andres"];
        andres.transform.localScale = new Vector3(0.9851111f, 1.1390858f, 1f);   // el de Aulas2 y el prefab (esta escena traía uno viejo, mucho más chico)
        andres.transform.position = spawn.transform.position;

        // ── Guía del tutorial ──
        var tm = Object.FindAnyObjectByType<TutorialManager>();
        tm.exitDoor = door;
        EditorUtility.SetDirty(tm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Escena Tutorial armada y guardada.");
        return log.ToString();
    }

    class Sprites
    {
        readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        public Sprite Get(string sheet, string name)
        {
            if (!cache.TryGetValue(sheet, out var all))
                cache[sheet] = all = AssetDatabase.LoadAllAssetsAtPath($"{Esc}/{sheet}.png").OfType<Sprite>().ToArray();
            var s = all.FirstOrDefault(x => x.name == name);
            if (s == null) throw new System.Exception($"No se encontró el sprite {name} en {sheet}.png");
            return s;
        }
    }

    static void Tiled(GameObject go, Sprite sprite, SpriteTileMode tileMode, float threshold, Material lit, Vector2 pos, Vector2 size, int order, bool collider)
    {
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = Vector3.one;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = tileMode;
        sr.adaptiveModeThreshold = threshold;
        sr.size = size;
        sr.color = Color.white;
        sr.sortingOrder = order;
        if (lit != null) sr.sharedMaterial = lit;
        if (collider)
        {
            var box = go.GetComponent<BoxCollider2D>();
            if (box == null) box = go.AddComponent<BoxCollider2D>();
            box.offset = Vector2.zero;
            box.size = size;
        }
    }

    static void StripPlaceholder(GameObject go)
    {
        var info = go.GetComponent<PlaceholderInfo>();
        if (info != null) Object.DestroyImmediate(info);   // ya tiene sprite real: sin rótulo del greybox
        foreach (var label in go.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(label.gameObject);
    }

    static GameObject Deco(GameObject parent, Sprites S, string sheet, string sprite, string name, Vector2 pos, Material lit, int order,
        Vector2 colliderSize, Vector2 colliderOffset, float uniformScale = 1f, Vector3? scale = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = scale ?? Vector3.one * uniformScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S.Get(sheet, sprite);
        sr.sortingOrder = order;
        if (lit != null) sr.sharedMaterial = lit;
        if (colliderSize != Vector2.zero)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = colliderSize / (scale.HasValue ? new Vector2(scale.Value.x, scale.Value.y) : Vector2.one * uniformScale);   // tamaño en el mundo
            box.offset = colliderOffset;
        }
        return go;
    }

    static void BuildDalia(GameObject parent, Material lit)
    {
        var idle = LoadSprites(IdleNorm);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimDir + "/Dalia.controller");

        // Raíz: collider SOLO en los pies + interacción. El dibujo va en un hijo, porque la animación cambia su escala.
        var root = new GameObject("Profesora Dalia");
        root.transform.SetParent(parent.transform, false);
        root.transform.position = new Vector3(-1.9f, 6.3f, 0f);   // los pies (pivote) quedan aquí

        var feet = root.AddComponent<BoxCollider2D>();
        feet.size = new Vector2(0.9f, 0.5f);
        feet.offset = new Vector2(0f, 0.25f);

        var dalia = root.AddComponent<ProfessorDalia>();
        dalia.prompt = "Press E";
        dalia.dialogue = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DialoguePath);
        dalia.book = AssetDatabase.LoadAssetAtPath<TutorialBookAsset>(BookPath);

        var visual = new GameObject("Dibujo");
        visual.transform.SetParent(root.transform, false);
        float s0 = DaliaHeightUnits / (IdleBodyH / 48f);
        visual.transform.localScale = new Vector3(s0, s0, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        sr.spriteSortPoint = SpriteSortPoint.Center;   // igual que Andrés: se ordena por la mitad del cuerpo
        if (lit != null) sr.sharedMaterial = lit;
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }
}
