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
        _rb.rotation = _restAngle;
        _prevAngle   = _restAngle;
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
        var rb = col.rigidbody;
        if (rb == null) return;

        // 如果挡板是在向下归位或静止状态，完全由完美的物理材质进行精确反射（100% 反射角守恒）
        // 只有当挡板在“向上挥击（Active Kick）”时，才给予弹珠额外的主动推送增量，而非覆盖速度
        bool isActivating = side == FlipperSide.Left
            ? _angularVelocity > 200f
            : _angularVelocity < -200f;

        if (isActivating)
        {
            ContactPoint2D contact = col.GetContact(0);
            Vector2 r       = (Vector2)contact.point - (Vector2)transform.position;
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

        // 强力限速锁：防止物理累加导致球速无限膨胀
        if (rb.velocity.magnitude > config.ballMaxSpeed)
        {
            rb.velocity = rb.velocity.normalized * config.ballMaxSpeed;
        }
    }
}
