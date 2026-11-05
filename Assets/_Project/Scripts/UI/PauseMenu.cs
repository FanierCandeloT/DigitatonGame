using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa (Esc dentro del juego): Resume, Settings y Main Menu.
/// También gestiona Esc para cerrar el cuaderno o salir de Settings.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pauseRoot;        // fondo oscuro + botones
    public GameObject buttonsRoot;      // solo los botones (se oculta al abrir Settings)
    public Button resumeButton;
    public Button settingsButton;
    public Button mainMenuButton;
    public SettingsPanel settingsPanel;
    public NotebookPanel notebook;

    public string mainMenuScene = "MainMenu";

    void OnEnable() => GameManager.PauseChanged += OnPauseChanged;
    void OnDisable() => GameManager.PauseChanged -= OnPauseChanged;

    void Start()
    {
        resumeButton.onClick.AddListener(() => GameManager.Instance.SetPaused(false));
        settingsButton.onClick.AddListener(OpenSettings);
        mainMenuButton.onClick.AddListener(() => SceneTransition.Go(mainMenuScene, null));
        settingsPanel.onBack.AddListener(OnSettingsClosed);

        pauseRoot.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        var gm = GameManager.Instance;
        if (gm.IsTransitioning) return;

        if (notebook != null && notebook.IsOpen) { notebook.Close(); return; }
        if (settingsPanel.gameObject.activeSelf) { settingsPanel.Back(); return; }

        if (gm.IsPaused) gm.SetPaused(false);
        else if (FindAnyObjectByType<PlayerController>() != null) gm.SetPaused(true);   // solo dentro del juego
    }

    void OnPauseChanged(bool paused)
    {
        if (paused)
        {
            pauseRoot.SetActive(true);
            buttonsRoot.SetActive(true);
            Select(resumeButton);
        }
        else
        {
            settingsPanel.gameObject.SetActive(false);
            pauseRoot.SetActive(false);
        }
    }

    void OpenSettings()
    {
        buttonsRoot.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
    }

    void OnSettingsClosed()
    {
        buttonsRoot.SetActive(true);
        Select(settingsButton);
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
