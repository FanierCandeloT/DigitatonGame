using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reduce (o amplía) la escala del MAPA de todas las escenas sin tocar el tamaño de los personajes, puertas ni objetos con sprite.
/// Escala: posiciones de todo, y el tamaño de pisos, caminos, muros (solo su largo), edificios, ágora, límites de cámara y objetos
/// de greybox sin sprite. No escala: Andrés, NPC, puertas, spawns, puntos de interacción pequeños ni muebles con sprite propio.
/// Se puede repetir: guarda el factor actual en un objeto "Escala del mapa (xN)" por escena y solo aplica la diferencia.
/// Después de escalar se vuelve a ejecutar la decoración (EscuelaDecorator.Run), que se adapta al nuevo tamaño.
/// Menú: DigitatonGame ▸ Reducir tamaño del mapa.
/// </summary>
public static class MapScaleTool
{
    /// <summary>Factor final del mapa respecto al greybox original (0,6 = 60 % del tamaño).</summary>
    public const float Factor = 0.6f;

    const string MarkerPrefix = "Escala del mapa (x";
    static readonly string[] Scenes =
    {
        "Campus", "Aulas1", "Aulas2", "Aulas3", "Aulas4", "Sotanos", "Bienestar",
        "Cafeteria", "SalaProfesores", "TechoAulas3", "MuseoLili",
    };

    [MenuItem("DigitatonGame/Reducir tamaño del mapa")]
    public static void Run()
    {
        if (SceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.SaveOpenScenes();

        foreach (var name in Scenes)
        {
            var path = $"Assets/_Project/Scenes/{name}.unity";
            if (!System.IO.File.Exists(path)) continue;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            float current = Read(sc);
            float ratio = Factor / current;
            if (Mathf.Abs(ratio - 1f) < 0.001f) { Debug.Log($"[Mapa] {name}: ya está a x{Factor}."); continue; }

            EscuelaDecorExtra.ClearPrevious(sc);   // la decoración se rehace después con el nuevo tamaño
            int n = ScaleScene(sc, ratio);
            Write(sc, Factor);
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            Debug.Log($"[Mapa] {name}: x{ratio:0.###} aplicado a {n} objetos (factor total x{Factor}).");
        }

        EscuelaDecorator.Run();   // vuelve a decorar con el tamaño nuevo
    }

    // ───────────────────────── Marca de escala ─────────────────────────

    /// <summary>Factor con el que está la escena respecto al greybox original (1 si nunca se escaló).</summary>
    public static float Read(Scene sc)
    {
        var marker = sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith(MarkerPrefix));
        if (marker == null) return 1f;
        var text = marker.name.Substring(MarkerPrefix.Length).TrimEnd(')');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f > 0.01f ? f : 1f;
    }

    static void Write(Scene sc, float factor)
    {
        foreach (var old in sc.GetRootGameObjects().Where(g => g.name.StartsWith(MarkerPrefix)).ToArray()) Object.DestroyImmediate(old);
        var go = new GameObject(MarkerPrefix + factor.ToString("0.###", CultureInfo.InvariantCulture) + ")");
        SceneManager.MoveGameObjectToScene(go, sc);
    }

    // ───────────────────────── Escalado ─────────────────────────

    static int ScaleScene(Scene sc, float r)
    {
        int n = 0;
        foreach (var root in sc.GetRootGameObjects())
        {
            if (root.name.StartsWith(MarkerPrefix) || root.GetComponentInChildren<Camera>(true) != null || root.GetComponent<Canvas>() != null) continue;

            n += ScaleObject(root.transform, r);
            // Contenedores vacíos (p. ej. "Palmas del Ágora"): sus hijos también se reubican
            if (IsContainer(root.transform))
                foreach (Transform child in root.transform) n += ScaleObject(child, r);
        }
        return n;
    }

    /// <summary>Grupo sin dibujo ni colisión: solo agrupa hijos.</summary>
    static bool IsContainer(Transform t) =>
        t.childCount > 0 && t.GetComponent<Renderer>() == null && t.GetComponent<Collider2D>() == null &&
        t.GetComponents<Component>().All(c => c is Transform || c is PlaceholderInfo);

    static int ScaleObject(Transform t, float r)
    {
        var go = t.gameObject;
        t.localPosition = new Vector3(t.localPosition.x * r, t.localPosition.y * r, t.localPosition.z);

        var sr = go.GetComponent<SpriteRenderer>();
        var box = go.GetComponent<BoxCollider2D>();

        // Límites de cámara y disparador circular del Ágora
        if (go.GetComponent<MapBounds>() != null && box != null) { box.size *= r; return 1; }
        if (go.GetComponent<AgoraTrigger>() != null && go.TryGetComponent(out CircleCollider2D circle)) { circle.radius *= r; return 1; }

        // Personajes, puertas, spawns y puntos de interacción pequeños: solo se mueven
        bool character = go.GetComponent<PlayerController>() != null || go.GetComponent<ToastInteractable>() != null ||
                         go.GetComponent<NpcChallengeInteractable>() != null || go.GetComponent<ClassLessonInteractable>() != null ||
                         go.GetComponent<Door>() != null || go.GetComponent<SpawnPoint>() != null;
        bool challenge = go.GetComponent<ChallengeInteractable>() != null;
        if (character) return 1;
        if (challenge)
        {
            // Zonas de reto grandes (banda, puzzle) se reducen; los marcadores pequeños (candado, canecas) se dejan
            if (box != null && Mathf.Max(box.size.x, box.size.y) > 3f)
            {
                box.size *= r;
                if (sr != null && sr.drawMode != SpriteDrawMode.Simple) sr.size *= r;
            }
            return 1;
        }

        // El pasto del campus: se rehace en trozos al decorar
        if (go.name == "Zonas verdes del campus" && sr != null)
        {
            var chunks = t.Cast<Transform>().Where(c => c.name == "Trozo de pasto").Select(c => c.GetComponent<SpriteRenderer>()).Where(s => s != null).ToArray();
            Vector2 size = sr.size;
            if (chunks.Length > 0)
            {
                var b = chunks[0].bounds;
                foreach (var c in chunks) b.Encapsulate(c.bounds);
                size = b.size;
                foreach (var c in chunks) Object.DestroyImmediate(c.gameObject);
            }
            t.localScale = Vector3.one;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size * r;
            sr.enabled = true;
            return 1;
        }

        if (sr == null) return 1;

        // ¿El dibujo se estira con la escala del transform o con 'size'?
        bool sizeBased = sr.drawMode != SpriteDrawMode.Simple;
        string sprite = sr.sprite != null ? sr.sprite.name : "";
        bool shapeSprite = sprite == "Circle" || sprite == "CircleBase" || sprite == "Square" || sprite == "SquareBase";
        bool layoutFloor = sr.sortingOrder <= -75;
        bool board = go.name.StartsWith("Tablero");

        if (!sizeBased)
        {
            // Formas del greybox, caminos escalados por transform y tableros estirados: se escalan; el resto (muebles con sprite) no
            if (shapeSprite || layoutFloor || board)
                t.localScale = new Vector3(t.localScale.x * r, t.localScale.y * r, t.localScale.z);
            return 1;
        }

        bool scaledTransform = Mathf.Abs(t.localScale.x - 1f) > 0.01f || Mathf.Abs(t.localScale.y - 1f) > 0.01f;
        if (scaledTransform)
        {
            t.localScale = new Vector3(t.localScale.x * r, t.localScale.y * r, t.localScale.z);
            return 1;
        }

        // Edificio: la colisión es solo la franja de la base (2 u de fondo): se escala el ancho y no el fondo
        bool building = box != null && !box.isTrigger && Mathf.Approximately(box.size.y, 2f) && Mathf.Approximately(box.offset.y, 1f);
        // Muro: delgado y largo; solo se escala el largo, para que siga siendo igual de grueso
        Vector2 baseSize = sr.size;
        float thin = box != null ? Mathf.Min(box.size.x, box.size.y) : 0f, longSide = box != null ? Mathf.Max(box.size.x, box.size.y) : 0f;
        bool wall = !building && box != null && !box.isTrigger && ((thin <= 0.6f && longSide > thin * 2f) || (thin <= 2f && longSide >= 6f));

        if (building)
        {
            sr.size = new Vector2(baseSize.x * r, baseSize.y * r);
            box.size = new Vector2(box.size.x * r, box.size.y);
        }
        else if (wall)
        {
            bool horizontal = baseSize.x >= baseSize.y;
            sr.size = horizontal ? new Vector2(baseSize.x * r, baseSize.y) : new Vector2(baseSize.x, baseSize.y * r);
            box.size = horizontal ? new Vector2(box.size.x * r, box.size.y) : new Vector2(box.size.x, box.size.y * r);
        }
        else
        {
            sr.size = baseSize * r;
            if (box != null) { box.size *= r; box.offset *= r; }
        }
        return 1;
    }
}
