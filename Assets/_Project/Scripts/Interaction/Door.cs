using UnityEngine;

/// <summary>
/// Puerta amarilla: al presionar E carga otra escena y coloca a Andrés en el SpawnPoint indicado.
/// Si la puerta no tiene escena destino (o la escena todavía no existe en el Build Profile),
/// muestra el mensaje "Coming soon" y no carga nada.
/// </summary>
public class Door : Interactable
{
    [Header("Destino")]
    [Tooltip("Nombre exacto de la escena a cargar. Vacío = todavía no existe (muestra 'Coming soon').")]
    public string targetScene;

    [Tooltip("Id del SpawnPoint en la escena destino donde aparecerá Andrés.")]
    public string targetSpawnId;

    [Tooltip("Mensaje cuando la puerta aún no lleva a ningún lado.")]
    public string comingSoonMessage = "Coming soon";

    public override void Interact(PlayerInteractor interactor)
    {
        if (string.IsNullOrEmpty(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            if (!string.IsNullOrEmpty(targetScene))
                Debug.LogWarning($"La escena '{targetScene}' no está en el Build Profile.", this);
            ToastMessage.Show(comingSoonMessage);
            return;
        }

        base.Interact(interactor);
        SceneTransition.Go(targetScene, targetSpawnId);
    }
}
