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

    /// <summary>Cuánto sube Andrés en la pantalla mientras el panel de bloques está abierto, para no tapar el mundo (en Cinemachine, negativo = más arriba).</summary>
    const float BuilderLift = -0.22f;

    CinemachinePositionComposer composer;

    void OnEnable()
    {
        SentenceBuilder.Opened += LiftForBuilder;
        SentenceBuilder.Closed += LowerAfterBuilder;
    }

    void OnDisable()
    {
        SentenceBuilder.Opened -= LiftForBuilder;
        SentenceBuilder.Closed -= LowerAfterBuilder;
    }

    void LiftForBuilder() => SetScreenY(BuilderLift);
    void LowerAfterBuilder() => SetScreenY(0f);

    void SetScreenY(float y)
    {
        if (composer == null && virtualCamera != null) composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null) return;
        var composition = composer.Composition;
        composition.ScreenPosition = new Vector2(composition.ScreenPosition.x, y);
        composer.Composition = composition;
    }

    void Start()
    {
        // El formato de pantalla se aplica aquí en todas las escenas, así ninguna se queda con un zoom distinto.
        var cam = GetComponentInChildren<Camera>();
        PixelCameraSettings.Apply(cam, cam != null ? cam.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>() : null);
        if (virtualCamera != null)
        {
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = PixelCameraSettings.OrthoSize;
            virtualCamera.Lens = lens;
        }

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
            boundsBox = bounds.GetComponent<BoxCollider2D>();
            if (boundsBox != null) authoredBoundsSize = boundsBox.size;
            FitBoundsToView();
            confiner.InvalidateBoundingShapeCache();
        }
        else if (bounds == null)
        {
            Debug.LogWarning("CameraRig: no hay MapBounds en la escena; la cámara no estará limitada.", this);
        }
    }

    // ───────────── Límites del mapa vs. lo que ve la cámara ─────────────

    BoxCollider2D boundsBox;
    Vector2 authoredBoundsSize;
    int lastScreenWidth, lastScreenHeight;

    void LateUpdate()
    {
        // Si la ventana cambia de tamaño, el Pixel Perfect Camera cambia el zoom: los límites se vuelven a ajustar
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) FitBoundsToView();
    }

    /// <summary>
    /// El Confiner 2D de Cinemachine deja la cámara CONGELADA en el centro si el MapBounds es más chico que la vista en
    /// algún eje (en ese caso no puede moverla dentro del límite). Para que las salas pequeñas o bajitas (Sótanos, Museo...)
    /// no dejen a Andrés salirse de la pantalla, los límites se agrandan en memoria hasta cubrir lo que ve la cámara;
    /// lo que sobra se ve negro. No cambia lo que está guardado en la escena.
    /// </summary>
    void FitBoundsToView()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        if (boundsBox == null || confiner == null) return;

        var view = PixelCameraSettings.VisibleSize(Screen.width, Screen.height);
        var scale = boundsBox.transform.lossyScale;
        var needed = new Vector2(view.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f), view.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f)) + Vector2.one * 0.5f;
        var size = new Vector2(Mathf.Max(authoredBoundsSize.x, needed.x), Mathf.Max(authoredBoundsSize.y, needed.y));

        if (boundsBox.size == size) return;
        boundsBox.size = size;
        confiner.InvalidateBoundingShapeCache();
    }
}
