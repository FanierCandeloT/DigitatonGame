/// <summary>
/// Fachada para cambiar de escena (la usan las puertas y los menús).
/// Toda la lógica vive en GameManager: fundido a negro, carga y guardado del punto de aparición.
/// </summary>
public static class SceneTransition
{
    /// <summary>Fundido a negro, carga la escena y deja a Andrés en el SpawnPoint indicado (vacío = el de por defecto).</summary>
    public static void Go(string sceneName, string spawnId)
    {
        GameManager.Instance.TransitionTo(sceneName, spawnId);
    }

    /// <summary>Devuelve el id de aparición pendiente y lo consume (solo se usa una vez por carga de escena).</summary>
    public static string ConsumePendingSpawnId()
    {
        return GameManager.Instance.ConsumeSpawnId();
    }
}
