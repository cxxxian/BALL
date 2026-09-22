using UnityEngine;

public enum FlipperSide { Left, Right }

[RequireComponent(typeof(Rigidbody2D))]
public class FlipperController : MonoBehaviour
{
    [Header("Settings")]
    public FlipperSide side;
    public GameConfig config;

    private float _restAngle;
    private float _activatedAngle;
    private float _targetAngle;
    private bool _isActivated = false;
    public  bool IsActivated  => _isActivated;
    private Rigidbody2D _rb;

    private float _prevAngle;
    private float _angularVelocity;   // 度/秒，正=逆时针

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Kinematic 使用 Speculative Continuous 阻绝高速旋转穿模
        _rb.constraints = RigidbodyConstraints2D.FreezePosition;
    }

    // 底部挡板区整体上移量（整组刚体一次平移，保持相对位置）。
    public const float HudClearanceY = 0.55f;

    private static bool _bottomClusterLifted;

    private static readonly string[] BottomClusterRoots =
    {
        "Flipper_Left",
        "Flipper_Right",
        "Flipper_Left_Mount",
        "Flipper_Right_Mount",
        "Slingshot_Left",
        "Slingshot_Right",
        "RailProto_Left",
        "RailProto_Right",
    };

    private void Start()
    {
        if (side == FlipperSide.Left)
        {
            _restAngle      = config.flipperRestAngle;
            _activatedAngle = config.flipperActivatedAngle;
        }
        else
        {
            _restAngle      = -config.flipperRestAngle;
            _activatedAngle = -config.flipperActivatedAngle;
        }
        _targetAngle = _restAngle;
        _prevAngle   = _restAngle;
        // 先整组抬高，再摆静止角（避免只抬挡板、弹射器/侧轨留在原地）
        LiftBottomClusterOnce();
        SnapRestPose();
    }

    private static void LiftBottomClusterOnce()
    {
        if (_bottomClusterLifted) return;
        _bottomClusterLifted = true;
        if (HudClearanceY <= 0.001f) return;

        var delta = Vector3.up * HudClearanceY;
        for (int i = 0; i < BottomClusterRoots.Length; i++)
        {
            var go = GameObject.Find(BottomClusterRoots[i]);
            if (go == null) continue;

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                var interp = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation2D.None;
                rb.position += (Vector2)delta;
                go.transform.position = rb.position;
                rb.interpolation = interp;
            }
            else
            {
                go.transform.position += delta;
            }
        }
    }

    private void SnapRestPose()
    {
        // 位置已由整组抬高完成，这里只对齐静止角与底座 Y
        Vector3 pos = transform.position;
        var rot = Quaternion.Euler(0f, 0f, _restAngle);
        var interp = _rb.interpolation;
        _rb.interpolation = RigidbodyInterpolation2D.None;
        _rb.position = pos;
        _rb.rotation = _restAngle;
        transform.SetPositionAndRotation(pos, rot);
        _rb.interpolation = interp;
        SyncMountPivotY();
    }

    /// <summary>旋钮（Mount）与挡板铰点同 Y；避免跨场景 static 导致只抬挡板不抬底座。</summary>
    private void SyncMountPivotY()
    {
        string mountName = side == FlipperSide.Left ? "Flipper_Left_Mount" : "Flipper_Right_Mount";
        var mount = GameObject.Find(mountName)?.transform;
        if (mount == null) return;
        var p = mount.position;
        p.y = transform.position.y;
        mount.position = p;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        var s = GameManager.Instance.State;
        if (s == GameState.Idle || s == GameState.GameOver || s == GameState.BuffSelection) return;
        if (InputManager.Instance == null) return;

        bool pressed = side == FlipperSide.Left
            ? InputManager.Instance.LeftFlipperPressed
            : InputManager.Instance.RightFlipperPressed;

        if (pressed && !_isActivated) { _isActivated = true;  _targetAngle = _activatedAngle; }
        else if (!pressed && _isActivated) { _isActivated = false; _targetAngle = _restAngle; }
    }

    private void FixedUpdate()
    {
        float current = _rb.rotation;
        while (current >  180f) current -= 360f;
        while (current < -180f) current += 360f;

        // 上弹用激活时长，落回用返回时长
        float range    = Mathf.Abs(_activatedAngle - _restAngle);
        float duration = _isActivated ? config.flipperActivateDuration : config.flipperReturnDuration;
        float maxStep  = range / duration * Time.fixedDeltaTime;

        float next = Mathf.MoveTowards(current, _targetAngle, maxStep);
        _rb.MoveRotation(next);

        // 记录角速度（供碰撞加速使用）
        _angularVelocity = (next - _prevAngle) / Time.fixedDeltaTime;
        _prevAngle = next;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;

        ComboSystem.Instance?.BreakOnFlipper();

        var rb = col.rigidbody;
        if (rb == null) return;

        var fx = GetComponent<FlipperFX>();
        // 任意触球：仅颜色高亮（无外形缩放）
        fx?.TriggerContactFlash();

        // 向上挥击：给球额外冲量 + Perfect Catch juice
        bool isActivating = side == FlipperSide.Left
            ? _angularVelocity > 100f
            : _angularVelocity < -100f;

        if (isActivating)
        {
            ContactPoint2D contact = col.GetContact(0);
            Vector2 hitPos = contact.point;
            JuiceRouter.FlipperPerfectCatch(hitPos, fx);

            Vector2 r       = hitPos - (Vector2)transform.position;
            float omegaRad  = _angularVelocity * Mathf.Deg2Rad;
            Vector2 surfVel = new Vector2(-r.y, r.x) * omegaRad;

            // 提取法线分量，给予额外的冲量加速
            Vector2 normal = contact.normal;
            float pushComponent = Vector2.Dot(surfVel, -normal); // 负号因为 normal 是指向球的
            
            if (pushComponent > 0.1f)
            {
                // 用 AddForce 注入一个瞬时的运动学推力
                Vector2 boostForce = -normal * pushComponent * config.flipperBoostFactor;
                rb.AddForce(boostForce, ForceMode2D.Impulse);
            }
        }

        // 挡板武器：按住挡板触球即可（不要求峰值角速度，避免顶住接球时发不出）
        if (_isActivated)
        {
            ContactPoint2D contact = col.GetContact(0);
            Vector2 hitPos = contact.point;
            var weaponCtrl = FlipperWeaponController.Instance ?? FlipperWeaponController.EnsureInstance();
            weaponCtrl.TryFireOnPerfectFlip(side, hitPos);
        }

        // 强力限速锁：与球的有效硬顶一致
        float maxSpeed = config.ballMaxSpeed;
        var ball = col.gameObject.GetComponent<BallController>();
        if (ball != null)
            maxSpeed = ball.EffectiveMaxSpeed;
        else if (DebuffManager.Instance != null)
            maxSpeed *= DebuffManager.Instance.BallMaxSpeedMultiplier;
        if (config.ballHardMaxSpeed > 0.1f)
            maxSpeed = Mathf.Min(maxSpeed, config.ballHardMaxSpeed);
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;
    }
}
