using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 后期导弹节奏扩展（先预留，当前正式局只用默认单发慢速）。
/// </summary>
[Serializable]
public class BossMissileScaling
{
    [Tooltip("相对基础飞行时长的倍率；&lt;1 更快")]
    public float flightDurationScale = 1f;

    [Tooltip("相对基础预警时长的倍率")]
    public float telegraphDurationScale = 1f;

    [Tooltip("单次齐射导弹数（预留）")]
    public int missilesPerVolley = 1;

    [Tooltip("齐射内相邻导弹间隔（秒）")]
    public float volleyStagger = 0.35f;

    [Tooltip("齐射左右交替弧摆幅额外加成")]
    public float volleySideBoost = 0.35f;

    public static BossMissileScaling Default => new BossMissileScaling();

    /// <summary>按波次给一个可调的后期曲线占位（未接入正式波次表前可手调）。</summary>
    public static BossMissileScaling ForWave(int waveIndex)
    {
        // 预留：W1–2 慢单发；更后可加速 / 多发（暂不启用多发）
        var s = Default;
        if (waveIndex <= 1)
        {
            s.flightDurationScale = 1f;
            s.missilesPerVolley = 1;
        }
        else if (waveIndex <= 3)
        {
            s.flightDurationScale = 0.9f;
            s.missilesPerVolley = 1;
        }
        else
        {
            s.flightDurationScale = Mathf.Max(0.55f, 1f - 0.06f * (waveIndex - 1));
            s.telegraphDurationScale = Mathf.Max(0.7f, 1f - 0.04f * (waveIndex - 1));
            s.missilesPerVolley = 1; // 多发后期：waveIndex>=6 ? 2 : 1；上限 3
        }
        return s;
    }
}

/// <summary>
/// Boss 导弹弹刀：警告虚线（时有时无）+ 感叹号 → 慢速飞行 → 外圈固定/内圈收缩弹刀。
/// </summary>
public class BossMissileAttack : MonoBehaviour
{
    [Header("Enable")]
    public int minWaveIndex = 1;

    [Header("Timing (base)")]
    public float firstDelay = 5.5f;
    public float cooldownP1 = 10f;
    public float cooldownP2 = 7f;
    [Tooltip("基础预警时长；实际 × scaling.telegraphDurationScale")]
    public float telegraphDuration = 1.25f;
    [Tooltip("基础飞行时长（已调慢）；实际 × scaling.flightDurationScale")]
    public float flightDuration = 2.45f;

    [Header("Parry (outer fixed + inner shrink)")]
    public float approachRadius = 1.75f;
    public float shrinkDuration = 0.68f;

    [Header("Fairness Gates")]
    [Tooltip("球离 Boss 过近时延后开火（世界单位）")]
    public float minFireDistance = 2.8f;
    [Tooltip("近距等待上限；超时仍开火，但强制拉长飞行/大弧")]
    public float closeBallWaitMax = 2.0f;
    [Tooltip("导弹最短飞行时间（贴脸时强制）")]
    public float minFlightDuration = 1.15f;
    [Tooltip("允许进入弹刀窗前的最短存活时间")]
    public float minApproachDelay = 1.0f;
    [Tooltip("球过近时弧摆幅倍率")]
    public float closeBallArcBoost = 1.75f;

    [Header("Damage")]
    public int goodDamage = 2;
    public int perfectDamage = 4;

    [Header("Arc")]
    public float sideSwing = 2.4f;
    public float predictLead = 0.4f;
    public float flightRetarget = 0.35f;

    [Header("Warning Dash")]
    public int dashCount = 16;
    public float dashLen = 0.2f;
    public float gapLen = 0.16f;
    [Tooltip("警告开关频率（Hz）：感叹号 + 球锁定同步显隐")]
    public float warningBlinkHz = 3.2f;
    public Sprite ballLockSprite;

    private const string BallLockAssetPath = "Assets/Art/UI/Fx/ui_ball_lockon_v1.png";
    private const string BallLockResourcePath = "UI/ui_ball_lockon_v1";

    [Header("Late-game Scaling (reserved)")]
    public bool useWaveScaling = true;
    public BossMissileScaling scalingOverride;

    private Boss _boss;
    private int _waveIndex;
    private Coroutine _loop;
    private BezierMissile _active;
    private int _swingSign = 1;
    private readonly List<LineRenderer> _dashSegs = new List<LineRenderer>();
    private Transform _dashRoot;
    private SpriteRenderer _edgeArrow;
    private SpriteRenderer _lockMark;
    private readonly List<SpriteRenderer> _warnBangs = new List<SpriteRenderer>();
    private SpriteRenderer _ballLock;
    private Vector3 _lastP0, _lastP1, _lastP2, _lastP3;
    private int _activeCount;

    [Header("Warn Bang Layout")]
    [Tooltip("感叹号绕 Boss 外圈半径，避免与身体重叠")]
    public float bangOrbitRadius = 1.15f;

    public bool HasActiveMissile => _activeCount > 0 || _active != null;

    public BossMissileScaling CurrentScaling =>
        scalingOverride ?? (useWaveScaling ? BossMissileScaling.ForWave(_waveIndex) : BossMissileScaling.Default);

    public void Initialize(Boss boss, int waveIndex)
    {
        _boss = boss;
        _waveIndex = waveIndex;
        EnsurePreview();
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        if (waveIndex < minWaveIndex)
            return;
        _loop = StartCoroutine(AttackLoop());
    }

    public bool TryFireNow()
    {
        if (_boss == null || _boss.IsDead) return false;
        if (HasActiveMissile) return false;
        if (_waveIndex < minWaveIndex) return false;

        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        _loop = StartCoroutine(FireNowThenResumeLoop());
        return true;
    }

    public bool TryFireImmediate()
    {
        if (_boss == null || _boss.IsDead) return false;
        if (HasActiveMissile) return false;

        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch)
            return false;

        StartCoroutine(FireVolleyRoutine(skipTelegraph: true));
        return true;
    }

    private void OnDisable()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        ClearActive();
        SetTelegraphVisible(false);
    }

    private IEnumerator FireNowThenResumeLoop()
    {
        yield return TelegraphAndFire();
        float cd = _boss != null && _boss.InPhase2 ? cooldownP2 : cooldownP1;
        yield return new WaitForSecondsRealtime(cd);
        _loop = StartCoroutine(AttackLoopFromCooldown());
    }

    private IEnumerator AttackLoopFromCooldown()
    {
        while (_boss != null && !_boss.IsDead)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsWaveSimActive())
            {
                yield return null;
                continue;
            }

            if (HasActiveMissile)
            {
                yield return null;
                continue;
            }

            yield return TelegraphAndFire();
            float cd = _boss.InPhase2 ? cooldownP2 : cooldownP1;
            yield return new WaitForSecondsRealtime(cd);
        }
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSecondsRealtime(firstDelay);
        yield return AttackLoopFromCooldown();
    }

    private IEnumerator TelegraphAndFire()
    {
        float waitBall = 0f;
        while (waitBall < 2f)
        {
            var b = BallController.Instance;
            if (b != null && !b.IsWaitingForLaunch)
                break;
            waitBall += Time.unscaledDeltaTime;
            yield return null;
        }

        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch)
            yield break;

        // 贴 Boss：先等球拉开一点，给读招时间
        float waitClose = 0f;
        while (IsBallTooClose(ball) && waitClose < closeBallWaitMax)
        {
            waitClose += Time.unscaledDeltaTime;
            yield return null;
            ball = BallController.Instance;
            if (ball == null || ball.IsWaitingForLaunch)
                yield break;
            if (_boss == null || _boss.IsDead)
                yield break;
        }

        EnsurePreview();
        SetTelegraphVisible(true);
        FlashBossMuzzle();

        var scale = CurrentScaling;
        float telDur = telegraphDuration * Mathf.Max(0.35f, scale.telegraphDurationScale);

        float t = 0f;
        while (t < telDur)
        {
            if (_boss == null || _boss.IsDead)
            {
                SetTelegraphVisible(false);
                ClearThreatVignette();
                yield break;
            }
            if (GameManager.Instance != null && !GameManager.Instance.IsWaveSimActive())
            {
                SetTelegraphVisible(false);
                ClearThreatVignette();
                yield break;
            }

            ball = BallController.Instance;
            if (ball == null || ball.IsWaitingForLaunch)
            {
                SetTelegraphVisible(false);
                ClearThreatVignette();
                yield break;
            }

            Vector3 p0 = GetMuzzlePos();
            BuildCurve(p0, ball, out Vector3 p1, out Vector3 p2, out Vector3 p3, _swingSign);
            _lastP0 = p0; _lastP1 = p1; _lastP2 = p2; _lastP3 = p3;

            float u = t / telDur;
            // 无虚线：Boss 感叹号 + 球身锁定框，三拍闪烁
            bool warnOn;
            bool urgent = u >= 0.75f;
            bool solid = urgent && (telDur - t) <= 0.16f;
            if (u < 0.40f)
            {
                warnOn = true; // 淡锁定常亮
                ApplyThreatVignette(0.06f + 0.04f * Mathf.Sin(Time.unscaledTime * 3f));
            }
            else if (!solid)
            {
                float hz = warningBlinkHz * (u >= 0.75f ? 2.0f : 1.15f);
                float duty = u >= 0.75f ? 0.38f : 0.28f;
                warnOn = Mathf.Repeat(Time.unscaledTime * hz, 1f) < duty;
                ApplyThreatVignette(warnOn ? (urgent ? 0.26f : 0.18f) : 0.05f);
            }
            else
            {
                warnOn = true;
                ApplyThreatVignette(0.3f);
            }

            // 虚线 / 球锁定框先关掉（锁定有 bug，暂禁用）
            HideDashAndExtras();
            if (_ballLock != null) _ballLock.enabled = false;

            int volleyCount = Mathf.Clamp(scale.missilesPerVolley, 1, 3);
            EnsureWarnBangs(volleyCount);
            UpdateWarnBangs(volleyCount, warnOn, urgent || solid);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        ClearThreatVignette();
        HideDashAndExtras();
        if (_ballLock != null) _ballLock.enabled = false;
        // 感叹号留给齐射逐发熄灭，不在此全关
        if (_boss == null || _boss.IsDead)
        {
            HideAllWarnBangs();
            yield break;
        }
        yield return FireVolleyRoutine(skipTelegraph: false);
    }

    private void ApplyThreatVignette(float amount)
    {
        SlowMoFX.Instance?.SetBossKillVignette(amount, new Color(1f, 0.18f, 0.05f));
    }

    private void ClearThreatVignette()
    {
        SlowMoFX.Instance?.SetBossKillVignette(0f, Color.clear);
    }

    private void HideDashAndExtras()
    {
        if (_dashRoot != null) _dashRoot.gameObject.SetActive(false);
        for (int i = 0; i < _dashSegs.Count; i++)
        {
            if (_dashSegs[i] != null) _dashSegs[i].enabled = false;
        }
        if (_edgeArrow != null) _edgeArrow.enabled = false;
        if (_lockMark != null) _lockMark.enabled = false;
    }

    /// <summary>齐射入口：当前默认 1 发；后期靠 scaling.missilesPerVolley 打开。</summary>
    private IEnumerator FireVolleyRoutine(bool skipTelegraph)
    {
        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch)
        {
            HideAllWarnBangs();
            yield break;
        }

        var scale = CurrentScaling;
        int count = Mathf.Clamp(scale.missilesPerVolley, 1, 3);
        // 轻微错开即可：太密会叠窗误触双弹，太疏第二发会接不上
        float stagger = Mathf.Clamp(scale.volleyStagger, 0.35f, 0.9f);

        EnsureWarnBangs(count);
        if (skipTelegraph)
        {
            float flash = 0f;
            while (flash < 0.4f)
            {
                UpdateWarnBangs(count, true, flash > 0.22f);
                flash += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            // 预警结束：全部常亮一瞬再开打
            UpdateWarnBangs(count, true, true);
        }

        for (int i = 0; i < count; i++)
        {
            if (_boss == null || _boss.IsDead) break;
            ball = BallController.Instance;
            if (ball == null || ball.IsWaitingForLaunch) break;

            int sign = (i % 2 == 0) ? _swingSign : -_swingSign;
            // 多发时进一步拉开左右弧，避免弹刀窗重叠
            if (count > 1 && i >= 2)
                sign = (i % 2 == 0) ? _swingSign : -_swingSign;

            Vector3 p0 = GetMuzzlePos();
            BuildCurve(p0, ball, out Vector3 p1, out Vector3 p2, out Vector3 p3, sign, i, count);

            if (!skipTelegraph && i == 0)
            {
                // 首发仍可用预警末帧落点，但侧摆按 index 0 重算更稳
                BuildCurve(_lastP0, ball, out p1, out p2, out p3, sign, 0, count);
                p0 = _lastP0;
            }

            // 熄灭对应方向感叹号，表示这一发已出膛
            SetWarnBangEnabled(i, false);
            FireMissile(p0, p1, p2, p3, scale);

            if (i < count - 1 && stagger > 0f)
                yield return new WaitForSecondsRealtime(stagger);
        }

        HideAllWarnBangs();
        _swingSign = -_swingSign;
    }

    private void FireMissile(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, BossMissileScaling scale)
    {
        var go = new GameObject("BossMissile");
        go.transform.position = p0;
        var missile = go.AddComponent<BezierMissile>();
        _active = missile;
        _activeCount++;
        missile.onFinished += OnMissileFinished;

        float dur = flightDuration * Mathf.Max(0.35f, scale.flightDurationScale);
        dur = Mathf.Max(minFlightDuration, dur);
        missile.Launch(
            _boss,
            p0, p1, p2, p3,
            dur,
            approachRadius,
            shrinkDuration,
            goodDamage,
            perfectDamage,
            flightRetarget,
            minApproachDelay);
    }

    private void OnMissileFinished(BezierMissile m)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        if (_active == m) _active = null;
    }

    private void ClearActive()
    {
        foreach (var m in FindObjectsOfType<BezierMissile>())
        {
            if (m != null) m.ForceDestroy();
        }
        _active = null;
        _activeCount = 0;
    }

    private Vector3 GetMuzzlePos()
    {
        if (_boss == null) return transform.position;
        var sr = _boss.MainSR;
        if (sr != null && sr.sprite != null)
            return sr.bounds.center + Vector3.down * (sr.bounds.extents.y * 0.35f);
        return transform.position + Vector3.down * 0.4f;
    }

    private bool IsBallTooClose(BallController ball)
    {
        if (ball == null || _boss == null) return false;
        float dist = Vector2.Distance(ball.transform.position, GetMuzzlePos());
        return dist < minFireDistance;
    }

    private void BuildCurve(Vector3 p0, BallController ball, out Vector3 p1, out Vector3 p2, out Vector3 p3, int swingSign, int volleyIndex = 0, int volleyCount = 1)
    {
        Vector2 ballPos = ball.transform.position;
        Vector2 ballVel = ball.Rb != null ? ball.Rb.velocity : Vector2.zero;
        float dist = Vector2.Distance(p0, ballPos);
        bool close = dist < minFireDistance * 1.25f;

        p3 = ballPos + ballVel * predictLead;
        p3.z = 0f;

        float side = sideSwing * swingSign;
        var scale = CurrentScaling;
        if (volleyCount > 1)
            side *= 1f + scale.volleySideBoost + 0.2f * volleyIndex;

        if (close)
        {
            side *= closeBallArcBoost;
            Vector2 away = (ballPos - (Vector2)p0);
            if (away.sqrMagnitude < 0.01f) away = Vector2.down;
            else away.Normalize();
            Vector2 lateral = new Vector2(-away.y, away.x) * swingSign;
            p3 = (Vector3)(ballPos + away * 1.6f + lateral * 1.1f + ballVel * (predictLead * 0.5f));
            p3.z = 0f;
        }

        p1 = p0 + new Vector3(side, -0.8f, 0f);
        Vector3 mid = (p0 + p3) * 0.5f;
        p2 = mid + new Vector3(-side * 0.65f, Mathf.Min(0.5f, (p0.y - p3.y) * 0.15f), 0f);
    }

    private void EnsurePreview()
    {
        if (_dashRoot == null)
        {
            var root = new GameObject("MissileTelegraphDashes");
            root.transform.SetParent(transform, false);
            _dashRoot = root.transform;
        }

        int need = Mathf.Max(4, dashCount);
        while (_dashSegs.Count < need)
        {
            var go = new GameObject($"Dash_{_dashSegs.Count}");
            go.transform.SetParent(_dashRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.035f;
            lr.numCapVertices = 2;
            lr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
            lr.sortingOrder = 20;
            lr.enabled = false;
            _dashSegs.Add(lr);
        }

        if (_edgeArrow == null)
        {
            var arrowGo = new GameObject("MissileEdgeArrow");
            arrowGo.transform.SetParent(transform, false);
            _edgeArrow = arrowGo.AddComponent<SpriteRenderer>();
            _edgeArrow.sprite = CreateArrowSprite();
            _edgeArrow.material = CyberVisualFactory.UnlitMaterial;
            _edgeArrow.sortingOrder = 25;
            _edgeArrow.enabled = false;
        }

        if (_lockMark == null)
        {
            var lockGo = new GameObject("MissileLockMark");
            lockGo.transform.SetParent(transform, false);
            _lockMark = lockGo.AddComponent<SpriteRenderer>();
            _lockMark.sprite = CreateLockSprite();
            _lockMark.material = CyberVisualFactory.UnlitMaterial;
            _lockMark.sortingOrder = 24;
            _lockMark.enabled = false;
        }

        EnsureWarnBangs(Mathf.Max(1, CurrentScaling.missilesPerVolley));

        // 球锁定框暂禁用（有 bug）；清理场景残留
        if (_ballLock != null)
        {
            Destroy(_ballLock.gameObject);
            _ballLock = null;
        }
        var orphan = GameObject.Find("MissileBallLockOn");
        if (orphan != null) Destroy(orphan);
        var orphanBang = GameObject.Find("MissileWarnBang");
        if (orphanBang != null && orphanBang.GetComponent<SpriteRenderer>() != null
            && !_warnBangs.Contains(orphanBang.GetComponent<SpriteRenderer>()))
            Destroy(orphanBang);
    }

    private void EnsureWarnBangs(int count)
    {
        count = Mathf.Clamp(count, 1, 3);
        while (_warnBangs.Count < count)
        {
            var bangGo = new GameObject($"MissileWarnBang_{_warnBangs.Count}");
            bangGo.transform.SetParent(transform, false);
            var sr = bangGo.AddComponent<SpriteRenderer>();
            sr.sprite = CreateBangSprite();
            sr.material = CyberVisualFactory.UnlitMaterial;
            sr.color = new Color(2.6f, 0.85f, 0.15f, 1f);
            sr.sortingOrder = 26;
            sr.enabled = false;
            _warnBangs.Add(sr);
        }
    }

    private void SetWarnBangEnabled(int index, bool on)
    {
        if (index < 0 || index >= _warnBangs.Count) return;
        if (_warnBangs[index] != null)
            _warnBangs[index].enabled = on;
    }

    private void HideAllWarnBangs()
    {
        for (int i = 0; i < _warnBangs.Count; i++)
        {
            if (_warnBangs[i] != null)
                _warnBangs[i].enabled = false;
        }
    }

    private Vector3 GetWarnBangWorldPos(int index, int count)
    {
        Vector3 center = _boss != null ? _boss.transform.position : transform.position;
        // 上半圈扇形：左 → 顶 → 右，拉开不与 Boss 重叠
        float t = count <= 1 ? 0.5f : index / (float)(count - 1);
        float angDeg = Mathf.Lerp(145f, 35f, t);
        float rad = angDeg * Mathf.Deg2Rad;
        float r = Mathf.Max(0.85f, bangOrbitRadius);
        return center + new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);
    }

    private void UpdateWarnBangs(int count, bool warnOn, bool urgent)
    {
        EnsureWarnBangs(count);
        for (int i = 0; i < _warnBangs.Count; i++)
        {
            var bang = _warnBangs[i];
            if (bang == null) continue;
            if (i >= count || !warnOn)
            {
                bang.enabled = false;
                continue;
            }

            bang.enabled = true;
            bang.transform.position = GetWarnBangWorldPos(i, count);
            // 轻微相位差，多号不同步闪，更好读
            float phase = i * 0.7f;
            float pulse = urgent
                ? 1.05f + 0.28f * Mathf.Sin(Time.unscaledTime * 22f + phase)
                : 0.85f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f + phase);
            bang.transform.localScale = Vector3.one * ((urgent ? 0.95f : 0.72f) * pulse);
            bang.color = urgent
                ? new Color(3f, 0.75f, 0.08f, 1f)
                : new Color(2.4f, 0.85f, 0.15f, 0.9f);
        }
    }

    private Sprite ResolveBallLockSprite()
    {
        if (ballLockSprite != null) return ballLockSprite;
        var fromRes = Resources.Load<Sprite>(BallLockResourcePath);
        if (fromRes != null) return fromRes;
#if UNITY_EDITOR
        var fromAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BallLockAssetPath);
        if (fromAsset != null) return fromAsset;
#endif
        return CreateBallLockSprite();
    }

    private void ApplyWarningVisibility(bool on)
    {
        HideDashAndExtras();
        if (!on) HideAllWarnBangs();
        if (_ballLock != null && !on) _ballLock.enabled = false;
    }

    private void UpdateDashedArc(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float crawlSpeed, bool dim, bool solid)
    {
        const int samples = 40;
        var pts = new Vector3[samples];
        for (int i = 0; i < samples; i++)
            pts[i] = BezierMissile.EvalCubic(p0, p1, p2, p3, i / (float)(samples - 1));

        var cum = new float[samples];
        cum[0] = 0f;
        for (int i = 1; i < samples; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        float total = cum[samples - 1];
        if (total < 0.01f)
        {
            for (int i = 0; i < _dashSegs.Count; i++)
                _dashSegs[i].enabled = false;
            return;
        }

        if (solid)
        {
            // 末段实线：把短划接成连续折线观感（多段紧挨）
            int n = Mathf.Min(_dashSegs.Count, samples - 1);
            var col = new Color(2.6f, 0.55f, 0.08f, 1f);
            for (int i = 0; i < n; i++)
            {
                var lr = _dashSegs[i];
                lr.enabled = true;
                lr.SetPosition(0, pts[i]);
                lr.SetPosition(1, pts[i + 1]);
                lr.startColor = col;
                lr.endColor = col;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.06f;
            }
            for (int i = n; i < _dashSegs.Count; i++)
                _dashSegs[i].enabled = false;
            return;
        }

        float period = Mathf.Max(0.05f, dashLen + gapLen);
        float scroll = Time.unscaledTime * crawlSpeed * period;
        int dashIdx = 0;
        float alpha = dim ? 0.38f : 0.95f;
        var baseCol = new Color(2.4f, 0.55f, 0.1f, alpha);
        for (float d0 = -Mathf.Repeat(scroll, period); d0 < total && dashIdx < _dashSegs.Count; d0 += period)
        {
            float a0 = Mathf.Max(0f, d0);
            float a1 = Mathf.Min(total, d0 + dashLen);
            if (a1 <= a0) continue;
            Vector3 a = SampleAlong(pts, cum, a0);
            Vector3 b = SampleAlong(pts, cum, a1);
            var lr = _dashSegs[dashIdx++];
            lr.enabled = true;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startColor = baseCol;
            lr.endColor = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * 0.5f);
            lr.startWidth = dim ? 0.045f : 0.065f;
            lr.endWidth = dim ? 0.028f : 0.038f;
        }

        for (int i = dashIdx; i < _dashSegs.Count; i++)
            _dashSegs[i].enabled = false;
    }

    private static Vector3 SampleAlong(Vector3[] pts, float[] cum, float dist)
    {
        if (dist <= 0f) return pts[0];
        for (int i = 1; i < pts.Length; i++)
        {
            if (cum[i] >= dist)
            {
                float span = cum[i] - cum[i - 1];
                float u = span > 0.0001f ? (dist - cum[i - 1]) / span : 0f;
                return Vector3.Lerp(pts[i - 1], pts[i], u);
            }
        }
        return pts[pts.Length - 1];
    }

    private void UpdateBallLockOn(Vector3 ballPos, bool dim, bool urgent)
    {
        if (_ballLock == null) return;
        _ballLock.enabled = true;
        _ballLock.transform.position = ballPos;
        float baseScale = urgent ? 0.42f : (dim ? 0.34f : 0.38f);
        float pulse = urgent
            ? 1f + 0.1f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f))
            : 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f);
        _ballLock.transform.localScale = Vector3.one * (baseScale * pulse);
        _ballLock.transform.rotation = Quaternion.identity;
        var c = Color.white;
        c.a = dim ? 0.55f : (urgent ? 1f : 0.88f);
        _ballLock.color = c;
    }

    private void SetTelegraphVisible(bool on)
    {
        ApplyWarningVisibility(on);
        if (!on)
        {
            if (_ballLock != null) _ballLock.enabled = false;
            ClearThreatVignette();
        }
    }

    private void OnDestroy()
    {
        if (_ballLock != null)
            Destroy(_ballLock.gameObject);
    }

    private void FlashBossMuzzle()
    {
        if (_boss != null && _boss.MainSR != null)
            StartCoroutine(MuzzleFlashRoutine(_boss.MainSR));
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    private static IEnumerator MuzzleFlashRoutine(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color orig = sr.color;
        sr.color = Color.Lerp(orig, new Color(2f, 0.6f, 0.2f, 1f), 0.7f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (sr != null) sr.color = orig;
    }

    private static Sprite CreateArrowSprite()
    {
        const int sz = 32;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[sz * sz];
        float half = sz * 0.5f;
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float dx = (x - half + 0.5f) / half;
            float dy = (y - half + 0.5f) / half;
            bool inside = dy > -0.55f && dy < 0.75f && Mathf.Abs(dx) < (0.75f - dy) * 0.7f;
            px[y * sz + x] = inside ? Color.white : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.35f), sz / 1.2f);
    }

    private static Sprite CreateLockSprite()
    {
        const int sz = 48;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[sz * sz];
        float half = sz * 0.5f;
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float dx = (x - half + 0.5f) / half;
            float dy = (y - half + 0.5f) / half;
            float adx = Mathf.Abs(dx);
            float ady = Mathf.Abs(dy);
            bool corner =
                (adx > 0.55f && adx < 0.85f && ady > 0.55f && ady < 0.85f) &&
                (adx > 0.68f || ady > 0.68f);
            bool cross = (adx < 0.08f && ady < 0.22f) || (ady < 0.08f && adx < 0.22f);
            px[y * sz + x] = (corner || cross) ? Color.white : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 1.4f);
    }

    /// <summary>加粗警告感叹号。</summary>
    private static Sprite CreateBangSprite()
    {
        const int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[sz * sz];
        float half = sz * 0.5f;
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float dx = (x - half + 0.5f) / half;
            float dy = (y - half + 0.5f) / half;
            bool bar = dy > -0.15f && dy < 0.78f && Mathf.Abs(dx) < 0.16f + (0.72f - dy) * 0.04f;
            float dDot = Mathf.Sqrt(dx * dx + (dy + 0.62f) * (dy + 0.62f));
            bool dot = dDot < 0.18f;
            bool barOutline = dy > -0.2f && dy < 0.82f && Mathf.Abs(dx) < 0.28f && !bar;
            px[y * sz + x] = (bar || dot) ? Color.white
                : barOutline ? new Color(1f, 1f, 1f, 0.25f)
                : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 1.6f);
    }

    /// <summary>球身锁定框：四角括号 + 中心点。</summary>
    private static Sprite CreateBallLockSprite()
    {
        const int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[sz * sz];
        float half = sz * 0.5f;
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float dx = (x - half + 0.5f) / half;
            float dy = (y - half + 0.5f) / half;
            float adx = Mathf.Abs(dx);
            float ady = Mathf.Abs(dy);
            // 四角 L 形
            bool corner =
                adx > 0.42f && adx < 0.88f && ady > 0.42f && ady < 0.88f &&
                (adx > 0.62f || ady > 0.62f);
            // 加粗：邻域
            bool thick =
                adx > 0.38f && adx < 0.92f && ady > 0.38f && ady < 0.92f &&
                (adx > 0.58f || ady > 0.58f) &&
                (adx > 0.42f && ady > 0.42f);
            bool core = adx < 0.08f && ady < 0.08f;
            px[y * sz + x] = (corner || thick || core) ? Color.white : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 1.35f);
    }
}
