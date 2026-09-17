using UnityEngine;

[System.Serializable]
public class RunLoadoutData
{
    public string ballId;
    public string flipperWeaponId;
}

/// <summary>战前配置：弹珠 + 挡板武器；槽0 强制协议改向，槽1 为身份技。</summary>
public static class RunLoadout
{
    public const int SlotCount = 2;
    public const string DefaultFlipperWeaponId = "cannon";

    private const string PrefBall = "run_ball_id";
    private const string PrefFlipperWeapon = "run_flipper_weapon_id";

    private static RunLoadoutData _data = new RunLoadoutData();
    public static RunLoadoutData Data => _data;

    public static void Load()
    {
        if (_data == null) _data = new RunLoadoutData();
        _data.ballId = PlayerPrefs.GetString(PrefBall, string.Empty);
        _data.flipperWeaponId = PlayerPrefs.GetString(PrefFlipperWeapon, string.Empty);
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PrefBall, _data.ballId ?? string.Empty);
        PlayerPrefs.SetString(PrefFlipperWeapon, _data.flipperWeaponId ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void EnsureDefaults(RunCatalog catalog)
    {
        if (catalog == null) return;

        PlayerProfile.Load();
        var defaultBall = catalog.GetDefaultBall();
        if (string.IsNullOrEmpty(Data.ballId)
            || catalog.GetBall(Data.ballId) == null
            || !PlayerProfile.IsBallUnlocked(Data.ballId))
            Data.ballId = defaultBall != null ? defaultBall.ballId : "standard";

        if (string.IsNullOrEmpty(Data.flipperWeaponId)
            || GetSelectedFlipperWeapon() == null)
            Data.flipperWeaponId = DefaultFlipperWeaponId;

        Save();
    }

    public static bool IsValid(RunCatalog catalog)
    {
        if (catalog == null) return false;
        var ball = GetSelectedBall(catalog);
        if (ball == null || !PlayerProfile.IsBallUnlocked(ball.ballId)) return false;
        if (GetSelectedFlipperWeapon() == null) return false;
        return catalog.GetSkill(SkillManager.ProtocolRedirectSkillId) != null
               || ball.primarySkill != null;
    }

    public static SkillDefinition GetSkillInSlot(int slotIndex, RunCatalog catalog)
    {
        if (catalog == null) return null;

        if (slotIndex == 0)
        {
            var redirect = catalog.GetSkill(SkillManager.ProtocolRedirectSkillId);
            if (redirect != null) return redirect;
            var ballFallback = GetSelectedBall(catalog);
            return ballFallback != null ? ballFallback.primarySkill : null;
        }

        if (slotIndex == 1)
        {
            var ball = GetSelectedBall(catalog);
            return ball != null ? ball.secondarySkill : null;
        }

        return null;
    }

    /// <summary>Loadout 预览用：槽0 始终协议改向；槽1 读球身份。</summary>
    public static SkillDefinition GetBoundSkillForPreview(BallDefinition ball, int slotIndex, RunCatalog catalog)
    {
        if (slotIndex == 0)
        {
            var redirect = catalog != null ? catalog.GetSkill(SkillManager.ProtocolRedirectSkillId) : null;
            if (redirect != null) return redirect;
            return ball != null ? ball.primarySkill : null;
        }

        return ball != null ? ball.secondarySkill : null;
    }

    public static BallDefinition GetSelectedBall(RunCatalog catalog) =>
        catalog != null ? catalog.GetBall(Data.ballId) : null;

    public static bool TrySelectBall(string ballId, RunCatalog catalog)
    {
        if (catalog == null || string.IsNullOrEmpty(ballId)) return false;
        var ball = catalog.GetBall(ballId);
        if (ball == null || !PlayerProfile.IsBallUnlocked(ballId)) return false;
        if (catalog.GetSkill(SkillManager.ProtocolRedirectSkillId) == null && ball.primarySkill == null)
            return false;

        Data.ballId = ballId;
        Save();
        return true;
    }

    public static FlipperWeaponDefinition GetSelectedFlipperWeapon() =>
        FlipperWeaponCatalog.Get(Data.flipperWeaponId);

    public static bool TrySelectFlipperWeapon(string weaponId)
    {
        var weapon = FlipperWeaponCatalog.Get(weaponId);
        if (weapon == null) return false;
        Data.flipperWeaponId = weapon.weaponId;
        Save();
        return true;
    }
}
