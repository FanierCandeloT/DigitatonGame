using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Construye en greybox las escenas interiores: Aulas1-4, EdificioCentral, Sotanos, Bienestar, Cafeteria,
/// SalaProfesores, TechoAulas3 y MuseoLili. Cada interior tiene su puerta de salida que devuelve a Andrés al campus
/// frente a la puerta correcta (los ids de SpawnPoint de Campus se crean en CampusBuilder).
/// Menú: DigitatonGame ▸ Construir interiores (reconstruye esas escenas).
/// </summary>
public static class InteriorsBuilder
{
    const string Root = "Assets/_Project";
    const string BienestarFlag = GameManager.FlagBienestarDone;

    static readonly Color DeskBlue = new Color(0.65f, 0.85f, 1f);
    static readonly Color Brown = new Color(0.45f, 0.30f, 0.15f);
    static readonly Color Shelf = new Color(0.40f, 0.25f, 0.12f);
    static readonly Color LightRed = new Color(1f, 0.55f, 0.55f);
    static readonly Color SolarBlue = new Color(0.08f, 0.12f, 0.45f);
    static readonly Color Glass = new Color(0.80f, 0.95f, 1f);
    static readonly Color Column = new Color(0.45f, 0.45f, 0.48f);
    static readonly Color Seat = new Color(0.50f, 0.55f, 0.70f);
    static readonly Color Floor = new Color(0.88f, 0.83f, 0.70f);
    static readonly Color Stage = new Color(0.55f, 0.38f, 0.20f);

    static Sprite sq;
    static Sprite Sq => sq != null ? sq : (sq = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Square.png"));

    [MenuItem("DigitatonGame/Construir interiores")]
    public static void BuildAll()
    {
        CampusBuilder.EnsureSprites();

        for (int n = 1; n <= 4; n++) BuildAulas(n);
        BuildEdificioCentral();
        BuildSotanos();
        BuildBienestar();
        BuildCafeteria();
        BuildSalaProfesores();
        BuildTechoAulas3();
        BuildMuseoLili();

        // Las escenas Intro y Agora ya no existen: el Intro ocurre en "Intro classroom" (Aulas2) y la Escena 5 en el Ágora del Campus.
        foreach (var old in new[] { "Intro", "Agora" })
        {
            var path = $"{Root}/Scenes/{old}.unity";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        GreyboxBuilder.RegisterScenesInBuild();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Interiores construidos.");
    }

    // ───────────────────────── Helpers públicos (también los usa CampusBuilder) ─────────────────────────

    public static GameObject Box(string name, PlaceholderKind kind, Vector2 pos, Vector2 size, string desc,
        bool solid = false, bool label = true, Color? color = null, int order = 0, float font = 5f)
    {
        var go = GreyboxBuilder.MakeBox(name, kind, pos, size, Sq, desc, null, solid, false, order, label);
        var info = go.GetComponent<PlaceholderInfo>();
        info.labelFontSize = font;
        if (color.HasValue)
        {
            info.applyKindColor = false;
            go.GetComponent<SpriteRenderer>().color = color.Value;
        }
        return go;
    }

    /// <summary>NPC rosado de 1x2 con colisión en los pies y un mensaje corto al interactuar.</summary>
    public static GameObject Npc(string name, Vector2 pos, string message, string desc)
    {
        var go = GreyboxBuilder.MakeBox(name, PlaceholderKind.NPC, pos, new Vector2(1f, 2f), Sq, desc, new Vector2Int(16, 32), false, false, 0, true);
        go.GetComponent<PlaceholderInfo>().labelFontSize = 5f;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);
        col.offset = new Vector2(0f, -0.5f);
        go.AddComponent<ToastInteractable>().message = message;
        return go;
    }

    /// <summary>Reto del guion: objeto cian con ChallengeInteractable.</summary>
    public static GameObject Challenge(string name, Vector2 pos, Vector2 size, string id, string display, int reward, string desc,
        int required = 0, string locked = "Something is still missing...", bool ends = false)
    {
        var go = GreyboxBuilder.MakeBox(name, PlaceholderKind.Interactive, pos, size, Sq, desc, null, false, true, 1, true);
        go.GetComponent<PlaceholderInfo>().labelFontSize = 5f;
        var c = go.AddComponent<ChallengeInteractable>();
        c.challengeId = id;
        c.displayName = display;
        c.memoryReward = reward;
        c.requiredMemories = required;
        c.lockedMessage = locked;
        c.endsGame = ends;
        return go;
    }

    const string StartMessage = "Class is about to start. Use the podium (E) to begin the lesson.";
    static readonly Vector2 LessonPodiumPos = new Vector2(5f, -1.2f);

    /// <summary>Atril del profesor: empieza la lección "Atrapa el error" (ClassLessonInteractable).</summary>
    public static GameObject LessonPodium(Vector2 pos)
    {
        var go = GreyboxBuilder.MakeBox("Atril de la lección", PlaceholderKind.Interactive, pos, new Vector2(2.2f, 1.4f), Sq,
            "Atril del profesor donde empieza la lección «Atrapa el error» (sprite de atril con libro, vista 3/4).", null, false, true, 1, true);
        go.GetComponent<PlaceholderInfo>().labelFontSize = 4f;
        go.AddComponent<ClassLessonInteractable>().prompt = "Press E to start the lesson";
        return go;
    }

    /// <summary>Agrega el atril a Aulas2 sin reconstruir la escena (no pisa ediciones a mano).</summary>
    [MenuItem("DigitatonGame/Agregar lección a Aulas2 (sin reconstruir)")]
    public static void AddLessonToAulas2()
    {
        CampusBuilder.EnsureSprites();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{Root}/Scenes/Aulas2.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);

        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<ClassLessonInteractable>(true) != null) exists = true;
            foreach (var m in root.GetComponentsInChildren<SceneStartMessage>(true)) m.message = StartMessage;
        }

        if (!exists)
        {
            var podium = LessonPodium(LessonPodiumPos);
            SceneManager.MoveGameObjectToScene(podium, scene);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        Debug.Log(exists ? "Aulas2: el atril ya existía; solo se actualizó el mensaje de inicio." : "Aulas2: atril de la lección agregado.");
    }

    /// <summary>Puerta/salida: lleva a otra escena (o aviso "Coming soon" si targetScene está vacío).</summary>
    public static GameObject ExitDoor(string name, Vector2 pos, Vector2 size, string targetScene, string targetSpawn, string desc,
        string requiredFlag = null, string lockedMessage = null)
    {
        var go = GreyboxBuilder.MakeDoor(name, pos, size, Sq, targetScene, targetSpawn);
        go.GetComponent<PlaceholderInfo>().finalAssetDescription = desc;
        go.GetComponent<PlaceholderInfo>().labelFontSize = 4f;
        var door = go.GetComponent<Door>();
        if (!string.IsNullOrEmpty(requiredFlag))
        {
            door.requiredFlag = requiredFlag;
            door.devBypass = false;
            if (!string.IsNullOrEmpty(lockedMessage)) door.lockedMessage = lockedMessage;
        }
        return go;
    }

    // ───────────────────────── Helpers de armado ─────────────────────────

    /// <summary>Escena interior: piso, límites y muros negros alrededor de "inner" (centrado en el origen).</summary>
    static Scene Begin(string sceneName, Vector2 inner, string floorDesc)
    {
        var scene = GreyboxBuilder.NewScene();
        GreyboxBuilder.AddGlobalLight();
        GreyboxBuilder.AddBounds(inner + Vector2.one * 2f);
        Box($"Piso {sceneName}", PlaceholderKind.Walkway, Vector2.zero, inner, floorDesc, false, false, null, -100);
        GreyboxBuilder.AddBorderWalls(inner, Sq);
        return scene;
    }

    static void End(string sceneName, Vector2 playerPos)
    {
        GreyboxBuilder.AddPlayerAndCamera(playerPos);
        GreyboxBuilder.SaveScene(SceneManager.GetActiveScene(), sceneName);
    }

    static void Spawn(string id, Vector2 pos, bool isDefault = false) => GreyboxBuilder.MakeSpawn(id, pos, isDefault);

    static void Wall(float x0, float y0, float x1, float y1)
    {
        var size = new Vector2(Mathf.Max(x1 - x0, 0.5f), Mathf.Max(y1 - y0, 0.5f));
        Box("Muro interior", PlaceholderKind.Wall, new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f), size,
            "Muro interior (pared de salón).", true, false);
    }

    /// <summary>Pared horizontal en y con huecos (puertas sin hoja) de 4 u de ancho centrados en gaps.</summary>
    static void HWall(float y, float x0, float x1, params float[] gaps)
    {
        float cursor = x0;
        var sorted = new List<float>(gaps);
        sorted.Sort();
        foreach (var g in sorted)
        {
            if (g - 2f > cursor) Wall(cursor, y - 0.25f, g - 2f, y + 0.25f);
            cursor = g + 2f;
        }
        if (x1 > cursor) Wall(cursor, y - 0.25f, x1, y + 0.25f);
    }

    static void VWall(float x, float y0, float y1, params float[] gaps)
    {
        float cursor = y0;
        var sorted = new List<float>(gaps);
        sorted.Sort();
        foreach (var g in sorted)
        {
            if (g - 2f > cursor) Wall(x - 0.25f, cursor, x + 0.25f, g - 2f);
            cursor = g + 2f;
        }
        if (y1 > cursor) Wall(x - 0.25f, cursor, x + 0.25f, y1);
    }

    static void RoomFloor(string name, Vector2 center, Vector2 size, string desc)
    {
        Box(name, PlaceholderKind.Walkway, center, size, desc, false, true, Floor, -90, 6f);
    }

    static void Desks(string roomName, float cx, float[] rows, float[] cols, float yOffset = 0f)
    {
        foreach (var r in rows)
            foreach (var c in cols)
                Box($"Pupitre ({roomName})", PlaceholderKind.ClassroomBuilding, new Vector2(cx + c, r + yOffset), new Vector2(1.4f, 1.0f),
                    "Pupitre de salón (cubo azul claro): escritorio de estudiante, vista 3/4.", true, false, DeskBlue, 0, 4f);
    }

    static void Board(string roomName, Vector2 pos, float width = 8f)
    {
        Box($"Tablero ({roomName})", PlaceholderKind.ClassroomBuilding, pos, new Vector2(width, 0.5f),
            "Tablero blanco del salón (rectángulo blanco).", false, false, Color.white, 1, 4f);
    }

    // ───────────────────────── Aulas 1 a 4 ─────────────────────────

    static void BuildAulas(int n)
    {
        var inner = new Vector2(52f, 20f);
        string scene = $"Aulas{n}";
        Begin(scene, inner, $"Piso del edificio Aulas {n}.");

        // Pasillo (sur) y 3 salones (norte) conectados por huecos en la pared
        Box($"Pasillo Aulas {n}", PlaceholderKind.Walkway, new Vector2(0, -7f), new Vector2(52f, 6f),
            $"Pasillo de Aulas {n}: baldosa y paredes laterales.", false, true, Floor, -90, 6f);
        float[] cx = { -17.4f, 0f, 17.4f };
        HWall(-4f, -26f, 26f, cx);
        VWall(-8.75f, -4f, 10f);
        VWall(8.75f, -4f, 10f);

        for (int k = 0; k < 3; k++)
        {
            string room = (n == 2 && k == 1) ? "Intro classroom" : $"Aulas {n} - Salón {k + 1}";
            RoomFloor(room, new Vector2(cx[k], 3f), new Vector2(16.5f, 14f),
                n == 2 && k == 1 ? "Salón donde ocurre la Intro del guion (Escena 1): pupitres, tablero y 3 estudiantes."
                                 : $"Salón de clase de Aulas {n}: piso de baldosa.");
            Board(room, new Vector2(cx[k], 9.5f));
            Desks(room, cx[k], new[] { 2f, 5.5f }, new[] { -4f, 0f, 4f });
        }

        // Salidas al campus: una por cada puerta del edificio (A al oeste, B al este)
        ExitDoor($"Salida A (Aulas {n})", new Vector2(-25.35f, -7f), new Vector2(1.5f, 3f), "Campus", $"Campus_Aulas{n}_A",
            $"Puerta de salida A de Aulas {n}: regresa al campus frente a la puerta A.");
        ExitDoor($"Salida B (Aulas {n})", new Vector2(25.35f, -7f), new Vector2(1.5f, 3f), "Campus", $"Campus_Aulas{n}_B",
            $"Puerta de salida B de Aulas {n}: regresa al campus frente a la puerta B.");
        Spawn($"Aulas{n}_A", new Vector2(-24f, -7f), true);
        Spawn($"Aulas{n}_B", new Vector2(24f, -7f));

        if (n == 2)
        {
            // Aquí empieza el juego (Escena 1: Intro)
            Npc("Valentina", new Vector2(-4f, 7.6f), "Valentina: (class dialogue pending)", "Sprite de Valentina, estudiante (NPC, vista 3/4, 16x32 px).");
            Npc("Santiago", new Vector2(0f, 7.6f), "Santiago: (class dialogue pending)", "Sprite de Santiago, estudiante (NPC, vista 3/4, 16x32 px).");
            Npc("Estudiante", new Vector2(4f, 7.6f), "Student: (class dialogue pending)", "Sprite de otro estudiante del salón (NPC, vista 3/4, 16x32 px).");
            Spawn("Aulas2_Intro", new Vector2(0f, -1.2f));
            LessonPodium(LessonPodiumPos);

            var msg = new GameObject("Mensaje de inicio (Escena 1)");
            var sm = msg.AddComponent<SceneStartMessage>();
            sm.message = StartMessage;
            sm.flagKey = "intro_shown";
            sm.seconds = 5f;
        }

        if (n == 3)
        {
            // Puerta a la sala de profesores y escalera al techo (pared sur del pasillo)
            ExitDoor("Puerta a Sala de Profesores", new Vector2(-8f, -9.4f), new Vector2(3f, 1.5f), "SalaProfesores", "SalaProfesores_Entrance",
                "Puerta de la sala de profesores (escena SalaProfesores).");
            ExitDoor("Escalera al techo", new Vector2(8f, -9.4f), new Vector2(3f, 2f), "TechoAulas3", "TechoAulas3_Stairs",
                "Escalera amarilla que sube al techo solar (escena TechoAulas3).");
            Spawn("Aulas3_FromSala", new Vector2(-8f, -7.8f));
            Spawn("Aulas3_FromStairs", new Vector2(8f, -7.8f));
        }

        End(scene, new Vector2(-24f, -7f));
    }

    // ───────────────────────── Edificio Central ─────────────────────────

    static void BuildEdificioCentral()
    {
        var inner = new Vector2(70f, 40f);
        Begin("EdificioCentral", inner, "Piso del Edificio Central.");

        Box("Pasillo central", PlaceholderKind.Walkway, new Vector2(0, -3f), new Vector2(70f, 6f),
            "Pasillo central del Edificio Central que une todas las salas.", false, true, Floor, -90, 6f);

        // Salas norte (y 0..20): Biblioteca, Auditorio Yquinde, Auditorio Quincha. Salas sur (y -20..-6): Recepción, Salón Lili, Bloque D2.
        HWall(0f, -35f, 35f, -22f, 3.5f, 25.5f);
        HWall(-6f, -35f, 35f, -23.5f, -2f, 21.5f);
        VWall(-9f, 0f, 20f); VWall(16f, 0f, 20f);
        VWall(-12f, -20f, -6f); VWall(8f, -20f, -6f);

        RoomFloor("Biblioteca", new Vector2(-22f, 10f), new Vector2(25.5f, 19.5f), "Biblioteca: piso de madera clara con estanterías y mesas de lectura.");
        RoomFloor("Auditorio Yquinde", new Vector2(3.5f, 10f), new Vector2(24.5f, 19.5f), "Auditorio Yquinde: sala de conferencias con butacas y escenario.");
        RoomFloor("Auditorio Quincha", new Vector2(25.5f, 10f), new Vector2(18.5f, 19.5f), "Auditorio Quincha: sala bajo el domo con butacas.");
        RoomFloor("Recepción e información", new Vector2(-23.5f, -13f), new Vector2(22.5f, 13.5f), "Recepción e información del Edificio Central.");
        RoomFloor("Salón Lili", new Vector2(-2f, -13f), new Vector2(19.5f, 13.5f), "Salón Lili: aula con pupitres y tablero.");
        RoomFloor("Bloque D2", new Vector2(21.5f, -13f), new Vector2(26.5f, 13.5f), "Bloque D2: salón grande de clases.");

        // Biblioteca: 8 estanterías en 4 filas, con pasillo central alineado con la puerta
        foreach (var y in new[] { 4f, 8f, 12f, 16f })
            foreach (var x in new[] { -29f, -15f })
                Box("Estantería", PlaceholderKind.AdminBuilding, new Vector2(x, y), new Vector2(8f, 1f),
                    "Estantería de libros de la biblioteca (sprite de estantería, vista 3/4).", true, false, Shelf, 0, 4f);

        // Auditorios: butacas en filas con pasillo central; escenario al lado
        foreach (var y in new[] { 5f, 7.5f, 10f, 12.5f })
        {
            foreach (var x in new[] { -6f, -4f, -2f, 0f, 7f, 9f, 11f, 13f })
                Box("Butaca (Yquinde)", PlaceholderKind.AdminBuilding, new Vector2(x, y), new Vector2(1f, 1f), "Butaca de auditorio.", true, false, Seat, 0, 4f);
            foreach (var x in new[] { 18f, 20f, 31f, 33f })
                Box("Butaca (Quincha)", PlaceholderKind.AdminBuilding, new Vector2(x, y), new Vector2(1f, 1f), "Butaca de auditorio.", true, false, Seat, 0, 4f);
        }
        Box("Escenario Yquinde", PlaceholderKind.AdminBuilding, new Vector2(9f, 17.5f), new Vector2(8f, 2f), "Escenario del Auditorio Yquinde.", false, true, Stage, -80, 4f);
        Box("Escenario Quincha", PlaceholderKind.AdminBuilding, new Vector2(26f, 16.2f), new Vector2(7f, 2f), "Escenario del Auditorio Quincha.", false, true, Stage, -80, 4f);

        // Recepción: mostrador
        Box("Mostrador de recepción", PlaceholderKind.AdminBuilding, new Vector2(-23.5f, -13f), new Vector2(8f, 1.5f),
            "Mostrador de la recepción e información (sprite de mostrador, vista 3/4).", true, true, null, 0, 5f);
        // Salón Lili y D2: pupitres y tableros
        Desks("Salón Lili", -2f, new[] { -9f, -12f, -15f }, new[] { -6f, -3f, 3f, 6f });
        Board("Salón Lili", new Vector2(-8f, -19.7f), 5f);
        Desks("Bloque D2", 21.5f, new[] { -9f, -12f, -15f }, new[] { -8f, -4f, 0f, 4f, 8f });
        Board("Bloque D2", new Vector2(29f, -19.7f), 6f);

        // Salidas al campus (una por cada puerta del edificio)
        ExitDoor("Salida Recepción", new Vector2(-26f, -19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_Recepcion", "Salida de la recepción: regresa al campus frente a la puerta de la Recepción.");
        ExitDoor("Salida Salón Lili", new Vector2(-2f, -19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_SalonLili", "Salida del Salón Lili: regresa al campus.");
        ExitDoor("Salida Bloque D2", new Vector2(21.5f, -19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_D2", "Salida del bloque D2: regresa al campus.");
        ExitDoor("Salida Biblioteca", new Vector2(-22f, 19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_Biblioteca", "Salida de la biblioteca: regresa al campus.");
        ExitDoor("Salida Auditorio Yquinde", new Vector2(3.5f, 19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_Yquinde", "Salida del Auditorio Yquinde: regresa al campus.");
        ExitDoor("Salida Auditorio Quincha", new Vector2(22f, 19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_Quincha", "Salida del Auditorio Quincha: regresa al campus.");
        ExitDoor("Salida Domo", new Vector2(30f, 19.4f), new Vector2(3f, 1.5f), "Campus", "Campus_EC_Domo", "Salida al domo: regresa al campus.");
        ExitDoor("Bajada a sótanos", new Vector2(34.4f, -3f), new Vector2(1.5f, 3f), "Sotanos", "Sotanos_FromEC", "Escalera/rampa que baja a los sótanos (escena Sotanos).");

        Spawn("EC_Recepcion", new Vector2(-26f, -17.8f), true);
        Spawn("EC_SalonLili", new Vector2(-2f, -17.8f));
        Spawn("EC_D2", new Vector2(21.5f, -17.8f));
        Spawn("EC_Biblioteca", new Vector2(-22f, 17.8f));
        Spawn("EC_Yquinde", new Vector2(3.5f, 17.8f));
        Spawn("EC_Quincha", new Vector2(22f, 17.8f));
        Spawn("EC_Domo", new Vector2(30f, 17.8f));
        Spawn("EC_Sotanos", new Vector2(33f, -3f));

        End("EdificioCentral", new Vector2(-26f, -17.8f));
    }

    // ───────────────────────── Sótanos ─────────────────────────

    static void BuildSotanos()
    {
        var inner = new Vector2(60f, 14f);
        Begin("Sotanos", inner, "Piso de concreto del pasillo subterráneo.");
        Box("Pasillo subterráneo", PlaceholderKind.Street, Vector2.zero, new Vector2(60f, 14f),
            "Pasillo subterráneo: piso de concreto y luz tenue.", false, true, null, -95, 6f);

        for (float x = -24f; x <= 24f; x += 8f)
            foreach (var y in new[] { -4.5f, 4.5f })
                Box("Columna", PlaceholderKind.Street, new Vector2(x, y), new Vector2(1.5f, 1.5f),
                    "Columna de concreto del sótano (sprite de columna, vista 3/4).", true, false, Column, 0, 4f);

        ExitDoor("Salida a la entrada de sótanos (campus)", new Vector2(-29.35f, 0f), new Vector2(1.5f, 3f), "Campus", "Campus_Sotanos",
            "Salida oeste: sube a la entrada a sótanos del campus.");
        ExitDoor("Salida al Edificio Central", new Vector2(29.35f, 0f), new Vector2(1.5f, 3f), "EdificioCentral", "EC_Sotanos",
            "Salida este: sube al Edificio Central.");
        Spawn("Sotanos_FromCampus", new Vector2(-27.8f, 0f), true);
        Spawn("Sotanos_FromEC", new Vector2(27.8f, 0f));
        End("Sotanos", new Vector2(-27.8f, 0f));
    }

    // ───────────────────────── Bienestar ─────────────────────────

    static void BuildBienestar()
    {
        var inner = new Vector2(44f, 20f);
        Begin("Bienestar", inner, "Piso interior de Bienestar Universitario.");

        VWall(0f, -10f, 10f, 0f);   // hueco entre recepción y enfermería
        RoomFloor("Recepción", new Vector2(-11f, 0f), new Vector2(21.5f, 19.5f), "Recepción de Bienestar Universitario.");
        RoomFloor("Enfermería", new Vector2(11f, 0f), new Vector2(21.5f, 19.5f), "Enfermería de Bienestar con camillas.");

        // Recepción: escritorio de Lucía y las preguntas 1-4
        Box("Escritorio de Lucía", PlaceholderKind.Health, new Vector2(-11f, 3f), new Vector2(6f, 1.5f),
            "Escritorio de Lucía (sprite de escritorio con computador, vista 3/4).", true, true, Brown, 0, 5f);
        // Lucía inicia el reto de las preguntas 1-4: primero conversa con Andrés y luego abre el minijuego de memoria.
        var lucia = Npc("Lucía", new Vector2(-11f, 5.4f), "Lucía: Take your time.", "Sprite de Lucía, enfermera de Bienestar (NPC, vista 3/4, 16x32 px).");
        Object.DestroyImmediate(lucia.GetComponent<ToastInteractable>());
        var luciaChallenge = lucia.AddComponent<NpcChallengeInteractable>();
        luciaChallenge.prompt = "Press E to talk";

        // Enfermería: camilla (cubo rojo claro) y botiquín
        Box("Camilla", PlaceholderKind.Health, new Vector2(12f, 4f), new Vector2(4f, 2f),
            "Camilla de la enfermería (sprite de camilla con sábana, vista 3/4).", true, true, LightRed, 0, 5f);
        Box("Camilla 2", PlaceholderKind.Health, new Vector2(18f, 4f), new Vector2(4f, 2f),
            "Segunda camilla de la enfermería.", true, true, LightRed, 0, 5f);
        Box("Botiquín", PlaceholderKind.Health, new Vector2(20.5f, 8.6f), new Vector2(2f, 1f),
            "Botiquín de primeros auxilios.", true, true, null, 0, 4f);

        // Salida: bloqueada hasta completar las preguntas 1-4
        ExitDoor("Salida al Campus", new Vector2(-11f, -9.4f), new Vector2(3f, 1.5f), "Campus", "Campus_FromBienestar",
            "Puerta de salida de Bienestar hacia el campus. Bloqueada hasta completar las preguntas 1-4.",
            BienestarFlag, "Lucía: You must rest a little more.");

        Spawn("Bienestar_Entrance", new Vector2(-11f, -7.8f), true);
        Spawn("Bienestar_Bed", new Vector2(12f, 1.6f));   // junto a la camilla (Escena 5)
        End("Bienestar", new Vector2(12f, 1.6f));
    }

    // ───────────────────────── Cafetería ─────────────────────────

    static void BuildCafeteria()
    {
        var inner = new Vector2(40f, 18f);
        Begin("Cafeteria", inner, "Piso de la Cafetería UAO.");

        Box("Mostrador de la cafetería", PlaceholderKind.Food, new Vector2(0f, 4f), new Vector2(16f, 1.5f),
            "Mostrador de la Cafetería UAO (sprite de mostrador con vitrina, vista 3/4).", true, true, null, 0, 5f);
        Npc("Doña Marta", new Vector2(0f, 6.4f), "Doña Marta: What can I get you?", "Sprite de Doña Marta, atiende la cafetería (NPC, vista 3/4, 16x32 px).");

        int i = 1;
        foreach (var y in new[] { -2f, -6f })
            foreach (var x in new[] { -12f, -4f, 4f })
                Box($"Mesa {i++}", PlaceholderKind.Food, new Vector2(x, y), new Vector2(3f, 2f),
                    "Mesa de la cafetería (sprite de mesa con sillas, vista 3/4).", true, false, null, 0, 4f);

        Challenge("Banda transportadora (Question 6)", new Vector2(13.5f, -2f), new Vector2(8f, 1.6f), "q6", "Question 6 – Conveyor belt", 1,
            "Banda transportadora de bandejas de la cafetería (sprite animado). Reto de la pregunta 6.");

        ExitDoor("Salida a la Cafetería (campus)", new Vector2(-19.35f, 0f), new Vector2(1.5f, 3f), "Campus", "Campus_Cafeteria",
            "Puerta de salida de la cafetería: regresa al campus frente a su puerta.");
        Spawn("Cafeteria_Entrance", new Vector2(-17.6f, 0f), true);
        End("Cafeteria", new Vector2(-17.6f, 0f));
    }

    // ───────────────────────── Sala de profesores ─────────────────────────

    static void BuildSalaProfesores()
    {
        var inner = new Vector2(30f, 16f);
        Begin("SalaProfesores", inner, "Piso de la sala de profesores.");

        Box("Escritorio principal", PlaceholderKind.AdminBuilding, new Vector2(0f, 4.2f), new Vector2(6f, 1.8f),
            "Escritorio principal de la sala de profesores (sprite de escritorio, vista 3/4).", true, true, Brown, 0, 5f);
        Challenge("Pila de exámenes (Questions 9-11)", new Vector2(0f, 4.2f), new Vector2(1.6f, 1.1f), "q9_11", "Questions 9-11", 3,
            "Pila de exámenes sobre el escritorio (sprite de papeles apilados). Reto de las preguntas 9 a 11.");
        foreach (var p in new[] { new Vector2(-9f, 1f), new Vector2(9f, 1f), new Vector2(-9f, -3f), new Vector2(9f, -3f) })
            Box("Escritorio de profesor", PlaceholderKind.AdminBuilding, p, new Vector2(4f, 1.5f),
                "Escritorio de profesor con computador.", true, false, Brown, 0, 4f);

        ExitDoor("Salida a Aulas 3", new Vector2(0f, -7.4f), new Vector2(3f, 1.5f), "Aulas3", "Aulas3_FromSala",
            "Puerta de la sala de profesores: regresa al pasillo de Aulas 3.");
        Spawn("SalaProfesores_Entrance", new Vector2(0f, -5.9f), true);
        End("SalaProfesores", new Vector2(0f, -5.9f));
    }

    // ───────────────────────── Techo de Aulas 3 ─────────────────────────

    static void BuildTechoAulas3()
    {
        var inner = new Vector2(50f, 24f);
        Begin("TechoAulas3", inner, "Piso del techo de Aulas 3 (concreto).");

        foreach (var y in new[] { 6f, 9.5f, -6f })
            foreach (var x in new[] { -21f, -15f, -9f, 9f, 15f, 21f })
                Box("Panel solar", PlaceholderKind.ClassroomBuilding, new Vector2(x, y), new Vector2(3.2f, 1.8f),
                    "Panel solar del techo (cubo azul oscuro; sprite de panel fotovoltaico, vista 3/4).", true, false, SolarBlue, 0, 4f);

        Challenge("Puzzle de bloques (Question 8)", new Vector2(0f, 0f), new Vector2(4f, 4f), "q8", "Question 8 – Solar roof puzzle", 1,
            "Espacio del puzzle de bloques del techo solar (tablero con bloques movibles). Reto de la pregunta 8.");

        ExitDoor("Escalera a Aulas 3", new Vector2(0f, -11.4f), new Vector2(3f, 2f), "Aulas3", "Aulas3_FromStairs",
            "Escalera amarilla que baja al pasillo de Aulas 3.");
        Spawn("TechoAulas3_Stairs", new Vector2(0f, -10f), true);
        End("TechoAulas3", new Vector2(0f, -10f));
    }

    // ───────────────────────── Museo Lili ─────────────────────────

    static void BuildMuseoLili()
    {
        var inner = new Vector2(24f, 12f);
        Begin("MuseoLili", inner, "Piso de madera del Museo Lili.");

        foreach (var p in new[] { new Vector2(-8f, 2.5f), new Vector2(-2.7f, 2.5f), new Vector2(2.7f, 2.5f), new Vector2(8f, 2.5f),
                                  new Vector2(-8f, -1.5f), new Vector2(8f, -1.5f) })
            Box("Vitrina", PlaceholderKind.AdminBuilding, p, new Vector2(2.6f, 1.2f),
                "Vitrina del museo (sprite de vitrina de cristal con objeto expuesto, vista 3/4).", true, false, Glass, 0, 4f);

        ExitDoor("Salida del Museo Lili", new Vector2(0f, -5.4f), new Vector2(3f, 1.5f), "Campus", "Campus_MuseoLili",
            "Puerta del Museo Lili: regresa al campus, al extremo oeste de Aulas 1.");
        Spawn("MuseoLili_Entrance", new Vector2(0f, -3.9f), true);
        End("MuseoLili", new Vector2(0f, -3.9f));
    }
}
