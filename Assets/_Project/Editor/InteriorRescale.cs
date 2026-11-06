using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reduce los interiores a la escala de 48 PPU (más compactos y proporcionados con Andrés) EN SU LUGAR,
/// sin regenerar nada: escala las posiciones de todos los objetos y el largo de pisos, muros y piezas largas.
/// Los muebles, puertas, NPC y objetos de retos conservan su tamaño (Andrés mide 1x2 casillas).
/// </summary>
public static class InteriorRescale
{
    static readonly string[] Skip = { "Andres", "CameraRig", "Global Light 2D" };

    public static string Run(float factor, params string[] sceneNames)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var name in sceneNames)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{name}.unity", OpenSceneMode.Single);
            int moved = 0, resized = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (Skip.Contains(go.name)) continue;

                var p = go.transform.position;
                go.transform.position = new Vector3(p.x * factor, p.y * factor, p.z);
                moved++;

                if (go.TryGetComponent(out MapBounds _) && go.TryGetComponent(out BoxCollider2D bounds))
                {
                    bounds.size *= factor;
                    continue;
                }

                if (!go.TryGetComponent(out SpriteRenderer sr) || sr.drawMode == SpriteDrawMode.Simple) continue;

                var info = go.GetComponent<PlaceholderInfo>();
                var old = sr.size;
                var ns = old;
                bool floorLike = sr.sortingOrder <= -50;
                bool isWall = info != null && info.kind == PlaceholderKind.Wall;
                bool keepSize = info != null && (info.kind == PlaceholderKind.Door || info.kind == PlaceholderKind.NPC || info.kind == PlaceholderKind.Interactive);

                if (floorLike) ns = old * factor;
                else if (isWall) { if (old.x >= old.y) ns.x *= factor; else ns.y *= factor; }
                else if (!keepSize)
                {
                    if (old.x >= old.y) { if (old.x > 4f) ns.x *= factor; }
                    else if (old.y > 4f) ns.y *= factor;
                }

                if (ns != old)
                {
                    sr.size = ns;
                    if (go.TryGetComponent(out BoxCollider2D col) && Vector2.Distance(col.size, old) < 0.05f) col.size = ns;
                    resized++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            lines.Add($"{name}: {moved} movidos, {resized} redimensionados");
        }
        return string.Join(" | ", lines);
    }
}
