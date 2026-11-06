using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Convierte el marcador provisional del techo de Aulas 3 ("Puzzle de bloques (Question 8)") en el reto final de los paneles
/// solares, sin reconstruir la escena: quita el marcador, agrega SolarPuzzleChallenge, el icono "!" y conecta las filas de paneles
/// (una fila por circuito, de la más cercana a la caja de control a la más lejana). Es repetible.
/// </summary>
public static class SolarPuzzleBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/TechoAulas3.unity";
    const string FinalName = "Caja de control solar (reto final)";

    [MenuItem("DigitatonGame/Agregar reto final (paneles solares) a TechoAulas3")]
    public static void AddToRoof()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameObject box = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.StartsWith("Puzzle de bloques") || root.name == FinalName) box = root;

        if (box == null)
        {
            Debug.LogError("TechoAulas3: no se encontró el marcador del puzzle ('Puzzle de bloques (Question 8)').");
            EditorSceneManager.CloseScene(scene, true);
            return;
        }

        var stub = box.GetComponent<ChallengeInteractable>();
        if (stub != null) Object.DestroyImmediate(stub);

        var puzzle = box.GetComponent<SolarPuzzleChallenge>();
        if (puzzle == null) puzzle = box.AddComponent<SolarPuzzleChallenge>();
        puzzle.prompt = "Press E to use the control box";
        puzzle.challengeId = BlackoutDirector.ChallengeId;
        puzzle.memoryReward = 2;

        // Filas de paneles: se agrupan por altura; el primer circuito es la fila más al sur (la más cercana a la escalera)
        var panels = scene.GetRootGameObjects()
            .Where(g => g.name == "Panel solar" && g.GetComponent<SpriteRenderer>() != null)
            .Select(g => g.GetComponent<SpriteRenderer>())
            .ToList();
        puzzle.rows = panels
            .GroupBy(p => Mathf.Round(p.transform.position.y * 2f) / 2f)
            .OrderBy(g => g.Key)
            .Select(g => new SolarPuzzleChallenge.PanelRow { panels = g.OrderBy(p => p.transform.position.x).ToArray() })
            .ToArray();

        // Icono "!" (se enciende solo durante el apagón)
        var iconTransform = box.transform.Find("Icono de reto");
        GameObject icon;
        if (iconTransform != null) icon = iconTransform.gameObject;
        else
        {
            icon = new GameObject("Icono de reto");
            icon.transform.SetParent(box.transform, false);
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
        puzzle.icon = icon;
        icon.SetActive(false);   // el script lo enciende cuando empieza el apagón

        if (box.TryGetComponent(out PlaceholderInfo info))
        {
            info.areaName = FinalName;
            info.finalAssetDescription = "Caja de control de los paneles solares del techo de Aulas 3 (reto final: reconectar los 3 circuitos con frases en voz pasiva).";
        }
        box.name = FinalName;

        EditorUtility.SetDirty(box);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"TechoAulas3: reto final listo ({puzzle.rows.Length} circuitos, {panels.Count} paneles).");
    }
}
