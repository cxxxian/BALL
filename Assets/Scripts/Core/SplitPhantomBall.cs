using UnityEngine;

/// <summary>分裂幻影球：可撞敌 / 墙 / Bumper / 挡板；触底自毁不掉命。</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class SplitPhantomBall : MonoBehaviour
{
    public CircleCollider2D Col { get; private set; }
    public Rigidbody2D Rb { get; private set; }

    private GameConfig _config;
    private bool _alive = true;

    public void Init(CircleCollider2D col, Rigidbody2D rb, GameConfig config)
    {
        Col = col;
        Rb = rb;
        _config = config;
    }

    private void FixedUpdate()
    {
        if (!_alive || Rb == null) return;

        float min = _config != null ? _config.ballMinSpeed : 5f;
        float max = _config != null ? _config.ballHardMaxSpeed : 19f;
        float speed = Rb.velocity.magnitude;
        if (speed < 0.05f) return;
        if (speed < min)
            Rb.velocity = Rb.velocity.normalized * min;
        else if (speed > max)
            Rb.velocity = Rb.velocity.normalized * max;

        float fallY = _config != null ? _config.ballFallLineY : -7.65f;
        if (transform.position.y <= fallY)
            Despawn();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!_alive) return;

        if (col.gameObject.GetComponentInParent<Bumper>() != null) return;
        if (col.gameObject.GetComponentInParent<Slingshot>() != null) return;
        if (col.gameObject.GetComponentInParent<EnemyBase>() != null) return;

        AudioManager.Instance?.PlayBounce();
    }

    public void Despawn()
    {
        if (!_alive) return;
        _alive = false;
        SplitProtocol.Instance?.NotifyPhantomDestroyed(this);
        Destroy(gameObject);
    }
}
