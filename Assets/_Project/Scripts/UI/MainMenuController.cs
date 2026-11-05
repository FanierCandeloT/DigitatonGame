using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Menú principal: Play, Tutorial y Settings. Funciona con mouse y con teclado (flechas + Enter).</summary>
public class MainMenuController : MonoBehaviour
{
    public Button playButton;
    public Button tutorialButton;
    public Button settingsButton;
    public GameObject buttonsRoot;
    public SettingsPanel settingsPanel;

    [Tooltip("Escena donde empieza el juego: el 'Intro classroom' dentro de Aulas2.")]
    public string startScene = "Aulas2";
    [Tooltip("SpawnPoint de esa escena donde aparece Andrés al empezar.")]
    public string startSpawn = "Aulas2_Intro";
    public string tutorialScene = "Tutorial";

    void Start()
    {
        playButton.onClick.AddListener(StartNewGame);
        tutorialButton.onClick.AddListener(() => SceneTransition.Go(tutorialScene, null));
        settingsButton.onClick.AddListener(OpenSettings);
        settingsPanel.onBack.AddListener(OnSettingsClosed);
        settingsPanel.gameObject.SetActive(false);
        Select(playButton);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && settingsPanel.gameObject.activeSelf)
            settingsPanel.Back();
    }

    /// <summary>Play: borra el progreso anterior y empieza de cero en el Intro classroom.</summary>
    void StartNewGame()
    {
        GameManager.Instance.ResetProgress();
        SceneTransition.Go(startScene, startSpawn);
    }

    void OpenSettings()
    {
        buttonsRoot.SetActive(false);   // así las flechas no saltan a los botones de atrás
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
