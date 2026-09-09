using UnityEngine;

[DefaultExecutionOrder(-100)]
public class RunBootstrap : MonoBehaviour
{
    private void Awake()
    {
        RunLoadout.Load();
        PlayerProfile.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null)
            RunLoadout.EnsureDefaults(catalog);

        // 直接进 SampleScene（未从主菜单）时默认按无尽处理
        if (RunSession.Mode == RunMode.None)
            RunSession.BeginEndless();
    }

    private void Start()
    {
        ApplyToBattle();
        EnsureTutorialDirector();
    }

    private static void ApplyToBattle()
    {
        var catalog = RunCatalog.Load();
        if (catalog == null) return;

        if (SkillManager.Instance != null)
            SkillManager.Instance.ApplyLoadout(catalog);

        var ball = RunLoadout.GetSelectedBall(catalog);
        if (ball != null && BallController.Instance != null)
            BallController.Instance.ApplyBallDefinition(ball);
    }

    private static void EnsureTutorialDirector()
    {
        if (!RunSession.IsTutorial) return;
        if (Object.FindAnyObjectByType<TutorialDirector>() != null) return;

        var go = new GameObject("TutorialDirector");
        go.AddComponent<TutorialDirector>();
    }
}
