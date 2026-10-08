#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Opt-in editor smoke checks. Never runs during normal play or in a player build.
public static class Scene2SmokeChecks
{
    private static readonly List<string> Results = new List<string>();
    private static bool _failed;
    private static void Check(bool passed, string label)
    {
        Results.Add((passed ? "PASS " : "FAIL ") + label);
        if (!passed) _failed = true;
        Debug.Log("[Scene2Smoke] " + Results[Results.Count - 1]);
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
        Check(condition(), "wait condition within " + timeout + "s");
    }
    public static IEnumerator Run(Scene2TrialDirector d)
    {
        Results.Clear(); _failed = false;
        yield return WaitUntil(() => d.CurrentBoss != null);
        var game = GameManager.Instance;
        var ball = BallController.Instance;
        ball.ForceTestInPlay(Vector2.up);
        ball.GetComponent<Rigidbody2D>().simulated = false;
        var boss = d.CurrentBoss;
        boss.maxHits = 10000;
        yield return new WaitForSeconds(0.25f);
        var shield = boss.GetComponent<Scene2WeaknessReveal>();
        Check(shield != null && shield.ShieldVisible, "reactive shield visible before full clear");
        var bossBody = boss.GetComponent<Rigidbody2D>();
        var originalConstraints = bossBody.constraints;
        bossBody.constraints = RigidbodyConstraints2D.FreezeAll;
        var ballBody = ball.GetComponent<Rigidbody2D>();
        ballBody.simulated = true;
        ballBody.position = (Vector2)boss.transform.position + Vector2.right * 1.4f;
        ball.ForceTestInPlay(Vector2.left * 8f);
        int collisionHits = boss.CurrentHits;
        yield return WaitUntil(() => shield.CollisionResponses > 0, 3f);
        Check(boss.CurrentHits > collisionHits && shield.CollisionResponses > 0,
            "real ball collision keeps damage and triggers localized shield response");
        Check(shield.CollisionResponses > 0 && shield.LastImpactWorld.x > boss.transform.position.x,
            "ripple origin follows actual right-side contact");
        ballBody.simulated = false;
        ball.ForceTestInPlay(Vector2.up);
        bossBody.constraints = originalConstraints;
        for (int i = 0; i < 5; i++) shield.PulseAt((Vector2)boss.transform.position + Vector2.right * 0.7f);
        Check(shield.ActiveRipples == 3, "rapid shield hits capped at three ripples");
        PauseMenuController.Instance.Open();
        yield return new WaitForSecondsRealtime(0.6f);
        Check(shield.ActiveRipples == 3, "real pause freezes shield ripple lifetime");
        PauseMenuController.Instance.Close();
        yield return new WaitForSeconds(0.55f);
        Check(shield.ActiveRipples == 0, "shield ripples expire after resuming");
        Check(WaveManager.Instance.CurrentBoss == boss, "new boss registered for weapons and targeting");
        int before = boss.CurrentHits;
        boss.TakeHit(2);
        Check(boss.CurrentHits - before == 2, "Boss damage allowed before clearing minions");
        boss.SetWeak(true, 1.5f);
        before = boss.CurrentHits;
        boss.TakeHit(1); boss.TakeHit(1);
        Check(boss.CurrentHits - before == 3, "1.5x basic damage with fractional carry");
        before = boss.CurrentHits;
        boss.TakeFlipperWeaponHit(2, boss.transform.position);
        Check(boss.CurrentHits - before == 3, "1.5x flipper weapon damage");
        before = boss.CurrentHits;
        boss.TakeHitFromCorePulse(2, boss.transform.position);
        Check(boss.CurrentHits - before == 3, "1.5x core pulse damage");
        before = boss.CurrentHits;
        for (int i = 0; i < 4; i++) boss.TakeBallHitScaled(0.5f);
        Check(boss.CurrentHits - before == 3, "1.5x fractional phantom damage");
        boss.SetWeak(false, 1f);
        before = boss.CurrentHits; boss.TakeHit(2);
        Check(boss.CurrentHits - before == 2, "normal damage restored after weakness");

        yield return WaitUntil(() => d.SpawnedInGroup == 3);
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>()) m.ForceKill();
        yield return null;
        Check(d.KilledInGroup == 3 && d.WeakSecondsLeft == 0f, "first batch alone does not award weakness");
        yield return WaitUntil(() => d.SpawnedInGroup == 6);
        SkillManager.Instance.slots[0].currentCD = 20f;
        SkillManager.Instance.slots[1].currentCD = 20f;
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>()) m.ForceKill();
        yield return WaitUntil(() => d.IsWeaknessRevealing);
        Check(d.WeakSecondsLeft == 0f && !boss.WeakActive,
            "shell reveal plays before weakness starts");
        var reveal = boss.GetComponent<Scene2WeaknessReveal>();
        float revealProgress = reveal.Progress;
        PauseMenuController.Instance.Open();
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Mathf.Abs(reveal.Progress - revealProgress) < 0.001f && d.WeakSecondsLeft == 0f,
            "actual pause freezes reveal without spending weakness time");
        PauseMenuController.Instance.Close();
        yield return WaitUntil(() => d.WeakSecondsLeft > 0f);
        Check(d.KilledInGroup == 6 && boss.WeakActive, "whole group kill awards weakness");
        Check(!shield.ShieldVisible && !shield.PulseAt(boss.transform.position), "weakness removes shield and ignores shield impacts");
        Check(!d.IsWeaknessRevealing && d.WeakSecondsLeft >= d.Config.weakDuration - 0.2f,
            "full weakness duration remains after shell reveal");
        Check(SkillManager.Instance.slots[0].currentCD > 15f && SkillManager.Instance.slots[1].currentCD > 15f,
            "reveal refreshes neither redirect nor identity cooldown");
        float pausedLeft = d.WeakSecondsLeft;
        PauseMenuController.Instance.Open();
        yield return new WaitForSecondsRealtime(0.25f);
        Check(Mathf.Abs(d.WeakSecondsLeft - pausedLeft) < 0.001f, "weakness timer pauses with simulation");
        Check(UnityEngine.Object.FindObjectsOfType<Minion>().Length == 0, "no spawning during weakness");
        PauseMenuController.Instance.Close();
        yield return WaitUntil(() => d.GroupNumber == 2 && d.SpawnedInGroup == 6);
        var members = UnityEngine.Object.FindObjectsOfType<Minion>();
        Check(members.Length == 6 && !boss.WeakActive, "second finite group appears after weakness and rest");
        Check(shield.ShieldVisible, "shield reforms for the next preparation phase");
        if (members.Length > 0)
        {
            members[0].transform.position = new Vector3(0f, -6f, 0f);
            yield return null;
            yield return null;
            for (int i = 1; i < members.Length; i++) if (members[i] != null) members[i].ForceKill();
        }
        yield return null; yield return null;
        Check(d.GroupFailed && d.KilledInGroup == 5 && d.WeakSecondsLeft == 0f, "one leak prevents reward; killed five do not count as full clear");
        Check(game.Lives > 0, "leak uses ordinary life penalty");
        boss.TakeHit(10000);
        yield return WaitUntil(() => game.State == GameState.BuffSelection);
        Check(game.Wave == 1 && RunSession.Chips == 2, "boss kill advances exactly one round and one reward");
        Check(UnityEngine.Object.FindObjectsOfType<Minion>().Length == 0, "boss kill clears remaining enemies");
        UnityEngine.Object.FindObjectOfType<BuffSelectionController>()?.Hide();
        game.OnBuffSelectionDone();
        Time.timeScale = 1f;
        yield return WaitUntil(() => d.CurrentBoss != null && d.CurrentBoss != boss);
        Check(game.Wave == 1 && d.CurrentBoss.maxHits == d.Config.bossHealth[1], "next round has a new boss and Scene2 health");
        yield return WaitUntil(() => d.SpawnedInGroup == 6);
        foreach (var m in UnityEngine.Object.FindObjectsOfType<Minion>()) m.ForceKill();
        yield return WaitUntil(() => d.IsWeaknessRevealing);
        d.CurrentBoss.TakeHit(d.CurrentBoss.maxHits);
        yield return WaitUntil(() => game.State == GameState.BuffSelection);
        Check(game.Wave == 2 && RunSession.Chips == 4 && d.WeakSecondsLeft == 0f && !d.IsWeaknessRevealing,
            "Boss death during reveal cancels presentation and awards one growth only");
        UnityEngine.Object.FindObjectOfType<BuffSelectionController>()?.Hide();
        Time.timeScale = 1f;
        game.StartGame();
        yield return WaitUntil(() => d.CurrentBoss != null && game.Wave == 0);
        Check(UnityEngine.Object.FindObjectsOfType<Boss>().Length == 1 && d.GroupNumber == 0 && RunSession.Chips == 0, "restart resets trial with one boss and no old rewards");
        string report = string.Join("\n", Results) + "\nRESULT " + (_failed ? "FAILED" : "PASSED");
        System.IO.File.WriteAllText("Temp/Scene2SmokeResult.txt", report);
        Debug.Log("[Scene2Smoke] " + (_failed ? "FAILED" : "PASSED"));
        UnityEditor.EditorApplication.isPlaying = false;
    }

    // Regression: two consecutive Boss kills with surviving minions must both
    // dissolve the field, without kill rewards or leak damage, before slots.
    public static IEnumerator RunClear(Scene2TrialDirector d)
    {
        Results.Clear(); _failed = false;
        Application.runInBackground = true;
        for (int wave = 0; wave < 2; wave++)
        {
            yield return WaitUntil(() => d.CurrentBoss != null && UnityEngine.Object.FindObjectsOfType<Minion>().Length >= 3);
            var game = GameManager.Instance;
            var boss = d.CurrentBoss;
            var minions = UnityEngine.Object.FindObjectsOfType<Minion>();
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            int clearDeaths = 0;
            foreach (var minion in minions)
            {
                left = Mathf.Min(left, minion.transform.position.x);
                right = Mathf.Max(right, minion.transform.position.x);
                minion.onDeath.AddListener(_ => clearDeaths++);
            }
            Check(right - left > 5f, "wave " + wave + " formation spans the table");
            int lives = game.Lives;
            float killedAt = Time.realtimeSinceStartup;
            boss.TakeHit(boss.maxHits, true);
            int scoreAfterBossKill = game.Score;
            yield return WaitUntil(() => game.State == GameState.BuffSelection);
            Check(UnityEngine.Object.FindObjectsOfType<Minion>().Length == 0, "wave " + wave + " all survivors dissolved before slots");
            Check(clearDeaths == 0 && game.Score == scoreAfterBossKill && game.Lives == lives, "wave " + wave + " system clear awards no kills and causes no leaks");
            Check(Time.realtimeSinceStartup - killedAt >= WaveManager.Instance.breachClearSettleSeconds, "wave " + wave + " particles have settle time before slots");
            Check(game.Wave == wave + 1 && RunSession.Chips == (wave + 1) * 2, "wave " + wave + " one slot reward only");
            if (wave == 0)
            {
                UnityEngine.Object.FindObjectOfType<BuffSelectionController>()?.Hide();
                game.OnBuffSelectionDone();
                Time.timeScale = 1f;
            }
        }
        System.IO.File.WriteAllText("Temp/Scene2ClearSmokeResult.txt", string.Join("\n", Results) + "\nRESULT " + (_failed ? "FAILED" : "PASSED"));
        Debug.Log("[Scene2Smoke] Clear regression " + (_failed ? "FAILED" : "PASSED"));
        UnityEditor.EditorApplication.isPlaying = false;
    }

    public static IEnumerator RunDeath(Scene2TrialDirector d)
    {
        Results.Clear(); _failed = false;
        Application.runInBackground = true;
        yield return WaitUntil(() => UnityEngine.Object.FindObjectsOfType<Minion>().Length == 3);
        BallController.Instance.GetComponent<Rigidbody2D>().simulated = false;
        var minions = UnityEngine.Object.FindObjectsOfType<Minion>();
        foreach (var m in minions)
        {
            m.moveSpeed = 0f;
            m.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        }
        var victim = minions[0];
        victim.transform.position = new Vector3(0f, 1f, 0f);
        var trace = victim.GetComponent<Scene2EnemyDeathTrace>();
        minions[1].transform.position = new Vector3(3.4f, 5f, 0f);
        minions[2].transform.position = new Vector3(-3.4f, 5f, 0f);
        SkillManager.Instance.ClearExecuteArm();
        BallController.Instance.StopExecuteChain();
        float until = Time.realtimeSinceStartup + 0.4f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Check(!victim.IsDead && victim.CurrentHits == 0, "no input/no collisions does not damage a grunt");
        Check(!SkillManager.Instance.IsExecuteArmed && !BallController.Instance.IsExecuteChainActive, "execute remains inactive without input");
        // Combo ignores a ball still waiting on the launcher. Mark it in flight
        // while retaining disabled physics; no skill activation is performed.
        BallController.Instance.ForceTestInPlay(Vector2.up);
        var combo = ComboSystem.Instance;
        int[] milestones = { 25, 35, 45, 55, 65 };
        foreach (int milestone in milestones)
        {
            while (combo.CurrentCombo < milestone)
                combo.RegisterAirtimeHit(new Vector2(0f, 1f));
            yield return new WaitForSeconds(1.1f);
            Check(victim != null && !victim.IsDead && victim.CurrentHits == 0,
                "combo " + milestone + " does not damage nearby grunt");
            Check(UnityEngine.GameObject.Find("BumperPulseWave") == null,
                "combo " + milestone + " does not spawn a pulse wave");
        }
        Check(!SkillManager.Instance.IsExecuteArmed && !BallController.Instance.IsExecuteChainActive, "high combo does not activate execute");
        Check(minions[1] != null && !minions[1].IsDead && minions[2] != null && !minions[2].IsDead, "other grunts survive high combo");
        victim.TakeHit(1, isFromBall: true, victim.transform.position);
        Check(victim.IsDead, "ordinary ball damage still kills 1 HP grunt");
        System.IO.File.WriteAllText("Temp/Scene2DeathSmokeResult.txt", string.Join("\n", Results) + "\nRESULT " + (_failed ? "FAILED" : "PASSED"));
        UnityEditor.EditorApplication.isPlaying = false;
    }
}
#endif
