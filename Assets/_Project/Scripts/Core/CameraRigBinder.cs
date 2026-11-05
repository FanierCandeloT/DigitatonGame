using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Va en el prefab CameraRig. Al empezar la escena conecta la Cinemachine Camera con Andrés
/// (objeto con PlayerController) y con los límites del mapa (MapBounds) para el Confiner 2D.
/// </summary>
public class CameraRigBinder : MonoBehaviour
{
    public CinemachineCamera virtualCamera;
    public CinemachineConfiner2D confiner;

    void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && virtualCamera != null)
        {
            virtualCamera.Follow = player.transform;
            // Evita que la cámara "viaje" desde otra posición al aparecer en un SpawnPoint.
            virtualCamera.PreviousStateIsValid = false;
        }
        else if (player == null)
        {
            Debug.LogWarning("CameraRig: no hay jugador en la escena.", this);
        }

        var bounds = FindFirstObjectByType<MapBounds>();
        if (bounds != null && confiner != null)
        {
            confiner.BoundingShape2D = bounds.Shape;
            confiner.InvalidateBoundingShapeCache();
        }
        else if (bounds == null)
        {
            Debug.LogWarning("CameraRig: no hay MapBounds en la escena; la cámara no estará limitada.", this);
        }
    }
}
