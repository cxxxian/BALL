using UnityEngine;
using UnityEngine.Events;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public int maxHits = 2;
    public float moveSpeed = 0.5f;
    public int scoreOnHit = 10;
    public int scoreOnKill = 50;

    public int CurrentHits { get; protected set; } = 0;
    public bool IsDead { get; protected set; } = false;

    public UnityEvent<EnemyBase> onDeath = new UnityEvent<EnemyBase>();

    protected virtual void Update()
    {
        if (IsDead) return;
        if (GameManager.Instance == null) return;
        var state = GameManager.Instance.State;
        if (state == GameState.GameOver || state == GameState.BuffSelection || state == GameState.Idle) return;
        MoveDown();
    }

    protected virtual void MoveDown()
    {
        transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDead) return;
        if (col.gameObject.CompareTag("Ball"))
        {
            BallController ball = col.gameObject.GetComponent<BallController>();
            if (ball != null && ball.IsInvincible) return;
            TakeHit();
        }
    }

    public virtual void TakeHit()
    {
        if (IsDead) return;
        CurrentHits++;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        OnHit();

        if (CurrentHits >= maxHits)
            Die();
    }

    protected virtual void OnHit() { }

    protected virtual void Die()
    {
        IsDead = true;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnKill);
        onDeath.Invoke(this);
        OnDie();
        Destroy(gameObject);
    }

    protected virtual void OnDie() { }
}
