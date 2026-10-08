using System.Collections;
using System.Reflection;
using UnityEngine;

// The only reflection adapter to the legacy wave registry/cleanup. Trial rules
// stay in Scene2; eventual merging can replace this adapter with a public API.
public sealed class Scene2WaveBridge
{
    private static readonly FieldInfo BossSlot = Field("_currentBoss");
    private static readonly FieldInfo ClearArmed = Field("_breachClearArmed");
    private static readonly FieldInfo ClearRoutine = Field("_breachClearRoutine");
    private static readonly FieldInfo ClearCount = Field("_lastBreachClearCount");
    private readonly WaveManager _waves;
    public bool IsValid => _waves != null && BossSlot != null && ClearArmed != null &&
        ClearRoutine != null && ClearCount != null;
    public Scene2WaveBridge(WaveManager waves) { _waves = waves; }
    private static FieldInfo Field(string name) => typeof(WaveManager).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic);

    public void PrepareWave()
    {
        ClearArmed.SetValue(_waves, false);
        ClearRoutine.SetValue(_waves, null);
        ClearCount.SetValue(_waves, 0);
    }

    public void RegisterBoss(Scene2Boss boss)
    {
        BossSlot.SetValue(_waves, boss);
        boss.onDeath.AddListener(_ =>
        {
            if ((Boss)BossSlot.GetValue(_waves) == boss) BossSlot.SetValue(_waves, null);
        });
    }

    public IEnumerator WaitForClear()
    {
        if (!(bool)ClearArmed.GetValue(_waves)) _waves.NotifyBossKilledClearField();
        if (VFXDirector.Instance != null) yield return VFXDirector.Instance.WaitForEffectComplete();
        var cleanup = ClearRoutine.GetValue(_waves) as Coroutine;
        if (cleanup != null) yield return cleanup;
        float settle = Mathf.Max(0f, _waves.breachClearSettleSeconds);
        if ((int)ClearCount.GetValue(_waves) > 0 && settle > 0f)
            yield return new WaitForSecondsRealtime(settle);
    }
}
