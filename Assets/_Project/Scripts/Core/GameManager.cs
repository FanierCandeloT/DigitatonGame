using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado global del juego. Se crea solo (desde Resources/GameManager) antes de cargar cualquier escena
/// y no se destruye al cambiar de escena. Guarda:
///  - recuerdos recuperados (0 a 12)
///  - el punto de aparición de la última puerta usada
/// También controla la pausa y el cambio de escena con fundido a negro.
/// El prefab trae además el fundido, el menú de pausa, el cuaderno y el EventSystem.
/// </summary>
public class GameManager : MonoBehaviour
{
    public const int MaxMemories = 12;

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

    [SerializeField] ScreenFader fader;
    [SerializeField, Min(0.05f)] float fadeDuration = 0.35f;

    int memoriesRecovered;
    bool hasPendingSpawn;
    string pendingSpawnId;

    public int MemoriesRecovered => memoriesRecovered;

    /// <summary>Id del SpawnPoint de la última puerta usada (vacío si ninguna).</summary>
    public string LastSpawnId { get; private set; }

    public bool IsPaused { get; private set; }
    public bool NotebookOpen { get; set; }
    public bool IsTransitioning { get; private set; }

    /// <summary>True cuando el jugador no debe moverse ni interactuar.</summary>
    public bool InputLocked => IsPaused || NotebookOpen || IsTransitioning;

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

    // ───────────── Recuerdos ─────────────

    public void SetMemories(int value)
    {
        value = Mathf.Clamp(value, 0, MaxMemories);
        if (value == memoriesRecovered) return;
        memoriesRecovered = value;
        MemoriesChanged?.Invoke(memoriesRecovered);
    }

    public void AddMemory(int amount = 1) => SetMemories(memoriesRecovered + amount);

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

    public void TransitionTo(string sceneName, string spawnId)
    {
        if (IsTransitioning) return;
        LastSpawnId = spawnId;
        pendingSpawnId = spawnId;
        hasPendingSpawn = !string.IsNullOrEmpty(spawnId);
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        IsTransitioning = true;
        if (fader != null) yield return fader.FadeTo(1f, fadeDuration);

        NotebookOpen = false;
        SetPaused(false);

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

        if (fader != null) yield return fader.FadeTo(0f, fadeDuration);
        IsTransitioning = false;
    }
}
