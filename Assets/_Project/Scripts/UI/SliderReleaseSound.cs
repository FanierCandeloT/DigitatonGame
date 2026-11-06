using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Va en el slider de efectos de Settings: mientras se arrastra no suena nada, y al SOLTARLO suena un Click con el volumen
/// elegido (muestra del volumen). Con el teclado, cada ajuste suena una vez (SettingsPanel usa Dragging para saber cuál es el caso).
/// </summary>
public class SliderReleaseSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool Dragging { get; private set; }

    public void OnPointerDown(PointerEventData eventData) => Dragging = true;

    public void OnPointerUp(PointerEventData eventData)
    {
        Dragging = false;
        AudioManager.PreviewSfx();
    }

    void OnDisable() => Dragging = false;
}
