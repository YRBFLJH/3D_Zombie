using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public Button startGameButton;
    public Button loadGameButton;
    public Button quitButton;
    public GameObject loadGamePanel;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        startGameButton.onClick.AddListener(StartNewGame);
        quitButton.onClick.AddListener(() => Application.Quit());

        if (loadGameButton != null)
        {
            loadGameButton.onClick.AddListener(() =>
            {
                if (loadGamePanel != null) loadGamePanel.SetActive(true);
            });
        }

        if (loadGamePanel != null) loadGamePanel.SetActive(false);
    }

    void StartNewGame()
    {
        SceneManager.LoadScene("Game");
    }
}
