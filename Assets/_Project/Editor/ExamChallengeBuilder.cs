using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Convierte la pila de exámenes de SalaProfesores (antes un reto provisional "Questions 9-11") en el reto 3 real,
/// sin reconstruir la escena: quita el marcador provisional, agrega ExamChallenge y el icono "!".
/// </summary>
public static class ExamChallengeBuilder
{
    [MenuItem("DigitatonGame/Agregar reto 3 (exámenes) a SalaProfesores")]
    public static void AddToTeachersRoom()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SalaProfesores.unity", OpenSceneMode.Additive);

        GameObject pile = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("Pila de exámenes")) pile = root;

        if (pile == null)
        {
            Debug.LogError("SalaProfesores: no se encontró la 'Pila de exámenes'.");
            EditorSceneManager.CloseScene(scene, true);
            return;
        }

        var stub = pile.GetComponent<ChallengeInteractable>();
        if (stub != null) Object.DestroyImmediate(stub);

        var exam = pile.GetComponent<ExamChallenge>();
        if (exam == null) exam = pile.AddComponent<ExamChallenge>();
        exam.prompt = "Press E to grade the exams";
        exam.challengeId = "r3_exams";
        exam.memoryReward = 2;

        var iconTransform = pile.transform.Find("Icono de reto");
        GameObject icon;
        if (iconTransform != null) icon = iconTransform.gameObject;
        else
        {
            icon = new GameObject("Icono de reto");
            icon.transform.SetParent(pile.transform, false);
            icon.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var text = icon.AddComponent<TextMeshPro>();
            text.text = "!";
            text.fontSize = 9f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.9f, 0.25f);
            text.sortingOrder = 1500;
            text.rectTransform.sizeDelta = new Vector2(2f, 2f);
            icon.AddComponent<QuestIcon>();
        }
        exam.icon = icon;
        pile.name = "Pila de exámenes (reto 3)";

        EditorUtility.SetDirty(pile);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log("SalaProfesores: reto 3 (exámenes) colocado en la pila de exámenes.");
    }
}
