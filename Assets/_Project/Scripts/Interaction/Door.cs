using UnityEngine;

/// <summary>
/// Puerta amarilla: al presionar E carga otra escena y coloca a Andrés en el SpawnPoint indicado.
/// La escena debe estar en el Build Profile.
/// </summary>
public class Door : Interactable
{
    [Header("Destino")]
    [Tooltip("Nombre exacto de la escena a cargar.")]
    public string targetScene;

    [Tooltip("Id del SpawnPoint en la escena destino donde aparecerá Andrés.")]
    public string targetSpawnId;

    public override void Interact(PlayerInteractor interactor)
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("La puerta no tiene escena destino.", this);
            return;
        }
        base.Interact(interactor);
        SceneTransition.Go(targetScene, targetSpawnId);
    }
}
