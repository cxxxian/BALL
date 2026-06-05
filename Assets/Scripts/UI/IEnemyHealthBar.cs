using UnityEngine;

public interface IEnemyHealthBar
{
    void Bind(EnemyBase enemy);
    void OnEnemyHit();
    void OnEnemyDeath();
}
