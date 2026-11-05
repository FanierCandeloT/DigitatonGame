using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera la base del greybox: sprite cuadrado, prefabs (Andrés, CameraRig), escenas y Build Profile.
/// Menú: DigitatonGame ▸ Construir base del greybox. Es seguro volver a ejecutarlo (recrea los prefabs y las escenas).
/// </summary>
public static class GreyboxBuilder
{
    const string Root = "Assets/_Project";
    const string SquarePath = Root + "/Art/Placeholders/Square.png";
    const string PlayerPrefabPath = Root + "/Prefabs/Andres.prefab";
    const string CameraPrefabPath = Root + "/Prefabs/CameraRig.prefab";

    static readonly string[] SceneOrder = { "MainMenu", "Campus", "Aulas1", "Aulas2", "Aulas3", "Aulas4", "EdificioCentral", "Sotanos", "Bienestar", "Cafeteria", "SalaProfesores", "TechoAulas3", "MuseoLili", "Tutorial" };

    // ───────────────────────── Menús ─────────────────────────

    [MenuItem("DigitatonGame/Construir base del greybox")]
    public static void BuildAll()
    {
        EnsureFolders();
        var sprite = EnsureSquareSprite();
        BuildPlayerPrefab(sprite);
        BuildCameraPrefab();
        BuildScenes(sprite);
        RegisterScenesInBuild();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Greybox construido.");
    }

    [MenuItem("GameObject/DigitatonGame/Placeholder (cubo)", false, 10)]
    static void CreatePlaceholderMenu()
    {
        var sprite = EnsureSquareSprite();
        var go = MakeBox("Placeholder", PlaceholderKind.Interactive, Vector2.zero, new Vector2(2, 2), sprite);
        var view = SceneView.lastActiveSceneView;
        if (view != null) go.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0);
        Undo.RegisterCreatedObjectUndo(go, "Crear placeholder");
        Selection.activeGameObject = go;
    }

    // ───────────────────────── Recursos ─────────────────────────

    static void EnsureFolders()
    {
        foreach (var f in new[] { "Scenes", "Scripts", "Prefabs", "Art", "Art/Placeholders", "Audio", "Editor" })
        {
            var p = Root + "/" + f;
            if (!AssetDatabase.IsValidFolder(p))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\', '/'), Path.GetFileName(p));
        }
    }

    static Sprite EnsureSquareSprite()
    {
        if (!File.Exists(SquarePath))
        {
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var px = new Color32[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SquarePath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
    }

    // ───────────────────────── Helpers de objetos ─────────────────────────

    public static GameObject MakeBox(string name, PlaceholderKind kind, Vector2 pos, Vector2 size, Sprite sprite,
        string finalAsset = "", Vector2Int? sizePx = null, bool solid = false, bool trigger = false,
        int order = 0, bool label = true)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = PlaceholderPalette.GetColor(kind);
        sr.sortingOrder = order;

        if (solid || trigger)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            col.isTrigger = trigger;
        }

        var info = go.AddComponent<PlaceholderInfo>();
        info.areaName = name;
        info.kind = kind;
        info.finalAssetDescription = finalAsset;
        info.suggestedSizePx = sizePx ?? new Vector2Int(Mathf.RoundToInt(size.x * 16), Mathf.RoundToInt(size.y * 16));
        info.owner = PlaceholderOwner.Integrante1;
        info.showLabel = label;
        return go;
    }

    public static GameObject MakeSpawn(string id, Vector2 pos, bool isDefault)
    {
        var go = new GameObject("Spawn_" + id);
        go.transform.position = pos;
        var sp = go.AddComponent<SpawnPoint>();
        sp.id = id;
        sp.isDefault = isDefault;
        return go;
    }

    public static GameObject MakeDoor(string name, Vector2 pos, Vector2 size, Sprite sprite, string targetScene, string targetSpawn)
    {
        var go = MakeBox(name, PlaceholderKind.Door, pos, size, sprite,
            "Puerta/entrada hacia " + targetScene + " (sprite de puerta o umbral).", null, trigger: true, order: 1);
        var door = go.AddComponent<Door>();
        door.targetScene = targetScene;
        door.targetSpawnId = targetSpawn;
        return go;
    }

    // ───────────────────────── Prefabs ─────────────────────────

    static void BuildPlayerPrefab(Sprite sprite)
    {
        var go = new GameObject("Andres");
        go.tag = "Player";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(1f, 2f);
        sr.color = Color.white;
        sr.sortingOrder = 0;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Colisión solo en la mitad inferior ("los pies"): en vista 3/4 se puede pasar "detrás" de los objetos.
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);
        col.offset = new Vector2(0f, -0.5f);

        go.AddComponent<PlayerController>();
        var interactor = go.AddComponent<PlayerInteractor>();

        var info = go.AddComponent<PlaceholderInfo>();
        info.areaName = "Andrés";
        info.kind = PlaceholderKind.Player;
        info.finalAssetDescription = "Sprite de Andrés (estudiante UAO), vista 3/4 top-down, con animación de caminar en 4/8 direcciones.";
        info.suggestedSizePx = new Vector2Int(16, 32);
        info.owner = PlaceholderOwner.Integrante1;

        var promptGo = new GameObject("InteractPrompt");
        promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        var tmp = promptGo.AddComponent<TextMeshPro>();
        tmp.text = "Press E";
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.95f, 0.3f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(10f, 1.5f);
        tmp.sortingOrder = 1001;
        promptGo.SetActive(false);
        interactor.promptLabel = tmp;

        PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
        Object.DestroyImmediate(go);
    }

    static void BuildCameraPrefab()
    {
        var rig = new GameObject("CameraRig");

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(rig.transform, false);
        camGo.transform.localPosition = new Vector3(0, 0, -10);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.625f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CinemachineBrain>();
        var ppc = camGo.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = 16;
        ppc.refResolutionX = 320;
        ppc.refResolutionY = 180;

        var vcGo = new GameObject("CinemachineCamera");
        vcGo.transform.SetParent(rig.transform, false);
        vcGo.transform.localPosition = new Vector3(0, 0, -10);
        var vcam = vcGo.AddComponent<CinemachineCamera>();
        var lens = vcam.Lens;
        lens.OrthographicSize = 5.625f;
        vcam.Lens = lens;
        var composer = vcGo.AddComponent<CinemachinePositionComposer>();
        composer.Damping = new Vector3(0.4f, 0.4f, 0f);
        var confiner = vcGo.AddComponent<CinemachineConfiner2D>();
        confiner.Damping = 0f;
        confiner.SlowingDistance = 0f;
        vcGo.AddComponent<CinemachinePixelPerfect>();

        var binder = rig.AddComponent<CameraRigBinder>();
        binder.virtualCamera = vcam;
        binder.confiner = confiner;

        PrefabUtility.SaveAsPrefabAsset(rig, CameraPrefabPath);
        Object.DestroyImmediate(rig);
    }

    // ───────────────────────── Escenas ─────────────────────────

    static void BuildScenes(Sprite sprite)
    {
        // Solo se crean las escenas que faltan: nunca se pisa el trabajo ya hecho en una escena existente.
        if (!SceneExists("MainMenu")) BuildMenuScene("MainMenu");
        if (!SceneExists("Campus")) BuildCampus(sprite);
        if (!SceneExists("Bienestar")) BuildBienestar(sprite);
        if (!SceneExists("SalaProfesores")) BuildRoomScene("SalaProfesores", "Sala de profesores", sprite);

        // Las escenas de plantilla ya no sirven: no tienen nada importante.
        foreach (var old in new[] { "Assets/Scenes/SampleScene.unity", "Assets/New Scene.unity" })
            if (File.Exists(old)) AssetDatabase.DeleteAsset(old);
        if (AssetDatabase.IsValidFolder("Assets/Scenes") && Directory.GetFiles("Assets/Scenes", "*.unity").Length == 0)
            AssetDatabase.DeleteAsset("Assets/Scenes");
    }

    public static bool SceneExists(string name) => File.Exists($"{Root}/Scenes/{name}.unity");

    public static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    public static void SaveScene(Scene scene, string name) =>
        EditorSceneManager.SaveScene(scene, $"{Root}/Scenes/{name}.unity");

    public static void AddGlobalLight()
    {
        var go = new GameObject("Global Light 2D");
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Global;
        l.intensity = 1f;
    }

    public static void BuildMenuScene(string name)
    {
        var scene = NewScene();
        AddGlobalLight();
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.625f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
        camGo.AddComponent<AudioListener>();
        var ppc = camGo.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = 16; ppc.refResolutionX = 320; ppc.refResolutionY = 180;
        SaveScene(scene, name);
    }

    public static void AddPlayerAndCamera(Vector2 playerPos)
    {
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
        player.transform.position = playerPos;
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath));
        rig.transform.position = Vector3.zero;
    }

    public static void AddBounds(Vector2 size)
    {
        var go = new GameObject("MapBounds");
        go.AddComponent<MapBounds>();                    // su Reset() fija un tamaño por defecto: va primero
        var box = go.GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
    }

    /// <summary>Muros negros alrededor de un rectángulo (centrado en el origen).</summary>
    public static void AddBorderWalls(Vector2 inner, Sprite sprite, float thickness = 0.5f)
    {
        float hx = inner.x / 2f, hy = inner.y / 2f, t = thickness;
        const string desc = "Muro/borde del mapa (puede ser una pared, cerca o límite visual).";
        MakeBox("Muro Norte", PlaceholderKind.Wall, new Vector2(0, hy + t / 2), new Vector2(inner.x + 2 * t, t), sprite, desc, null, true, label: false);
        MakeBox("Muro Sur", PlaceholderKind.Wall, new Vector2(0, -hy - t / 2), new Vector2(inner.x + 2 * t, t), sprite, desc, null, true, label: false);
        MakeBox("Muro Oeste", PlaceholderKind.Wall, new Vector2(-hx - t / 2, 0), new Vector2(t, inner.y), sprite, desc, null, true, label: false);
        MakeBox("Muro Este", PlaceholderKind.Wall, new Vector2(hx + t / 2, 0), new Vector2(t, inner.y), sprite, desc, null, true, label: false);
    }

    static void BuildCampus(Sprite sprite)
    {
        var scene = NewScene();
        AddGlobalLight();

        var inner = new Vector2(34f, 22f);
        AddBounds(inner + Vector2.one);
        MakeBox("Suelo Campus (prueba)", PlaceholderKind.Walkway, Vector2.zero, inner, sprite,
            "Tileset del campus (caminos y pasto) reemplazando el suelo de prueba.", new Vector2Int(544, 352), order: -100, label: false);
        AddBorderWalls(inner, sprite);

        // Muros de prueba (negros, no se pueden atravesar)
        MakeBox("Muro de prueba A", PlaceholderKind.Wall, new Vector2(-6f, 3f), new Vector2(8f, 0.5f), sprite,
            "Muro/cerca del campus.", null, solid: true);
        MakeBox("Muro de prueba B", PlaceholderKind.Wall, new Vector2(-9.75f, 0.25f), new Vector2(0.5f, 6.5f), sprite,
            "Muro/cerca del campus.", null, solid: true);

        // Edificio de Bienestar (rojo) con su puerta amarilla al frente (sur)
        MakeBox("Edificio Bienestar", PlaceholderKind.Health, new Vector2(7f, 6f), new Vector2(8f, 5f), sprite,
            "Fachada del edificio de Bienestar Universitario vista 3/4.", new Vector2Int(128, 80), solid: true);
        MakeDoor("Puerta a Bienestar", new Vector2(7f, 3.5f), new Vector2(1.5f, 1f), sprite, "Bienestar", "Bienestar_Entrance");

        // Objeto interactivo de prueba (cian)
        var challenge = MakeBox("Reto de prueba", PlaceholderKind.Interactive, new Vector2(-3f, -6f), new Vector2(1f, 1f), sprite,
            "Objeto interactivo / reto del campus.", null, trigger: true, order: 1);
        challenge.AddComponent<DebugInteractable>().message = "¡Interacción de prueba en el Campus!";

        // Puntos de aparición
        MakeSpawn("Campus_Default", new Vector2(0f, -4f), true);
        MakeSpawn("Campus_FromBienestar", new Vector2(7f, 2f), false);

        AddPlayerAndCamera(new Vector2(0f, -4f));
        SaveScene(scene, "Campus");
    }

    static void BuildBienestar(Sprite sprite)
    {
        var scene = NewScene();
        AddGlobalLight();

        var inner = new Vector2(22f, 12f);
        AddBounds(inner + Vector2.one * 2f);
        MakeBox("Piso Bienestar (prueba)", PlaceholderKind.Walkway, Vector2.zero, inner, sprite,
            "Piso interior de Bienestar.", new Vector2Int(352, 192), order: -100, label: false);
        AddBorderWalls(inner, sprite);

        MakeBox("Camilla", PlaceholderKind.Health, new Vector2(-5f, 2f), new Vector2(3f, 2f), sprite,
            "Camilla/mobiliario de enfermería.", null, solid: true);
        var counter = MakeBox("Mostrador", PlaceholderKind.Interactive, new Vector2(4f, 2f), new Vector2(1f, 1f), sprite,
            "Mostrador de atención.", null, trigger: true, order: 1);
        counter.AddComponent<DebugInteractable>().message = "¡Interacción de prueba en Bienestar!";

        // Puerta de regreso al Campus: aparece frente a la puerta por la que entró
        MakeDoor("Salida al Campus", new Vector2(0f, -6.25f), new Vector2(1.5f, 1f), sprite, "Campus", "Campus_FromBienestar");
        MakeSpawn("Bienestar_Entrance", new Vector2(0f, -4.5f), true);

        AddPlayerAndCamera(new Vector2(0f, -4.5f));
        SaveScene(scene, "Bienestar");
    }

    static void BuildRoomScene(string sceneName, string displayName, Sprite sprite)
    {
        var scene = NewScene();
        AddGlobalLight();
        var inner = new Vector2(22f, 12f);
        AddBounds(inner + Vector2.one * 2f);
        MakeBox("Piso " + displayName + " (prueba)", PlaceholderKind.Walkway, Vector2.zero, inner, sprite,
            "Piso de " + displayName + ".", new Vector2Int(352, 192), order: -100, label: false);
        AddBorderWalls(inner, sprite);
        MakeSpawn(sceneName + "_Default", new Vector2(0f, -3f), true);
        AddPlayerAndCamera(new Vector2(0f, -3f));
        SaveScene(scene, sceneName);
    }

    // ───────────────────────── Build Profile ─────────────────────────

    public static void RegisterScenesInBuild()
    {
        var list = new List<EditorBuildSettingsScene>();
        foreach (var n in SceneOrder)
        {
            var path = $"{Root}/Scenes/{n}.unity";
            if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }
}
