using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeathScreenUI : MonoBehaviour
{
    public static DeathScreenUI instance;

    public GameObject deathPanel;
    public TextMeshProUGUI respawnCountdownText;
    public Button respawnButton;
    public Button mainMenuButton;

    private Player_State playerState;

    void Awake()
    {
        instance = this;
        deathPanel.SetActive(false);
    }

    void Start()
    {
        respawnButton.onClick.AddListener(OnRespawnClicked);
        mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    public void Show(Player_State state)
    {
        playerState = state;
        deathPanel.SetActive(true);
        respawnButton.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        deathPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UpdateCountdown(float seconds)
    {
        if (respawnCountdownText != null)
            respawnCountdownText.text = string.Format("{0:0.0}", seconds);

        if (seconds <= 0 && !respawnButton.gameObject.activeSelf)
            respawnButton.gameObject.SetActive(true);
    }

    void OnRespawnClicked()
    {
        Hide();
    }

    void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
