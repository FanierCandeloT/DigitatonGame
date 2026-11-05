using UnityEngine;

/// <summary>
/// Círculo del Ágora (Collider2D en modo trigger). La primera vez que Andrés entra: no puede moverse,
/// fundido a negro, texto "Your memory has been lost." durante 3 segundos y carga Bienestar junto a la camilla.
/// </summary>
public class AgoraTrigger : MonoBehaviour
{
    public string nextScene = "Bienestar";
    public string spawnId = "Bienestar_Bed";
    public string caption = "Your memory has been lost.";
    [Min(0.5f)] public float captionSeconds = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        var gm = GameManager.Instance;
        if (gm.HasFlag(GameManager.FlagAgoraDone) || gm.IsTransitioning) return;

        gm.SetFlag(GameManager.FlagAgoraDone);
        gm.TransitionTo(nextScene, spawnId, caption, captionSeconds);   // bloquea a Andrés desde el primer frame
    }
}
