using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// El apagón del campus: el último tramo de la historia (reto "r6_solar", los paneles solares del techo de Aulas 3).
///
/// 1. Cuando Andrés termina la clase (done:class1) y se cierra el diálogo, las luces parpadean y se apagan, y una estudiante
///    se le acerca caminando y le cuenta qué pasó y adónde ir (los paneles del techo de Aulas 3).
/// 2. Mientras el reto no se complete, TODAS las escenas se ven a oscuras (se baja la luz global y se tiñe de azul).
/// 3. Al completar el reto, la luz vuelve poco a poco. SolarPuzzleChallenge pone la marca done:r6_solar.
///
/// Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena). No toca nada en las escenas: solo ajusta
/// las luces globales (Light2D) mientras dura el apagón y las deja como estaban al terminar.
/// </summary>
public class BlackoutDirector : MonoBehaviour
{
    /// <summary>Marca que se pone cuando el apagón ya empezó (la historia lo recuerda).</summary>
    public const string FlagStarted = "blackout_started";

    /// <summary>Id del reto que lo resuelve.</summary>
    public const string ChallengeId = "r6_solar";

    // Cuánto se oscurece: la luz global baja a este porcentaje de la original y se tiñe de azul noche.
    const float DarkIntensity = 0.3f;
    static readonly Color DarkTint = new Color(0.45f, 0.55f, 1f);
    const float FadeSeconds = 2f;

    static readonly string[] IdleScenes = { "MainMenu", "Tutorial" };

    static BlackoutDirector instance;

    /// <summary>True mientras el campus está sin luz (empezó el apagón y todavía no se arreglan los paneles).</summary>
    public static bool IsDark
    {
        get
        {
            if (!GameManager.Exists) return false;
            var gm = GameManager.Instance;
            return gm.HasFlag(FlagStarted) && !gm.IsChallengeDone(ChallengeId);
        }
    }

    class LightState
    {
        public float Intensity;
        public Color Color;
    }

    readonly Dictionary<Light2D, LightState> originals = new Dictionary<Light2D, LightState>();
    float level;                 // 0 = luz normal, 1 = apagón completo
    bool flickering;
    bool sequenceRunning;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("BlackoutDirector");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BlackoutDirector>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        originals.Clear();   // las luces de la escena anterior ya no existen
        nextScan = 0f;
    }

    // ───────────────────────── Luz ─────────────────────────

    void Update()
    {
        if (!GameManager.Exists) return;

        TryStartSequence();

        if (!flickering)
        {
            float target = IsDark && !IsIdleScene() ? 1f : 0f;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / FadeSeconds);
        }
        ApplyLights(level, false);
    }

    static bool IsIdleScene() => Array.IndexOf(IdleScenes, SceneManager.GetActiveScene().name) >= 0;

    bool touchedLights;   // true mientras las luces están modificadas (para devolverlas a su estado original una sola vez)

    /// <summary>Aplica el nivel de oscuridad a las luces globales de la escena (guarda cómo estaban para devolverlas).</summary>
    void ApplyLights(float darkness, bool force)
    {
        if (force || Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            foreach (var light in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
            {
                if (light.lightType != Light2D.LightType.Global || originals.ContainsKey(light)) continue;
                originals[light] = new LightState { Intensity = light.intensity, Color = light.color };
            }
        }

        // Sin apagón y sin nada que restaurar: no se toca nada (así no se pisan los ajustes de las escenas)
        bool dark = darkness > 0.0001f;
        if (!dark && !touchedLights && !force) return;
        touchedLights = dark;

        foreach (var pair in originals)
        {
            if (pair.Key == null) continue;
            pair.Key.intensity = Mathf.Lerp(pair.Value.Intensity, pair.Value.Intensity * DarkIntensity, darkness);
            pair.Key.color = Color.Lerp(pair.Value.Color, pair.Value.Color * DarkTint, darkness);
        }
    }

    /// <summary>
    /// Ejecuta una acción con la luz normal (por ejemplo, la foto del mapa) y vuelve a dejar el apagón como estaba.
    /// </summary>
    public static void WithNormalLight(Action action)
    {
        if (instance == null) { action(); return; }
        instance.ApplyLights(0f, true);
        try { action(); }
        finally { instance.ApplyLights(instance.level, true); }
    }

    // ───────────────────────── La escena del apagón ─────────────────────────

    void TryStartSequence()
    {
        if (sequenceRunning || IsIdleScene()) return;
        var gm = GameManager.Instance;
        if (!gm.IsChallengeDone("class1") || gm.HasFlag(FlagStarted) || gm.IsChallengeDone(ChallengeId)) return;
        if (gm.ModalOpen || gm.IsTransitioning || gm.IsPaused || gm.NotebookOpen || gm.ResultsOpen) return;   // espera a que cierre el diálogo de la clase
        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        StartCoroutine(Sequence(player.transform));
    }

    IEnumerator Sequence(Transform player)
    {
        sequenceRunning = true;
        var gm = GameManager.Instance;
        var input = gm.LockInput();   // Andrés queda quieto mientras pasa todo (estándar de los retos)
        Transform npc = null;
        Vector3 npcHome = Vector3.zero;
        Collider2D npcCollider = null;
        bool colliderWasOn = false;

        try
        {
            yield return new WaitForSecondsRealtime(0.6f);
            gm.SetFlag(FlagStarted);
            yield return Flicker();
            if (player == null) yield break;   // se cambió de escena durante el parpadeo: no hay a quién hablarle (finally libera el bloqueo)

            npc = FindStudent(player);
            if (npc != null)
            {
                npcHome = npc.position;
                npcCollider = npc.GetComponent<Collider2D>();
                if (npcCollider != null) { colliderWasOn = npcCollider.enabled; npcCollider.enabled = false; }
                float side = npc.position.x < player.position.x ? -1f : 1f;
                yield return Walk(npc, player.position + new Vector3(1.8f * side, 0f, 0f), 4.5f);
            }

            bool finished = false;
            DialoguePanel.Show(Lines(npc != null ? NpcName(npc) : "Valentina"), () => finished = true);
            while (!finished) yield return null;

            if (npc != null)
            {
                yield return Walk(npc, npcHome, 4.5f);
                if (npcCollider != null) npcCollider.enabled = colliderWasOn;
            }
        }
        finally
        {
            if (npcCollider != null) npcCollider.enabled = colliderWasOn;
            input.Release();   // idempotente
            sequenceRunning = false;
        }
    }

    /// <summary>Las luces parpadean unos instantes y se apagan del todo.</summary>
    IEnumerator Flicker()
    {
        flickering = true;
        var pattern = new[] { 0.9f, 0.1f, 0.8f, 0.0f, 0.95f, 0.2f, 0.7f, 1f };
        foreach (float value in pattern)
        {
            level = value;
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.07f, 0.2f));
        }
        level = 1f;
        flickering = false;
    }

    IEnumerator Walk(Transform who, Vector3 to, float speed)
    {
        to.z = who.position.z;
        while (who != null && Vector3.Distance(who.position, to) > 0.05f)
        {
            who.position = Vector3.MoveTowards(who.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>La estudiante que viene a hablarle: Valentina si está cerca; si no, cualquier NPC con mensaje a ≤ 12 u.</summary>
    static Transform FindStudent(Transform player)
    {
        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (var npc in FindObjectsByType<ToastInteractable>(FindObjectsInactive.Exclude))
        {
            float distance = Vector2.Distance(npc.transform.position, player.position);
            if (distance > 12f || npc.GetComponent<SpriteRenderer>() == null) continue;
            float score = distance - (npc.name.StartsWith("Valentina") ? 100f : 0f);   // Valentina tiene prioridad
            if (score < bestScore) { bestScore = score; best = npc.transform; }
        }
        return best;
    }

    static string NpcName(Transform npc)
    {
        string n = npc.name;
        int cut = n.IndexOf('(');
        if (cut > 0) n = n.Substring(0, cut);
        n = n.Replace("Extra:", "").Trim();
        return string.IsNullOrEmpty(n) ? "Student" : n;
    }

    static DialogueLine[] Lines(string npc) => new[]
    {
        new DialogueLine(npc, "Professor! The lights were turned off! The whole campus is in the dark!"),
        new DialogueLine("Andrés",
            "What happened? Who turned them off?",
            "¿Qué pasó? ¿Quién {turned them off}?",
            "¿Qué pasó? ¿Quién las apagó?"),
        new DialogueLine(npc, "Nobody knows... It is said that the solar panels on the roof of Aulas 3 were disconnected."),
        new DialogueLine(npc, "They can only be reconnected by the right sentences. Please, go up to the roof! The stairs are in the hallway of Aulas 3."),
        new DialogueLine("Andrés",
            "The panels were disconnected... Then they will be reconnected. By me.",
            "Los paneles {were disconnected}... Entonces {will be reconnected}. {By me}.",
            "Los paneles fueron desconectados... Entonces serán reconectados. Por mí."),
    };
}
