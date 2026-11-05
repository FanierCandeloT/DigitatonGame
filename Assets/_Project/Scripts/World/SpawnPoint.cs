using UnityEngine;

/// <summary>Punto donde puede aparecer Andrés al entrar a una escena.</summary>
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Identificador único dentro de la escena. Las puertas lo referencian.")]
    public string id = "Default";

    [Tooltip("Se usa cuando la escena se abre sin una puerta (por ejemplo, al darle Play directo).")]
    public bool isDefault;

    /// <summary>Busca el punto de aparición para esta escena según el id pendiente.</summary>
    public static SpawnPoint Resolve(string requestedId)
    {
        var all = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude);
        SpawnPoint fallback = null;
        foreach (var sp in all)
        {
            if (!string.IsNullOrEmpty(requestedId) && sp.id == requestedId) return sp;
            if (sp.isDefault) fallback = sp;
        }
        if (!string.IsNullOrEmpty(requestedId))
            Debug.LogWarning($"SpawnPoint '{requestedId}' no existe en esta escena; usando el de por defecto.");
        return fallback;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isDefault ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.down * 0.4f, "Spawn: " + id);
#endif
    }
}
