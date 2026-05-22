using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("HUD References")]
    public Text  scoreText;
    public Text  waveText;
    public Image[] lifeIcons;

    [Header("Panels")]
    public GameObject gameOverPanel;
    public GameObject startPanel;
    public Text finalScoreText;

    private int _comboDisplayThreshold = 3;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onLivesChanged.AddListener(UpdateLives);
            GameManager.Instance.onScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.onWaveChanged.AddListener(UpdateWave);
            GameManager.Instance.onGameOver.AddListener(ShowGameOver);
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            _comboDisplayThreshold = GameManager.Instance.config != null
                ? GameManager.Instance.config.comboDisplayThreshold : 3;
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startPanel    != null) startPanel.SetActive(true);
    }

    private void OnGameStart()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateLives(GameManager.Instance.Lives);
        UpdateScore(0);
        UpdateWave(0);
    }

    private void UpdateLives(int lives)
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null)
                lifeIcons[i].enabled = i < lives;
        }
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    private void UpdateWave(int wave)
    {
        if (waveText != null)
            waveText.text = "Wave " + wave;
    }

    private void ShowGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null && GameManager.Instance != null)
            finalScoreText.text = "Score: " + GameManager.Instance.Score;
    }

    public void OnStartButtonClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    public void OnRestartButtonClicked()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }
}
