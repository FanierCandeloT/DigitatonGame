using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Fundido a negro. Va en un Canvas con CanvasGroup y una imagen negra. Usa tiempo sin escala (funciona en pausa).
/// También puede mostrar un texto centrado sobre el negro (por ejemplo "Your memory has been lost.").
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Tooltip("Texto centrado que se muestra sobre el negro (opcional).")]
    public TMP_Text caption;

    CanvasGroup group;

    CanvasGroup Group => group != null ? group : (group = GetComponent<CanvasGroup>());

    public void SetAlpha(float alpha)
    {
        Group.alpha = alpha;
        Group.blocksRaycasts = alpha > 0.01f;   // mientras está oscuro, el mouse no atraviesa
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        float start = Group.alpha;
        float t = 0f;
        Group.blocksRaycasts = true;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(target);
    }

    public void ShowCaption(string text)
    {
        if (caption == null) return;
        caption.text = text;
        caption.gameObject.SetActive(true);
    }

    public void HideCaption()
    {
        if (caption != null) caption.gameObject.SetActive(false);
    }
}
