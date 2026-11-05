using UnityEngine;

/// <summary>
/// Puerta amarilla: al presionar E carga otra escena y coloca a Andrés en el SpawnPoint indicado.
///  - Si tiene requiredFlag y esa marca del guion aún no existe, muestra lockedMessage y no deja pasar.
///  - Si no tiene escena destino (o la escena no está en el Build Profile), muestra "Coming soon".
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

    [Header("Bloqueo por guion")]
    [Tooltip("Marca del GameManager que debe existir para poder pasar (por ejemplo 'done:q1_4'). Vacío = siempre abierta.")]
    public string requiredFlag;

    [Tooltip("Mensaje mientras la puerta está bloqueada.")]
    public string lockedMessage = "Locked for now.";

    [Tooltip("Si está activo, al dar Play directo en una escena del juego (modo desarrollo) la puerta se abre sola.")]
    public bool devBypass = true;

    public override void Interact(PlayerInteractor interactor)
    {
        var gm = GameManager.Instance;

        if (!string.IsNullOrEmpty(requiredFlag) && !gm.HasFlag(requiredFlag) && !(devBypass && gm.HasFlag(GameManager.FlagDevOpen)))
        {
            ToastMessage.Show(lockedMessage);
            return;
        }

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
