using UnityEngine.SceneManagement;

/// <summary>
/// Cambia de escena recordando en qué SpawnPoint debe aparecer Andrés en la escena destino.
/// </summary>
public static class SceneTransition
{
    /// <summary>Id del SpawnPoint donde debe aparecer el jugador en la siguiente escena (vacío = el de por defecto).</summary>
    public static string PendingSpawnId { get; private set; }

    public static void Go(string sceneName, string spawnId)
    {
        PendingSpawnId = spawnId;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Devuelve el id pendiente y lo borra (solo se usa una vez).</summary>
    public static string ConsumePendingSpawnId()
    {
        var id = PendingSpawnId;
        PendingSpawnId = null;
        return id;
    }
}
