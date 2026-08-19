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

    [Header("技能槽（由 RunLoadout 注入）")]
    public SkillSlot[] slots = new SkillSlot[]
    {
        new SkillSlot(),
        new SkillSlot()
    };

    public SkillAimMode AimMode { get; private set; } = SkillAimMode.None;
    public bool IsAiming        => AimMode == SkillAimMode.ExecuteChain;
    public bool IsGroundAiming  => AimMode == SkillAimMode.GravityWell;
    public int  AimingSlot      { get; private set; } = -1;

    [HideInInspector] public UnityEvent<int, float> onSlotCooldownChanged   = new UnityEvent<int, float>();
    [HideInInspector] public UnityEvent<int>        onSlotActivated         = new UnityEvent<int>();
    [HideInInspector] public UnityEvent             onExecuteChainActivated = new UnityEvent();
    [HideInInspector] public UnityEvent<Vector2>     onFired                 = new UnityEvent<Vector2>();
    [HideInInspector] public UnityEvent             onAimingAborted         = new UnityEvent();
    [HideInInspector] public UnityEvent             onGroundAimAborted      = new UnityEvent();
    [HideInInspector] public UnityEvent<int>        onAimingEnded           = new UnityEvent<int>();
    [HideInInspector] public UnityEvent             onExecuteChainStarted   = new UnityEvent();
    [HideInInspector] public UnityEvent             onGravityWellAimStarted = new UnityEvent();

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GravityWellAim.EnsureInstance();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
        }
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
    }

    private void OnBallLost() => CancelAiming();

    public void ApplyLoadout(RunCatalog catalog)
    {
        if (catalog == null) return;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (slots == null || i >= slots.Length)
                break;

            slots[i].definition = RunLoadout.GetSkillInSlot(i, catalog);
            slots[i].currentCD  = 0f;
            onSlotCooldownChanged.Invoke(i, 0f);
        }
    }

    private void Update()
    {
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
        float reduce = Config != null ? Config.skillComboCDReduce : 0.4f;
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
        if (AimMode != SkillAimMode.ExecuteChain) return;

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
        if (AimMode != SkillAimMode.ExecuteChain) return;
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

        var slot = slots[slotIndex];
        if (slot.definition == null) return false;
        if (!slot.IsReady) return false;

        switch (slot.definition.implementationType)
        {
            case ActiveSkillType.ExecuteChain:
                if (AimMode != SkillAimMode.None) return false;
                if (TimestopAura.Instance != null && TimestopAura.Instance.IsActive) return false;
                if (BallController.Instance != null && BallController.Instance.IsWaitingForLaunch) return false;
                if (BallController.Instance != null && BallController.Instance.IsExecuteChainActive) return false;
                AimMode    = SkillAimMode.ExecuteChain;
                AimingSlot = slotIndex;
                onSlotActivated.Invoke(slotIndex);
                onExecuteChainActivated.Invoke();
                break;

            case ActiveSkillType.BlockShield:
                if (AimMode == SkillAimMode.ExecuteChain) return false;
                BlockShield.Instance?.Activate();
                StartCooldown(slotIndex);
                onSlotActivated.Invoke(slotIndex);
                break;

            case ActiveSkillType.TimestopAura:
                if (AimMode == SkillAimMode.ExecuteChain) return false;
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

            case ActiveSkillType.TestPlaceholder:
                return false;
        }

        return true;
    }

    public void Fire(Vector2 direction)
    {
        if (AimMode != SkillAimMode.ExecuteChain) return;
        int idx    = AimingSlot;
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;

        StartCooldown(idx);
        onFired.Invoke(direction);
        onAimingEnded.Invoke(idx);

        if (BallController.Instance != null && direction.sqrMagnitude > 0.001f)
        {
            BallController.Instance.StartExecuteChain(3);
            onExecuteChainStarted.Invoke();
        }
    }

    public void StartCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        var slot = slots[slotIndex];
        float cd = slot.MaxCooldown;
        slot.currentCD = cd;
        onSlotCooldownChanged.Invoke(slotIndex, slot.CooldownRatio);
    }

    public bool TryActivate() => TryActivate(0);

    private void OnGameStart()
    {
        AimMode    = SkillAimMode.None;
        AimingSlot = -1;
        GravityWell.Instance?.DestroyWell();
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].currentCD = 0f;
            onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
