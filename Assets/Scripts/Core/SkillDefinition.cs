using UnityEngine;

public enum SkillCategory { Offense, Defense, Control }

public enum SkillActivationMode { Instant, Aim }

/// <summary>运行时技能实现类型（与具体 MonoBehaviour 逻辑对应）。</summary>
public enum ActiveSkillType
{
    /// <summary>斩杀武装（Instant）：武装后配合协议改向确认开链。</summary>
    ExecuteChain = 0,
    BlockShield = 1,
    TimestopAura = 2,
    GravitySpike = 3,
    TestPlaceholder = 4,
    /// <summary>协议改向（Aim）：仅改向，不开斩杀链。</summary>
    ProtocolRedirect = 5,
    CorePulse = 6,
    SplitProtocol = 7
}

public enum SkillAimMode
{
    None = 0,
    /// <summary>弹道时缓瞄准（协议改向）。</summary>
    ProtocolRedirect = 1,
    GravityWell = 2,
    /// <summary>旧名；与 ProtocolRedirect 同值。</summary>
    ExecuteChain = ProtocolRedirect
}

[CreateAssetMenu(fileName = "Skill_New", menuName = "Ball/SkillDefinition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    public string skillId;
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    [Header("Rules")]
    public SkillCategory category = SkillCategory.Offense;
    public SkillActivationMode activationMode = SkillActivationMode.Instant;
    public ActiveSkillType implementationType = ActiveSkillType.ExecuteChain;
    public float baseCooldown = 12f;

    [Header("Pool")]
    public bool isAvailable = true;

    public string GetSlotKeyHint(int slotIndex) => slotIndex switch
    {
        0 => "右键 / Q",
        1 => "E",
        _ => string.Empty
    };

    public string GetBriefDescription()
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description.Trim();
        return string.Empty;
    }
}
