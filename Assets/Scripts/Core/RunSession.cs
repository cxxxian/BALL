/// <summary>当局模式：主菜单进关前设置，回主菜单时清空。</summary>
public enum RunMode
{
    None = 0,
    Endless = 1,
    Campaign = 2,
    Tutorial = 3
}

public static class RunSession
{
    public static RunMode Mode { get; private set; } = RunMode.None;

    public static bool IsTutorial => Mode == RunMode.Tutorial;
    public static bool IsEndless => Mode == RunMode.Endless;

    public static void BeginEndless()
    {
        Mode = RunMode.Endless;
    }

    public static void BeginCampaign()
    {
        Mode = RunMode.Campaign;
    }

    public static void BeginTutorial()
    {
        Mode = RunMode.Tutorial;

        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog == null) return;

        var ball = catalog.GetDefaultBall();
        if (ball != null)
            RunLoadout.TrySelectBall(ball.ballId, catalog);
        else
            RunLoadout.EnsureDefaults(catalog);
    }

    public static void Clear()
    {
        Mode = RunMode.None;
    }
}
