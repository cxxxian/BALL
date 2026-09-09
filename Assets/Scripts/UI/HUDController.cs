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
    public Text creditsEarnedText;
    public Text totalCreditsText;
    public Text waveReachedText;

    private static readonly Color ShieldFlashColor = new Color(0.2f, 0.95f, 1f, 1f);

    private Coroutine _shieldFlashCoroutine;
    private Coroutine _scorePunchCoroutine;
    private Vector3 _scoreBaseScale = Vector3.one;

    public bool IsGameOverVisible =>
        (SettlementController.Instance != null && SettlementController.Instance.IsVisible)
        || (gameOverPanel != null && gameOverPanel.activeSelf);

    private void Awake()
    {
        Instance = this;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        EnsureScorePopUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ApplyHudGlowStyles();
        if (scoreText != null)
            _scoreBaseScale = scoreText.rectTransform.localScale;

        EnsureScorePopUI();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onLivesChanged.AddListener(UpdateLives);
            GameManager.Instance.onScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.onWaveChanged.AddListener(UpdateWave);
            GameManager.Instance.onGameOver.AddListener(ShowGameOver);
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        SettlementController.Instance?.Hide();
    }

    private void OnGameStart()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        SettlementController.Instance?.Hide();
        RunSettlement.ResetForNewRun();
        UpdateLives(GameManager.Instance.Lives);
        UpdateScore(0);
        UpdateWave(0);
    }

    private void UpdateLives(int lives)
    {
        if (lifeIcons == null) return;
        lives = Mathf.Max(0, lives);
        int maxSlots = lifeIcons.Length;
        int cap = GameManager.Instance != null ? Mathf.Min(maxSlots, GameManager.Instance.MaxLives) : maxSlots;
        for (int i = 0; i < maxSlots; i++)
        {
            if (lifeIcons[i] == null) continue;
            // 槽位按 MaxLives 显示；满血亮、空槽压暗（不再整颗隐藏）
            bool inCap = i < cap;
            lifeIcons[i].gameObject.SetActive(inCap);
            if (!inCap) continue;
            lifeIcons[i].enabled = true;
            bool filled = i < lives;
            lifeIcons[i].color = filled
                ? NeonUiColors.DangerUi(1.15f)
                : new Color(1f, 1f, 1f, 0.14f);
            if (filled)
            {
                var glow = lifeIcons[i].GetComponent<CyberHudGlow>();
                if (glow != null) glow.enabled = true;
                var outline = lifeIcons[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = true;
                CyberHudGlow.Ensure(lifeIcons[i], CyberHudGlow.GlowStyle.DangerRed);
            }
            else
            {
                var glow = lifeIcons[i].GetComponent<CyberHudGlow>();
                if (glow != null) glow.enabled = false;
                var outline = lifeIcons[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
            }
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

    private void EnsureScorePopUI()
    {
        if (ScorePopUI.Instance != null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("ScorePopUI", typeof(RectTransform), typeof(ScorePopUI));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = Mathf.Max(0, score).ToString("000000");
    }

    /// <summary>得分飘字吸入时轻弹 Score。</summary>
    public void PunchScore()
    {
        if (scoreText == null) return;
        if (_scorePunchCoroutine != null)
            StopCoroutine(_scorePunchCoroutine);
        _scorePunchCoroutine = StartCoroutine(ScorePunchRoutine());
    }

    private IEnumerator ScorePunchRoutine()
    {
        var rt = scoreText.rectTransform;
        float t = 0f;
        const float dur = 0.14f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = u < 0.35f
                ? Mathf.Lerp(1f, 1.18f, u / 0.35f)
                : Mathf.Lerp(1.18f, 1f, (u - 0.35f) / 0.65f);
            rt.localScale = _scoreBaseScale * s;
            yield return null;
        }
        rt.localScale = _scoreBaseScale;
        _scorePunchCoroutine = null;
    }

    private void UpdateWave(int wave)
    {
        // 左上 StatusCard 已有 WAVE 标签，数字单独显示
        if (waveText != null)
            waveText.text = Mathf.Max(0, wave).ToString("00");
    }

    private void ShowGameOver()
    {
        if (GameManager.Instance == null) return;

        var gm = GameManager.Instance;
        var result = RunSettlement.SettleRun(gm.Score, gm.Wave, gm.config);

        // 优先 UITK 赛博结算页；旧 uGUI 面板仅作回退
        if (SettlementController.Instance != null)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            SettlementController.Instance.Show(result);
            return;
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (waveReachedText != null)
            waveReachedText.text = $"Wave {result.Wave}";

        if (finalScoreText != null)
            finalScoreText.text = $"Score: {result.Score}";

        if (creditsEarnedText != null)
            creditsEarnedText.text = $"+{result.CreditsEarned} 协议币";

        if (totalCreditsText != null)
            totalCreditsText.text = $"累计 {result.TotalCredits} 协议币";

        if (creditsEarnedText == null && totalCreditsText == null && finalScoreText != null)
        {
            finalScoreText.text =
                $"Wave {result.Wave}\nScore: {result.Score}\n+{result.CreditsEarned} 协议币\n累计 {result.TotalCredits} 协议币";
        }
    }

    public void OnRestartButtonClicked()
    {
        SettlementController.Instance?.Hide();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }
}
