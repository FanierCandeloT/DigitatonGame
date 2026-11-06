using TMPro;
using UnityEngine;

/// <summary>
/// Mensaje corto en pantalla (por ejemplo "Coming soon"). Vive en el prefab del GameManager,
/// así que se puede llamar desde cualquier escena: ToastMessage.Show("texto").
/// </summary>
public class ToastMessage : MonoBehaviour
{
    static ToastMessage instance;

    public TMP_Text label;
    public CanvasGroup group;

    float hideAt;

    void Awake()
    {
        instance = this;
        group.alpha = 0f;

        // Panel opaco con borde dorado para que el aviso se lea sobre cualquier escena
        var panel = label.transform.parent != null ? label.transform.parent.GetComponent<UnityEngine.UI.Image>() : null;
        label.fontSizeMax = label.fontSize;   // los avisos largos se encogen para caber en el panel
        label.fontSizeMin = 18f;
        label.enableAutoSizing = true;
        if (panel != null) PixelUi.Restyle(panel, new Color(1f, 0.85f, 0.30f), new Color(0.06f, 0.08f, 0.16f, 0.96f));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Show(string message, float seconds = 1.8f)
    {
        if (instance == null)
        {
            Debug.Log(message);
            return;
        }
        instance.label.text = message;
        instance.hideAt = Time.unscaledTime + seconds;
        instance.group.alpha = 1f;
    }

    void Update()
    {
        if (group.alpha > 0f && Time.unscaledTime > hideAt)
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 3f);
    }
}
