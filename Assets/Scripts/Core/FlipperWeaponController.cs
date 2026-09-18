using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 左右挡板共享的 Flipper Weapon：Combo 充能 → Ready → Perfect Flip 释放。
/// </summary>
public class FlipperWeaponController : MonoBehaviour
{
    public static FlipperWeaponController Instance { get; private set; }

    [Header("Default Loadout")]
    [Tooltip("为空时从 Resources/FlipperWeapons/Weapon_Cannon 加载")]
    public FlipperWeaponDefinition defaultWeapon;

    public FlipperWeaponDefinition EquippedWeapon { get; private set; }
    public float Energy { get; private set; }
    public float MaxEnergy => Config != null ? Config.flipperWeaponMaxEnergy : 100f;
    public float EnergyRatio => MaxEnergy > 0.01f ? Mathf.Clamp01(Energy / MaxEnergy) : 0f;
    public bool IsReady => Energy >= MaxEnergy - 0.01f;
    public bool IsFiring => _laserRoutine != null;

    /// <summary>教程：T07 之前禁止 Perfect Flip 释放，避免连击阶段误触。</summary>
    public static bool TutorialFireLocked { get; private set; }

    public UnityEvent<float> onEnergyChanged = new UnityEvent<float>();
    public UnityEvent onWeaponReady = new UnityEvent();
    public UnityEvent onWeaponFired = new UnityEvent();
    public UnityEvent<FlipperWeaponDefinition> onWeaponEquipped = new UnityEvent<FlipperWeaponDefinition>();

    private static readonly Collider2D[] OverlapBuf = new Collider2D[64];

    private int _lastComboSeen;
    private bool _readyNotified;
    private Coroutine _laserRoutine;
    private LineRenderer _laserLine;

    private GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureHud();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_laserLine != null) Destroy(_laserLine.gameObject);
    }

    private void Start()
    {
        ApplyFromLoadout();
        ResetEnergy();

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
    }

    public static FlipperWeaponController EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(FlipperWeaponController));
        return go.AddComponent<FlipperWeaponController>();
    }

    public void ApplyFromLoadout()
    {
        RunLoadout.Load();
        var fromLoadout = RunLoadout.GetSelectedFlipperWeapon();
        EquipWeapon(fromLoadout != null
            ? fromLoadout
            : (defaultWeapon != null ? defaultWeapon : FlipperWeaponCatalog.GetDefault()));
    }

    public void EquipWeapon(FlipperWeaponDefinition weapon)
    {
        EquippedWeapon = weapon != null ? weapon : CreateFallbackWeapon(FlipperWeaponType.Cannon);
        onWeaponEquipped.Invoke(EquippedWeapon);
    }

    /// <summary>Perfect Flip 且能量已满时由 FlipperController 调用。</summary>
    public bool TryFireOnPerfectFlip(FlipperSide side, Vector2 contactPos)
    {
        if (TutorialFireLocked) return false;
        if (EquippedWeapon == null || !IsReady || IsFiring) return false;
        if (GameManager.Instance == null) return false;
        var state = GameManager.Instance.State;
        if (state == GameState.Idle || state == GameState.GameOver || state == GameState.BuffSelection)
            return false;

        ConsumeEnergy();
        FireWeapon(side, contactPos);
        return true;
    }

    private void OnComboChanged(int combo)
    {
        if (combo <= _lastComboSeen)
        {
            _lastComboSeen = combo;
            return;
        }

        int gained = combo - _lastComboSeen;
        _lastComboSeen = combo;

        float perHit = Config != null ? Config.flipperWeaponEnergyPerCombo : 8f;
        AddEnergy(perHit * gained);
    }

    private void OnGameStart()
    {
        _lastComboSeen = 0;
        _readyNotified = false;
        if (_laserRoutine != null)
        {
            StopCoroutine(_laserRoutine);
            _laserRoutine = null;
        }
        HideLaserLine();
        ApplyFromLoadout();
        ResetEnergy();
    }

    private void AddEnergy(float amount)
    {
        if (amount <= 0f || IsReady) return;

        float prev = Energy;
        Energy = Mathf.Min(MaxEnergy, Energy + amount);
        onEnergyChanged.Invoke(Energy / MaxEnergy);

        if (!IsReady) return;
        Energy = MaxEnergy;
        if (_readyNotified) return;
        _readyNotified = true;
        onWeaponReady.Invoke();
    }

    private void ConsumeEnergy()
    {
        Energy = 0f;
        _readyNotified = false;
        onEnergyChanged.Invoke(0f);
    }

    private void ResetEnergy()
    {
        Energy = 0f;
        _readyNotified = false;
        onEnergyChanged.Invoke(0f);
    }

    /// <summary>教程：T07 之前锁住释放；进入挡板武器教学或教程结束时打开。</summary>
    public static void SetTutorialFireLocked(bool locked) => TutorialFireLocked = locked;

    /// <summary>教程：将武器能量设为满，不修改其它战斗规则。</summary>
    public void TutorialFillEnergyForLesson()
    {
        Energy = MaxEnergy;
        _readyNotified = true;
        onEnergyChanged.Invoke(1f);
        onWeaponReady.Invoke();
    }

    private void FireWeapon(FlipperSide side, Vector2 contactPos)
    {
        onWeaponFired.Invoke();
        var weapon = EquippedWeapon;
        if (weapon == null) return;

        switch (weapon.weaponType)
        {
            case FlipperWeaponType.Cannon:
                FireCannon(weapon, contactPos);
                break;
            case FlipperWeaponType.Bomb:
                FireBomb(weapon, contactPos);
                break;
            case FlipperWeaponType.Laser:
                if (_laserRoutine != null) StopCoroutine(_laserRoutine);
                _laserRoutine = StartCoroutine(LaserRoutine(weapon, side, contactPos));
                break;
        }
    }

    private void FireCannon(FlipperWeaponDefinition weapon, Vector2 contactPos)
    {
        var boss = WaveManager.Instance != null ? WaveManager.Instance.CurrentBoss : null;
        Vector2 target = boss != null && !boss.IsDead
            ? (Vector2)boss.transform.position
            : contactPos + Vector2.up * 4f;

        JuiceRouter.FlipperWeaponFire(JuiceRouter.Tier.Ultimate, contactPos, target, weapon.effectColor);

        if (boss != null && !boss.IsDead)
            boss.TakeFlipperWeaponHit(weapon.cannonBossDamage, target);
    }

    private void FireBomb(FlipperWeaponDefinition weapon, Vector2 contactPos)
    {
        Vector2 center = contactPos + Vector2.up * 3.5f;
        float radius = weapon.bombRadius;

        Color color = weapon.effectColor;
        JuiceRouter.FlipperWeaponFire(JuiceRouter.Tier.Ultimate, contactPos, center, color);
        ImpactFX.Instance?.SpawnBumperPulseWave(center, radius, color, 0.85f, 0);

        int n = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuf);
        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;

            Vector2 hitPos = enemy.transform.position;
            if (enemy is Boss)
                enemy.TakeFlipperWeaponHit(weapon.bombBossDamage, hitPos);
            else
                enemy.TakeFlipperWeaponHit(weapon.bombMinionDamage, hitPos);
        }
    }

    private IEnumerator LaserRoutine(FlipperWeaponDefinition weapon, FlipperSide side, Vector2 origin)
    {
        var boss = WaveManager.Instance != null ? WaveManager.Instance.CurrentBoss : null;
        if (boss == null || boss.IsDead)
        {
            JuiceRouter.FlipperWeaponFire(JuiceRouter.Tier.Skill, origin, origin + Vector2.up * 5f, weapon.effectColor);
            _laserRoutine = null;
            yield break;
        }

        EnsureLaserLine(weapon.effectColor);
        float elapsed = 0f;
        float tick = Mathf.Max(0.1f, weapon.laserTickInterval);
        float nextTick = 0f;

        JuiceRouter.FlipperWeaponFire(JuiceRouter.Tier.Skill, origin, boss.transform.position, weapon.effectColor);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);

        while (elapsed < weapon.laserDuration)
        {
            if (boss == null || boss.IsDead) break;

            Vector2 bossPos = boss.transform.position;
            UpdateLaserLine(origin, bossPos, weapon.effectColor);

            if (elapsed >= nextTick)
            {
                boss.TakeFlipperWeaponHit(weapon.laserBossDamagePerTick, bossPos);
                ImpactFX.Instance?.SpawnHit(bossPos, weapon.effectColor, 1.1f);
                CameraShake.Instance?.Shake(CameraShake.Preset.Light);
                nextTick += tick;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        HideLaserLine();
        _laserRoutine = null;
    }

    private void EnsureLaserLine(Color color)
    {
        if (_laserLine != null) { _laserLine.enabled = true; return; }

        var go = new GameObject("FlipperLaserBeam");
        go.transform.SetParent(transform, false);
        _laserLine = go.AddComponent<LineRenderer>();
        _laserLine.useWorldSpace = true;
        _laserLine.positionCount = 2;
        _laserLine.startWidth = 0.14f;
        _laserLine.endWidth = 0.06f;
        _laserLine.numCapVertices = 4;
        _laserLine.sortingOrder = 20;
        _laserLine.sharedMaterial = CyberVisualFactory.UnlitMaterial;
        _laserLine.startColor = color;
        _laserLine.endColor = color * 0.65f;
    }

    private void UpdateLaserLine(Vector2 from, Vector2 to, Color color)
    {
        if (_laserLine == null) return;
        _laserLine.SetPosition(0, from);
        _laserLine.SetPosition(1, to);
        _laserLine.startColor = color;
        _laserLine.endColor = color * 0.65f;
    }

    private void HideLaserLine()
    {
        if (_laserLine != null) _laserLine.enabled = false;
    }

    private static FlipperWeaponDefinition CreateFallbackWeapon(FlipperWeaponType type)
    {
        var def = ScriptableObject.CreateInstance<FlipperWeaponDefinition>();
        def.weaponId = type.ToString().ToLowerInvariant();
        def.displayName = type.ToString().ToUpperInvariant();
        def.weaponType = type;
        def.effectColor = type switch
        {
            FlipperWeaponType.Bomb  => new Color(1f, 0.35f, 0.1f, 1f),
            FlipperWeaponType.Laser => new Color(0.2f, 0.95f, 1f, 1f),
            _                       => new Color(1f, 0.72f, 0.15f, 1f)
        };
        return def;
    }

    private static void EnsureHud()
    {
        if (Object.FindAnyObjectByType<FlipperWeaponHud>() != null) return;
        var hudGo = new GameObject("FlipperWeaponHud");
        hudGo.AddComponent<FlipperWeaponHud>();
    }
}
