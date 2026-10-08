using System.Diagnostics;
using UnityEngine;

// Trial-only diagnostics. Logs the actual synchronous damage call stack on kill.
// This component does not apply damage or change enemy behaviour.
public sealed class Scene2EnemyDeathTrace : MonoBehaviour
{
    private Minion _enemy;
    private bool _combatDeath;
    public string LastSource { get; private set; }
    private void Awake()
    {
        _enemy = GetComponent<Minion>();
        if (_enemy != null) _enemy.onDeath.AddListener(OnCombatDeath);
    }
    private void OnCombatDeath(EnemyBase enemy)
    {
        _combatDeath = true;
        string stack = new StackTrace(false).ToString();
        LastSource = Classify(stack, BallController.Instance != null && BallController.Instance.IsExecuteChainActive);
        int combo = ComboSystem.Instance != null ? ComboSystem.Instance.CurrentCombo : 0;
        UnityEngine.Debug.Log("[Scene2Death] source=" + LastSource + " enemy=" + enemy.name +
            " pos=" + transform.position.ToString("F2") + " hp=" + enemy.maxHits + " damage=" + enemy.CurrentHits +
            " combo=" + combo + " slash=" + (BallController.Instance != null && BallController.Instance.IsExecuteChainActive), this);
    }
    public static string Classify(string stack, bool slashActive)
    {
        if (stack.Contains("BumperPulseWaveRoutine")) return "ProtocolRewardPulse";
        if (stack.Contains("ElectricCombat")) return "ElectricChain";
        if (stack.Contains("FrostCombat")) return "FrostBurst";
        if (stack.Contains("TakeHitFromCorePulse")) return "CorePulseSkill";
        if (stack.Contains("TakeFlipperWeaponHit")) return "FlipperWeapon";
        if (stack.Contains("TeslaTower")) return "TeslaTower";
        if (stack.Contains("HomingBullet")) return "CannonBullet";
        if (slashActive && stack.Contains("BallController.OnCollisionEnter2D")) return "ExecuteChainSkill";
        if (stack.Contains("EnemyBase.OnCollisionEnter2D") || stack.Contains("TakeBallHitScaled")) return "BallCollision";
        if (stack.Contains("Scene2SmokeChecks")) return "EditorTest";
        return "OtherDamage";
    }
    private void OnDestroy()
    {
        if (_enemy != null) _enemy.onDeath.RemoveListener(OnCombatDeath);
        if (_combatDeath || !Application.isPlaying || _enemy == null) return;
        LastSource = _enemy.FrozenForWaveClear ? "BossClear" : _enemy.IsDead ? "BottomOrShieldExit" : "SystemRemoval";
        UnityEngine.Debug.Log("[Scene2Death] source=" + LastSource + " enemy=" + _enemy.name + " pos=" + transform.position.ToString("F2"));
    }
}
