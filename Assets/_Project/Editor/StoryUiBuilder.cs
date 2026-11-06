using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agrega al prefab del GameManager la UI de la historia: mensaje "Coming soon" (toast), texto sobre negro,
/// contador "Memories: X/12" y pantalla de resultados. Es idempotente: solo agrega lo que falta.
/// Menú: DigitatonGame ▸ Agregar UI de historia al GameManager.
/// </summary>
public static class StoryUiBuilder
{
    const string PrefabPath = "Assets/_Project/Resources/GameManager.prefab";

    [MenuItem("DigitatonGame/Agregar UI de historia al GameManager")]
    public static void EnsureAll()
    {
        if (!File.Exists(PrefabPath)) return;
        CampusBuilder.EnsureToast();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var gm = root.GetComponent<GameManager>();

        // 1) Texto centrado sobre el negro del fundido
        var fader = root.GetComponentInChildren<ScreenFader>(true);
        if (fader.caption == null)
        {
            var cap = MenuFlowBuilder.NewText("Caption", fader.transform, "", 56, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuFlowBuilder.Stretch(cap.rectTransform);
            cap.rectTransform.offsetMin = new Vector2(80, 80);
            cap.rectTransform.offsetMax = new Vector2(-80, -80);
            cap.gameObject.SetActive(false);
            fader.caption = cap;
        }

        // 2) Contador de recuerdos (arriba a la derecha)
        if (root.GetComponentInChildren<MemoryHud>(true) == null)
        {
            var canvas = MenuFlowBuilder.MakeCanvas("HudCanvas", 50, raycaster: false);
            canvas.transform.SetParent(root.transform, false);

            var panel = MenuFlowBuilder.NewImage("MemoriesPanel", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
            panel.raycastTarget = false;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 1f);
            prt.sizeDelta = new Vector2(330, 56);
            prt.anchoredPosition = new Vector2(-20, -20);

            var label = MenuFlowBuilder.NewText("Label", panel.transform, "Memories: 0/12", 34, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuFlowBuilder.Stretch(label.rectTransform);
            label.color = new Color(1f, 0.92f, 0.45f);

            var hud = canvas.AddComponent<MemoryHud>();
            hud.label = label;
            hud.root = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        // 3) Pantalla de resultados
        var results = root.GetComponentInChildren<ResultsPanel>(true);
        if (results == null)
        {
            // La pantalla de resultados se arma por código al mostrarla (ResultsPanel); aquí solo hace falta el componente
            var holder = new GameObject("ResultsPanel");
            holder.transform.SetParent(root.transform, false);
            results = holder.AddComponent<ResultsPanel>();
        }

        var so = new SerializedObject(gm);
        so.FindProperty("results").objectReferenceValue = results;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
