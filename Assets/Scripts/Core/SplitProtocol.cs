using System.Collections.Generic;
using UnityEngine;

/// <summary>分裂协议：主球 + 2 幻影，短时多球清场。</summary>
public class SplitProtocol : MonoBehaviour
{
    public static SplitProtocol Instance { get; private set; }

    public bool IsActive { get; private set; }

    private readonly List<SplitPhantomBall> _phantoms = new List<SplitPhantomBall>();
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

    public static SplitProtocol EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(SplitProtocol));
        return go.AddComponent<SplitProtocol>();
    }

    public float DamageScale =>
        IsActive ? (Config != null ? Config.splitDamageScale : 0.5f) : 1f;

    public void Activate()
    {
        if (IsActive) Cancel();

        var main = BallController.Instance;
        if (main == null || main.Rb == null) return;
        if (main.IsWaitingForLaunch) return;

        SkillManager.Instance?.ClearExecuteArm();
        if (SkillManager.Instance != null && SkillManager.Instance.IsAiming)
            SkillManager.Instance.CancelAiming();

        IsActive = true;
        float angle = Config != null ? Config.splitSpawnAngleDeg : 28f;
        float alpha = Config != null ? Config.splitPhantomAlpha : 0.92f;
        Vector2 vel = main.Rb.velocity;
        if (vel.sqrMagnitude < 0.25f)
            vel = Vector2.up * (main.config != null ? main.config.ballMinSpeed : 5f);

        SpawnPhantom(main, Quaternion.Euler(0f, 0f, angle) * vel, alpha);
        SpawnPhantom(main, Quaternion.Euler(0f, 0f, -angle) * vel, alpha);

        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        SlowMoFX.Instance?.PulseFlash(new Color(1f, 0.45f, 0.85f), 0.4f, 0.1f);

        float duration = Config != null ? Config.splitDuration : 5f;
        _routine = StartCoroutine(SplitRoutine(duration));
    }

    private void SpawnPhantom(BallController main, Vector2 velocity, float alpha)
    {
        var go = new GameObject("SplitPhantom");
        go.tag = "Ball"; // 与挡板 / Bumper / 齿轮交互
        go.transform.position = main.transform.position;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = main.Rb.gravityScale;
        rb.collisionDetectionMode = main.Rb.collisionDetectionMode;
        rb.interpolation = main.Rb.interpolation;
        rb.sharedMaterial = main.Rb.sharedMaterial;
        rb.velocity = velocity;

        var col = go.AddComponent<CircleCollider2D>();
        var mainCol = main.GetComponent<CircleCollider2D>();
        if (mainCol != null)
        {
            col.radius = mainCol.radius;
            col.sharedMaterial = mainCol.sharedMaterial;
        }

        // 不与主球 / 其它幻影互撞
        if (mainCol != null)
            Physics2D.IgnoreCollision(col, mainCol, true);
        foreach (var p in _phantoms)
        {
            if (p != null && p.Col != null)
                Physics2D.IgnoreCollision(col, p.Col, true);
        }

        var sr = go.AddComponent<SpriteRenderer>();
        var mainSr = main.GetComponent<SpriteRenderer>();
        if (mainSr != null)
        {
            sr.sprite = mainSr.sprite;
            sr.sortingLayerID = mainSr.sortingLayerID;
            sr.sortingOrder = mainSr.sortingOrder + 1;
            // 高亮品红幻影，避免半透明过暗
            Color baseCol = main.ballDefinition != null ? main.ballDefinition.glowColor : mainSr.color;
            Color phantomCol = Color.Lerp(baseCol, new Color(1f, 0.55f, 0.95f, 1f), 0.55f);
            phantomCol.a = Mathf.Clamp01(alpha);
            sr.color = phantomCol;
        }

        var mainTrail = main.GetComponent<TrailRenderer>();
        if (mainTrail != null)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = mainTrail.time * 0.85f;
            trail.minVertexDistance = mainTrail.minVertexDistance;
            trail.widthMultiplier = mainTrail.widthMultiplier * 1.05f;
            trail.sharedMaterial = mainTrail.sharedMaterial;
            trail.sortingLayerID = mainTrail.sortingLayerID;
            trail.sortingOrder = mainTrail.sortingOrder;
            Color tip = new Color(1f, 0.7f, 1f, 0.95f);
            Color tail = new Color(1f, 0.35f, 0.85f, 0f);
            trail.startColor = tip;
            trail.endColor = tail;
            trail.startWidth = mainTrail.startWidth * 1.1f;
            trail.endWidth = mainTrail.endWidth;
        }

        var phantom = go.AddComponent<SplitPhantomBall>();
        phantom.Init(col, rb, main.config);
        _phantoms.Add(phantom);
    }

    private System.Collections.IEnumerator SplitRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        EndSplit();
    }

    public void Cancel()
    {
        if (!IsActive && _phantoms.Count == 0) return;
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        EndSplit();
    }

    private void EndSplit()
    {
        for (int i = _phantoms.Count - 1; i >= 0; i--)
        {
            if (_phantoms[i] != null)
                _phantoms[i].Despawn();
        }
        _phantoms.Clear();
        IsActive = false;
        _routine = null;
    }

    public void NotifyPhantomDestroyed(SplitPhantomBall phantom)
    {
        _phantoms.Remove(phantom);
    }
}
