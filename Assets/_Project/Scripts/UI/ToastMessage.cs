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
