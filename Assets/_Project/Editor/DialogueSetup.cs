using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Crea la carpeta Assets/_Project/Data/Dialogues con los diálogos de prueba (Lucía, Don Jairo, Doña Marta) y los asigna a esos NPC.
/// Sin menús: se llama desde código y es repetible (no pisa los textos de un diálogo que ya existe).
/// Para escribir uno nuevo: Assets ▸ Create ▸ DigitatonGame ▸ Dialogue, y arrastrarlo al campo "Dialogue" del NPC.
/// </summary>
public static class DialogueSetup
{
    public const string Dir = "Assets/_Project/Data/Dialogues";

    public static string CreateTestAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data")) AssetDatabase.CreateFolder("Assets/_Project", "Data");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Dialogues");

        var log = new List<string>();
        Make("Lucia_Test", log,
            new DialogueLine("Lucía", "Welcome back! How are you feeling today?"),
            new DialogueLine("Andrés",
                "Better, thank you. I keep remembering new things.",
                "{Better}, gracias. Sigo {remembering} cosas nuevas.",
                "Mejor, gracias. Sigo recordando cosas nuevas."),
            new DialogueLine("Lucía", "Good. Explore the campus: every place can help you remember something."));

        Make("DonJairo_Test", log,
            new DialogueLine("Don Jairo", "Good morning! Welcome to the university."),
            new DialogueLine("Don Jairo", "This padlock has been stuck for days. Nobody knows who closed it."),
            new DialogueLine("Andrés",
                "Strange... Someone must have done it.",
                "{Strange}... alguien lo {must have done}.",
                "Qué raro... alguien tuvo que hacerlo."));

        Make("DonaMarta_Test", log,
            new DialogueLine("Doña Marta", "Welcome to the cafeteria! The empanadas were made this morning."),
            new DialogueLine("Doña Marta", "The coffee is served hot, and the juice is made fresh every day."),
            new DialogueLine("Doña Marta", "Take a seat and enjoy your meal."));

        AssetDatabase.SaveAssets();
        return string.Join(", ", log);
    }

    static void Make(string name, List<string> log, params DialogueLine[] lines)
    {
        var path = $"{Dir}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            asset.lines = lines;
            AssetDatabase.CreateAsset(asset, path);
            log.Add($"{name} creado ({lines.Length} líneas)");
        }
        else log.Add($"{name} ya existía (no se pisó)");
    }

    static DialogueAsset Load(string name) => AssetDatabase.LoadAssetAtPath<DialogueAsset>($"{Dir}/{name}.asset");

    /// <summary>Asigna el diálogo a Don Jairo (Campus), Doña Marta (Cafetería) y Lucía (Bienestar).</summary>
    public static string AssignToNpcs()
    {
        var log = new List<string>();
        log.Add(Assign("Campus", "Don Jairo", "DonJairo_Test"));
        log.Add(Assign("Cafeteria", "Doña Marta", "DonaMarta_Test"));
        log.Add(Assign("Bienestar", "Lucía", "Lucia_Test"));
        return string.Join(" | ", log);
    }

    static string Assign(string sceneName, string npcName, string assetName)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{sceneName}.unity", OpenSceneMode.Single);
        var asset = Load(assetName);   // se carga DESPUÉS de abrir la escena (al cambiar de escena se descargan los assets sueltos)
        GameObject npc = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == npcName) npc = g;
        if (npc == null) return $"{sceneName}: no se encontró '{npcName}'";

        if (npc.TryGetComponent(out ToastInteractable toast)) toast.dialogue = asset;
        else if (npc.TryGetComponent(out NpcChallengeInteractable challenge)) challenge.doneDialogue = asset;
        else return $"{sceneName}: '{npcName}' no es un NPC con diálogo";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{sceneName}/{npcName} <- {asset.name}";
    }
}
