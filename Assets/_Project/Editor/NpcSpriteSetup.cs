using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pone los sprites recortados (NpcSpriteSlicer) a los NPC de las escenas, sin reconstruirlas: cambia el sprite, mantiene
/// los pies donde estaban y deja el colisionador solo en los pies. Lucía ya usa la hoja de la enfermera (EscuelaDecorExtra).
/// </summary>
public static class NpcSpriteSetup
{
    // escena, nombre del objeto, hoja del personaje
    static readonly (string scene, string npc, int sheet)[] Assignments =
    {
        ("Aulas2", "Valentina", 13),
        ("Aulas2", "Santiago", 1),
        ("Aulas2", "Estudiante", 0),
        ("Campus", "Don Jairo", 7),
        ("Cafeteria", "Doña Marta", 17),
    };

    [MenuItem("DigitatonGame/Poner sprites a los NPC")]
    public static void Apply()
    {
        if (NpcSpriteSlicer.Load(1).Length < 12) NpcSpriteSlicer.SliceAll();

        int changed = 0;
        foreach (var group in new[] { "Aulas2", "Campus", "Cafeteria" })
        {
            var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{group}.unity", OpenSceneMode.Additive);
            foreach (var (sceneName, npcName, sheet) in Assignments)
            {
                if (sceneName != group) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name != npcName || t.GetComponent<ToastInteractable>() == null) continue;
                        changed += Apply(t.gameObject, sheet) ? 1 : 0;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log($"NPC: {changed} personajes con sprite recortado.");
    }

    /// <summary>Pone la hoja recortada a un NPC ya creado (sprite quieto, pies en su sitio, colisión solo en los pies).</summary>
    public static bool Apply(GameObject npc, int sheet)
    {
        var frames = NpcSpriteSlicer.Load(sheet);
        if (frames.Length < 12 || !npc.TryGetComponent(out SpriteRenderer sr)) return false;

        float oldFeet = sr.sprite != null ? sr.bounds.min.y : npc.transform.position.y - 1f;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sprite = frames[1];   // quieto, mirando hacia abajo
        sr.color = Color.white;
        npc.transform.localScale = Vector3.one;

        var p = npc.transform.position;
        npc.transform.position = new Vector3(p.x, p.y + (oldFeet - sr.bounds.min.y), p.z);

        if (npc.TryGetComponent(out PlaceholderInfo info))
        {
            info.applyKindColor = false;
            info.showLabel = false;
        }
        if (npc.TryGetComponent(out BoxCollider2D col))
        {
            col.size = new Vector2(0.9f, 0.7f);
            col.offset = new Vector2(0f, 0.35f);   // colisiona solo en los pies
        }
        EditorUtility.SetDirty(npc);
        return true;
    }
}
