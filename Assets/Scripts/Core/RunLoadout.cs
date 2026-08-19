using UnityEngine;

[System.Serializable]
public class RunLoadoutData
{
    public string ballId;
    public string skillSlot0Id;
    public string skillSlot1Id;
}

/// <summary>战前配置：弹珠 + 双技能槽，跨场景持久化。</summary>
public static class RunLoadout
{
    public const int SlotCount = 2;

    private const string PrefBall     = "run_ball_id";
    private const string PrefSkill0   = "run_skill_0";
    private const string PrefSkill1   = "run_skill_1";

    private static RunLoadoutData _data = new RunLoadoutData();
    public static RunLoadoutData Data => _data;

    public static void Load()
    {
        if (_data == null) _data = new RunLoadoutData();
        _data.ballId       = PlayerPrefs.GetString(PrefBall, string.Empty);
        _data.skillSlot0Id = PlayerPrefs.GetString(PrefSkill0, string.Empty);
        _data.skillSlot1Id = PlayerPrefs.GetString(PrefSkill1, string.Empty);
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PrefBall, _data.ballId ?? string.Empty);
        PlayerPrefs.SetString(PrefSkill0, _data.skillSlot0Id ?? string.Empty);
        PlayerPrefs.SetString(PrefSkill1, _data.skillSlot1Id ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void EnsureDefaults(RunCatalog catalog)
    {
        if (catalog == null) return;

        var defaultBall = catalog.GetDefaultBall();
        if (string.IsNullOrEmpty(Data.ballId) || catalog.GetBall(Data.ballId) == null)
            Data.ballId = defaultBall != null ? defaultBall.ballId : "standard";

        var available = catalog.GetAvailableSkills();
        if (available.Count == 0) return;

        if (string.IsNullOrEmpty(Data.skillSlot0Id) || catalog.GetSkill(Data.skillSlot0Id) == null)
            Data.skillSlot0Id = available[0].skillId;

        if (string.IsNullOrEmpty(Data.skillSlot1Id) || catalog.GetSkill(Data.skillSlot1Id) == null)
            Data.skillSlot1Id = available.Count > 1 ? available[1].skillId : available[0].skillId;

        ResolveDuplicateSlots(catalog);
        Save();
    }

    public static bool IsValid(RunCatalog catalog)
    {
        if (catalog == null) return false;
        if (string.IsNullOrEmpty(Data.ballId) || catalog.GetBall(Data.ballId) == null) return false;

        var s0 = catalog.GetSkill(Data.skillSlot0Id);
        var s1 = catalog.GetSkill(Data.skillSlot1Id);
        if (s0 == null || s1 == null || !s0.isAvailable || !s1.isAvailable) return false;
        if (s0.skillId == s1.skillId) return false;
        return true;
    }

    public static SkillDefinition GetSkillInSlot(int slotIndex, RunCatalog catalog)
    {
        if (catalog == null) return null;
        string id = slotIndex switch
        {
            0 => Data.skillSlot0Id,
            1 => Data.skillSlot1Id,
            _ => null
        };
        return catalog.GetSkill(id);
    }

    public static BallDefinition GetSelectedBall(RunCatalog catalog) =>
        catalog != null ? catalog.GetBall(Data.ballId) : null;

    public static bool TryEquipSkill(SkillDefinition skill, int slotIndex, RunCatalog catalog)
    {
        if (skill == null || catalog == null || slotIndex < 0 || slotIndex >= SlotCount) return false;
        if (!skill.isAvailable) return false;

        string id = skill.skillId;
        if (slotIndex == 0)
        {
            if (Data.skillSlot1Id == id) Data.skillSlot1Id = Data.skillSlot0Id;
            Data.skillSlot0Id = id;
        }
        else
        {
            if (Data.skillSlot0Id == id) Data.skillSlot0Id = Data.skillSlot1Id;
            Data.skillSlot1Id = id;
        }

        ResolveDuplicateSlots(catalog);
        Save();
        return true;
    }

    public static void SwapSkillSlots()
    {
        (Data.skillSlot0Id, Data.skillSlot1Id) = (Data.skillSlot1Id, Data.skillSlot0Id);
        Save();
    }

    /// <summary>拖拽：两装备槽互换技能。</summary>
    public static void MoveSkillBetweenSlots(int fromSlot, int toSlot)
    {
        if (fromSlot == toSlot || fromSlot < 0 || toSlot < 0 || fromSlot >= SlotCount || toSlot >= SlotCount)
            return;

        string fromId = fromSlot == 0 ? Data.skillSlot0Id : Data.skillSlot1Id;
        string toId   = toSlot   == 0 ? Data.skillSlot0Id : Data.skillSlot1Id;

        if (fromSlot == 0) Data.skillSlot0Id = toId;
        else               Data.skillSlot1Id = toId;

        if (toSlot == 0) Data.skillSlot0Id = fromId;
        else             Data.skillSlot1Id = fromId;

        Save();
    }

    public static bool IsSkillEquipped(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;
        return Data.skillSlot0Id == skillId || Data.skillSlot1Id == skillId;
    }

    private static void ResolveDuplicateSlots(RunCatalog catalog)
    {
        if (catalog == null) return;
        if (Data.skillSlot0Id == Data.skillSlot1Id)
        {
            foreach (var s in catalog.GetAvailableSkills())
            {
                if (s != null && s.skillId != Data.skillSlot0Id)
                {
                    Data.skillSlot1Id = s.skillId;
                    break;
                }
            }
        }
    }
}
