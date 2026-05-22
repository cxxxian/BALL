using UnityEngine;
using UnityEngine.Events;

public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    public int CurrentCombo { get; private set; }

    public UnityEvent<int> onComboChanged = new UnityEvent<int>();

    private float _lastHitTime = -99f;

    private GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
    }

    private void Update()
    {
        if (Config == null || CurrentCombo == 0) return;
        if (Time.time - _lastHitTime > Config.comboTimeout)
            ResetCombo();
    }

    // Bumper/Slingshot 命中时调用
    public void RegisterHit()
    {
        if (Config == null) return;

        if (Time.time - _lastHitTime > Config.comboTimeout)
            CurrentCombo = 0;

        CurrentCombo++;
        _lastHitTime = Time.time;
        onComboChanged.Invoke(CurrentCombo);
    }

    public void ResetCombo()
    {
        if (CurrentCombo == 0) return;
        CurrentCombo = 0;
        onComboChanged.Invoke(0);
    }

    private void OnGameStart()
    {
        CurrentCombo = 0;
        _lastHitTime = -99f;
        onComboChanged.Invoke(0);
    }
}
