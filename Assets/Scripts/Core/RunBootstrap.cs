using UnityEngine;

[DefaultExecutionOrder(-100)]
public class RunBootstrap : MonoBehaviour
{
    private void Awake()
    {
        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null)
            RunLoadout.EnsureDefaults(catalog);
    }

    private void Start()
    {
        ApplyToBattle();
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
}
