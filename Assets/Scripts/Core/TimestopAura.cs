using System.Collections;
using UnityEngine;

/// <summary>
/// 装备技能「时间减速」：仅降低敌人移动与 Boss 派兵节奏；不改全局 timeScale。
/// </summary>
public class TimestopAura : MonoBehaviour
{
    public static TimestopAura Instance { get; private set; }

    public bool IsActive { get; private set; }

    private Coroutine _routine;

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(Cancel);
            GameManager.Instance.onBallLost.AddListener(Cancel);
            GameManager.Instance.onGameOver.AddListener(Cancel);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.RemoveListener(Cancel);
            GameManager.Instance.onBallLost.RemoveListener(Cancel);
            GameManager.Instance.onGameOver.RemoveListener(Cancel);
        }
        if (Instance == this) Instance = null;
    }

    public static TimestopAura EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(TimestopAura));
        return go.AddComponent<TimestopAura>();
    }

    public void Activate()
    {
        if (_routine != null) StopCoroutine(_routine);

        IsActive = true;
        SlowMoFX.Instance?.ActivateEnemyTimestopVisual();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        SlowMoFX.Instance?.PulseFlash(new Color(0.35f, 0.85f, 1f), 0.35f, 0.12f);

        float duration = Config != null ? Config.timestopDuration : 4f;
        _routine = StartCoroutine(TimestopRoutine(duration));
    }

    public void Cancel()
    {
        if (!IsActive) return;
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        EndTimestop();
    }

    public float GetMinionSpeedScale()
    {
        if (!IsActive) return 1f;
        return Config != null ? Config.timestopMinionSpeedScale : 0.35f;
    }

    public float GetBossSpeedScale()
    {
        if (!IsActive) return 1f;
        return Config != null ? Config.timestopBossSpeedScale : 0.6f;
    }

    /// <summary>派兵间隔墙钟乘数（大于 1 表示等更久）。</summary>
    public float GetSpawnIntervalMultiplier()
    {
        if (!IsActive) return 1f;
        float scale = GetMinionSpeedScale();
        return scale > 0.01f ? 1f / scale : 1f;
    }

    private IEnumerator TimestopRoutine(float duration)
    {
        float remaining = duration;

        while (remaining > 0f)
        {
            if (ShouldEndEarly())
            {
                Cancel();
                yield break;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsWaveSimActive())
                remaining -= Time.unscaledDeltaTime;

            yield return null;
        }

        if (_routine != null)
        {
            _routine = null;
            EndTimestop();
        }
    }

    private static bool ShouldEndEarly()
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;
        var state = gm.State;
        return state != GameState.Playing && state != GameState.BallRespawning;
    }

    private void EndTimestop()
    {
        IsActive = false;
        SlowMoFX.Instance?.DeactivateEnemyTimestopVisual();
    }
}
