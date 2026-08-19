using System.Collections.Generic;
using UnityEngine;

public enum DebuffId
{
    D1_SlowGears,
    D2_ShatteredEcho,
    D3_WornBumper,
    D4_TowerOverload,
    D5_GravityChaos,
    D6_NextWaveGloom
}

public enum DebuffTier
{
    Light,
    Medium
}

public class DebuffManager : MonoBehaviour
{
    public static DebuffManager Instance { get; private set; }

    private readonly List<DebuffId> _active = new List<DebuffId>();
    private bool _nextWaveCommonWeightBoost;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ResetForNewGame);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(ResetForNewGame);
    }

    public IReadOnlyList<DebuffId> ActiveDebuffs => _active;

    public bool ConsumeNextWaveCommonWeightBoost()
    {
        if (!_nextWaveCommonWeightBoost) return false;
        _nextWaveCommonWeightBoost = false;
        return true;
    }

    public float ComboTimeoutModifier
    {
        get
        {
            float mod = 0f;
            foreach (var id in _active)
                if (id == DebuffId.D2_ShatteredEcho) mod -= 0.3f;
            return mod;
        }
    }

    public float BallMaxSpeedMultiplier
    {
        get
        {
            float mult = 1f;
            foreach (var id in _active)
                if (id == DebuffId.D5_GravityChaos) mult *= 0.92f;
            return mult;
        }
    }

    public float TowerAttackIntervalMultiplier
    {
        get
        {
            float mult = 1f;
            foreach (var id in _active)
                if (id == DebuffId.D4_TowerOverload) mult *= 1.15f;
            return mult;
        }
    }

    public int BumperDamagePenalty
    {
        get
        {
            int penalty = 0;
            foreach (var id in _active)
                if (id == DebuffId.D3_WornBumper) penalty += 1;
            return penalty;
        }
    }

    public void ApplyDebuff(DebuffId id)
    {
        if (_active.Contains(id)) return;

        if (id == DebuffId.D6_NextWaveGloom)
            _nextWaveCommonWeightBoost = true;

        _active.Add(id);
        Debug.Log($"[DebuffManager] Applied: {GetDisplayName(id)}");
    }

    public DebuffId RollLightDebuff()
    {
        var pool = new List<DebuffId> { DebuffId.D1_SlowGears, DebuffId.D2_ShatteredEcho, DebuffId.D3_WornBumper };
        if (GameManager.Instance != null && GameManager.Instance.Lives <= 1)
            pool.Remove(DebuffId.D5_GravityChaos);
        return pool[Random.Range(0, pool.Count)];
    }

    public DebuffId RollMediumDebuff(bool preferJackpotTax = false)
    {
        if (preferJackpotTax)
            return DebuffId.D6_NextWaveGloom;

        var pool = new List<DebuffId>
        {
            DebuffId.D4_TowerOverload,
            DebuffId.D5_GravityChaos,
            DebuffId.D6_NextWaveGloom
        };

        if (GameManager.Instance != null && GameManager.Instance.Lives <= 1)
            pool.Remove(DebuffId.D5_GravityChaos);

        return pool[Random.Range(0, pool.Count)];
    }

    public DebuffId PickDebuffForTax(DebuffTier tier, bool jackpot = false)
    {
        bool forceLight = _active.Count >= 4 && tier == DebuffTier.Medium;
        if (forceLight) tier = DebuffTier.Light;

        if (tier == DebuffTier.Light)
            return RollLightDebuff();

        return RollMediumDebuff(jackpot);
    }

    public static string GetDisplayName(DebuffId id) => id switch
    {
        DebuffId.D1_SlowGears      => "迟钝齿轮",
        DebuffId.D2_ShatteredEcho  => "碎盾回响",
        DebuffId.D3_WornBumper     => "机关磨损",
        DebuffId.D4_TowerOverload  => "塔网过载",
        DebuffId.D5_GravityChaos   => "引力紊乱",
        DebuffId.D6_NextWaveGloom  => "下波晦气",
        _ => id.ToString()
    };

    public static string GetDescription(DebuffId id) => id switch
    {
        DebuffId.D1_SlowGears     => "主动技能 CD +10%",
        DebuffId.D2_ShatteredEcho => "Combo 超时 −0.3s",
        DebuffId.D3_WornBumper    => "Bumper 伤害 −1（最低 0）",
        DebuffId.D4_TowerOverload => "塔攻击间隔 +15%",
        DebuffId.D5_GravityChaos  => "球最大速度 −8%",
        DebuffId.D6_NextWaveGloom => "下波拉霸 Common 权重 +10%",
        _ => string.Empty
    };

    public static string GetTierLabel(DebuffId id) => id switch
    {
        DebuffId.D1_SlowGears or DebuffId.D2_ShatteredEcho or DebuffId.D3_WornBumper => "轻",
        _ => "中"
    };

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("DebuffManager_Auto");
        go.AddComponent<DebuffManager>();
    }

    private void ResetForNewGame()
    {
        _active.Clear();
        _nextWaveCommonWeightBoost = false;
    }
}
