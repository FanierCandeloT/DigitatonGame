using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Panel de opciones: volumen de música, volumen de efectos, pantalla completa y velocidad del texto.
/// Guarda con SettingsData (PlayerPrefs). Se reutiliza en el menú principal y en el menú de pausa.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public Slider musicSlider;
    public TMP_Text musicValue;
    public Slider sfxSlider;
    public TMP_Text sfxValue;
    public Toggle fullscreenToggle;
    public Button textSpeedButton;
    public TMP_Text textSpeedLabel;
    public Button backButton;

    /// <summary>Se invoca cuando el jugador vuelve atrás (botón Back o Esc).</summary>
    public UnityEvent onBack;

    bool wired;

    void Wire()
    {
        if (wired) return;
        wired = true;

        musicSlider.onValueChanged.AddListener(v => { SettingsData.MusicVolume = v; UpdateLabels(); });
        sfxSlider.onValueChanged.AddListener(v => { SettingsData.SfxVolume = v; UpdateLabels(); });
        fullscreenToggle.onValueChanged.AddListener(on => SettingsData.Fullscreen = on);
        textSpeedButton.onClick.AddListener(() =>
        {
            SettingsData.TextSpeed = (TextSpeed)(((int)SettingsData.TextSpeed + 1) % 3);
            UpdateLabels();
        });
        backButton.onClick.AddListener(Back);
    }

    void OnEnable()
    {
        Wire();
        musicSlider.SetValueWithoutNotify(SettingsData.MusicVolume);
        sfxSlider.SetValueWithoutNotify(SettingsData.SfxVolume);
        fullscreenToggle.SetIsOnWithoutNotify(SettingsData.Fullscreen);
        UpdateLabels();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
    }

    void UpdateLabels()
    {
        musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        textSpeedLabel.text = "Text Speed: " + SettingsData.TextSpeed;
    }

    public void Back()
    {
        gameObject.SetActive(false);
        onBack?.Invoke();
    }
}
