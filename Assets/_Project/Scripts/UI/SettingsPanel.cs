using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panel de opciones: volumen de música, volumen de efectos, pantalla completa y velocidad del texto.
/// Guarda con SettingsData (PlayerPrefs). Se reutiliza en el menú principal y en el menú de pausa.
/// Los volúmenes viven SIEMPRE en el rango 0..1 (SettingsData, sliders y AudioMixer): el porcentaje es solo valor x 100.
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

    /// <summary>Se invoca cuando el jugador vuelve atrás (botón Back o Esc), ya con el panel cerrado.</summary>
    public UnityEvent onBack;

    /// <summary>Se invoca al EMPEZAR a cerrar (antes de la animación de cierre, si la hay).</summary>
    public UnityEvent onClosing;

    bool wired;
    SettingsPanelAnimator animator;

    void Wire()
    {
        if (wired) return;
        wired = true;
        animator = GetComponent<SettingsPanelAnimator>();

        musicSlider.onValueChanged.AddListener(v => { SettingsData.MusicVolume = v; UpdateLabels(); });
        var release = sfxSlider.GetComponent<SliderReleaseSound>();
        if (release == null) release = sfxSlider.gameObject.AddComponent<SliderReleaseSound>();
        sfxSlider.onValueChanged.AddListener(v =>
        {
            SettingsData.SfxVolume = v;
            UpdateLabels();
            if (!release.Dragging) AudioManager.PreviewSfx();   // con el teclado suena en cada ajuste; con el mouse, al soltar (SliderReleaseSound)
        });
        fullscreenToggle.onValueChanged.AddListener(on => SettingsData.Fullscreen = on);
        textSpeedButton.onClick.AddListener(() => CycleTextSpeed(+1));
        backButton.onClick.AddListener(Back);
    }

    static void ConfigureVolumeSlider(Slider s)
    {
        // El rango se fuerza aquí para que nunca dependa de lo que traiga la escena (antes podía venir 0..100 y la bolita quedaba en 0)
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
    }

    void OnEnable()
    {
        Wire();
        ConfigureVolumeSlider(musicSlider);
        ConfigureVolumeSlider(sfxSlider);
        musicSlider.SetValueWithoutNotify(SettingsData.MusicVolume);
        sfxSlider.SetValueWithoutNotify(SettingsData.SfxVolume);
        fullscreenToggle.SetIsOnWithoutNotify(SettingsData.Fullscreen);
        UpdateLabels();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
    }

    void Update()
    {
        // Text Speed también se cambia con izquierda / derecha cuando está seleccionado
        var kb = Keyboard.current;
        if (kb == null || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != textSpeedButton.gameObject) return;
        if (kb.rightArrowKey.wasPressedThisFrame) CycleTextSpeed(+1);
        else if (kb.leftArrowKey.wasPressedThisFrame) CycleTextSpeed(-1);
    }

    void CycleTextSpeed(int step)
    {
        SettingsData.TextSpeed = (TextSpeed)(((int)SettingsData.TextSpeed + step + 3) % 3);
        UpdateLabels();
    }

    void UpdateLabels()
    {
        musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        textSpeedLabel.text = "Text Speed: " + SettingsData.TextSpeed;
    }

    public void Back()
    {
        if (animator == null) animator = GetComponent<SettingsPanelAnimator>();
        if (animator != null && animator.isActiveAndEnabled)
        {
            if (animator.Closing) return;
            onClosing?.Invoke();
            animator.PlayClose(FinishBack);
            return;
        }
        onClosing?.Invoke();
        FinishBack();
    }

    void FinishBack()
    {
        gameObject.SetActive(false);
        onBack?.Invoke();
    }
}
