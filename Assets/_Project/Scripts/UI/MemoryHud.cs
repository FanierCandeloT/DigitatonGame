using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contador "Memories: X/12" en la esquina superior derecha, con el estado del habla de Andrés debajo
/// ("SPEECH 33% · confused"). Cuando el habla mejora de etapa avisa con un mensaje, cuando ya no hay diálogos abiertos.
/// Solo se ve durante el juego (cuando la escena tiene a Andrés): no aparece en el menú ni en el tutorial.
/// </summary>
public class MemoryHud : MonoBehaviour
{
    public TMP_Text label;
    public GameObject root;

    static readonly string[] HiddenScenes = { "MainMenu", "Tutorial" };

    TextMeshProUGUI speechLabel, objectiveLabel;
    int lastMemories;

    void OnEnable()
    {
        GameManager.MemoriesChanged += OnMemoriesChanged;
        GameManager.ProgressChanged += Refresh;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameManager.MemoriesChanged -= OnMemoriesChanged;
        GameManager.ProgressChanged -= Refresh;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        BuildSpeechLabel();
        lastMemories = GameManager.Instance.MemoriesRecovered;
        Refresh();
    }

    void OnMemoriesChanged(int value)
    {
        string message = SpeechRecovery.ImprovementMessage(lastMemories, value);
        lastMemories = value;
        if (message != null) StartCoroutine(AnnounceWhenFree(message));
        Refresh();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    /// <summary>Estado del habla justo debajo del contador de recuerdos.</summary>
    void BuildSpeechLabel()
    {
        var go = new GameObject("Habla", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        speechLabel = go.AddComponent<TextMeshProUGUI>();
        speechLabel.font = label.font;
        speechLabel.fontSize = 22f;
        speechLabel.fontStyle = FontStyles.Bold;
        speechLabel.alignment = TextAlignmentOptions.Right;
        speechLabel.raycastTarget = false;

        var rect = speechLabel.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(0f, -4f);
        rect.sizeDelta = new Vector2(360f, 30f);

        // Objetivo actual (lo que sigue en la historia), justo debajo del habla
        var objective = new GameObject("Objetivo", typeof(RectTransform));
        objective.transform.SetParent(root.transform, false);
        objectiveLabel = objective.AddComponent<TextMeshProUGUI>();
        objectiveLabel.font = label.font;
        objectiveLabel.fontSize = 20f;
        objectiveLabel.alignment = TextAlignmentOptions.TopRight;
        objectiveLabel.textWrappingMode = TextWrappingModes.Normal;
        objectiveLabel.color = new Color(0.85f, 0.9f, 1f);
        objectiveLabel.raycastTarget = false;
        var objectiveRect = objectiveLabel.rectTransform;
        objectiveRect.anchorMin = new Vector2(1f, 0f);
        objectiveRect.anchorMax = new Vector2(1f, 0f);
        objectiveRect.pivot = new Vector2(1f, 1f);
        objectiveRect.anchoredPosition = new Vector2(0f, -38f);
        objectiveRect.sizeDelta = new Vector2(420f, 64f);
    }

    void Refresh()
    {
        var gm = GameManager.Instance;
        label.text = $"Memories: {gm.MemoriesRecovered}/{GameManager.MaxMemories}";

        if (speechLabel != null)
        {
            var stage = SpeechRecovery.Stage;
            speechLabel.text = $"SPEECH {Mathf.RoundToInt(SpeechRecovery.Progress * 100f)}%  -  {SpeechRecovery.StageName(stage)}";
            speechLabel.color = SpeechRecovery.StageColor(stage);
        }

        if (objectiveLabel != null)
        {
            string objective = StoryFlow.CurrentObjective(gm);
            objectiveLabel.text = string.IsNullOrEmpty(objective) ? "" : $"<b>OBJECTIVE</b>\n{objective}";
        }

        bool inGame = FindAnyObjectByType<PlayerController>() != null &&
                      System.Array.IndexOf(HiddenScenes, SceneManager.GetActiveScene().name) < 0;
        root.SetActive(inGame);
    }

    /// <summary>Espera a que se cierren diálogos y minijuegos para que el aviso no tape ni sea tapado.</summary>
    IEnumerator AnnounceWhenFree(string message)
    {
        yield return null;
        while (GameManager.Instance.ModalOpen || GameManager.Instance.IsTransitioning)
            yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
        ToastMessage.Show(message, 3.5f);
    }
}
