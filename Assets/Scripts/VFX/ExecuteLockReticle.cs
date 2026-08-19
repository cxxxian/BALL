using UnityEngine;

/// <summary>斩杀瞄准时在目标敌人上显示的角标锁定框。</summary>
public class ExecuteLockReticle : MonoBehaviour
{
    public static ExecuteLockReticle Instance { get; private set; }

    private const int Corners = 4;
    private const int PointsPerCorner = 3;
    private const float ArmRatio = 0.22f;
    private const float PaddingRatio = 0.10f;

    private readonly LineRenderer[] _corners = new LineRenderer[Corners];
    private EnemyBase _target;
    private float _alpha = 1f;
    private float _fadeTarget = 1f;
    private bool _visible;

    private static Color ReticleColor => NeonColors.Active.GetBase(NeonRole.SkillExecute);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        var mat = CyberVisualFactory.UnlitMaterial;
        for (int i = 0; i < Corners; i++)
        {
            var go = new GameObject($"Corner_{i}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = PointsPerCorner;
            lr.loop = false;
            lr.material = mat;
            lr.sortingOrder = 16;
            lr.startWidth = 0.055f;
            lr.endWidth = 0.035f;
            lr.enabled = false;
            _corners[i] = lr;
        }
    }

    private void Update()
    {
        if (!_visible) return;

        _alpha = Mathf.MoveTowards(_alpha, _fadeTarget, Time.unscaledDeltaTime / 0.08f);

        if (_target == null || _target.IsDead)
        {
            SetCornersEnabled(false);
            return;
        }

        float pulse = 0.92f + Mathf.PingPong(Time.unscaledTime * 2.8f, 1f) * 0.08f;
        RebuildCorners(_target, pulse * _alpha);
    }

    public void Show(EnemyBase enemy)
    {
        _visible = true;
        UpdateTarget(enemy);
    }

    public void Hide()
    {
        _visible = false;
        _target = null;
        _fadeTarget = 0f;
        SetCornersEnabled(false);
        _alpha = 1f;
    }

    public void UpdateTarget(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsDead)
        {
            _target = null;
            _fadeTarget = 0f;
            return;
        }

        _visible = true;

        if (_target != enemy)
        {
            _target = enemy;
            _fadeTarget = 1f;
            _alpha = 0f;
        }

        SetCornersEnabled(true);
    }

    private void RebuildCorners(EnemyBase enemy, float alpha)
    {
        var sr = enemy.MainSR;
        Bounds bounds = sr != null ? sr.bounds : new Bounds(enemy.transform.position, Vector3.one * 0.6f);

        float padX = bounds.size.x * PaddingRatio;
        float padY = bounds.size.y * PaddingRatio;
        float armX = bounds.size.x * ArmRatio;
        float armY = bounds.size.y * ArmRatio;

        float left = bounds.min.x - padX;
        float right = bounds.max.x + padX;
        float bottom = bounds.min.y - padY;
        float top = bounds.max.y + padY;
        float z = -0.08f;

        Color c = ReticleColor;
        var hdr = new Color(c.r * 2.5f, c.g * 2.5f, c.b * 2.5f, alpha);

        SetCorner(_corners[0], left, top, left + armX, top, left, top - armY, z, hdr);
        SetCorner(_corners[1], right, top, right - armX, top, right, top - armY, z, hdr);
        SetCorner(_corners[2], left, bottom, left + armX, bottom, left, bottom + armY, z, hdr);
        SetCorner(_corners[3], right, bottom, right - armX, bottom, right, bottom + armY, z, hdr);
    }

    private static void SetCorner(LineRenderer lr, float ax, float ay, float bx, float by, float cx, float cy, float z, Color color)
    {
        lr.SetPosition(0, new Vector3(ax, ay, z));
        lr.SetPosition(1, new Vector3(bx, by, z));
        lr.SetPosition(2, new Vector3(cx, cy, z));
        lr.startColor = color;
        lr.endColor = color;
    }

    private void SetCornersEnabled(bool enabled)
    {
        for (int i = 0; i < Corners; i++)
            _corners[i].enabled = enabled;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("ExecuteLockReticle");
        go.AddComponent<ExecuteLockReticle>();
    }
}
