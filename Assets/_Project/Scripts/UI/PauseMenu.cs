using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa (Esc dentro del juego): Resume, Settings y Main Menu. Vive en el prefab del GameManager, así que es el
/// mismo en TODAS las escenas. Al pausar, la escena de atrás se difumina (PauseBackdrop) y la ventana entra con un fundido
/// y un leve crecimiento desde el centro; al reanudar se cierra igual al revés. Settings abre el panel Settings encima
/// (la ventana de pausa se esconde mientras tanto) y Back regresa a la pausa. Esc reanuda, cierra el cuaderno o sale de Settings.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pauseRoot;        // fondo difuminado + ventana (se activa solo mientras hay pausa o animación)
    public CanvasGroup rootGroup;       // fundido de todo el menú
    public RectTransform window;        // ventana con título y botones (crece desde el centro)
    public CanvasGroup windowGroup;     // se esconde cuando Settings está abierto
    public PauseBackdrop backdrop;
    public GameObject buttonsRoot;      // (compatibilidad) la ventana
    public Button resumeButton;
    public Button settingsButton;
    public Button mainMenuButton;
    public SettingsPanel settingsPanel;
    public NotebookPanel notebook;

    public string mainMenuScene = "MainMenu";

    const float Seconds = 0.22f, FromScale = 0.9f;
    float t;                 // 0 = cerrado, 1 = abierto
    bool open;               // lo que se quiere (true = pausado)
    float windowAlpha = 1f;  // 0 mientras Settings está abierto

    void OnEnable() => GameManager.PauseChanged += OnPauseChanged;
    void OnDisable() => GameManager.PauseChanged -= OnPauseChanged;

    void Start()
    {
        resumeButton.onClick.AddListener(() => GameManager.Instance.SetPaused(false));
        settingsButton.onClick.AddListener(OpenSettings);
        mainMenuButton.onClick.AddListener(() => SceneTransition.Go(mainMenuScene, null));
        settingsPanel.onClosing.AddListener(OnSettingsClosing);
        settingsPanel.onBack.AddListener(OnSettingsClosed);

        pauseRoot.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        t = 0f;
    }

    void Update()
    {
        Animate();

        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        var gm = GameManager.Instance;
        if (gm.IsTransitioning || gm.ResultsOpen || gm.ModalOpen) return;

        if (notebook != null && notebook.IsOpen) { notebook.Close(); AudioManager.PlayOpenBook(); return; }
        if (settingsPanel.gameObject.activeSelf) { settingsPanel.Back(); return; }

        if (gm.IsPaused) gm.SetPaused(false);
        else if (FindAnyObjectByType<PlayerController>() != null) gm.SetPaused(true);   // solo dentro del juego
    }

    void Animate()
    {
        if (!pauseRoot.activeSelf) return;

        float dt = Time.unscaledDeltaTime / Seconds;
        t = Mathf.Clamp01(t + (open ? dt : -dt));
        float e = t * t * (3f - 2f * t);
        rootGroup.alpha = e;
        window.localScale = Vector3.one * Mathf.Lerp(FromScale, 1f, e);

        windowGroup.alpha = Mathf.MoveTowards(windowGroup.alpha, windowAlpha, Time.unscaledDeltaTime * 8f);

        if (!open && t <= 0f)
        {
            pauseRoot.SetActive(false);
            backdrop.Release();
        }
    }

    void OnPauseChanged(bool paused)
    {
        open = paused;
        if (paused)
        {
            settingsPanel.gameObject.SetActive(false);
            windowAlpha = 1f;
            windowGroup.alpha = 1f;
            windowGroup.interactable = true;
            windowGroup.blocksRaycasts = true;
            backdrop.Capture();          // la escena tal como se ve, ANTES de mostrar el menú
            if (!pauseRoot.activeSelf) t = 0f;
            pauseRoot.SetActive(true);
            Select(resumeButton);
        }
        else
        {
            // Settings se cierra de golpe (el juego ya está corriendo); la ventana se desvanece con la animación
            settingsPanel.gameObject.SetActive(false);
            windowGroup.interactable = false;
        }
    }

    void OpenSettings()
    {
        windowAlpha = 0f;                       // la ventana de pausa se esconde mientras Settings está encima
        windowGroup.interactable = false;
        windowGroup.blocksRaycasts = false;
        settingsPanel.gameObject.SetActive(true);
    }

    void OnSettingsClosing()
    {
        if (!open) return;
        windowAlpha = 1f;
        windowGroup.interactable = true;
        windowGroup.blocksRaycasts = true;
    }

    void OnSettingsClosed()
    {
        if (open) Select(settingsButton);
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
