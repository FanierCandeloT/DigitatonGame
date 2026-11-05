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
        (new[] { new Vector2(58f, 26.4f), new Vector2(106f, 26.4f) }, 1.8f),        // frente a Aulas 4
        (new[] { new Vector2(-25f, 50.4f), new Vector2(112f, 50.4f) }, 1.7f),       // camino norte
        (new[] { new Vector2(-29.8f, -48f), new Vector2(-29.8f, 48f) }, 1.6f),      // camino norte-sur central
        (new[] { new Vector2(-28.6f, 46f), new Vector2(-28.6f, -46f) }, 1.3f),
        (new[] { new Vector2(-115f, -18.4f), new Vector2(-45f, -18.4f) }, 1.5f),    // portería oeste - Ágora
        (new[] { new Vector2(60f, -7.1f), new Vector2(79f, -7.1f) }, 1.4f),         // camino a la Cafetería
        (new[] { new Vector2(-30f, -10f), new Vector2(-22f, -10f), new Vector2(-22f, -3f), new Vector2(-30f, -3f) }, 1.2f),   // alrededor del Ágora
    };

    static readonly Color[] Shirts =
    {
        new Color(0.95f, 0.65f, 0.75f), new Color(0.65f, 0.8f, 0.95f), new Color(0.95f, 0.85f, 0.55f),
        new Color(0.65f, 0.9f, 0.7f), new Color(0.8f, 0.7f, 0.95f), new Color(0.95f, 0.75f, 0.6f),
    };

    [MenuItem("DigitatonGame/Agregar peatones al Campus")]
    public static void AddToCampus()
    {
        CampusBuilder.EnsureSprites();
        var prefab = BuildPrefab();

        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus.unity", OpenSceneMode.Additive);
        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<AmbientCrowd>(true) != null) exists = true;

        if (!exists) PrefabUtility.InstantiatePrefab(prefab, scene);

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
        var walkers = new AmbientWalker[Routes.Length];

        for (int i = 0; i < Routes.Length; i++)
        {
            var (points, speed) = Routes[i];
            var go = GreyboxBuilder.MakeBox("Peatón " + (i + 1), PlaceholderKind.NPC, points[0], new Vector2(0.9f, 1.8f), square,
                "Estudiante que camina por el campus (NPC de ambiente, vista 3/4, 16x32 px). Animaciones: caminar izquierda/derecha/arriba/abajo. " +
                "Solo está activo cuando se ve en cámara.", new Vector2Int(16, 32), false, false, 0, false);
            go.GetComponent<PlaceholderInfo>().applyKindColor = false;
            go.GetComponent<SpriteRenderer>().color = Shirts[i % Shirts.Length];
            go.transform.SetParent(rootGo.transform, true);

            var walker = go.AddComponent<AmbientWalker>();
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
