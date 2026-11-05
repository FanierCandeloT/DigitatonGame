using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contador "Memories: X/12" en la esquina superior derecha.
/// Solo se ve durante el juego (cuando la escena tiene a Andrés): no aparece en el menú ni en el tutorial.
/// </summary>
public class MemoryHud : MonoBehaviour
{
    public TMP_Text label;
    public GameObject root;

    static readonly string[] HiddenScenes = { "MainMenu", "Tutorial" };

    void OnEnable()
    {
        GameManager.MemoriesChanged += OnMemoriesChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        GameManager.MemoriesChanged -= OnMemoriesChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start() => Refresh();

    void OnMemoriesChanged(int value) => Refresh();
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    void Refresh()
    {
        label.text = $"Memories: {GameManager.Instance.MemoriesRecovered}/{GameManager.MaxMemories}";

        bool inGame = FindAnyObjectByType<PlayerController>() != null &&
                      System.Array.IndexOf(HiddenScenes, SceneManager.GetActiveScene().name) < 0;
        root.SetActive(inGame);
    }
}
