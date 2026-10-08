using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Added only to SampleScene2. The legacy WaveManager remains a registry/service;
// its wave coroutine is stopped, while its ball-loss and bomber services remain.
[DefaultExecutionOrder(200)]
public sealed class Scene2TrialDirector : MonoBehaviour
{
    [SerializeField] private Scene2Config config;
    private Scene2WaveBridge _bridge;
    private readonly Scene2KeyTargetRule _keyTarget = new Scene2KeyTargetRule();
    private Scene2LoopRule _runRule;
    private readonly List<Minion> _members = new List<Minion>();
    private GameManager _game;
    private WaveManager _waves;
    private BossDefinition _bossDefinition;
    private GameConfig _trialGameConfig;
    private Scene2Boss _boss;
    private Scene2WeaknessReveal _reveal;
    private Coroutine _loop;
    private int _generation;
    private float _weakLeft;
    private string _status = "准备派兵";
    private float _waveTime;
    private int _kills, _leaks, _weakCount;
    private int _weakDamage, _emptyWindows;
    public int GroupNumber { get; private set; }
    public int SpawnedInGroup { get; private set; }
    public int KilledInGroup { get; private set; }
    public bool GroupFailed { get; private set; }
    public float WeakSecondsLeft => _weakLeft;
    public Scene2Boss CurrentBoss => _boss;
    public Scene2Config Config => config;
    public string Status => _status;
    public bool UsesKeyTarget => _runRule == Scene2LoopRule.KeyTarget;
    public Minion KeyTarget => _keyTarget.Target;
    public int WeakWindowsThisWave => _weakCount;
    public bool IsWeaknessRevealing => _reveal != null && _reveal.IsPlaying;

    private void Awake()
    {
        // Direct editor entry must also use the regular loadout, not tutorial mode.
        RunSession.BeginEndless();
        var game = GameManager.Instance;
        if (game != null && game.config != null)
        {
            _trialGameConfig = Instantiate(game.config);
            _trialGameConfig.scoreToCreditsRate = 0f;
            _trialGameConfig.minCreditsPerRun = 0;
            game.config = _trialGameConfig;
        }
    }

    private void Start()
    {
        _game = GameManager.Instance;
        _waves = WaveManager.Instance;
        _bridge = new Scene2WaveBridge(_waves);
        if (config == null || config.boss == null || config.grunt == null || _game == null || !_bridge.IsValid)
        {
            Debug.LogError("[Scene2] Missing trial config or legacy integration slot.");
            enabled = false;
            return;
        }
        _bossDefinition = Instantiate(config.boss);
        _bossDefinition.spawnTypes = Array.Empty<MinionDefinition>();
        _game.onGameStart.AddListener(BeginRun);
        _game.onGameOver.AddListener(StopRun);
        BeginRun();
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool("Scene2KeyTargetSmoke", false))
        {
            UnityEditor.SessionState.SetBool("Scene2KeyTargetSmoke", false);
            StartCoroutine(Scene2KeyTargetSmokeChecks.Run(this));
        }
        if (UnityEditor.SessionState.GetBool("Scene2DeathSmoke", false))
        {
            UnityEditor.SessionState.SetBool("Scene2DeathSmoke", false);
            StartCoroutine(Scene2SmokeChecks.RunDeath(this));
        }
        if (UnityEditor.SessionState.GetBool("Scene2ClearSmoke", false))
        {
            UnityEditor.SessionState.SetBool("Scene2ClearSmoke", false);
            StartCoroutine(Scene2SmokeChecks.RunClear(this));
        }
        if (UnityEditor.SessionState.GetBool("Scene2Smoke", false))
        {
            UnityEditor.SessionState.SetBool("Scene2Smoke", false);
            StartCoroutine(Scene2SmokeChecks.Run(this));
        }
#endif
    }

    private void BeginRun()
    {
        if (_reveal != null) _reveal.End();
        _runRule = config.loopRule;
        _keyTarget.Reset();
        _generation++;
        if (_loop != null) StopCoroutine(_loop);
        _waves.StopAllCoroutines();
        _waves.ClearTutorialField();
        _boss = null;
        _members.Clear();
        _weakLeft = 0f;
        _loop = StartCoroutine(Run());
    }

    private void StopRun()
    {
        _generation++;
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
        _weakLeft = 0f;
        if (_boss != null) _boss.SetWeak(false, 1f);
        if (_reveal != null) _reveal.End();
        _keyTarget.Reset();
        _status = "试玩结束";
        LogWave("failed");
    }

    private IEnumerator Run()
    {
        // Allow original cleanup and all Start listeners to complete.
        yield return null;
        while (_game != null && _game.State != GameState.GameOver)
        {
            int wave = _game.Wave;
            _members.Clear();
            GroupNumber = SpawnedInGroup = KilledInGroup = 0;
            _kills = _leaks = _weakCount = 0;
            _weakDamage = _emptyWindows = 0;
            _waveTime = 0f;
            // Legacy RunWave normally resets these. Scene2 owns that loop, so
            // re-arm the same particle cleanup for every Boss, not only the first.
            _bridge.PrepareWave();
            _waves.onWaveStart.Invoke(wave);
            _boss = SpawnBoss(wave);
            yield return WaitCombat(config.openingDelay);
            while (_boss != null && !_boss.IsDead && _game.State != GameState.GameOver)
            {
                yield return RunGroup(wave, ++GroupNumber);
                if (_boss == null || _boss.IsDead) break;
                if (!UsesKeyTarget && !GroupFailed && KilledInGroup == config.groupSize)
                {
                    yield return OpenWeakness(false);
                }
                else if (!UsesKeyTarget) _status = "编队漏怪 · 本组无弱点奖励";
                else _status = _keyTarget.TargetKilled ? "编队处理完毕 · 准备下一组" : "破局目标漏出 · 本组无弱点";
                yield return WaitCombat(config.groupRest);
            }
            if (_game.State == GameState.GameOver) yield break;
            _weakLeft = 0f;
            LogWave("boss-killed");
            _status = "Boss 击破 · 准备成长";
            _keyTarget.Reset();
            yield return _bridge.WaitForClear();
            if (_game.State == GameState.GameOver) yield break;
            _game.CompleteWave();
            while (_game.State == GameState.BuffSelection) yield return null;
            yield return WaitCombat(1f, false);
        }
    }

    private Scene2Boss SpawnBoss(int wave)
    {
        float cx = (_waves.spawnMinX + _waves.spawnMaxX) * 0.5f;
        var go = new GameObject("Scene2_Boss_W" + (wave + 1));
        go.tag = "Enemy";
        go.transform.position = new Vector3(cx, _waves.bossSpawnY, 0f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.mass = 500f; rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        go.AddComponent<BoxCollider2D>().size = new Vector2(1.5f, 1.5f);
        var boss = go.AddComponent<Scene2Boss>();
        _reveal = go.AddComponent<Scene2WeaknessReveal>();
        boss.Initialize(_bossDefinition, cx - _bossDefinition.moveRangeX * 0.5f, cx + _bossDefinition.moveRangeX * 0.5f, wave);
        // Definition has no spawn types, so Initialize cannot spawn legacy enemies.
        boss.StopAllCoroutines();
        if (config.bossHealth != null && config.bossHealth.Length > 0)
            boss.maxHits = Mathf.Max(1, config.bossHealth[Mathf.Min(wave, config.bossHealth.Length - 1)]);
        _bridge.RegisterBoss(boss);
        return boss;
    }

    private IEnumerator RunGroup(int wave, int group)
    {
        _members.Clear();
        _keyTarget.Reset();
        SpawnedInGroup = KilledInGroup = 0;
        GroupFailed = false;
        int generation = _generation;
        while (SpawnedInGroup < config.groupSize && BossAlive())
        {
            int count = Mathf.Min(Mathf.Max(1, config.batchSize), Mathf.Max(1, config.aliveCap), config.groupSize - SpawnedInGroup);
            _status = "编队 " + group + " · 下一批 " + count + " 只";
            while (BossAlive() && FindLivingMinions() + count > config.aliveCap)
            {
                yield return CheckKeyOpening();
                yield return null;
            }
            if (!BossAlive()) yield break;
            for (int i = 0; i < count; i++)
            {
                int index = SpawnedInGroup++;
                MinionDefinition def = config.grunt;
                if (wave >= 1 && index == config.groupSize - 1 && config.armored != null) def = config.armored;
                if (wave >= 2 && index == config.groupSize - 2 && config.bomber != null) def = config.bomber;
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                int batch = index / Mathf.Max(1, config.batchSize);
                float center = (_waves.spawnMinX + _waves.spawnMaxX) * 0.5f;
                float boardHalfWidth = _game.config.worldWidth * 0.5f;
                float halfWidth = Mathf.Min(config.spawnHalfWidth, Mathf.Max(0.5f, boardHalfWidth - Mathf.Abs(center) - 0.8f));
                // Spread over the usable table width; shift and reverse the
                // next batch so it does not repeat the same three lanes.
                float lane = count == 1 ? ((batch % 2 == 0) ? -0.45f : 0.45f) : Mathf.Lerp(-1f, 1f, t);
                if ((group + batch) % 2 == 0) lane = -lane;
                float shift = (batch % 2 == 0 ? -0.2f : 0.2f) * halfWidth;
                float x = center + Mathf.Clamp(lane * halfWidth + shift, -halfWidth, halfWidth);
                float y = _waves.bossSpawnY - 1.5f - ((i + batch) % count) * config.spawnVerticalStep;
                var minion = _waves.SpawnMinion(def, new Vector3(x, y, 0f), 0);
                if (minion == null) { GroupFailed = true; continue; }
                minion.gameObject.AddComponent<Scene2EnemyDeathTrace>();
                _members.Add(minion);
                // First enemy of the first batch: visible immediately, one target
                // per finite group. Existing enemy stats/kill rewards stay intact.
                if (UsesKeyTarget && index == 0) _keyTarget.Assign(minion);
                minion.onDeath.AddListener(_ =>
                {
                    if (_generation != generation || !BossAlive()) return;
                    KilledInGroup++; _kills++;
                });
            }
            _status = "编队 " + group + " · 击杀 " + KilledInGroup + "/" + config.groupSize;
            if (SpawnedInGroup < config.groupSize) yield return WaitGroupDelay(config.batchDelay);
        }
        while (BossAlive())
        {
            yield return CheckKeyOpening();
            if (!BossAlive()) yield break;
            int living = 0;
            foreach (var member in _members) if (member != null && !member.IsDead) living++;
            string target = !UsesKeyTarget ? "" : _keyTarget.TargetKilled ? " · 机会已兑现" :
                _keyTarget.TargetMissed ? " · 目标漏出" : " · 击杀菱形目标开弱点";
            _status = "编队 " + group + " · 击杀 " + KilledInGroup + "/" + config.groupSize + " · 在场 " + living + target;
            if (living == 0) break;
            yield return null;
        }
        if (!BossAlive()) yield break;
        int missed = config.groupSize - KilledInGroup;
        if (missed > 0) { GroupFailed = true; _leaks += missed; }
    }

    private IEnumerator CheckKeyOpening()
    {
        if (UsesKeyTarget && BossAlive() && _keyTarget.TryConsumeOpening())
            yield return OpenWeakness(true);
    }

    private IEnumerator OpenWeakness(bool refreshRedirect)
    {
        if (!refreshRedirect && config.weakRevealDuration > 0f)
            yield return PlayClearReveal();
        if (!BossAlive()) yield break;
        _weakCount++;
        var windowBoss = _boss;
        int hitsBefore = windowBoss.CurrentHits;
        _weakLeft = config.weakDuration;
        _boss.SetWeak(true, config.weakMultiplier);
        if (refreshRedirect) Scene2KeyTargetRule.RefreshRedirect();
        _status = "弱点暴露 · 全伤害 ×" + config.weakMultiplier.ToString("0.0") +
            (refreshRedirect ? " · 改向已就绪，转火 Boss" : "");
        Debug.Log("[Scene2] opening rule=" + _runRule + " group=" + GroupNumber +
            " kills=" + KilledInGroup + " spawned=" + SpawnedInGroup);
        // This blocks only the trial's spawn coroutine. Surviving minions keep
        // advancing, so turning to the Boss still carries a readable risk.
        while (_weakLeft > 0f && BossAlive())
        {
            if (CanAct()) _weakLeft = Mathf.Max(0f, _weakLeft - Time.deltaTime);
            yield return null;
        }
        if (_boss != null) _boss.SetWeak(false, 1f);
        int damage = Mathf.Max(0, windowBoss.CurrentHits - hitsBefore);
        _weakDamage += damage;
        if (damage == 0) _emptyWindows++;
        Debug.Log("[Scene2] opening-ended rule=" + _runRule + " group=" + GroupNumber + " damage=" + damage);
        _weakLeft = 0f;
    }

    private IEnumerator PlayClearReveal()
    {
        _status = "编队全清 · 能量外壳破裂";
        _reveal.Begin();
        float elapsed = 0f;
        while (elapsed < config.weakRevealDuration && BossAlive())
        {
            // Normal combat/input continue. A real pause suspends only the
            // presentation clock; weakness has not started or spent any time.
            if (Time.timeScale > 0f && _game.IsWaveSimActive() &&
                (PauseMenuController.Instance == null || !PauseMenuController.Instance.IsOpen))
                elapsed += Time.deltaTime;
            _reveal.Show(elapsed / config.weakRevealDuration);
            yield return null;
        }
        if (_reveal != null) _reveal.End();
    }

    private IEnumerator WaitGroupDelay(float seconds)
    {
        float left = seconds;
        while (left > 0f && BossAlive())
        {
            yield return CheckKeyOpening();
            if (_game.IsWaveSimActive()) left -= Time.deltaTime;
            _status = UsesKeyTarget && !_keyTarget.TargetKilled && !_keyTarget.TargetMissed
                ? "编队 " + GroupNumber + " · 击杀菱形目标开弱点"
                : "编队 " + GroupNumber + " · 下一批准备中";
            yield return null;
        }
        // Catch a kill on the final delay frame before spawning another batch.
        yield return CheckKeyOpening();
    }

    private bool BossAlive() => _boss != null && !_boss.IsDead && _game.State != GameState.GameOver;
    private bool CanAct() => Time.timeScale > 0f && _game != null && _game.State == GameState.Playing &&
        (PauseMenuController.Instance == null || !PauseMenuController.Instance.IsOpen) &&
        (BallController.Instance == null || !BallController.Instance.IsWaitingForLaunch) &&
        (SkillManager.Instance == null || (!SkillManager.Instance.IsAiming && !SkillManager.Instance.IsGroundAiming));

    private IEnumerator WaitCombat(float seconds, bool requireBoss = true)
    {
        float left = seconds;
        while (left > 0f && _game.State != GameState.GameOver && (!requireBoss || BossAlive()))
        {
            // Spawns continue during ball respawn, as in the existing game.
            if (_game.IsWaveSimActive()) left -= Time.deltaTime;
            yield return null;
        }
    }

    private int FindLivingMinions()
    {
        int count = 0;
        foreach (var minion in _members) if (minion != null && !minion.IsDead) count++;
        return count;
    }

    private void Update()
    {
        if (_game != null && _game.IsWaveSimActive() && BossAlive()) _waveTime += Time.deltaTime;
    }

    private void LogWave(string outcome)
    {
        Debug.Log("[Scene2] " + outcome + " rule=" + _runRule + " wave=" + (_game.Wave + 1) + " seconds=" + _waveTime.ToString("0.0") +
            " kills=" + _kills + " leaks=" + _leaks + " weakWindows=" + _weakCount +
            " weakDamage=" + _weakDamage + " emptyWindows=" + _emptyWindows);
    }

    private void OnDestroy()
    {
        _keyTarget.Reset();
        if (_game != null) { _game.onGameStart.RemoveListener(BeginRun); _game.onGameOver.RemoveListener(StopRun); }
        if (_bossDefinition != null) Destroy(_bossDefinition);
        if (_trialGameConfig != null) Destroy(_trialGameConfig);
    }
}
