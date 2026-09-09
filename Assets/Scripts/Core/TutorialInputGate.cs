using System;

[Flags]
public enum TutorialInputMask
{
    None = 0,
    /// <summary>仅终端 ACK / 跳过（吃掉一切玩法输入）。</summary>
    UiOnly = 1 << 0,
    Launch = 1 << 1,
    FlipperLeft = 1 << 2,
    FlipperRight = 1 << 3,
    Skill0 = 1 << 4,
    Skill1 = 1 << 5,
    Parry = 1 << 6,
    Pause = 1 << 7,

    Flippers = FlipperLeft | FlipperRight,
    /// <summary>实战观察拍：挡板常开，其它按需加。</summary>
    PlayfieldBasic = Flippers | Pause,
}

/// <summary>协议校准输入门控：未允许的键位/点击直接丢弃。</summary>
public static class TutorialInputGate
{
    public static bool Active { get; private set; }
    public static TutorialInputMask Allowed { get; private set; } = TutorialInputMask.None;

    public static void Enable(TutorialInputMask allowed)
    {
        Active = true;
        Allowed = allowed;
    }

    public static void Disable()
    {
        Active = false;
        Allowed = TutorialInputMask.None;
    }

    public static bool Allows(TutorialInputMask flag)
    {
        if (!Active) return true;
        if ((Allowed & TutorialInputMask.UiOnly) != 0)
            return false;
        return (Allowed & flag) != 0;
    }

    public static bool BlocksGameplay
    {
        get
        {
            if (!Active) return false;
            return (Allowed & TutorialInputMask.UiOnly) != 0 || Allowed == TutorialInputMask.None;
        }
    }
}
