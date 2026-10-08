using UnityEngine;

[CreateAssetMenu(menuName = "PinballGame/Scene2 Trial Config")]
public sealed class Scene2Config : ScriptableObject
{
    public BossDefinition boss;
    public MinionDefinition grunt;
    public MinionDefinition armored;
    public MinionDefinition bomber;
    public Font font;
    [Header("Loop experiment (Scene2 only)")]
    public Scene2LoopRule loopRule = Scene2LoopRule.WholeGroup;
    [Min(1)] public int groupSize = 6;
    [Min(1)] public int batchSize = 3;
    [Min(1)] public int aliveCap = 8;
    [Min(0.1f)] public float openingDelay = 2f;
    [Min(0.1f)] public float batchDelay = 4f;
    [Min(0.1f)] public float groupRest = 3f;
    [Header("Formation")]
    [Min(0.5f)] public float spawnHalfWidth = 3.4f;
    [Min(0.1f)] public float spawnVerticalStep = 0.65f;
    [Min(0.1f)] public float weakDuration = 6f;
    [Range(1f, 3f)] public float weakMultiplier = 1.5f;
    public int[] bossHealth = { 20, 28, 36, 44, 52, 60 };
    [Header("Cosmetic full-clear reveal")]
    [Range(0f, 0.6f)] public float weakRevealDuration = 0.35f;

    private void OnValidate()
    {
        groupSize = Mathf.Max(1, groupSize);
        aliveCap = Mathf.Max(1, aliveCap);
        batchSize = Mathf.Clamp(batchSize, 1, Mathf.Min(groupSize, aliveCap));
        openingDelay = Mathf.Max(0.1f, openingDelay);
        batchDelay = Mathf.Max(0.1f, batchDelay);
        groupRest = Mathf.Max(0.1f, groupRest);
        spawnHalfWidth = Mathf.Max(0.5f, spawnHalfWidth);
        spawnVerticalStep = Mathf.Max(0.1f, spawnVerticalStep);
        weakDuration = Mathf.Max(0.1f, weakDuration);
        weakRevealDuration = Mathf.Clamp(weakRevealDuration, 0f, 0.6f);
        weakMultiplier = Mathf.Clamp(weakMultiplier, 1f, 3f);
    }
}
