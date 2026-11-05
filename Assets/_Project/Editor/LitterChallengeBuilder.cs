using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea el prefab del reto 2 (estudiante con botellas junto a la caneca) y lo coloca en el Campus sin reconstruir la escena.
/// Como todo el reto vive en un prefab, en el Campus queda una sola instancia: así hay pocos conflictos de git
/// con quien esté decorando esa escena.
/// </summary>
public static class LitterChallengeBuilder
{
    const string PrefabPath = "Assets/_Project/Prefabs/Reto2_Basura.prefab";
    const string SquarePath = "Assets/_Project/Art/Placeholders/Square.png";

    // Donde estaba el reto provisional de las canecas (junto a Aulas 1)
    // (el mapa quedó al 60 %: 34,5 x 0,6 = 20,7 y -37,13 x 0,6 = -22,28; ahí está ahora la caneca provisional)
    static readonly Vector2 CampusPosition = new Vector2(20.7f, -22.28f);
    const int StudentSheet = 13;   // hoja de Valentina (NpcSpriteSlicer)

    [MenuItem("DigitatonGame/Agregar reto 2 (basura) al Campus")]
    public static void AddToCampus()
    {
        CampusBuilder.EnsureSprites();
        if (NpcSpriteSlicer.Load(StudentSheet).Length < 12) NpcSpriteSlicer.SliceAll();
        var prefab = BuildPrefab();

        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Campus.unity", OpenSceneMode.Additive);

        // Quita el reto provisional de las canecas (pregunta 7) y a su Valentina: el reto 2 los reemplaza
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name == "Canecas (Question 7)" || t.name == "Valentina (canecas)") Object.DestroyImmediate(t.gameObject);
            }
        }

        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<LitterChallenge>(true) != null) exists = true;

        if (!exists)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = CampusPosition;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log(exists ? "Campus: el reto 2 ya estaba colocado; solo se actualizó el prefab." : "Campus: reto 2 (basura) agregado en el lugar del reto provisional de las canecas.");
    }

    static GameObject BuildPrefab()
    {
        var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);

        // Se arma en una escena temporal para no ensuciar la escena abierta
        var previous = SceneManager.GetActiveScene();
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temp);

        var rootGo = new GameObject("Reto2_Basura");

        // Caneca
        var bin = GreyboxBuilder.MakeBox("Caneca de reciclaje", PlaceholderKind.Interactive, Vector2.zero, new Vector2(1.6f, 1.2f), square,
            "Caneca de reciclaje junto a Aulas 1 (sprite de caneca verde, vista 3/4). Se ve más limpia al completar el reto 2.", null, false, false, 0, true);
        Tint(bin, new Color(0.55f, 0.58f, 0.6f));
        bin.GetComponent<PlaceholderInfo>().labelFontSize = 4f;
        bin.transform.SetParent(rootGo.transform, false);
        bin.transform.localPosition = Vector3.zero;

        // Estudiante (Valentina)
        var student = GreyboxBuilder.MakeBox("Valentina (reto 2)", PlaceholderKind.NPC, Vector2.zero, new Vector2(1f, 2f), square,
            "Sprite de Valentina, estudiante que tira botellas (NPC, vista 3/4, 16x32 px). Animaciones: caminar, tirar, recoger.", new Vector2Int(16, 32), false, false, 0, true);
        // Sprite real de Valentina: pivote en los pies, así que el transform queda a la altura de los pies
        var frames = NpcSpriteSlicer.Load(StudentSheet);
        var studentInfo = student.GetComponent<PlaceholderInfo>();
        if (frames.Length >= 12)
        {
            var studentRenderer = student.GetComponent<SpriteRenderer>();
            studentRenderer.sprite = frames[1];
            studentRenderer.color = Color.white;
            student.transform.localScale = Vector3.one;
            studentInfo.applyKindColor = false;
            studentInfo.showLabel = false;
        }
        studentInfo.labelFontSize = 5f;
        var col = student.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 0.7f);
        col.offset = new Vector2(0f, 0.35f);   // colisiona solo en los pies
        student.transform.SetParent(rootGo.transform, false);
        student.transform.localPosition = new Vector3(3.25f, -2.4f, 0f);

        // Botellas: 3 iniciales y 3 que aparecen si la frase empeora el desorden
        Vector3[] spots =
        {
            new Vector3(1.8f, -0.9f), new Vector3(2.4f, -1.9f), new Vector3(1.3f, -1.8f),
            new Vector3(0.4f, -2.3f), new Vector3(3.0f, -0.2f), new Vector3(-0.9f, -1.2f),
        };
        var bottles = new Transform[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            var bottle = GreyboxBuilder.MakeBox("Botella " + (i + 1), PlaceholderKind.Interactive, Vector2.zero, new Vector2(0.4f, 0.7f), square,
                "Botella de plástico en el suelo (sprite pequeño 6x11 px, vista 3/4).", null, false, false, 0, false);
            Tint(bottle, new Color(0.6f, 0.85f, 1f));
            bottle.transform.SetParent(rootGo.transform, false);
            bottle.transform.localPosition = spots[i];
            bottles[i] = bottle.transform;
        }

        // Icono "!" sobre la cabeza: avisa que esa estudiante tiene un reto
        var iconGo = new GameObject("Icono de reto");
        iconGo.transform.SetParent(student.transform, false);
        iconGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);   // sobre la cabeza (el sprite mide 2 u desde los pies)
        var text = iconGo.AddComponent<TextMeshPro>();
        text.text = "!";
        text.fontSize = 9f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.9f, 0.25f);
        text.sortingOrder = 1500;
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
        iconGo.AddComponent<QuestIcon>();

        // El reto vive en la estudiante (es lo que se toca con E)
        var challenge = student.AddComponent<LitterChallenge>();
        challenge.prompt = "Press E to talk";
        challenge.student = student.transform;
        challenge.bin = bin.transform;
        challenge.bottles = bottles;
        challenge.icon = iconGo;

        var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
        Object.DestroyImmediate(rootGo);

        SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(temp, true);
        return prefab;
    }

    static void Tint(GameObject go, Color color)
    {
        go.GetComponent<PlaceholderInfo>().applyKindColor = false;
        go.GetComponent<SpriteRenderer>().color = color;
    }
}
