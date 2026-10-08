#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicitly armed editor regression; absent from player builds and normal runs.
public sealed class BumperBomberRegression : MonoBehaviour
{
    private readonly List<string> _checks = new List<string>();
    private bool _failed;
    private Bumper[] _bumpers;
    private WaveManager _waves;
    private GameManager _game;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!UnityEditor.SessionState.GetBool("BumperBomberRegression", false)) return;
        UnityEditor.SessionState.SetBool("BumperBomberRegression", false);
        new GameObject("BumperBomberRegression").AddComponent<BumperBomberRegression>();
    }
    private void Check(bool passed, string label)
    {
        _checks.Add((passed ? "PASS " : "FAIL ") + label);
        _failed |= !passed;
        Debug.Log("[BumperBomberTest] " + _checks[_checks.Count - 1]);
    }
    private bool AllAvailable()
    {
        foreach (var b in _bumpers)
            if (b.IsDisabled || b.IsPassthrough || !b.GetComponent<Collider2D>().enabled) return false;
        return true;
    }
    private bool AllBomberDisabled()
    {
        foreach (var b in _bumpers)
            if (!b.IsDisabled || b.GetComponent<Collider2D>().enabled) return false;
        return true;
    }
    private IEnumerator WaitUntil(Func<bool> condition, string label, float timeout = 8f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            foreach (var m in FindObjectsOfType<Minion>())
            {
                m.moveSpeed = 0f;
                m.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            }
            yield return null;
        }
        Check(condition(), label);
    }
    private IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null; yield return null;
        _waves = WaveManager.Instance;
        _game = GameManager.Instance;
        _bumpers = FindObjectsOfType<Bumper>();
        BallController.Instance.GetComponent<Rigidbody2D>().simulated = false;
        // Wait for the real first-wave event before injecting timed penalties;
        // otherwise the legitimate opening reset races the timer test.
        yield return WaitUntil(() => _waves.CurrentBoss != null, "opening round started");
        Check(_bumpers.Length > 0 && AllAvailable(), "initial bumpers visible/available");
        var subject = _bumpers[0];
        var renderer = subject.GetComponentInChildren<SpriteRenderer>();
        Color normalColor = renderer.color;
        int lives = _game.Lives;
        var bomberDef = UnityEditor.AssetDatabase.LoadAssetAtPath<MinionDefinition>("Assets/ScriptableObjects/Enemies/Minion_Bomber.asset");
        _waves.SpawnMinion(bomberDef, new Vector3(0f, -6f, 0f));
        yield return null; yield return null;
        Check(_game.Lives == lives - bomberDef.damageToPlayer && AllBomberDisabled(), "actual bomber breach disables all physical bumpers");
        Check(renderer.color != normalColor, "disabled bumper has distinct dim appearance");
        subject.SetPassthrough(true);
        subject.SetPassthrough(false);
        Check(AllBomberDisabled(), "ending execute does not re-enable bomber-disabled colliders");
        subject.SetPassthrough(true);
        _waves.onWaveStart.Invoke(_game.Wave);
        Check(!subject.IsDisabled && subject.IsPassthrough && !subject.GetComponent<Collider2D>().enabled, "clearing bomber penalty preserves execute passthrough");
        subject.SetPassthrough(false);
        Check(AllAvailable() && renderer.color == normalColor, "new wave restores physics and normal visuals");

        _waves.TriggerBomberEffect(0.65f);
        yield return new WaitForSecondsRealtime(0.15f);
        _waves.TriggerBomberEffect(0.05f);
        yield return new WaitForSecondsRealtime(0.15f);
        Check(AllBomberDisabled(), "shorter subsequent breach cannot shorten existing penalty");
        yield return WaitUntil(AllAvailable, "timer naturally restores all bumpers", 2f);
        subject.SetPassthrough(true);
        _waves.TriggerBomberEffect(0.15f);
        yield return new WaitForSecondsRealtime(0.35f);
        Check(!subject.IsDisabled && !subject.GetComponent<Collider2D>().enabled, "timer expiry cannot override active execute passthrough");
        subject.SetPassthrough(false);
        Check(AllAvailable(), "ending final blocker restores collider");

        _waves.TriggerBomberEffect(10f);
        _game.StartGame();
        yield return null; yield return null;
        Check(AllAvailable() && renderer.color == normalColor, "restart during active penalty restores appearance and collision");
        _waves.TriggerBomberEffect(0.15f);
        Check(AllBomberDisabled(), "new penalty starts after interrupted previous coroutine");
        yield return WaitUntil(AllAvailable, "new penalty expires normally after restart", 2f);
        yield return WaitUntil(() => _waves.CurrentBoss != null, "boss exists for round-transition check");
        _waves.TriggerBomberEffect(10f);
        _waves.CurrentBoss.TakeHit(_waves.CurrentBoss.maxHits, true);
        Check(AllAvailable(), "boss kill clears temporary bomber penalty immediately");
        yield return WaitUntil(() => _game.State == GameState.BuffSelection, "boss kill reaches slot machine");
        BuffSelectionController.Instance?.Hide();
        _game.OnBuffSelectionDone();
        Time.timeScale = 1f;
        yield return WaitUntil(() => _waves.CurrentBoss != null, "next round begins");
        Check(AllAvailable() && renderer.color == normalColor, "next actual round has fully restored bumpers");
        System.IO.File.WriteAllText("Temp/BumperBomber-" + SceneManager.GetActiveScene().name + ".txt", string.Join("\n", _checks) + "\nRESULT " + (_failed ? "FAILED" : "PASSED"));
        UnityEditor.EditorApplication.isPlaying = false;
    }
}
#endif
