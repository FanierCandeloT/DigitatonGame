using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado global del juego. Se crea solo (desde Resources/GameManager) antes de cargar cualquier escena
/// y no se destruye al cambiar de escena. Guarda:
///  - recuerdos recuperados (0 a 12)
///  - retos completados y otras marcas del guion ("flags")
///  - el punto de aparición de la última puerta usada
/// También controla la pausa, el cambio de escena con fundido a negro, las transiciones con texto y los resultados.
/// El prefab trae además el fundido, el menú de pausa, el cuaderno, el contador de recuerdos y el EventSystem.
/// </summary>
public class GameManager : MonoBehaviour
{
    public const int MaxMemories = 12;

    /// <summary>Marca que se activa al completar las preguntas 1-4 (Bienestar). Abre los edificios del campus.</summary>
    public const string FlagBienestarDone = "done:q1_4";
    public const string FlagAgoraDone = "agora_done";
    public const string FlagDevOpen = "dev_open";

    static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null) Bootstrap();
            return instance;
        }
    }

    public static event Action<bool> PauseChanged;
    public static event Action<int> MemoriesChanged;

    /// <summary>Se dispara cuando cambia cualquier marca del guion o un reto (el HUD actualiza el objetivo).</summary>
    public static event Action ProgressChanged;

    [SerializeField] ScreenFader fader;
    [SerializeField] ResultsPanel results;
    [SerializeField, Min(0.05f)] float fadeDuration = 0.35f;

    readonly HashSet<string> flags = new HashSet<string>();
    int memoriesRecovered;
    bool hasPendingSpawn;
    string pendingSpawnId;

    public int MemoriesRecovered => memoriesRecovered;

    /// <summary>Id del SpawnPoint de la última puerta usada (vacío si ninguna).</summary>
    public string LastSpawnId { get; private set; }

    public bool IsPaused { get; private set; }
    public bool NotebookOpen { get; set; }
    public bool IsTransitioning { get; private set; }
    public bool ResultsOpen { get; private set; }
    /// <summary>
    /// Bloqueo de control del jugador. Cada diálogo o reto toma el suyo con LockInput() y lo suelta con Release():
    /// mientras haya al menos uno, Andrés no se mueve ni interactúa. Como cada uno es independiente (y Release es
    /// idempotente), dos retos encadenados nunca se desbloquean entre sí.
    /// </summary>
    public sealed class ModalLock
    {
        readonly GameManager owner;
        bool released;

        internal ModalLock(GameManager owner) => this.owner = owner;

        public void Release()
        {
            if (released) return;
            released = true;
            if (owner != null) owner.ReleaseModal();
        }
    }

    int modalCount;

    /// <summary>True si hay un diálogo o reto abierto (el jugador debe estar quieto).</summary>
    public bool ModalOpen => modalCount > 0;

    public ModalLock LockInput()
    {
        modalCount++;
        return new ModalLock(this);
    }

    void ReleaseModal() => modalCount = Mathf.Max(0, modalCount - 1);

    /// <summary>True si el GameManager ya existe (no lo crea). Sirve para limpiar estado al destruir objetos.</summary>
    public static bool Exists => instance != null;

    /// <summary>True cuando el jugador no debe moverse ni interactuar.</summary>
    public bool InputLocked => IsPaused || NotebookOpen || IsTransitioning || ResultsOpen || ModalOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var prefab = Resources.Load<GameObject>("GameManager");
        if (prefab == null)
        {
            Debug.LogError("Falta Assets/_Project/Resources/GameManager.prefab (DigitatonGame ▸ Construir flujo de menús).");
            return;
        }
        var go = Instantiate(prefab);
        go.name = "GameManager";
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        SettingsData.ApplyAll();

        // El módulo de UI necesita sus acciones; se asignan al iniciar para no depender de lo serializado.
        var ui = GetComponentInChildren<InputSystemUIInputModule>(true);
        if (ui != null && ui.actionsAsset == null) ui.AssignDefaultActions();

        if (fader != null) fader.SetAlpha(1f);   // el juego arranca con un fundido desde negro
    }

    void Start()
    {
        // Atajo para desarrollo: si se da Play directamente en una escena del juego (no en el menú),
        // los edificios del campus quedan abiertos para poder probar.
        var current = SceneManager.GetActiveScene().name;
        if (current != "MainMenu" && current != "Tutorial")
        {
            SetFlag(FlagDevOpen);
            SetFlag(FlagAgoraDone);   // el accidente ya lo cuenta la introducción: el disparador antiguo del Ágora no debe mandarte al hospital
        }

        if (fader != null) StartCoroutine(fader.FadeTo(0f, fadeDuration));
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Time.timeScale = 1f;
        }
    }

    // ───────────── Progreso ─────────────

    public bool HasFlag(string flag) => flags.Contains(flag);
    public void SetFlag(string flag)
    {
        if (flags.Add(flag)) ProgressChanged?.Invoke();
    }

    public bool IsChallengeDone(string id) => flags.Contains("done:" + id);

    // Cifras del jugador (puntajes de las lecciones, intentos...) para el resumen final.
    readonly Dictionary<string, int> stats = new Dictionary<string, int>();

    public void SetStat(string key, int value) => stats[key] = value;
    public int GetStat(string key) => stats.TryGetValue(key, out var value) ? value : 0;

    /// <summary>Marca un reto como completado (no se puede repetir) y suma sus recuerdos.</summary>
    public void CompleteChallenge(string id, int memoryReward)
    {
        flags.Add("done:" + id);
        AddMemory(memoryReward);
        ProgressChanged?.Invoke();
    }

    /// <summary>Borra todo el progreso: así "Play" siempre empieza de cero.</summary>
    public void ResetProgress()
    {
        flags.Clear();
        stats.Clear();
        modalCount = 0;
        ResultsOpen = false;
        NotebookOpen = false;
        hasPendingSpawn = false;
        pendingSpawnId = null;
        LastSpawnId = null;
        SetMemories(0);
        if (results != null) results.Hide();
        ProgressChanged?.Invoke();
    }

    /// <summary>
    /// Empieza una partida nueva: borra el progreso y reproduce la introducción (el accidente) sobre negro
    /// antes de despertar en el hospital. Cada pantalla se puede saltar con cualquier tecla o clic.
    /// </summary>
    public void StartStory()
    {
        if (IsTransitioning) return;
        ResetProgress();
        SetFlag(StoryFlow.FlagIntroDone);
        SetFlag(FlagAgoraDone);   // el accidente ya se contó aquí: el disparador antiguo del Ágora no debe repetirlo
        TransitionToCaptions(StoryFlow.StartScene, StoryFlow.StartSpawn, StoryFlow.IntroCaptions, StoryFlow.CaptionSeconds);
    }

    // ───────────── Recuerdos ─────────────

    public void SetMemories(int value)
    {
        value = Mathf.Clamp(value, 0, MaxMemories);
        if (value == memoriesRecovered) return;
        memoriesRecovered = value;
        MemoriesChanged?.Invoke(memoriesRecovered);
    }

    public void AddMemory(int amount = 1) => SetMemories(memoriesRecovered + amount);

    // ───────────── Resultados ─────────────

    public void ShowResults()
    {
        ResultsOpen = true;
        if (results != null) results.Show();
    }

    public void CloseResults() => ResultsOpen = false;

    // ───────────── Puntos de aparición ─────────────

    /// <summary>Devuelve el id de aparición pendiente (si lo hay) y lo consume.</summary>
    public string ConsumeSpawnId()
    {
        if (!hasPendingSpawn) return null;
        hasPendingSpawn = false;
        return pendingSpawnId;
    }

    // ───────────── Pausa ─────────────

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        PauseChanged?.Invoke(paused);
    }

    // ───────────── Cambio de escena ─────────────

    /// <summary>Fundido a negro, carga la escena y deja a Andrés en spawnId. Con caption: muestra ese texto sobre negro antes de cargar.</summary>
    public void TransitionTo(string sceneName, string spawnId, string caption = null, float captionSeconds = 0f) =>
        TransitionToCaptions(sceneName, spawnId, string.IsNullOrEmpty(caption) ? null : new[] { caption }, captionSeconds);

    /// <summary>Como TransitionTo, pero con varias pantallas de texto seguidas sobre negro (cada una se puede saltar con una tecla o clic).</summary>
    public void TransitionToCaptions(string sceneName, string spawnId, string[] captions, float secondsEach)
    {
        if (IsTransitioning) return;
        LastSpawnId = spawnId;
        pendingSpawnId = spawnId;
        hasPendingSpawn = !string.IsNullOrEmpty(spawnId);
        StartCoroutine(TransitionRoutine(sceneName, captions, secondsEach));
    }

    IEnumerator TransitionRoutine(string sceneName, string[] captions, float secondsEach)
    {
        IsTransitioning = true;   // bloquea a Andrés desde el primer momento
        if (fader != null) yield return fader.FadeTo(1f, fadeDuration);

        NotebookOpen = false;
        SetPaused(false);

        if (captions != null && fader != null)
        {
            foreach (var caption in captions)
            {
                fader.ShowCaption(caption);
                yield return WaitCaption(secondsEach);
            }
        }

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"No se pudo cargar la escena '{sceneName}'. ¿Está en el Build Profile?");
        }
        else
        {
            while (!op.isDone) yield return null;
            yield return null;   // un frame para que la escena nueva ejecute Awake/Start
        }

        if (fader != null)
        {
            fader.HideCaption();
            yield return fader.FadeTo(0f, fadeDuration);
        }
        IsTransitioning = false;
    }

    /// <summary>Espera los segundos indicados o hasta que el jugador pulse una tecla o haga clic (con un mínimo para poder leer).</summary>
    static IEnumerator WaitCaption(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (t > 0.5f && AdvancePressed()) yield break;
            yield return null;
        }
    }

    static bool AdvancePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var pointer = Pointer.current;
        return pointer != null && pointer.press.wasPressedThisFrame;
    }
}
