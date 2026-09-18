/// <summary>教程文案与 PC/触屏提示。</summary>
public static class TutorialProtocolCopy
{
    public const int TotalSteps = 10;

    public static string HintFlipper =>
        IsTouch ? "按住左右挡板" : "按住 Z / X 或 ← / →";

    public static string HintFlipperLeft =>
        IsTouch ? "按住左挡板" : "按住 Z 或 ←";

    public static string HintFlipperRight =>
        IsTouch ? "按住右挡板" : "按住 X 或 →";

    public static string HintLaunch =>
        IsTouch ? "点击发球" : "左键 / 空格";

    public static string HintRedirect =>
        IsTouch ? "技能 0 → 瞄准 → 确认" : "Q → 左键确认";

    public static string HintIdentityE =>
        IsTouch ? "点击技能 1" : "按 E";

    public static string HintComboEthenQ =>
        IsTouch ? "技能 1 → 技能 0" : "E → Q → 左键";

    public static string HintParry =>
        IsTouch ? "点击弹刀" : "左键弹刀";

    public static string HintSlotSpin =>
        IsTouch ? "拖动拉杆" : "拉杆 / 空格";

    public static string HintSlotReroll =>
        IsTouch ? "点选一轮 → 拉杆" : "点选一轮 → 拉杆";

    public static string HintPerfectFlip =>
        IsTouch ? "按住挡板接球" : "按住 Z / X 接球";

    private static bool IsTouch =>
#if UNITY_ANDROID || UNITY_IOS
        true;
#else
        UnityEngine.Application.isMobilePlatform;
#endif
}
