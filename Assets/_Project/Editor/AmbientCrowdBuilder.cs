using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea el prefab "Peatones del Campus" (estudiantes que caminan de ida y vuelta por los caminos) y lo coloca en el Campus
/// sin reconstruir la escena. Todo vive en un prefab, así en el Campus queda una sola instancia (pocos conflictos con git).
/// </summary>
public static class AmbientCrowdBuilder
{
    const string PrefabPath = "Assets/_Project/Prefabs/Peatones_Campus.prefab";
    const string SquarePath = "Assets/_Project/Art/Placeholders/Square.png";

    // Cada fila: puntos de la ruta (coordenadas del mundo, sobre los caminos del campus) y velocidad.
    static readonly (Vector2[] points, float speed)[] Routes =
    {
        (new[] { new Vector2(-20f, -44.0f), new Vector2(100f, -44.0f) }, 1.7f),     // camino sur (Aulas 1 y Bienestar)
        (new[] { new Vector2(95f, -45.4f), new Vector2(-15f, -45.4f) }, 1.4f),
        (new[] { new Vector2(0f, -13.6f), new Vector2(52f, -13.6f) }, 1.6f),        // frente a Aulas 2
        (new[] { new Vector2(24f, 7.6f), new Vector2(78f, 7.6f) }, 1.5f),           // frente a Aulas 3
        (new[] { new Vector2(58f, 26.4f), new Vector2(97f, 26.4f) }, 1.8f),        // frente a Aulas 4
        (new[] { new Vector2(-25f, 50.4f), new Vector2(112f, 50.4f) }, 1.7f),       // camino norte
        (new[] { new Vector2(-29.8f, -48f), new Vector2(-29.8f, 48f) }, 1.6f),      // camino norte-sur central
        (new[] { new Vector2(-28.6f, 46f), new Vector2(-28.6f, -46f) }, 1.3f),
        (new[] { new Vector2(60.3f, -50f), new Vector2(60.3f, 6f) }, 1.5f),        // camino este (Aulas 2 - sur); antes la portería oeste, que ya no existe
        (new[] { new Vector2(60f, -7.1f), new Vector2(79f, -7.1f) }, 1.4f),         // camino a la Cafetería
        (new[] { new Vector2(-30f, -10f), new Vector2(-22f, -10f), new Vector2(-22f, -3f), new Vector2(-30f, -3f) }, 1.2f),   // alrededor del Ágora
    };

    // Peatones extra: los mismos caminos recorridos en sentido contrario (más gente sin inventar rutas que crucen edificios).
    static readonly int[] ReversedRoutes = { 0, 2, 3, 4, 5, 8, 9 };

    // Las rutas de arriba se dibujaron con el mapa original; el campus quedó al 60 % ("mapa al 60 %"), así que se escalan.
    const float MapScale = 0.6f;

    // Hojas de personajes (NpcSpriteSlicer) para cada peatón: estudiantes y gente del campus, sin los personajes fantásticos.
    static readonly int[] Sheets = { 1, 3, 10, 13, 15, 19, 21, 22, 25, 16, 2 };

    [MenuItem("DigitatonGame/Agregar peatones al Campus")]
    public static void AddToCampus()
    {
        CampusBuilder.EnsureSprites();
        if (NpcSpriteSlicer.Load(1).Length < 12) NpcSpriteSlicer.SliceAll();
        var prefab = BuildPrefab();

        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus.unity", OpenSceneMode.Additive);
        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<AmbientCrowd>(true) != null) exists = true;

        if (!exists) PrefabUtility.InstantiatePrefab(prefab, scene);
        // Si ya estaba colocado, la instancia del Campus toma el prefab actualizado (rutas y sprites nuevos) por ser instancia del prefab.

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log(exists ? "Campus: los peatones ya estaban colocados; solo se actualizó el prefab." : "Campus: peatones agregados.");
    }

    static GameObject BuildPrefab()
    {
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);

        // Se arma en una escena temporal para no ensuciar la escena abierta
        var previous = SceneManager.GetActiveScene();
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temp);

        var rootGo = new GameObject("Peatones del Campus");
        var crowd = rootGo.AddComponent<AmbientCrowd>();
        var walkers = new AmbientWalker[Routes.Length + ReversedRoutes.Length];

        for (int i = 0; i < walkers.Length; i++)
        {
            bool reversed = i >= Routes.Length;
            var (rawPoints, speed) = Routes[reversed ? ReversedRoutes[i - Routes.Length] : i];
            var points = new Vector2[rawPoints.Length];
            for (int p = 0; p < points.Length; p++)
                points[p] = rawPoints[reversed ? points.Length - 1 - p : p] * MapScale;
            if (reversed) speed *= 0.9f;

            // Los extra usan hojas desfasadas para que no se repita el mismo personaje en la misma ruta
            var frames = NpcSpriteSlicer.Load(Sheets[(i + (reversed ? 5 : 0)) % Sheets.Length]);
            var go = new GameObject("Peatón " + (i + 1));
            go.transform.SetParent(rootGo.transform, false);
            go.transform.position = points[0];
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames.Length >= 12 ? frames[1] : square;   // cuadro quieto mirando hacia abajo

            var walker = go.AddComponent<AmbientWalker>();
            walker.frames = frames.Length >= 12 ? frames : new Sprite[0];
            walker.path = points;
            walker.speed = speed;
            walker.pauseSeconds = 1.2f + (i % 3) * 0.6f;
            walker.timeOffset = i * 13.7f;   // cada uno en un momento distinto de su recorrido
            walkers[i] = walker;
        }
        crowd.walkers = walkers;

        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(temp, true);
        return prefab;
    }
}
