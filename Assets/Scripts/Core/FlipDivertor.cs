using System.Collections;
using UnityEngine;

/// <summary>
/// 翻转分流闸：球从入口吸入后，沿短轨送到当前出口再弹出。
/// 用左右挡板键（或 Z/X）切换出口 —— 玩家主动选路，不是瞬移。
/// </summary>
public class FlipDivertor : MonoBehaviour
{
    [Header("Ports")]
    public Transform intake;
    public Transform exitLeft;
    public Transform exitRight;

    [Header("Launch")]
    public Vector2 launchDirLeft = new Vector2(-0.55f, 1f);
    public Vector2 launchDirRight = new Vector2(0.55f, 1f);
    public float exitSpeed = 12.5f;
    public float transitDuration = 0.22f;
    public float minEntrySpeed = 4.5f;
    public float cooldown = 1.8f;
    public int scoreOnRoute = 45;

    [Header("Visual")]
    public Color activeColor = new Color(2f, 1.1f, 0.2f, 1f);
    public Color idleColor = new Color(0.8f, 0.45f, 0.1f, 0.7f);

    private bool _useRight = true;
    private bool _busy;
    private float _cooldownLeft;
    private LineRenderer _rail;
    private SpriteRenderer _indicator;

    public bool CanAccept => !_busy && _cooldownLeft <= 0f;

    private void Awake()
    {
        if (intake == null) intake = transform.Find("Intake");
        if (exitLeft == null) exitLeft = transform.Find("Exit_L");
        if (exitRight == null) exitRight = transform.Find("Exit_R");
        EnsureRail();
        EnsureIndicator();
        EnsureIntakeZone();
        RefreshVisuals();
    }

    private void Update()
    {
        if (_cooldownLeft > 0f)
            _cooldownLeft -= Time.deltaTime;

        // 瞄准/教学时不抢输入；挡板键切换分流
        if (EnergyCannon.IsPlayerAiming) return;
        if (TutorialInputGate.BlocksGameplay) return;

        bool left = Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.RightArrow);
        if (InputManager.Instance != null)
        {
            // 按下瞬间切换：用 GetKeyDown 对挡板不完美，额外读键盘
        }
        if (left) SetExit(false);
        if (right) SetExit(true);
    }

    public void SetExit(bool right)
    {
        if (_useRight == right) return;
        _useRight = right;
        RefreshVisuals();
        JuiceRouter.Play(JuiceRouter.Tier.Tap,
            intake != null ? (Vector2)intake.position : (Vector2)transform.position,
            activeColor, 0.6f);
    }

    internal void HandleIntake(Collider2D other)
    {
        if (!CanAccept) return;
        if (!other.CompareTag("Ball")) return;
        var ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsWaitingForLaunch) return;
        if (ball.Rb == null || ball.Rb.velocity.sqrMagnitude < minEntrySpeed * minEntrySpeed)
            return;
        StartCoroutine(RouteRoutine(ball));
    }

    private IEnumerator RouteRoutine(BallController ball)
    {
        _busy = true;
        var rb = ball.Rb;
        var col = ball.GetComponent<Collider2D>();
        Vector3 savedScale = ball.transform.localScale;

        Transform exit = _useRight ? exitRight : exitLeft;
        Vector2 launchDir = (_useRight ? launchDirRight : launchDirLeft).normalized;
        Vector2 from = intake != null ? (Vector2)intake.position : (Vector2)transform.position;
        Vector2 to = exit != null ? (Vector2)exit.position : from + launchDir * 2.5f;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;

        ComboSystem.Instance?.RegisterAirtimeHit(from);
        GameManager.Instance?.AddScore(scoreOnRoute);
        AudioManager.Instance?.PlayBounce();

        float t = 0f;
        while (t < transitDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / transitDuration);
            ball.transform.position = Vector2.Lerp(from, to, u);
            ball.transform.localScale = Vector3.Lerp(savedScale, savedScale * 0.7f, Mathf.Sin(u * Mathf.PI) * 0.35f);
            yield return null;
        }

        ball.transform.position = to;
        ball.transform.localScale = savedScale;
        if (col != null) col.enabled = true;
        rb.isKinematic = false;
        rb.velocity = launchDir * exitSpeed;

        JuiceRouter.Play(JuiceRouter.Tier.Hit, to, activeColor);
        _cooldownLeft = cooldown;
        _busy = false;
        RefreshVisuals();
    }

    private void EnsureIntakeZone()
    {
        if (intake == null) return;
        var zone = intake.GetComponent<DivertorIntakeZone>();
        if (zone == null) zone = intake.gameObject.AddComponent<DivertorIntakeZone>();
        zone.owner = this;
    }

    private void EnsureRail()
    {
        _rail = GetComponent<LineRenderer>();
        if (_rail == null) _rail = gameObject.AddComponent<LineRenderer>();
        _rail.positionCount = 3;
        _rail.startWidth = 0.06f;
        _rail.endWidth = 0.06f;
        _rail.material = new Material(Shader.Find("Sprites/Default"));
        _rail.sortingOrder = 3;
        _rail.useWorldSpace = true;
    }

    private void EnsureIndicator()
    {
        var t = transform.Find("Indicator");
        if (t == null)
        {
            var go = new GameObject("Indicator");
            go.transform.SetParent(transform, false);
            t = go.transform;
            _indicator = go.AddComponent<SpriteRenderer>();
        }
        else _indicator = t.GetComponent<SpriteRenderer>();

        if (_indicator != null)
        {
            _indicator.sprite = CyberVisualFactory.CreateCannonSprite(activeColor);
            _indicator.material = CyberVisualFactory.UnlitMaterial;
            _indicator.sortingOrder = 6;
            t.localScale = Vector3.one * 0.55f;
            if (intake != null)
                t.position = intake.position + Vector3.up * 0.55f;
        }
    }

    private void RefreshVisuals()
    {
        if (_rail != null && intake != null && exitLeft != null && exitRight != null)
        {
            _rail.SetPosition(0, exitLeft.position);
            _rail.SetPosition(1, intake.position);
            _rail.SetPosition(2, exitRight.position);
            Color c = _useRight ? activeColor : idleColor;
            Color other = _useRight ? idleColor : activeColor;
            // LineRenderer 只有两端色，用混合表达
            _rail.startColor = other;
            _rail.endColor = c;
        }

        if (_indicator != null)
        {
            _indicator.color = activeColor;
            Vector2 dir = _useRight ? launchDirRight : launchDirLeft;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _indicator.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }
    }
}

public class DivertorIntakeZone : MonoBehaviour
{
    [HideInInspector] public FlipDivertor owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.HandleIntake(other);
    }
}
