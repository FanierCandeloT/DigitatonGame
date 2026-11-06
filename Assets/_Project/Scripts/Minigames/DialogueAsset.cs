using System;
using UnityEngine;

/// <summary>
/// Un diálogo como asset: lista de líneas (quién habla y qué dice), pueden alternar personajes distintos.
/// Se crea desde el menú Assets ▸ Create ▸ DigitatonGame ▸ Dialogue, se guarda en Assets/_Project/Data/Dialogues y
/// se asigna al NPC (campo "Dialogue" de ToastInteractable). Así se escribe sin tocar código.
/// Las líneas de Andrés pueden traer sus 3 versiones (inglés, mezcla con {palabras} y español).
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "DigitatonGame/Dialogue")]
public class DialogueAsset : ScriptableObject
{
    [Tooltip("Líneas en orden. En cada una se escribe el nombre de quien habla (Lucía, Don Jairo, Andrés...).")]
    public DialogueLine[] lines;

    public bool HasLines => lines != null && lines.Length > 0;

    /// <summary>Abre el diálogo (Andrés queda quieto mientras dura) y llama a onDone al terminar.</summary>
    public void Show(Action onDone = null) => DialoguePanel.Show(lines, onDone);
}
