using System.Collections;
using UnityEngine;

/// <summary>Muestra un texto temporal al entrar a la escena (una sola vez si se indica flagKey).</summary>
public class SceneStartMessage : MonoBehaviour
{
    [TextArea] public string message;
    [Tooltip("Marca del GameManager para no repetir el mensaje. Vacío = se muestra siempre.")]
    public string flagKey;
    [Min(0.5f)] public float seconds = 5f;

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(0.9f);   // deja terminar el fundido de entrada

        var gm = GameManager.Instance;
        if (!string.IsNullOrEmpty(flagKey))
        {
            if (gm.HasFlag(flagKey)) yield break;
            gm.SetFlag(flagKey);
        }
        ToastMessage.Show(message, seconds);
    }
}
