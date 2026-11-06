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
            if (HasRealSprite(info)) return;   // ya tiene sprite final: sale de la tabla
            var r = new Row
            {
                scene = scene,
                name = Clean(info.areaName),
                description = Clean(info.finalAssetDescription),
                // Los tamaños se guardaron a 16 px por unidad; con 48 PPU cada unidad son 48 px (x3).
                size = $"{info.suggestedSizePx.x * 3}x{info.suggestedSizePx.y * 3}",
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
        sb.AppendLine("Tabla de todos los placeholders del greybox (objetos con `PlaceholderInfo`): qué asset final los reemplazará, su tamaño sugerido en píxeles (48 píxeles = 1 casilla = 1 unidad; Andrés mide 1x2 casillas) y quién es responsable.");
        sb.AppendLine("Se genera sola con el menú `DigitatonGame ▸ Generar PLACEHOLDERS.md`; no se edita a mano (los cambios se hacen en el campo `PlaceholderInfo` del objeto y se vuelve a generar).");
        sb.AppendLine();
        sb.AppendLine("**Leyenda de colores:** blanco jugador · azul aulas · morado administrativos/servicios · rojo salud · naranja comida · verde zonas verdes · beige caminos · gris oscuro calles/parqueaderos · negro muros y bordes · amarillo puertas/entradas · rosado NPC · cian objetos interactivos y retos.");
        sb.AppendLine();
        sb.AppendLine("Los objetos idénticos de una misma escena se agrupan en una sola fila con `(×N)`. El responsable por defecto es Integrante 1 hasta que se reparta el trabajo.");
        sb.AppendLine("**Solo aparece lo que sigue en greybox:** lo que ya usa un sprite real (edificios del campus, Ágora, Edificio Central, árboles, caminos, puertas y muebles con sprite, Andrés) no está en la tabla.");
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

    static readonly System.Collections.Generic.HashSet<string> PlaceholderSprites =
        new System.Collections.Generic.HashSet<string> { "Square", "SquareBase", "Circle", "CircleBase" };

    /// <summary>True si el objeto ya usa un sprite real (no el cuadrado/círculo de color del greybox).</summary>
    static bool HasRealSprite(PlaceholderInfo info)
    {
        if (!info.TryGetComponent(out SpriteRenderer sr) || sr.sprite == null) return false;
        return !PlaceholderSprites.Contains(sr.sprite.name);
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
        return s.Replace("16x32 px", "48x96 px").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
