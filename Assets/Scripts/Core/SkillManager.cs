using UnityEngine;
using UnityEngine.Events;

public enum ActiveSkillType { BulletTimeAim, ExecuteChain }

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("Skill Settings")]
    public ActiveSkillType activeSkill = ActiveSkillType.ExecuteChain; // Default to the epic new ExecuteChain!

    public float CooldownRatio => _maxCD > 0f ? Mathf.Clamp01(_currentCD / _maxCD) : 0f;
    public bool  IsReady       => _currentCD <= 0f;
    public bool  IsActive      { get; private set; }

    public UnityEvent<float>   onCooldownChanged = new UnityEvent<float>();   // 0..1，0=就绪
    public UnityEvent          onActivated       = new UnityEvent();
    public UnityEvent          onExecuteChainStarted = new UnityEvent();      // 专门给斩杀连锁的事件
    public UnityEvent<Vector2> onFired           = new UnityEvent<Vector2>(); // zero=取消

    private float _currentCD;
    private float _maxCD;

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
    }

    private void Update()
    {
        if (IsActive || IsReady) return;
        if (GameManager.Instance?.State != GameState.Playing) return;

        _currentCD = Mathf.Max(0f, _currentCD - Time.deltaTime);
        onCooldownChanged.Invoke(CooldownRatio);
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= 0 || IsActive || IsReady) return;
        float reduce = Config != null ? Config.skillComboCDReduce : 0.4f;
        _currentCD = Mathf.Max(0f, _currentCD - reduce);
        onCooldownChanged.Invoke(CooldownRatio);
    }

    public bool TryActivate()
    {
        if (!IsReady || IsActive) return false;
        if (GameManager.Instance?.State != GameState.Playing) return false;
        if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;

        // 统一在激活时开启时缓和瞄准导向
        IsActive = true;
        onActivated.Invoke();
        return true;
    }

    public void Fire(Vector2 direction)
    {
        if (!IsActive) return;
        IsActive   = false;
        _maxCD     = Config != null ? Config.skillCooldown : 12f;
        // 应用被动 Buff 减 CD（连击大师等）
        if (BuffManager.Instance != null)
            _maxCD = Mathf.Max(2f, _maxCD * (1f - BuffManager.Instance.ComboThresholdReduction * 0.15f));

        _currentCD = _maxCD;
        onCooldownChanged.Invoke(CooldownRatio);

        // 发射事件（通知 BulletTimeAim 恢复时间并给予初始发射速度）
        onFired.Invoke(direction);

        // 核心融合：发射后，弹珠立即进入 3 次极速斩杀连锁锁定模式！
        if (BallController.Instance != null && direction.sqrMagnitude > 0.001f)
        {
            BallController.Instance.StartExecuteChain(3); // 3 连斩启动！
            onExecuteChainStarted.Invoke();
        }
    }

    private void OnGameStart()
    {
        IsActive   = false;
        _currentCD = 0f;
        _maxCD     = Config != null ? Config.skillCooldown : 12f;
        onCooldownChanged.Invoke(0f);
    }
}
