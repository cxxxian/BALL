using UnityEngine;

public enum SkillCategory { Offense, Defense, Control }

public enum SkillActivationMode { Instant, Aim }

/// <summary>运行时技能实现类型（与具体 MonoBehaviour 逻辑对应）。</summary>
public enum ActiveSkillType { ExecuteChain, BlockShield, TimestopAura, GravitySpike, TestPlaceholder }

public enum SkillAimMode { None, ExecuteChain, GravityWell }

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
