using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú principal: Play, Tutorial y Settings. Funciona con mouse y con teclado (flechas + Enter).
/// Al abrir, el título y los botones aparecen con un fundido, uno tras otro. Con Settings abierto, el menú se oculta y el
/// fondo animado se difumina (BackdropBlur); al cerrar vuelve todo.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public Button playButton;
    public Button tutorialButton;
    public Button settingsButton;
    public GameObject buttonsRoot;
    public SettingsPanel settingsPanel;

    [Header("Rediseño")]
    [Tooltip("Capa con todo lo que se esconde cuando Settings está abierto (degradado, título, botones, ayuda).")]
    public CanvasGroup menuLayer;
    [Tooltip("Elementos que entran con fundido, en este orden (título, botones, ayuda, créditos).")]
    public CanvasGroup[] introSequence;
    public BackdropBlur blur;

    public string tutorialScene = "Tutorial";

    const float IntroFade = 0.4f, IntroStagger = 0.16f, LayerFade = 0.2f;
    Coroutine layerRoutine;

    void Start()
    {
        playButton.onClick.AddListener(StartNewGame);
        tutorialButton.onClick.AddListener(() => SceneTransition.Go(tutorialScene, null));
        settingsButton.onClick.AddListener(OpenSettings);
        settingsPanel.onClosing.AddListener(OnSettingsClosing);
        settingsPanel.onBack.AddListener(OnSettingsClosed);
        settingsPanel.gameObject.SetActive(false);
        Select(playButton);
        StartCoroutine(Intro());
    }

    IEnumerator Intro()
    {
        if (introSequence == null) yield break;
        foreach (var g in introSequence) if (g != null) g.alpha = 0f;
        yield return null;
        foreach (var g in introSequence)
        {
            if (g == null) continue;
            StartCoroutine(Fade(g, 1f, IntroFade));
            yield return new WaitForSecondsRealtime(IntroStagger);
        }
    }

    static IEnumerator Fade(CanvasGroup g, float to, float seconds)
    {
        float from = g.alpha, t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
        g.alpha = to;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && settingsPanel.gameObject.activeSelf)
            settingsPanel.Back();
    }

    /// <summary>Play: borra el progreso anterior, cuenta el accidente (introducción) y despierta en el hospital (ver StoryFlow).</summary>
    void StartNewGame() => GameManager.Instance.StartStory();

    void OpenSettings()
    {
        SetLayer(false);                       // el menú se esconde (y deja de recibir flechas / clics)
        if (blur != null) blur.Show(true);     // el fondo se difumina
        settingsPanel.gameObject.SetActive(true);
    }

    void OnSettingsClosing()
    {
        if (blur != null) blur.Show(false);
        SetLayer(true);
    }

    void OnSettingsClosed()
    {
        Select(settingsButton);
    }

    void SetLayer(bool visible)
    {
        if (menuLayer == null) { if (buttonsRoot != null) buttonsRoot.SetActive(visible); return; }
        menuLayer.interactable = visible;
        menuLayer.blocksRaycasts = visible;
        if (layerRoutine != null) StopCoroutine(layerRoutine);
        layerRoutine = StartCoroutine(Fade(menuLayer, visible ? 1f : 0f, LayerFade));
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
