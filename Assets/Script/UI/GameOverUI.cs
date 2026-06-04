using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI instance;

    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public TextMeshProUGUI wavesSurvivedText;
    public TextMeshProUGUI enemiesKilledText;
    public Button mainMenuButton;

    void Awake()
    {
        instance = this;
        victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    void Start()
    {
        mainMenuButton.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        });
    }

    public void ShowVictory(int enemiesKilled)
    {
        victoryPanel.SetActive(true);
        if (enemiesKilledText != null)
            enemiesKilledText.text = "击杀敌人: " + enemiesKilled;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowDefeat(int enemiesKilled)
    {
        if (defeatPanel != null) defeatPanel.SetActive(true);
        victoryPanel.SetActive(true);
        if (enemiesKilledText != null)
            enemiesKilledText.text = "击杀敌人: " + enemiesKilled;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
