using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockShield : MonoBehaviour
{
    public static BlockShield Instance { get; private set; }

    public bool IsActive { get; private set; }

    [Header("护盾位置与尺寸")]
    [Tooltip("护盾线在世界坐标的 Y 值（挡板上方）")]
    public float shieldY         = -4.0f;
    [Tooltip("护盾线半宽（与墙壁宽度匹配）")]
    public float shieldHalfWidth = 4.25f;

    [Header("时间参数")]
    public float shieldDuration  = 5f;
    public float blinkStartTime  = 1.5f;

    // ── LineRenderer ──────────────────────────────────────────────────────
    private LineRenderer _line;
    private Coroutine    _routine;
    private float        _timer;

    private static readonly Color ColOff = new Color(0f, 0f, 0f, 0f);

    private Color ColActive => NeonColors.Active.GetBase(NeonRole.SkillShield);
    private Color ColDim    => NeonPalette.Dim(ColActive, 0.55f);
    private Color ColAbsorb => Color.Lerp(ColActive, Color.white, 0.45f);
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _line = GetComponent<LineRenderer>();
        if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
        InitLine();
        _line.enabled = false;
    }

    private void InitLine()
    {
        _line.positionCount = 2;
        _line.SetPosition(0, new Vector3(-shieldHalfWidth, shieldY, 0f));
        _line.SetPosition(1, new Vector3( shieldHalfWidth, shieldY, 0f));
        _line.startWidth   = 0.18f;
        _line.endWidth     = 0.18f;
        _line.startColor   = ColActive;
        _line.endColor     = ColActive;
        _line.useWorldSpace = true;
        _line.sortingOrder  = 15;
        var mat = new Material(Shader.Find("Sprites/Default"));
        _line.material = mat;
    }

    // ── 激活护盾 ─────────────────────────────────────────────────────────
    public void Activate()
    {
        if (_routine != null) StopCoroutine(_routine);
        IsActive      = true;
        _line.enabled = true;

        // 更新端点位置（支持运行时修改 shieldY/shieldHalfWidth）
        _line.SetPosition(0, new Vector3(-shieldHalfWidth, shieldY, 0f));
        _line.SetPosition(1, new Vector3( shieldHalfWidth, shieldY, 0f));

        _routine = StartCoroutine(ShieldRoutine());
    }

    // ── 被小兵触底触发 ────────────────────────────────────────────────────
    public void TriggerAbsorb()
    {
        if (!IsActive) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(AbsorbRoutine());
    }

    // ── 护盾存活主协程 ────────────────────────────────────────────────────
    private IEnumerator ShieldRoutine()
    {
        _timer = shieldDuration;

        // 入场：从中心向两侧展开
        yield return StartCoroutine(ExpandRoutine(0.25f));
        JuiceRouter.ShieldActivate(shieldY, shieldHalfWidth);

        // 正常脉动阶段
        while (_timer > blinkStartTime)
        {
            _timer -= Time.deltaTime;
            float pulse = (Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f);
            SetLineColor(Color.Lerp(ColDim, ColActive, pulse));
            yield return null;
        }

        // 警告闪烁阶段（最后 blinkStartTime 秒）
        while (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            float blink = Mathf.PingPong(Time.time * 8f, 1f);
            SetLineColor(Color.Lerp(ColOff, ColDim, blink));
            yield return null;
        }

        // 消散特效
        yield return StartCoroutine(FadeOutRoutine(0.4f));
        Deactivate();
    }

    // ── 吸收伤害协程 ─────────────────────────────────────────────────────
    private IEnumerator AbsorbRoutine()
    {
        IsActive = false;

        // 瞬间亮起
        SetLineColor(ColAbsorb);
        _line.startWidth = 0.40f;
        _line.endWidth   = 0.40f;

        JuiceRouter.ShieldAbsorb(shieldY, shieldHalfWidth);

        StartCoroutine(ClearMinionsRoutine());

        // 护盾线膨胀然后快速消散
        float t = 0f, dur = 0.35f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = t / dur;
            float w = Mathf.Lerp(0.40f, 0f, p);
            _line.startWidth = w;
            _line.endWidth   = w;
            SetLineColor(Color.Lerp(ColAbsorb, ColOff, p));
            yield return null;
        }

        Deactivate();
    }

    // ── 视觉辅助 ─────────────────────────────────────────────────────────
    private IEnumerator ExpandRoutine(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float half = shieldHalfWidth * p;
            _line.SetPosition(0, new Vector3(-half, shieldY, 0f));
            _line.SetPosition(1, new Vector3( half, shieldY, 0f));
            SetLineColor(Color.Lerp(ColDim, ColActive, p));
            yield return null;
        }
        _line.SetPosition(0, new Vector3(-shieldHalfWidth, shieldY, 0f));
        _line.SetPosition(1, new Vector3( shieldHalfWidth, shieldY, 0f));
    }

    private IEnumerator FadeOutRoutine(float dur)
    {
        float t = 0f;
        Color start = _line.startColor;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetLineColor(Color.Lerp(start, ColOff, t / dur));
            float w = Mathf.Lerp(0.18f, 0f, t / dur);
            _line.startWidth = w;
            _line.endWidth   = w;
            yield return null;
        }
    }

    private void SetLineColor(Color c)
    {
        _line.startColor = c;
        _line.endColor   = c;
    }

    // ── 护盾清场：统一青闪 → 解体 ─────────────────────────────────────────
    private IEnumerator ClearMinionsRoutine()
    {
        var toLower = CollectLowerMinions();
        if (toLower.Count == 0) yield break;

        foreach (var e in toLower)
            EnemyJuice.ShieldAbsorbFlash(e);

        yield return new WaitForSecondsRealtime(0.04f);

        foreach (var e in toLower)
        {
            if (e != null && !e.IsDead)
                EnemyJuice.ShieldAbsorbDissolve(e);
            yield return new WaitForSecondsRealtime(Random.Range(0f, 0.03f));
        }
    }

    private List<EnemyBase> CollectLowerMinions()
    {
        var allEnemies = FindObjectsOfType<EnemyBase>();
        var toLower = new List<EnemyBase>();

        foreach (var e in allEnemies)
        {
            if (e == null || e.IsDead || e is Boss) continue;
            if (e.transform.position.y < shieldY)
                toLower.Add(e);
        }

        return toLower;
    }

    private void Deactivate()
    {
        IsActive         = false;
        _line.enabled    = false;
        _line.startWidth = 0.10f;
        _line.endWidth   = 0.10f;
        _routine         = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
