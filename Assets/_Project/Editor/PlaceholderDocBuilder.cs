using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Genera Docs/PLACEHOLDERS.md con una tabla de todos los PlaceholderInfo del proyecto
/// (escenas del Build Profile y prefabs): escena, nombre, descripción del asset final, tamaño sugerido y responsable.
/// Menú: DigitatonGame ▸ Generar PLACEHOLDERS.md
/// </summary>
public static class PlaceholderDocBuilder
{
    const string OutputPath = "Docs/PLACEHOLDERS.md";

    struct Row
    {
        public string scene, name, description, size, owner;
        public string Key => scene + "|" + name + "|" + description + "|" + size + "|" + owner;
    }

    [MenuItem("DigitatonGame/Generar PLACEHOLDERS.md")]
    public static void Generate()
    {
        var rows = new List<Row>();
        var counts = new Dictionary<string, int>();
        var order = new List<string>();

        void Add(string scene, PlaceholderInfo info)
        {
            var r = new Row
            {
                scene = scene,
                name = Clean(info.areaName),
                description = Clean(info.finalAssetDescription),
                size = $"{info.suggestedSizePx.x}x{info.suggestedSizePx.y}",
                owner = OwnerLabel(info.owner),
            };
            if (counts.ContainsKey(r.Key)) { counts[r.Key]++; return; }
            counts[r.Key] = 1;
            order.Add(r.Key);
            rows.Add(r);
        }

        // Escenas del Build Profile (en su orden)
        var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            var name = Path.GetFileNameWithoutExtension(s.path);
            EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            var infos = new List<PlaceholderInfo>(Object.FindObjectsByType<PlaceholderInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            infos.Sort((a, b) => string.Compare(a.areaName, b.areaName, System.StringComparison.OrdinalIgnoreCase));
            foreach (var info in infos) Add(name, info);
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

        // Prefabs
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var info in prefab.GetComponentsInChildren<PlaceholderInfo>(true))
                Add("(Prefab) " + Path.GetFileNameWithoutExtension(path), info);
        }

        // Escritura
        var sb = new StringBuilder();
        sb.AppendLine("# Placeholders del proyecto");
        sb.AppendLine();
        sb.AppendLine("Tabla de todos los placeholders del greybox (objetos con `PlaceholderInfo`): qué asset final los reemplazará, su tamaño sugerido en píxeles (16 píxeles = 1 unidad) y quién es responsable.");
        sb.AppendLine("Se genera sola con el menú `DigitatonGame ▸ Generar PLACEHOLDERS.md`; no se edita a mano (los cambios se hacen en el campo `PlaceholderInfo` del objeto y se vuelve a generar).");
        sb.AppendLine();
        sb.AppendLine("**Leyenda de colores:** blanco jugador · azul aulas · morado administrativos/servicios · rojo salud · naranja comida · verde zonas verdes · beige caminos · gris oscuro calles/parqueaderos · negro muros y bordes · amarillo puertas/entradas · rosado NPC · cian objetos interactivos y retos.");
        sb.AppendLine();
        sb.AppendLine("Los objetos idénticos de una misma escena se agrupan en una sola fila con `(×N)`. El responsable por defecto es Integrante 1 hasta que se reparta el trabajo.");
        sb.AppendLine();
        sb.AppendLine($"Total: {rows.Count} filas ({TotalObjects(counts)} objetos).");
        sb.AppendLine();
        sb.AppendLine("| Escena | Nombre | Descripción del asset final | Tamaño sugerido (px) | Responsable |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in rows)
        {
            int n = counts[r.Key];
            var name = n > 1 ? $"{r.name} (×{n})" : r.name;
            sb.AppendLine($"| {r.scene} | {name} | {r.description} | {r.size} | {r.owner} |");
        }

        Directory.CreateDirectory("Docs");
        File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"{OutputPath} generado: {rows.Count} filas ({TotalObjects(counts)} objetos).");
    }

    static int TotalObjects(Dictionary<string, int> counts)
    {
        int t = 0;
        foreach (var c in counts.Values) t += c;
        return t;
    }

    static string OwnerLabel(PlaceholderOwner o)
    {
        switch (o)
        {
            case PlaceholderOwner.Integrante1: return "Integrante 1";
            case PlaceholderOwner.Integrante2: return "Integrante 2";
            default: return "Integrante 3";
        }
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "—";
        return s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
