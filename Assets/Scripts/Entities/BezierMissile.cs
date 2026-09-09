using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 贝塞尔导弹：Flight → Approach（外圈固定 + 内圈收缩弹刀）→ Parried / Miss。
/// Approach 脱战：关圈不算 Miss，可再咬住重开窗口（有次数帽）；Miss 只搅球角度，不扣命。
/// </summary>
public class BezierMissile : MonoBehaviour
{
    public enum State { Flight, Approach, Chase, Parried, Done }

    public event Action<BezierMissile> onFinished;

    /// <summary>任意导弹弹刀成功（教学用）。参数 = Perfect。</summary>
    public static event Action<bool> OnAnyParried;

    /// <summary>弹刀导弹命中 Boss（教学 / 反馈用）。参数 = Perfect。</summary>
    public static event Action<bool> OnAnyParryBossHit;

    public State CurrentState { get; private set; } = State.Flight;

    private Boss _owner;
    private Vector3 _p0, _p1, _p2, _p3;
    private float _flightDuration;
    private float _ringRadius;
    private float _shrinkDuration;
    private int _goodDamage;
    private int _perfectDamage;
    private float _retargetBlend;
    private float _minApproachDelay;

    private float _t;
    private float _shrinkT;
    private float _aliveTime;
    private bool _windowOpen;
    private float _disengageTimer;
    private int _approachWindowsUsed;
    private const float MaxLifetime = 12f;
    private const int MaxApproachWindows = 2;
    private const float DisengageRadiusMul = 1.25f;
    private const float DisengageHold = 0.12f;
    private float _chaseElapsed;
    private float _lastChaseDist = float.MaxValue;

    private LineRenderer _outerRing;
    private LineRenderer _innerRing;
    private LineRenderer _sweetRing;
    private LineRenderer _burstRing;
    private SpriteRenderer _body;
    private TrailRenderer _trail;
    private Coroutine _juiceCo;

    // HDR 金黄，与齿轮黄同一语系
    private static readonly Color MissileColor = new Color(2.8f, 2.1f, 0.4f, 1f);
    private static readonly Color OuterRingColor = new Color(2.2f, 1.55f, 0.35f, 0.9f);
    private static readonly Color InnerRingColor = new Color(2.8f, 2.2f, 0.55f, 0.95f);
    private static readonly Color SweetColor = new Color(0.45f, 2.4f, 1.9f, 0.75f);
    private static readonly Color PerfectFlash = new Color(0.55f, 2.9f, 2.4f, 1f);
    private static readonly Color GoodFlash = new Color(3.0f, 2.35f, 0.55f, 1f);

    public void Launch(
        Boss owner,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        float flightDuration,
        float ringRadius,
        float shrinkDuration,
        int goodDamage,
        int perfectDamage,
        float retargetBlend = 0.35f,
        float minApproachDelay = 1.0f)
    {
        _owner = owner;
        _p0 = p0; _p1 = p1; _p2 = p2; _p3 = p3;
        _flightDuration = Mathf.Max(0.5f, flightDuration);
        _ringRadius = Mathf.Max(0.6f, ringRadius);
        _shrinkDuration = Mathf.Max(0.35f, shrinkDuration);
        _goodDamage = Mathf.Max(1, goodDamage);
        _perfectDamage = Mathf.Max(_goodDamage, perfectDamage);
        _retargetBlend = Mathf.Clamp01(retargetBlend);
        _minApproachDelay = Mathf.Max(0.35f, minApproachDelay);
        _t = 0f;
        _aliveTime = 0f;
        _approachWindowsUsed = 0;
        _disengageTimer = 0f;
        CurrentState = State.Flight;

        BuildVisuals();
        transform.position = p0;
    }

    public void ForceDestroy() => FinishAndDestroy();

    private void Update()
    {
        if (CurrentState == State.Done) return;

        _aliveTime += Time.unscaledDeltaTime;
        if (_aliveTime > MaxLifetime)
        {
            FinishAndDestroy();
            return;
        }

        if (GameManager.Instance != null)
        {
            var gs = GameManager.Instance.State;
            if (gs == GameState.GameOver || gs == GameState.BuffSelection || gs == GameState.Idle)
            {
                FinishAndDestroy();
                return;
            }
        }

        if (_owner == null || _owner.IsDead)
        {
            FinishAndDestroy();
            return;
        }

        switch (CurrentState)
        {
            case State.Flight: TickFlight(); break;
            case State.Approach: TickApproach(); break;
            case State.Chase: TickChase(); break;
            case State.Parried: TickParried(); break;
        }
    }

    private void TickFlight()
    {
        if (_t > 0.45f && _retargetBlend > 0.01f)
        {
            var ball = BallController.Instance;
            if (ball != null && !ball.IsWaitingForLaunch)
            {
                float pull = _retargetBlend * Time.unscaledDeltaTime * 2.0f;
                _p3 = Vector3.Lerp(_p3, ball.transform.position, pull);
            }
        }

        _t += Time.unscaledDeltaTime / _flightDuration;
        float u = EaseInOut(Mathf.Clamp01(_t));
        Vector3 pos = EvalCubic(_p0, _p1, _p2, _p3, u);
        Vector3 deriv = EvalCubicDerivative(_p0, _p1, _p2, _p3, u);
        transform.position = pos;
        FaceDirection(deriv);

        var b = BallController.Instance;
        if (b != null && !b.IsWaitingForLaunch)
        {
            float dist = Vector2.Distance(pos, b.transform.position);
            bool canApproach = _aliveTime >= _minApproachDelay;
            if (canApproach && (dist <= _ringRadius || _t >= 0.92f))
                EnterApproach();
            else if (_t >= 1f && !canApproach)
            {
                // 最短飞行未满：进入追击再咬
                EnterChase();
            }
        }
        else if (_t >= 1f)
        {
            FinishAndDestroy();
        }
    }

    private void EnterApproach()
    {
        if (_approachWindowsUsed >= MaxApproachWindows)
        {
            FinishAndDestroy();
            return;
        }

        _approachWindowsUsed++;
        CurrentState = State.Approach;
        _shrinkT = 0f;
        _windowOpen = true;
        _missExplodePlayed = false;
        _disengageTimer = 0f;
        SetRingsVisible(true);
        if (_burstRing != null) _burstRing.enabled = false;
        UpdateRings(BallController.Instance != null
            ? BallController.Instance.transform.position
            : transform.position, 1f);
    }

    private void EnterChase()
    {
        CurrentState = State.Chase;
        _windowOpen = false;
        _chaseElapsed = 0f;
        _lastChaseDist = float.MaxValue;
        _disengageTimer = 0f;
        SetRingsVisible(false);
        if (_burstRing != null) _burstRing.enabled = false;
    }

    private void CancelApproachToChase()
    {
        // 脱战：关圈不算 Miss
        _windowOpen = false;
        SetRingsVisible(false);
        if (_burstRing != null) _burstRing.enabled = false;
        _missExplodePlayed = false;

        if (_approachWindowsUsed >= MaxApproachWindows)
        {
            FinishAndDestroy();
            return;
        }

        EnterChase();
    }

    private void TickApproach()
    {
        if (_finishing) return;

        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch)
        {
            // 球不可用：安静取消，不搅角
            FinishAndDestroy();
            return;
        }

        Vector3 ballPos = ball.transform.position;
        ChaseToward(ballPos, ball);
        FaceDirection(ballPos - transform.position);

        float dist = Vector2.Distance(transform.position, ballPos);
        float exitR = _ringRadius * DisengageRadiusMul;
        if (dist > exitR)
        {
            _disengageTimer += Time.unscaledDeltaTime;
            if (_disengageTimer >= DisengageHold)
            {
                CancelApproachToChase();
                return;
            }
        }
        else
        {
            _disengageTimer = 0f;
        }

        float remain = 1f - Mathf.Clamp01(_shrinkT);

        // 接战内才收圈；滞后区内圈暂停但仍可弹刀
        if (dist <= _ringRadius)
        {
            _shrinkT += Time.unscaledDeltaTime / _shrinkDuration;
            remain = 1f - Mathf.Clamp01(_shrinkT);
        }

        UpdateRings(ballPos, remain);

        if (_windowOpen && TryConsumeParryInput())
        {
            ResolveParry(remain);
            return;
        }

        float anticipate = AudioManager.Instance != null ? AudioManager.Instance.ExplodeAnticipate : 0.06f;
        if (dist <= _ringRadius &&
            !_missExplodePlayed &&
            _shrinkT >= 1f - anticipate / Mathf.Max(0.01f, _shrinkDuration))
        {
            _missExplodePlayed = true;
            AudioManager.Instance?.PlayExplode(0.92f);
        }

        if (_shrinkT >= 1f && dist <= _ringRadius)
            ApplyMiss(ball);
    }

    private void TickChase()
    {
        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch)
        {
            FinishAndDestroy();
            return;
        }

        _chaseElapsed += Time.unscaledDeltaTime;

        Vector3 ballPos = ball.transform.position;
        ChaseToward(ballPos, ball);
        FaceDirection(ballPos - transform.position);

        float dist = Vector2.Distance(transform.position, ballPos);
        if (dist < _lastChaseDist - 0.02f)
            _chaseElapsed = 0f; // 有在逼近则不计时，避免追不上被误杀
        _lastChaseDist = dist;

        if (dist <= _ringRadius && _approachWindowsUsed < MaxApproachWindows)
            EnterApproach();
    }

    private void ChaseToward(Vector3 ballPos, BallController ball)
    {
        float ballSpeed = ball.Rb != null ? ball.Rb.velocity.magnitude : 0f;
        float dist = Vector2.Distance(transform.position, ballPos);

        // 脱节时贴近球速追赶；近身时减速便于重新开 Approach 窗
        float chase;
        if (dist > _ringRadius * 1.8f)
            chase = Mathf.Clamp(ballSpeed * 1.02f + 2.5f, 6f, 17f);
        else if (dist > _ringRadius)
            chase = Mathf.Clamp(ballSpeed * 0.55f + 4f, 5f, 13f);
        else
            chase = Mathf.Clamp(ballSpeed * 0.3f + 3f, 3f, 9f);

        float step = chase * Time.unscaledDeltaTime;
        transform.position = Vector3.MoveTowards(transform.position, ballPos, step);
    }

    private void ResolveParry(float innerRemain01)
    {
        _windowOpen = false;
        // 回到「内圈剩余比例」策略：15%–40% Perfect，其余窗内 Good
        bool perfect = innerRemain01 >= 0.15f && innerRemain01 <= 0.40f;
        int dmg = perfect ? _perfectDamage : _goodDamage;

        Vector3 start = transform.position;
        Vector3 ballPos = BallController.Instance != null
            ? BallController.Instance.transform.position
            : start;

        if (_juiceCo != null) StopCoroutine(_juiceCo);
        _juiceCo = StartCoroutine(ParryJuice(perfect, ballPos));

        OnAnyParried?.Invoke(perfect);

        Vector3 end = GetBossHitPos();
        Vector3 mid = (start + end) * 0.5f;
        Vector3 side = Vector3.Cross((end - start).normalized, Vector3.forward);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side = side.normalized * (perfect ? 0.25f : 0.7f);
        _p0 = start;
        _p1 = mid + side;
        _p2 = mid - side * 0.35f;
        _p3 = end;
        _t = 0f;
        _flightDuration = perfect ? 0.42f : 0.58f;
        CurrentState = State.Parried;

        if (_body != null)
            _body.color = perfect ? PerfectFlash : GoodFlash;
        if (_trail != null)
        {
            _trail.startColor = perfect ? PerfectFlash : GoodFlash;
            _trail.endColor = new Color(GoodFlash.r, GoodFlash.g, GoodFlash.b, 0f);
            _trail.time = perfect ? 0.4f : 0.28f;
        }

        ComboSystem.Instance?.RegisterAirtimeHit(start);
        _pendingDamage = dmg;
        _pendingPerfect = perfect;
        _hitExplodePlayed = false;
        AudioManager.Instance?.PlayRebound(perfect ? 1.06f : 1f);
    }

    private int _pendingDamage;
    private bool _pendingPerfect;
    private bool _missExplodePlayed;
    private bool _hitExplodePlayed;
    private bool _finishing;

    private IEnumerator ParryJuice(bool perfect, Vector3 at)
    {
        SetRingsVisible(false);

        // 仅反馈：圈爆 + 顿帧 + 震屏；粒子等到打中 Boss 再出
        if (_burstRing != null)
        {
            _burstRing.enabled = true;
            float burstT = 0f;
            float startR = _ringRadius * 0.25f;
            float endR = _ringRadius * (perfect ? 1.55f : 1.2f);
            while (burstT < 0.16f)
            {
                burstT += Time.unscaledDeltaTime;
                float u = burstT / 0.16f;
                float r = Mathf.Lerp(startR, endR, u);
                var c = perfect ? PerfectFlash : GoodFlash;
                c.a = 1f - u;
                _burstRing.startColor = c;
                _burstRing.endColor = c;
                _burstRing.startWidth = Mathf.Lerp(0.14f, 0.02f, u);
                _burstRing.endWidth = _burstRing.startWidth;
                DrawCircle(_burstRing, at, r);
                yield return null;
            }
            _burstRing.enabled = false;
        }

        CameraShake.Instance?.Shake(perfect ? CameraShake.Preset.Heavy : CameraShake.Preset.Medium);
        SlowMoFX.Instance?.PulseFlash(
            perfect ? PerfectFlash : GoodFlash,
            perfect ? 0.55f : 0.32f,
            perfect ? 0.14f : 0.08f);

        // 顿帧：统一走 HitStop（与瞄准/教程态仲裁）
        HitStop.PulseParry(perfect);
        float stopDur = perfect ? 0.13f : 0.07f;
        float w = 0f;
        while (w < stopDur && CurrentState != State.Done)
        {
            w += Time.unscaledDeltaTime;
            yield return null;
        }

        if (CurrentState == State.Done) yield break;

        if (_body != null)
        {
            _body.color = perfect ? PerfectFlash : GoodFlash;
            transform.localScale = Vector3.one * (perfect ? 0.55f : 0.48f);
        }
        if (_trail != null)
        {
            _trail.startColor = perfect ? PerfectFlash : GoodFlash;
            _trail.time = perfect ? 0.45f : 0.3f;
        }
    }

    private void TickParried()
    {
        if (_owner != null && !_owner.IsDead)
            _p3 = Vector3.Lerp(_p3, GetBossHitPos(), Time.unscaledDeltaTime * 4f);

        _t += Time.unscaledDeltaTime / _flightDuration;
        float u = EaseInOut(Mathf.Clamp01(_t));
        Vector3 pos = EvalCubic(_p0, _p1, _p2, _p3, u);
        Vector3 deriv = EvalCubicDerivative(_p0, _p1, _p2, _p3, u);
        transform.position = pos;
        FaceDirection(deriv);

        float anticipate = AudioManager.Instance != null ? AudioManager.Instance.ExplodeAnticipate : 0.06f;
        float anticipateT = anticipate / Mathf.Max(0.01f, _flightDuration);
        if (!_hitExplodePlayed && _t >= 1f - anticipateT)
        {
            _hitExplodePlayed = true;
            AudioManager.Instance?.PlayExplode(_pendingPerfect ? 1.08f : 1f);
        }

        if (_t < 1f) return;

        if (_owner != null && !_owner.IsDead)
        {
            Vector2 hitPos = pos;
            _owner.TakeHit(_pendingDamage, isFromBall: true, hitPos: hitPos);
            ImpactFX.Instance?.SpawnMissileShatter(
                hitPos,
                _pendingPerfect ? PerfectFlash : GoodFlash,
                _pendingPerfect ? 1.35f : 1.05f);
            ImpactFX.Instance?.SpawnHit(hitPos, _pendingPerfect ? PerfectFlash : GoodFlash, _pendingPerfect ? 1.7f : 1.3f);
            if (!_hitExplodePlayed)
                AudioManager.Instance?.PlayExplode(_pendingPerfect ? 1.08f : 1f);
            CameraShake.Instance?.Shake(_pendingPerfect ? CameraShake.Preset.Heavy : CameraShake.Preset.Medium);
            if (_pendingPerfect)
                SlowMoFX.Instance?.PulseFlash(PerfectFlash, 0.45f, 0.12f);
            OnAnyParryBossHit?.Invoke(_pendingPerfect);
        }
        FinishAndDestroy();
    }

    private void ApplyMiss(BallController ball)
    {
        if (_finishing) return;
        _finishing = true;
        _windowOpen = false;
        SetRingsVisible(false);

        Vector3 ballPos = ball != null ? ball.transform.position : transform.position;
        if (ball != null)
        {
            ball.ApplyMissileGlitch(15f);
            ball.PulseParryFlash(new Color(2f, 0.25f, 0.15f, 1f), 0.16f);
            ImpactFX.Instance?.SpawnMissileShatter(ballPos, new Color(2.2f, 0.35f, 0.08f, 1f), 1.2f);
            ImpactFX.Instance?.SpawnHit(ballPos, new Color(1.6f, 0.15f, 0.08f, 1f), 1.05f);
        }

        if (_body != null) _body.enabled = false;
        if (_trail != null) _trail.emitting = false;

        if (!_missExplodePlayed)
            AudioManager.Instance?.PlayExplode(0.92f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        SlowMoFX.Instance?.PulseFlash(new Color(1f, 0.12f, 0.05f), 0.35f, 0.08f);
        FinishAndDestroy();
    }

    private bool TryConsumeParryInput()
    {
        if (SkillManager.Instance != null &&
            (SkillManager.Instance.IsAiming || SkillManager.Instance.IsGroundAiming))
            return false;

        if (InputManager.Instance != null && InputManager.Instance.ParryPressed)
            return true;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) return true;
#endif
        if (Input.GetKeyDown(KeyCode.F)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    private Vector3 GetBossHitPos()
    {
        if (_owner == null) return _p0;
        var sr = _owner.MainSR;
        if (sr != null && sr.sprite != null)
            return sr.bounds.center;
        return _owner.transform.position;
    }

    private void BuildVisuals()
    {
        _body = gameObject.AddComponent<SpriteRenderer>();
        _body.sprite = CyberVisualFactory.CreateMissileDiamondSprite();
        _body.material = CyberVisualFactory.UnlitMaterial;
        _body.color = MissileColor;
        _body.sortingOrder = 22;
        transform.localScale = Vector3.one * 0.42f;

        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time = 0.28f;
        _trail.startWidth = 0.12f;
        _trail.endWidth = 0.02f;
        _trail.sharedMaterial = CyberVisualFactory.UnlitMaterial;
        _trail.startColor = new Color(MissileColor.r, MissileColor.g, MissileColor.b, 0.85f);
        _trail.endColor = new Color(MissileColor.r, MissileColor.g, MissileColor.b, 0f);
        _trail.sortingOrder = 21;
        _trail.minVertexDistance = 0.05f;

        _outerRing = CreateRing("ParryOuter", OuterRingColor, 0.07f, 28);
        _innerRing = CreateRing("ParryInner", InnerRingColor, 0.055f, 28);
        _sweetRing = CreateRing("ParrySweet", SweetColor, 0.04f, 24);
        _burstRing = CreateRing("ParryBurst", PerfectFlash, 0.1f, 32);
        _burstRing.enabled = false;
        SetRingsVisible(false);
    }

    private LineRenderer CreateRing(string name, Color col, float width, int segments)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = segments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
        lr.startColor = col;
        lr.endColor = col;
        lr.sortingOrder = 23;
        lr.enabled = false;
        return lr;
    }

    private void SetRingsVisible(bool on)
    {
        if (_outerRing != null) _outerRing.enabled = on;
        if (_innerRing != null) _innerRing.enabled = on;
        if (_sweetRing != null) _sweetRing.enabled = on;
    }

    private void UpdateRings(Vector3 ballPos, float remain)
    {
        float outerR = _ringRadius * 0.95f;
        float innerR = outerR * remain;
        float sweetR = outerR * 0.275f;

        DrawCircle(_outerRing, ballPos, outerR);
        DrawCircle(_innerRing, ballPos, Mathf.Max(0.05f, innerR));
        DrawCircle(_sweetRing, ballPos, sweetR);

        bool inSweet = remain >= 0.15f && remain <= 0.40f;
        if (_sweetRing != null)
        {
            var c = SweetColor;
            c.a = inSweet ? 0.95f : 0.3f;
            _sweetRing.startColor = c;
            _sweetRing.endColor = c;
            float w = inSweet ? 0.07f : 0.035f;
            _sweetRing.startWidth = w;
            _sweetRing.endWidth = w;
        }

        if (_innerRing != null)
        {
            var ic = inSweet ? PerfectFlash : InnerRingColor;
            ic.a = inSweet ? 1f : 0.85f;
            _innerRing.startColor = ic;
            _innerRing.endColor = ic;
        }
    }

    private static void DrawCircle(LineRenderer lr, Vector3 center, float radius)
    {
        if (lr == null || !lr.enabled) return;
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
    }

    private void FinishAndDestroy()
    {
        if (CurrentState == State.Done) return;
        CurrentState = State.Done;
        if (_juiceCo != null)
        {
            StopCoroutine(_juiceCo);
            _juiceCo = null;
        }
        onFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static Vector3 EvalCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0
             + 3f * u * u * t * p1
             + 3f * u * t * t * p2
             + t * t * t * p3;
    }

    public static Vector3 EvalCubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return 3f * u * u * (p1 - p0)
             + 6f * u * t * (p2 - p1)
             + 3f * t * t * (p3 - p2);
    }
}
