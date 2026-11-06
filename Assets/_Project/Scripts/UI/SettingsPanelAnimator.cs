using System;
using UnityEngine;

/// <summary>
/// Aparece con un fundido y un leve crecimiento desde el centro, y se cierra igual al revés (tiempo real).
/// SettingsPanel lo usa si existe (en el menú de pausa no hay: ahí se abre y se cierra directo).
/// </summary>
public class SettingsPanelAnimator : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform window;
    public float seconds = 0.22f;
    [Range(0.7f, 1f)] public float fromScale = 0.9f;

    float t;
    bool closing;
    Action onClosed;

    public bool Closing => closing;

    void OnEnable()
    {
        closing = false; onClosed = null; t = 0f;
        group.interactable = true;
        Apply();
    }

    public void PlayClose(Action done)
    {
        if (closing) return;
        closing = true;
        onClosed = done;
        group.interactable = false;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
        if (!closing) t = Mathf.Min(1f, t + dt);
        else
        {
            t = Mathf.Max(0f, t - dt);
            if (t <= 0f)
            {
                var cb = onClosed; onClosed = null; closing = false;
                Apply();
                cb?.Invoke();
                return;
            }
        }
        Apply();
    }

    void Apply()
    {
        float e = t * t * (3f - 2f * t);
        group.alpha = e;
        window.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, e);
    }
}
