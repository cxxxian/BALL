#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Opt-in integration regression. Only enabled by an Editor SessionState flag.
public static class Scene2KeyTargetSmokeChecks
{
    private static readonly List<string> Results = new List<string>();
    private static bool _failed;

    private static void Check(bool passed, string label)
    {
        Results.Add((passed ? "PASS " : "FAIL ") + label);
        _failed |= !passed;
        Debug.Log("[Scene2KeySmoke] " + Results[Results.Count - 1]);
    }

    private static void FreezeMinions()
    {
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>())
        {
            m.moveSpeed = 0f;
            m.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        }
    }

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout = 20f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            FreezeMinions();
            yield return null;
        }
        Check(condition(), "condition within " + timeout + "s");
    }

    private static void Finish()
    {
        Time.timeScale = 1f;
        System.IO.File.WriteAllText("Temp/Scene2KeyTargetSmokeResult.txt",
            string.Join("\n", Results) + "\nRESULT " + (_failed ? "FAILED" : "PASSED"));
        Debug.Log("[Scene2KeySmoke] " + (_failed ? "FAILED" : "PASSED"));
        UnityEditor.EditorApplication.isPlaying = false;
    }

    public static IEnumerator Run(Scene2TrialDirector d)
    {
        Results.Clear(); _failed = false;
        Application.runInBackground = true;
        yield return WaitUntil(() => d.KeyTarget != null && d.SpawnedInGroup == 3);
        if (_failed) { Finish(); yield break; }
        var game = GameManager.Instance;
        var ball = BallController.Instance;
        var skills = SkillManager.Instance;
        ball.ForceTestInPlay(Vector2.up);
        ball.GetComponent<Rigidbody2D>().simulated = false;
        var boss = d.CurrentBoss;
        boss.maxHits = 10000;
        Check(d.UsesKeyTarget && d.KeyTarget.GetComponent<Scene2KeyTargetMarker>() != null,
            "new rule and visible marker active");
        var ruleProbe = new Scene2KeyTargetRule();
        ruleProbe.Assign(d.KeyTarget);

        int lives = game.Lives;
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>())
            if (m != d.KeyTarget) { m.transform.position = new Vector3(0f, -30f, 0f); break; }
        yield return null; yield return null;
        Check(game.Lives == lives - 1 && d.WeakSecondsLeft == 0f,
            "ordinary leak costs life but does not open weakness");
        skills.slots[0].currentCD = 15f;
        skills.slots[1].currentCD = 15f;
        d.KeyTarget.ForceKill();
        yield return WaitUntil(() => d.WeakSecondsLeft > 0f);
        Check(d.SpawnedInGroup == 3 && d.KilledInGroup == 1 && boss.WeakActive,
            "target kill opens immediately despite ordinary leak, before second batch");
        Check(skills.slots[0].IsReady && skills.slots[1].currentCD > 10f,
            "only redirect refreshed; identity cooldown retained");
        Check(ruleProbe.TryConsumeOpening() && !ruleProbe.TryConsumeOpening(),
            "one target cannot award duplicate openings");
        ruleProbe.Reset();
        int before = boss.CurrentHits;
        boss.TakeHit(2);
        Check(boss.CurrentHits - before == 3, "weakness still grants 1.5x damage");

        float left = d.WeakSecondsLeft;
        var pause = PauseMenuController.Instance;
        Check(pause != null, "real pause menu available");
        pause.Open();
        yield return new WaitForSecondsRealtime(0.3f);
        Check(Mathf.Abs(left - d.WeakSecondsLeft) < 0.001f && d.SpawnedInGroup == 3,
            "pause freezes timer and subsequent spawning");
        pause.Close();
        Check(skills.TryActivate(0), "refreshed redirect is usable");
        left = d.WeakSecondsLeft;
        yield return new WaitForSecondsRealtime(0.3f);
        Check(Mathf.Abs(left - d.WeakSecondsLeft) < 0.001f && d.SpawnedInGroup == 3,
            "aiming freezes weakness and subsequent spawning");
        skills.CancelAiming();
        SlowMoFX.Instance?.ForceRestore();
        Time.timeScale = 1f;
        game.BallFellDown();
        left = d.WeakSecondsLeft;
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Mathf.Abs(left - d.WeakSecondsLeft) < 0.001f,
            "ball respawn freezes weakness timer");
        yield return WaitUntil(() => ball.IsWaitingForLaunch);
        left = d.WeakSecondsLeft;
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Mathf.Abs(left - d.WeakSecondsLeft) < 0.001f,
            "waiting for launch freezes weakness timer");
        ball.ForceTestInPlay(Vector2.up);
        game.OnBallRespawned();
        yield return WaitUntil(() => d.WeakSecondsLeft == 0f && d.SpawnedInGroup == 6);
        Check(!boss.WeakActive, "weakness closes and postponed batch resumes");
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>()) m.ForceKill();
        yield return WaitUntil(() => d.GroupNumber == 2 && d.KeyTarget != null);
        Check(d.WeakWindowsThisWave == 1, "later whole clear does not award a second opening");

        lives = game.Lives;
        d.KeyTarget.transform.position = new Vector3(0f, -30f, 0f);
        yield return null; yield return null;
        Check(game.Lives == lives - 1 && d.WeakSecondsLeft == 0f,
            "target leak costs life and grants no opening");
        yield return WaitUntil(() => d.SpawnedInGroup == 6);
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>()) m.ForceKill();
        yield return WaitUntil(() => d.GroupNumber == 3 && d.KeyTarget != null);
        Check(d.WeakWindowsThisWave == 1, "finishing a group with leaked target gives no consolation opening");
        d.KeyTarget.ForceKill();
        yield return WaitUntil(() => d.WeakSecondsLeft > 0f);
        boss.TakeHit(10000);
        yield return WaitUntil(() => game.State == GameState.BuffSelection);
        Check(game.Wave == 1 && RunSession.Chips == 2 && d.WeakSecondsLeft == 0f,
            "boss death during opening awards exactly one growth and ends weakness");
        Check(UnityEngine.Object.FindObjectsOfType<Minion>().Length == 0,
            "survivors dissolve before growth");
        UnityEngine.Object.FindObjectOfType<BuffSelectionController>()?.Hide();
        game.OnBuffSelectionDone();
        Time.timeScale = 1f;
        yield return WaitUntil(() => d.CurrentBoss != null && d.CurrentBoss != boss);
        Check(WaveManager.Instance.CurrentBoss == d.CurrentBoss,
            "next boss remains registered for weapons and targeting");
        game.StartGame();
        yield return WaitUntil(() => d.CurrentBoss != null && game.Wave == 0);
        Check(d.KeyTarget == null && d.WeakSecondsLeft == 0f && d.WeakWindowsThisWave == 0 &&
            RunSession.Chips == 0 && UnityEngine.Object.FindObjectsOfType<Boss>().Length == 1,
            "restart resets target, openings, rewards and boss registry");
        Finish();
    }
}
#endif
