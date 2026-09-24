using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class SkillSlot
{
    public SkillDefinition definition;

    [System.NonSerialized] public float currentCD = 0f;

    public float MaxCooldown => definition != null ? definition.baseCooldown : 12f;
    public float CooldownRatio => MaxCooldown > 0f ? Mathf.Clamp01(currentCD / MaxCooldown) : 0f;
    public bool  IsReady       => currentCD <= 0f;
}

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    public const int MaxSlots = 2;
    public const string ProtocolRedirectSkillId = "protocol_redirect";

    [Header("技能槽（由 RunLoadout 注入）")]
    public SkillSlot[] slots = new SkillSlot[]
    {
        new SkillSlot(),
        new SkillSlot()
    };

    public SkillAimMode AimMode { get; private set; } = SkillAimMode.None;
    public bool IsAiming        => AimMode == SkillAimMode.ProtocolRedirect;
    public bool IsGroundAiming  => AimMode == SkillAimMode.GravityWell;
    public int  AimingSlot      { get; private set; } = -1;

    public bool IsExecuteArmed { get; private set; }
    private float _executeArmedUntilUnscaled;

    [HideInInspector] public UnityEvent<int, float> onSlotCooldownChanged   = new UnityEvent<int, float>();
    [HideInInspector] public UnityEvent<int>        onSlotActivated         = new UnityEvent<int>();
    /// <summary>协议改向瞄准开始（原 onExecuteChainActivated）。</summary>
    [HideInInspector] public UnityEvent             onProtocolRedirectActivated = new UnityEvent();
    [HideInInspector] public UnityEvent             onExecuteChainActivated = new UnityEvent();
    [HideInInspector] public UnityEvent<Vector2>     onFired                 = new UnityEvent<Vector2>();
    [HideInInspector] public UnityEvent             onAimingAborted         = new UnityEvent();
    [HideInInspector] public UnityEvent             onGroundAimAborted      = new UnityEvent();
    [HideInInspector] public UnityEvent<int>        onAimingEnded           = new UnityEvent<int>();
    [HideInInspector] public UnityEvent             onExecuteChainStarted   = new UnityEvent();
    [HideInInspector] public UnityEvent             onGravityWellAimStarted = new UnityEvent();
    [HideInInspector] public UnityEvent             onExecuteArmStarted     = new UnityEvent();
    [HideInInspector] public UnityEvent             onExecuteArmEnded       = new UnityEvent();

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GravityWellAim.EnsureInstance();
        CorePulse.EnsureInstance();
        SplitProtocol.EnsureInstance();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
        }
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
    }

    private void OnBallLost()
    {
        CancelAiming();
        ClearExecuteArm();
    }

    public void ApplyLoadout(RunCatalog catalog)
    {
        if (catalog == null) return;

        ClearExecuteArm();

        var redirect = catalog.GetSkill(ProtocolRedirectSkillId);
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots == null || i >= slots.Length)
                break;

            if (i == 0)
                slots[i].definition = redirect ?? RunLoadout.GetSkillInSlot(0, catalog);
            else
                slots[i].definition = RunLoadout.GetSkillInSlot(i, catalog);

            slots[i].currentCD = 0f;
            onSlotCooldownChanged.Invoke(i, 0f);
        }
    }

    private void Update()
    {
        if (IsExecuteArmed && Time.unscaledTime >= _executeArmedUntilUnscaled)
            ClearExecuteArm();

        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.IsWaveSimActive()) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsReady) continue;
            slot.currentCD = Mathf.Max(0f, slot.currentCD - dt);
            onSlotCooldownChanged.Invoke(i, slot.CooldownRatio);
        }
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= 0) return;
        float reduce = Config != null ? Config.skillComboCDReduce : 0.28f;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.IsReady) continue;
            slot.currentCD = Mathf.Max(0f, slot.currentCD - reduce);
            onSlotCooldownChanged.Invoke(i, slot.CooldownRatio);
        }
    }

    public void CancelAiming()
    {
        if (AimMode == SkillAimMode.GravityWell)
            CancelGroundAimInternal();
        if (AimMode != SkillAimMode.ProtocolRedirect) return;

        int idx = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;
        onAimingAborted.Invoke();
        onAimingEnded.Invoke(idx);
    }

    public void CancelGroundAim()
    {
        if (AimMode != SkillAimMode.GravityWell) return;
        CancelGroundAimInternal();
    }

    private void CancelGroundAimInternal()
    {
        int idx = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;
        onGroundAimAborted.Invoke();
        onAimingEnded.Invoke(idx);
    }

    public void AbortAiming()
    {
        if (AimMode != SkillAimMode.ProtocolRedirect) return;
        int idx = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;
        StartCooldown(idx);
        onAimingAborted.Invoke();
        onAimingEnded.Invoke(idx);
    }

    public void ConfirmGravityWell(Vector2 worldPos)
    {
        if (AimMode != SkillAimMode.GravityWell) return;
        if (!GravityWellAim.IsValidPlacement(worldPos)) return;

        int idx = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;

        GravityWell.Spawn(worldPos);
        StartCooldown(idx);
        onAimingEnded.Invoke(idx);
    }

    public bool TryActivate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;
        if (GameManager.Instance?.State != GameState.Playing) return false;

        if (TutorialInputGate.Active)
        {
            var need = slotIndex == 0 ? TutorialInputMask.Skill0 : TutorialInputMask.Skill1;
            if (!TutorialInputGate.Allows(need)) return false;
        }

        var slot = slots[slotIndex];
        if (slot.definition == null) return false;
        if (!slot.IsReady) return false;

        switch (slot.definition.implementationType)
        {
            case ActiveSkillType.ProtocolRedirect:
                if (AimMode != SkillAimMode.None) return false;
                // 敌方时缓进行中允许改向：标准连招 = 先减速再瞄准切入
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive) return false;
                AimMode    = SkillAimMode.ProtocolRedirect;
                AimingSlot = slotIndex;
                onSlotActivated.Invoke(slotIndex);
                onProtocolRedirectActivated.Invoke();
                onExecuteChainActivated.Invoke();
                break;

            case ActiveSkillType.ExecuteChain:
                if (AimMode != SkillAimMode.None) return false;
                if (SplitProtocol.Instance != null && SplitProtocol.Instance.IsActive) return false;
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive) return false;
                ArmExecute(slotIndex);
                break;

            case ActiveSkillType.BlockShield:
                if (IsAiming) return false;
                BlockShield.Instance?.Activate();
                StartCooldown(slotIndex);
                onSlotActivated.Invoke(slotIndex);
                break;

            case ActiveSkillType.TimestopAura:
                if (IsAiming) return false;
                if (TimestopAura.Instance != null && TimestopAura.Instance.IsActive) return false;
                TimestopAura.EnsureInstance().Activate();
                StartCooldown(slotIndex);
                onSlotActivated.Invoke(slotIndex);
                break;

            case ActiveSkillType.GravitySpike:
                if (AimMode != SkillAimMode.None) return false;
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                AimMode    = SkillAimMode.GravityWell;
                AimingSlot = slotIndex;
                onSlotActivated.Invoke(slotIndex);
                onGravityWellAimStarted.Invoke();
                break;

            case ActiveSkillType.CorePulse:
                if (IsAiming) return false;
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                float pulseRefund = CorePulse.EnsureInstance().Activate();
                StartCooldown(slotIndex);
                if (pulseRefund > 0f)
                    ReduceCooldownForType(ActiveSkillType.CorePulse, pulseRefund);
                onSlotActivated.Invoke(slotIndex);
                break;

            case ActiveSkillType.SplitProtocol:
                if (AimMode != SkillAimMode.None) return false;
                if (SplitProtocol.Instance != null && SplitProtocol.Instance.IsActive) return false;
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive) return false;
                SplitProtocol.EnsureInstance().Activate();
                StartCooldown(slotIndex);
                onSlotActivated.Invoke(slotIndex);
                break;

            case ActiveSkillType.TestPlaceholder:
                return false;
        }

        return true;
    }

    private void ArmExecute(int slotIndex)
    {
        float window = Config != null ? Config.executeArmWindowSeconds : 5f;
        IsExecuteArmed = true;
        _executeArmedUntilUnscaled = Time.unscaledTime + Mathf.Max(0.1f, window);
        onSlotActivated.Invoke(slotIndex);
        onExecuteArmStarted.Invoke();
    }

    public void ClearExecuteArm()
    {
        if (!IsExecuteArmed) return;
        IsExecuteArmed = false;
        _executeArmedUntilUnscaled = 0f;
        onExecuteArmEnded.Invoke();
    }

    /// <summary>教学用：延长斩杀武装窗口（说明拍切换时不让玩家卡关）。</summary>
    public void TutorialExtendExecuteArm(float windowSeconds = 60f)
    {
        IsExecuteArmed = true;
        _executeArmedUntilUnscaled = Time.unscaledTime + Mathf.Max(0.1f, windowSeconds);
        onExecuteArmStarted.Invoke();
    }

    public void Fire(Vector2 direction)
    {
        if (AimMode != SkillAimMode.ProtocolRedirect) return;
        int redirectSlot = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;

        bool confirmed = direction.sqrMagnitude > 0.001f;
        bool armed = IsExecuteArmed;

        StartCooldown(redirectSlot);
        onFired.Invoke(direction);
        onAimingEnded.Invoke(redirectSlot);

        if (!confirmed)
        {
            // Escape / 取消瞄准：改向进 CD，武装保留至窗口结束（可再开改向）
            return;
        }

        if (armed && BallController.Instance != null)
        {
            if (SplitProtocol.Instance != null && SplitProtocol.Instance.IsActive)
            {
                ClearExecuteArm();
                return;
            }

            int jumps = Config != null ? Mathf.Max(1, Config.executeChainMaxJumps) : 3;
            BallController.Instance.StartExecuteChain(jumps);
            onExecuteChainStarted.Invoke();

            int execSlot = FindSlotIndex(ActiveSkillType.ExecuteChain);
            if (execSlot >= 0)
                StartCooldown(execSlot);

            ClearExecuteArm();
        }
    }

    private int FindSlotIndex(ActiveSkillType type)
    {
        if (slots == null) return -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i]?.definition != null && slots[i].definition.implementationType == type)
                return i;
        }
        return -1;
    }

    public void StartCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        var slot = slots[slotIndex];
        float cd = slot.MaxCooldown;
        if (DebuffManager.Instance != null)
            cd *= DebuffManager.Instance.SkillCooldownMultiplier;
        slot.currentCD = cd;
        onSlotCooldownChanged.Invoke(slotIndex, slot.CooldownRatio);
    }

    /// <summary>全体技能 CD 回退（协议缓存盘等台面奖励）。</summary>
    public void ReduceAllCooldowns(float seconds)
    {
        if (slots == null || seconds <= 0f) return;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.IsReady) continue;
            slot.currentCD = Mathf.Max(0f, slot.currentCD - seconds);
            onSlotCooldownChanged.Invoke(i, slot.CooldownRatio);
        }
    }

    /// <summary>按技能类型回退 CD（Pulse Recycle 等）。</summary>
    public void ReduceCooldownForType(ActiveSkillType type, float seconds)
    {
        if (slots == null || seconds <= 0f) return;
        int idx = FindSlotIndex(type);
        if (idx < 0) return;
        var slot = slots[idx];
        if (slot == null || slot.IsReady) return;
        slot.currentCD = Mathf.Max(0f, slot.currentCD - seconds);
        onSlotCooldownChanged.Invoke(idx, slot.CooldownRatio);
    }

    public bool TryActivate() => TryActivate(0);

    private void OnGameStart()
    {
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;
        ClearExecuteArm();
        GravityWell.Instance?.DestroyWell();
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].currentCD = 0f;
            onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
