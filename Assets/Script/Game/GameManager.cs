using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [HideInInspector] public bool isGameOver;
    [HideInInspector] public int totalKills;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }

    public void AddKill()
    {
        totalKills++;
    }

    public void GameOver(bool isVictory)
    {
        if (isGameOver) return;
        isGameOver = true;

        if (GameOverUI.instance != null)
        {
            if (isVictory)
                GameOverUI.instance.ShowVictory(totalKills);
            else
                GameOverUI.instance.ShowDefeat(totalKills);
        }
    }
}
