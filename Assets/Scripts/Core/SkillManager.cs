using UnityEngine;
using UnityEngine.Events;

public enum ActiveSkillType { ExecuteChain, BlockShield }

[System.Serializable]
public class SkillSlot
{
    public ActiveSkillType type        = ActiveSkillType.ExecuteChain;
    public float           maxCooldown = 12f;

    [System.NonSerialized] public float currentCD = 0f;

    public float CooldownRatio => maxCooldown > 0f ? Mathf.Clamp01(currentCD / maxCooldown) : 0f;
    public bool  IsReady       => currentCD <= 0f;
}

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("技能槽（最多 2 个）")]
    public SkillSlot[] slots = new SkillSlot[]
    {
        new SkillSlot { type = ActiveSkillType.ExecuteChain, maxCooldown = 12f },
        new SkillSlot { type = ActiveSkillType.BlockShield,  maxCooldown = 15f }
    };

    // ── 瞄准状态（仅 ExecuteChain 需要）─────────────────────────────────
    public bool IsAiming   { get; private set; }
    public int  AimingSlot { get; private set; } = -1;

    // ── 事件 ──────────────────────────────────────────────────────────────
    [HideInInspector] public UnityEvent<int, float> onSlotCooldownChanged  = new UnityEvent<int, float>();
    [HideInInspector] public UnityEvent<int>        onSlotActivated        = new UnityEvent<int>();
    [HideInInspector] public UnityEvent             onExecuteChainActivated = new UnityEvent();
    [HideInInspector] public UnityEvent<Vector2>    onFired                = new UnityEvent<Vector2>();
    [HideInInspector] public UnityEvent             onExecuteChainStarted  = new UnityEvent();

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
        if (GameManager.Instance?.State != GameState.Playing) return;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsReady) continue;
            slot.currentCD = Mathf.Max(0f, slot.currentCD - Time.deltaTime);
            onSlotCooldownChanged.Invoke(i, slot.CooldownRatio);
        }
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= 0) return;
        float reduce = Config != null ? Config.skillComboCDReduce : 0.4f;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsReady) continue;
            slot.currentCD = Mathf.Max(0f, slot.currentCD - reduce);
            onSlotCooldownChanged.Invoke(i, slot.CooldownRatio);
        }
    }

    // ── 激活指定槽位 ─────────────────────────────────────────────────────
    public bool TryActivate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;
        var slot = slots[slotIndex];
        if (!slot.IsReady) return false;
        if (IsAiming)      return false;
        if (GameManager.Instance?.State != GameState.Playing) return false;

        switch (slot.type)
        {
            case ActiveSkillType.ExecuteChain:
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                IsAiming   = true;
                AimingSlot = slotIndex;
                onSlotActivated.Invoke(slotIndex);
                onExecuteChainActivated.Invoke();
                break;

            case ActiveSkillType.BlockShield:
                BlockShield.Instance?.Activate();
                StartCooldown(slotIndex);
                onSlotActivated.Invoke(slotIndex);
                break;
        }
        return true;
    }

    // ── BulletTimeAim 瞄准完成后调用 ─────────────────────────────────────
    public void Fire(Vector2 direction)
    {
        if (!IsAiming) return;
        int idx    = AimingSlot;
        IsAiming   = false;
        AimingSlot = -1;

        StartCooldown(idx);
        onFired.Invoke(direction);

        if (BallController.Instance != null && direction.sqrMagnitude > 0.001f)
        {
            BallController.Instance.StartExecuteChain(3);
            onExecuteChainStarted.Invoke();
        }
    }

    // ── 开始冷却（含 Buff 减 CD 修正）────────────────────────────────────
    private void StartCooldown(int slotIndex)
    {
        var slot  = slots[slotIndex];
        float cd  = slot.maxCooldown;
        if (BuffManager.Instance != null)
            cd = Mathf.Max(2f, cd * (1f - BuffManager.Instance.ComboThresholdReduction * 0.15f));
        slot.currentCD = cd;
        onSlotCooldownChanged.Invoke(slotIndex, slot.CooldownRatio);
    }

    // ── 向后兼容：无参版本默认激活槽 0 ──────────────────────────────────
    public bool TryActivate() => TryActivate(0);

    private void OnGameStart()
    {
        IsAiming   = false;
        AimingSlot = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].currentCD = 0f;
            onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
