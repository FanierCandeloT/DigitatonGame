using System;
using UnityEngine;

/// <summary>
/// Cinemática como asset: una lista de tomas (imagen + texto + movimiento de cámara). Se edita sin tocar código:
/// Assets ▸ Create ▸ DigitatonGame ▸ Cinematic. La introducción vive en Resources/Cinematics/Intro.asset;
/// si ese asset no existe, el juego usa las pantallas de texto de StoryFlow.IntroCaptions.
/// </summary>
[CreateAssetMenu(fileName = "Cinematic", menuName = "DigitatonGame/Cinematic")]
public class CinematicAsset : ScriptableObject
{
    public enum Move { None, ZoomIn, ZoomOut, PanLeft, PanRight }

    [Serializable]
    public class Shot
    {
        public string name;
        public Sprite image;

        [Tooltip("Texto en inglés que aparece con máquina de escribir debajo de la imagen. Vacío = sin texto.")]
        [TextArea(1, 4)] public string caption;

        [Tooltip("Segundos que se queda la toma DESPUÉS de terminar de escribir el texto (o en total si no hay texto).")]
        public float holdSeconds = 2f;

        public Move move = Move.ZoomIn;

        [Tooltip("Color que oscurece o tiñe la toma (blanco = sin cambio).")]
        public Color tint = Color.white;
    }

    public Shot[] shots;

    [Tooltip("Segundos del fundido entre una toma y la siguiente.")]
    public float crossFadeSeconds = 0.6f;
}
