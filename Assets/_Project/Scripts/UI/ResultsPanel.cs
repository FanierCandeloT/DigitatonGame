using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pantalla de resultados (temporal): "Results screen (pending)" y un botón para volver al menú.</summary>
public class ResultsPanel : MonoBehaviour
{
    public GameObject root;
    public Button backButton;
    public string mainMenuScene = "MainMenu";

    bool wired;

    void Wire()
    {
        if (wired) return;
        wired = true;
        backButton.onClick.AddListener(() =>
        {
            Hide();
            GameManager.Instance.CloseResults();
            SceneTransition.Go(mainMenuScene, null);
        });
    }

    void Start()
    {
        Wire();
        root.SetActive(false);
    }

    public void Show()
    {
        Wire();
        root.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
