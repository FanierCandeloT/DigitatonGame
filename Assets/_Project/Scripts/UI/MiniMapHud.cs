using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mapa del campus en la esquina superior izquierda. Lucía se lo da a Andrés junto con el cuaderno (mismo flag, ver
/// NotebookPanel.GiveToPlayer) y marca con una "!" hacia dónde ir para el siguiente reto (el mismo que muestra StoryFlow).
///
/// - La imagen del mapa NO es un arte aparte: al entrar al Campus se fotografía la propia escena desde arriba con una cámara
///   ortográfica que cubre el MapBounds, así siempre coincide con lo que hay en el campus (edificios, caminos, árboles).
/// - Solo se ve en el Campus, con el cuaderno ya entregado (o en modo desarrollo), y se esconde durante diálogos, retos,
///   pausa, cuaderno y fundidos.
/// - Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena ni prefab).
/// </summary>
public class MiniMapHud : MonoBehaviour
{
    const string CampusScene = "Campus";
    const float MapWidth = 300f;             // ancho del mapa en unidades del canvas (1280x720 de referencia)
    const int TextureWidth = 640;            // resolución de la foto del campus
    const float RefreshSeconds = 0.5f;

    static readonly Color Frame = new Color(1f, 0.85f, 0.30f);
    static readonly Color Fill = new Color(0.06f, 0.08f, 0.16f, 0.94f);
    static readonly Color Background = new Color(0.05f, 0.07f, 0.12f, 1f);

    static MiniMapHud instance;

    GameObject panel;
    RectTransform mapRect, playerDot, objectiveMarker;
    TextMeshProUGUI legend;
    RawImage mapImage;
    RenderTexture texture;
    Camera snapshotCamera;

    Bounds worldBounds;
    bool hasMap;
    Transform player;
    Vector2? target;
    float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("MiniMapHud");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MiniMapHud>();
    }

    // ───────────────────────── Ciclo de vida ─────────────────────────

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        BuildUi();
        panel.SetActive(false);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameManager.ProgressChanged += OnProgressChanged;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameManager.ProgressChanged -= OnProgressChanged;
    }

    void Start() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

    void OnDestroy()
    {
        ReleaseTexture();
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        target = null;
        ReleaseTexture();
        hasMap = false;
        if (scene.name == CampusScene) StartCoroutine(CaptureWhenReady());
    }

    void OnProgressChanged() => nextRefresh = 0f;   // recalcula el destino en el siguiente cuadro

    void ReleaseTexture()
    {
        if (mapImage != null) mapImage.texture = null;
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }

    // ───────────────────────── Foto del campus ─────────────────────────

    IEnumerator CaptureWhenReady()
    {
        yield return null;   // espera a que todos los objetos de la escena hayan despertado
        yield return null;

        var bounds = FindAnyObjectByType<MapBounds>();
        var box = bounds != null ? bounds.GetComponent<BoxCollider2D>() : null;
        if (box == null)
        {
            Debug.LogWarning("[MiniMap] El Campus no tiene MapBounds: no se puede dibujar el mapa.");
            yield break;
        }

        worldBounds = box.bounds;
        if (!Capture(worldBounds)) yield break;

        // La imagen respeta la proporción del campus
        float height = Mathf.Clamp(MapWidth * worldBounds.size.y / Mathf.Max(worldBounds.size.x, 0.01f), 90f, 230f);
        ResizePanel(height);
        hasMap = true;
        RefreshTarget();
    }

    /// <summary>Fotografía (una sola vez) el área indicada desde arriba. Devuelve false si no se pudo.</summary>
    bool Capture(Bounds area)
    {
        int width = TextureWidth;
        int height = Mathf.Max(64, Mathf.RoundToInt(TextureWidth * area.size.y / Mathf.Max(area.size.x, 0.01f)));
        texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "MiniMap",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.Create();

        if (snapshotCamera == null)
        {
            var camGo = new GameObject("MiniMapCamera");
            camGo.transform.SetParent(transform, false);
            snapshotCamera = camGo.AddComponent<Camera>();
            snapshotCamera.enabled = false;               // solo renderiza cuando se le pide
            snapshotCamera.orthographic = true;
            snapshotCamera.clearFlags = CameraClearFlags.SolidColor;
            snapshotCamera.backgroundColor = Background;
            snapshotCamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
            snapshotCamera.nearClipPlane = 0.1f;
            snapshotCamera.farClipPlane = 100f;
        }
        snapshotCamera.orthographicSize = area.size.y / 2f;
        snapshotCamera.transform.position = new Vector3(area.center.x, area.center.y, -50f);
        snapshotCamera.targetTexture = texture;

        // Se esconde lo que se mueve o es solo de depuración (Andrés, peatones, rótulos, marcadores de retos)
        var hidden = new System.Collections.Generic.List<Renderer>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled) continue;
            bool hide = r is MeshRenderer ||
                        r.GetComponentInParent<PlayerController>() != null ||
                        r.GetComponentInParent<AmbientWalker>() != null ||
                        r.GetComponentInParent<ChallengeInteractable>() != null;
            if (!hide) continue;
            r.enabled = false;
            hidden.Add(r);
        }

        bool ok = true;
        // Con la luz normal aunque haya un apagón: el mapa debe poder leerse (BlackoutDirector vuelve a oscurecer al terminar)
        BlackoutDirector.WithNormalLight(() =>
        {
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (RenderPipeline.SupportsRenderRequest(snapshotCamera, request))
                    RenderPipeline.SubmitRenderRequest(snapshotCamera, request);
                else
                    snapshotCamera.Render();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MiniMap] No se pudo fotografiar el campus: {e.Message}");
                ok = false;
            }
            finally
            {
                foreach (var r in hidden) if (r != null) r.enabled = true;
                snapshotCamera.targetTexture = null;
            }
        });

        if (!ok) { ReleaseTexture(); return false; }
        mapImage.texture = texture;
        return true;
    }

    // ───────────────────────── Interfaz ─────────────────────────

    void BuildUi()
    {
        var canvas = PixelUi.NewCanvas("MiniMapCanvas", 90);
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;   // es solo informativo: no debe comerse los clics

        var border = PixelUi.NewPanel("Mapa", canvas.transform, Frame, Fill, out var fill);
        border.raycastTarget = false;
        panel = border.gameObject;
        PixelUi.Place(border.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(MapWidth + 24f, 160f));

        var title = PixelUi.NewLabel("Titulo", fill.transform, "CAMPUS MAP", 20f, Vector2.zero, new Vector2(MapWidth, 24f), FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = Frame;
        PixelUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(MapWidth, 24f));

        // Recuadro del mapa (la foto se acomoda dentro)
        var mapBorder = PixelUi.NewBox("MarcoMapa", fill.transform, new Color(0.30f, 0.34f, 0.46f));
        mapBorder.raycastTarget = false;
        PixelUi.Place(mapBorder.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -30f), new Vector2(MapWidth + 8f, 100f));

        var imageRect = PixelUi.NewRect("Foto", mapBorder.transform);
        mapImage = imageRect.gameObject.AddComponent<RawImage>();
        mapImage.raycastTarget = false;
        mapImage.color = Color.white;
        PixelUi.Stretch(imageRect);
        imageRect.offsetMin = new Vector2(4f, 4f);
        imageRect.offsetMax = new Vector2(-4f, -4f);
        mapRect = imageRect;

        // Los hijos se dibujan SOBRE el padre: cada marcador es un contenedor vacío con borde, relleno (y signo) como hermanos, en ese orden
        var dark = new Color(0.05f, 0.07f, 0.12f);

        // Andrés: punto blanco con borde oscuro
        playerDot = PixelUi.NewRect("Andres", mapRect);
        playerDot.sizeDelta = new Vector2(10f, 10f);
        AddOutlinedBox(playerDot, dark, Color.white, 2f);

        // Siguiente reto: cuadro amarillo con "!"
        objectiveMarker = PixelUi.NewRect("Destino", mapRect);
        objectiveMarker.sizeDelta = new Vector2(24f, 24f);
        AddOutlinedBox(objectiveMarker, dark, Frame, 3f);
        var bang = PixelUi.NewLabel("Signo", objectiveMarker, "!", 22f, Vector2.zero, new Vector2(24f, 24f), FontStyles.Bold);
        bang.color = new Color(0.12f, 0.08f, 0.02f);
        PixelUi.Stretch(bang.rectTransform);
        objectiveMarker.gameObject.SetActive(false);

        legend = PixelUi.NewLabel("Leyenda", fill.transform, "", 17f, Vector2.zero, new Vector2(MapWidth, 22f), FontStyles.Normal, TextAlignmentOptions.Left);
        legend.color = new Color(0.85f, 0.9f, 1f);
    }

    /// <summary>Dentro de un contenedor: una caja oscura (el borde, más grande) y encima el relleno de color.</summary>
    static void AddOutlinedBox(RectTransform parent, Color outline, Color fill, float border)
    {
        var back = PixelUi.NewBox("Borde", parent, outline);
        back.raycastTarget = false;
        PixelUi.Stretch(back.rectTransform);
        back.rectTransform.offsetMin = new Vector2(-border, -border);
        back.rectTransform.offsetMax = new Vector2(border, border);

        var front = PixelUi.NewBox("Relleno", parent, fill);
        front.raycastTarget = false;
        PixelUi.Stretch(front.rectTransform);
    }

    /// <summary>Ajusta el panel a la altura de la foto (título arriba, leyenda abajo).</summary>
    void ResizePanel(float mapHeight)
    {
        var border = (RectTransform)panel.transform;
        border.sizeDelta = new Vector2(MapWidth + 24f, mapHeight + 30f + 8f + 26f + 8f);

        var frame = (RectTransform)mapRect.parent;
        frame.sizeDelta = new Vector2(MapWidth + 8f, mapHeight + 8f);

        PixelUi.Place(legend.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 4f), new Vector2(MapWidth, 22f));
    }

    // ───────────────────────── Actualización ─────────────────────────

    void Update()
    {
        bool show = ShouldShow();
        if (panel.activeSelf != show) panel.SetActive(show);
        if (!show) return;

        if (player == null)
        {
            var found = FindAnyObjectByType<PlayerController>();
            if (found != null) player = found.transform;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            RefreshTarget();
        }

        if (player != null) Place(playerDot, player.position);

        if (objectiveMarker.gameObject.activeSelf)
        {
            // Pulso suave para que se note
            float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 5f);
            objectiveMarker.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    /// <summary>El mapa solo existe en el Campus, con el cuaderno entregado, y no estorba diálogos, retos ni menús.</summary>
    bool ShouldShow()
    {
        if (!hasMap || !GameManager.Exists) return false;
        var gm = GameManager.Instance;
        if (!(gm.HasFlag(GameManager.FlagNotebookGiven) || gm.HasFlag(GameManager.FlagDevOpen))) return false;
        if (SceneManager.GetActiveScene().name != CampusScene) return false;
        if (gm.IsPaused || gm.NotebookOpen || gm.ModalOpen || gm.IsTransitioning || gm.ResultsOpen) return false;
        return FindAnyObjectByType<PlayerController>() != null;
    }

    void Place(RectTransform marker, Vector3 world)
    {
        float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, world.x);
        float v = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, world.y);
        marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        marker.anchoredPosition = Vector2.zero;
    }

    void RefreshTarget()
    {
        var gm = GameManager.Instance;
        string id = CurrentStepId(gm);
        target = id != null ? TargetFor(id) : null;

        bool marked = target.HasValue;
        objectiveMarker.gameObject.SetActive(marked);
        if (marked) Place(objectiveMarker, target.Value);

        legend.text = marked
            ? "<color=#FFD54D><b>!</b></color> your next task"
            : (id == null ? "All the tasks are done" : "Your next task is not on this map");
    }

    // ───────────────────────── Siguiente reto ─────────────────────────

    /// <summary>Id del primer reto construido que todavía no se completó (el mismo criterio que el objetivo del HUD).</summary>
    static string CurrentStepId(GameManager gm)
    {
        foreach (var step in StoryFlow.Steps)
            if (step.Built && !gm.IsChallengeDone(step.Id)) return step.Id;
        return null;
    }

    /// <summary>
    /// Dónde está el reto en el campus. Los retos dentro de un edificio se marcan en la entrada de ese edificio.
    /// Para un reto nuevo, agregar su caso aquí; si no hay caso, el mapa dice que no está marcado.
    /// </summary>
    static Vector2? TargetFor(string stepId)
    {
        switch (stepId)
        {
            case "q1_4": return DoorsTo("Bienestar");
            case "r2_litter":
                var litter = FindAnyObjectByType<LitterChallenge>();
                return litter != null ? (Vector2?)litter.transform.position : null;
            case "r3_exams": return DoorsTo("Aulas3");     // la Sala de Profesores está dentro de Aulas 3
            case "class1": return DoorsTo("Aulas2");       // el atril de la lección está en Aulas 2
            case "r6_solar": return DoorsTo("Aulas3");     // la escalera al techo con los paneles solares está en Aulas 3
            default: return null;
        }
    }

    /// <summary>Punto medio de las puertas del campus que llevan a una escena (los edificios tienen dos entradas).</summary>
    static Vector2? DoorsTo(string scene)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (var door in FindObjectsByType<Door>(FindObjectsInactive.Exclude))
        {
            if (door.targetScene != scene) continue;
            sum += (Vector2)door.transform.position;
            count++;
        }
        return count > 0 ? (Vector2?)(sum / count) : null;
    }
}
