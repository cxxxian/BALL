using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>独立于死亡父代的裂解延迟；清场和重置会取消全部待出生子代。</summary>
public sealed class Phase6SplitSpawn : MonoBehaviour
{
    private static readonly HashSet<Phase6SplitSpawn> Pending = new HashSet<Phase6SplitSpawn>();

    private MinionDefinition _mini;
    private Vector3 _origin;
    private int _wave;
    private float _delay;

    public static void Schedule(MinionDefinition mini, Vector3 origin, int wave, float delay)
    {
        if (mini == null) return;
        var go = new GameObject("PendingMiniSplit");
        var pending = go.AddComponent<Phase6SplitSpawn>();
        pending._mini = mini;
        pending._origin = origin;
        pending._wave = wave;
        pending._delay = delay;
        Pending.Add(pending);
        pending.StartCoroutine(pending.SpawnAfterDelay());
    }

    public static void CancelAll()
    {
        if (Pending.Count > 0) Phase6EnemyBehaviour.Report($"Cancelled {Pending.Count} pending split(s)");
        foreach (var pending in new List<Phase6SplitSpawn>(Pending))
            if (pending != null) Destroy(pending.gameObject);
        Pending.Clear();
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(_delay);
        if (this == null || !Pending.Remove(this)) yield break;
        if (GameManager.Instance == null || GameManager.Instance.State == GameState.GameOver)
        {
            Destroy(gameObject);
            yield break;
        }

        float y = Mathf.Max(_origin.y, MinionLineRules.GetAttackLineY() + 0.8f);
        for (int i = 0; i < 2; i++)
            Phase6EnemyFactory.Spawn(_mini, new Vector3(_origin.x + (i == 0 ? -0.23f : 0.23f), y, 0f), _wave);
        Phase6EnemyBehaviour.Report("Splitter produced 2 Mini");
        Destroy(gameObject);
    }

    private void OnDestroy() => Pending.Remove(this);
}
