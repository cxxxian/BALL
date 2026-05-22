using UnityEngine;
using UnityEngine.Events;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    public float CooldownRatio => _maxCD > 0f ? Mathf.Clamp01(_currentCD / _maxCD) : 0f;
    public bool  IsReady       => _currentCD <= 0f;
    public bool  IsActive      { get; private set; }

    public UnityEvent<float>   onCooldownChanged = new UnityEvent<float>();   // 0..1，0=就绪
    public UnityEvent          onActivated       = new UnityEvent();
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

        IsActive = true;
        onActivated.Invoke();
        return true;
    }

    public void Fire(Vector2 direction)
    {
        if (!IsActive) return;
        IsActive   = false;
        _maxCD     = Config != null ? Config.skillCooldown : 12f;
        _currentCD = _maxCD;
        onCooldownChanged.Invoke(CooldownRatio);
        onFired.Invoke(direction);
    }

    private void OnGameStart()
    {
        IsActive   = false;
        _currentCD = 0f;
        _maxCD     = Config != null ? Config.skillCooldown : 12f;
        onCooldownChanged.Invoke(0f);
    }
}
