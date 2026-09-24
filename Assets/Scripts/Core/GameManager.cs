using UnityEngine;
using UnityEngine.Events;

public enum GameState { Idle, Playing, BallRespawning, BuffSelection, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Config")]
    public GameConfig config;

    public GameState State { get; private set; } = GameState.Idle;
    public int Lives { get; private set; }
    public int Score { get; private set; }
    public int Wave { get; private set; } = 0;

    public UnityEvent<int> onLivesChanged  = new UnityEvent<int>();
    public UnityEvent<int> onScoreChanged  = new UnityEvent<int>();
    public UnityEvent<int> onWaveChanged   = new UnityEvent<int>();
    public UnityEvent      onGameOver      = new UnityEvent();
    public UnityEvent      onGameStart     = new UnityEvent();
    public UnityEvent      onBallLost      = new UnityEvent();
    public UnityEvent      onBuffSelection = new UnityEvent();   // Wave 结束时通知 Buff UI

    private int _maxHPBonus = 0;
    public int MaxLives => Mathf.Min(config.maxLives, config.initialLives + _maxHPBonus);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerProfile.Load();
        ApplyCyberVisuals();
    }

    private void Start()
    {
        // 进场景即开局，避免未点 Start 时波次/派兵/技能 CD 全部停摆
        if (State == GameState.Idle)
            StartGame();
    }

    private void ApplyCyberVisuals()
    {
        // ── 1. 升级所有的 Bumper ──
        foreach (var b in FindObjectsOfType<Bumper>())
        {
            var sr = b.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var bumperColor = NeonColors.Active.GetBase(NeonRole.Bumper);
                if (config != null && config.bumperRoundSprite != null)
                {
                    sr.sprite = config.bumperRoundSprite;
                    // 美术贴已含金属+霓虹；保持近白 tint，避免 HDR 调色盘染灰金属
                    sr.color = Color.white;
                    if (config.bumperMaterial != null)
                        sr.sharedMaterial = config.bumperMaterial;
                    else
                        sr.material = CyberVisualFactory.UnlitMaterial;
                }
                else
                {
                    sr.sprite = CyberVisualFactory.CreateBumperSprite(bumperColor);
                    sr.color = bumperColor;
                    sr.material = CyberVisualFactory.UnlitMaterial;
                }
                b.RefreshFromPalette();
            }
        }

        // ── 2. SpringBoard ──
        foreach (var s in FindObjectsOfType<SpringBoard>())
        {
            var sr = s.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = CyberVisualFactory.CreateSpringBoardSprite(s.chargedColor);
                sr.color = s.chargedColor;
                sr.material = CyberVisualFactory.UnlitMaterial;
            }
        }

        // ── 3. 升级所有的 BoostGear ──
        foreach (var bg in FindObjectsOfType<BoostGear>())
        {
            var sr = bg.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                var visual = bg.transform.Find("Visual");
                if (visual == null)
                {
                    var vgo = new GameObject("Visual");
                    visual = vgo.transform;
                    visual.SetParent(bg.transform, false);
                }
                sr = visual.GetComponent<SpriteRenderer>();
                if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 6;
            }

            if (config != null && config.boostGearSprite != null)
            {
                sr.sprite = config.boostGearSprite;
                sr.color = Color.white;
                if (config.boostGearMaterial != null)
                    sr.sharedMaterial = config.boostGearMaterial;
                else
                    sr.material = CyberVisualFactory.UnlitMaterial;
            }
            else
            {
                sr.sprite = CyberVisualFactory.CreateBoostGearSprite(bg.boostTrailColor);
                sr.color = bg.boostTrailColor;
                sr.material = CyberVisualFactory.UnlitMaterial;
            }
        }

        // ── 4. Portal ──
        foreach (var p in FindObjectsOfType<Portal>())
        {
            var sr = p.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = CyberVisualFactory.CreatePortalSprite(p.portalColor);
                sr.color = p.portalColor;
                sr.material = CyberVisualFactory.UnlitMaterial;
            }
        }

        // ── 5. ReflectivePrism ──
        foreach (var pr in FindObjectsOfType<ReflectivePrism>())
        {
            var sr = pr.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = CyberVisualFactory.CreatePrismSprite(pr.prismColor);
                sr.color = pr.prismColor;
                sr.material = CyberVisualFactory.UnlitMaterial;
            }
        }

        // ── 6. EnergyCannon（炮口 Visual）──
        foreach (var ec in FindObjectsOfType<EnergyCannon>())
        {
            if (ec.muzzle == null) continue;
            var sr = ec.muzzle.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;
            sr.sprite = CyberVisualFactory.CreateCannonSprite(ec.readyColor);
            sr.color = ec.readyColor;
            sr.material = CyberVisualFactory.UnlitMaterial;
        }
    }

    public void StartGame()
    {
        RunSettlement.ResetForNewRun();
        RunSession.ResetForNewRun();
        RunTelemetry.ResetForNewRun();
        RunSession.SnapshotWaveScore(0);
        Lives = config.initialLives;
        Score = 0;
        Wave = 0;
        State = GameState.Playing;

        // 显式清 Buff / Combo 战斗态，避免 OnEnable 订事件竞态导致旧层残留
        BuffManager.Instance?.ResetForNewGame();
        ComboCombat.DevReset();

        onGameStart.Invoke();
        onLivesChanged.Invoke(Lives);
        onScoreChanged.Invoke(Score);
        onWaveChanged.Invoke(Wave);
    }

    public void BallFellDown()
    {
        if (State != GameState.Playing) return;
        State = GameState.BallRespawning;
        onBallLost.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (State != GameState.Playing && State != GameState.BallRespawning) return;
        if (RunSession.IsTutorial)
        {
            // 教学中保留至少 1 命，避免误触底直接结束校准
            Lives = Mathf.Max(1, Lives - amount);
            onLivesChanged.Invoke(Lives);
            CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
            return;
        }

        Lives = Mathf.Max(0, Lives - amount);
        onLivesChanged.Invoke(Lives);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        if (Lives <= 0)
            TriggerGameOver();
    }

    public void OnBallRespawned()
    {
        if (State == GameState.BallRespawning)
            State = GameState.Playing;
    }

    public void AddScore(int points)
    {
        Score = Mathf.Max(0, Score + points);
        onScoreChanged.Invoke(Score);
    }

    public void CompleteWave()
    {
        RunSession.AwardChipsForCompletedWave(Score);
        Wave++;
        onWaveChanged.Invoke(Wave);
        State = GameState.BuffSelection;
        onBuffSelection.Invoke();
    }

    public void OnBuffSelectionDone()
    {
        if (State == GameState.BuffSelection)
            State = GameState.Playing;
    }

    public void Heal(int amount)
    {
        Lives = Mathf.Min(MaxLives, Lives + amount);
        onLivesChanged.Invoke(Lives);
    }

    public void SetMaxHPBonus(int bonus)
    {
        int cap = config.maxLives - config.initialLives;
        int prevMax = MaxLives;
        _maxHPBonus = Mathf.Clamp(bonus, 0, cap);
        Lives = Mathf.Min(MaxLives, Lives);
        if (MaxLives > prevMax)
            Lives = Mathf.Min(MaxLives, Lives + (MaxLives - prevMax));
        onLivesChanged.Invoke(Lives);
    }

    public void TriggerGameOver()
    {
        State = GameState.GameOver;
        onGameOver.Invoke();
    }

    public bool IsPlaying() => State == GameState.Playing;

    /// <summary>局内模拟是否运行：含待发球、球重生；不含 Idle / Buff 选卡 / GameOver。</summary>
    public bool IsWaveSimActive() =>
        State == GameState.Playing || State == GameState.BallRespawning;
}
