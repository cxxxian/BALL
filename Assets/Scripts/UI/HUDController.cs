using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("HUD References")]
    public Text  scoreText;
    public Text  waveText;
    public Image[] lifeIcons;

    [Header("Panels")]
    public GameObject gameOverPanel;
    public Text finalScoreText;

    private static readonly Color ShieldFlashColor = new Color(0.2f, 0.95f, 1f, 1f);

    private Coroutine _shieldFlashCoroutine;

    public bool IsGameOverVisible =>
        gameOverPanel != null && gameOverPanel.activeSelf;

    private void Awake()
    {
        Instance = this;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ApplyHudGlowStyles();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onLivesChanged.AddListener(UpdateLives);
            GameManager.Instance.onScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.onWaveChanged.AddListener(UpdateWave);
            GameManager.Instance.onGameOver.AddListener(ShowGameOver);
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void OnGameStart()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateLives(GameManager.Instance.Lives);
        UpdateScore(0);
        UpdateWave(0);
    }

    private void UpdateLives(int lives)
    {
        if (lifeIcons == null) return;
        lives = Mathf.Max(0, lives);
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] == null) continue;
            bool show = i < lives;
            lifeIcons[i].gameObject.SetActive(show);
            if (!show) continue;
            lifeIcons[i].enabled = true;
            lifeIcons[i].color = NeonUiColors.DangerUi(1.1f);
            CyberHudGlow.Ensure(lifeIcons[i], CyberHudGlow.GlowStyle.DangerRed);
        }
    }

    public void PlayHeartGuardShieldVfx()
    {
        if (_shieldFlashCoroutine != null)
            StopCoroutine(_shieldFlashCoroutine);
        _shieldFlashCoroutine = StartCoroutine(ShieldFlashCoroutine());
    }

    private IEnumerator ShieldFlashCoroutine()
    {
        if (lifeIcons == null || GameManager.Instance == null) yield break;

        int lives = GameManager.Instance.Lives;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] == null || i >= lives) continue;
            lifeIcons[i].color = ShieldFlashColor;
        }

        yield return new WaitForSeconds(0.35f);

        UpdateLives(GameManager.Instance.Lives);
        _shieldFlashCoroutine = null;
    }

    private void ApplyHudGlowStyles()
    {
        CyberHudGlow.Ensure(waveText, CyberHudGlow.GlowStyle.BumperCyan);
        CyberHudGlow.Ensure(scoreText, CyberHudGlow.GlowStyle.WhiteScore);
        if (lifeIcons == null) return;
        foreach (var icon in lifeIcons)
        {
            if (icon == null || !icon.gameObject.activeInHierarchy) continue;
            CyberHudGlow.Ensure(icon, CyberHudGlow.GlowStyle.DangerRed);
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

    public void OnRestartButtonClicked()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }
}
