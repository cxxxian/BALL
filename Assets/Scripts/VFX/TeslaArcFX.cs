using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特斯拉电弧：噪声折线 + 细白核心 / 青蓝底线。
/// 发光靠场景 URP Bloom（HDR 颜色 > threshold），不做多层假 bloom。
/// 折线感来自 BezierLineUtil.FillLightningBolt（对齐电流描边的 waveAmp/Freq/Speed）。
/// </summary>
public class TeslaArcFX : MonoBehaviour
{
    public static TeslaArcFX Instance { get; private set; }

    private const int PoolSize = 12;
    private const int BoltSegments = 20;
    private const float ArcDuration = 0.18f;

    // 高于 TronGlobalProfile Bloom threshold(1.1)，交给真 Bloom
    private static readonly Color CoreHdr = new Color(3.2f, 3.6f, 4.0f, 1f);
    private static readonly Color GlowHdr = new Color(0.25f, 1.6f, 2.4f, 0.85f);

    private const float WaveAmp = 1.05f;
    private const float WaveFreq = 1.7f;
    private const float WaveSpeed = 2.4f;

    private readonly Queue<ArcInstance> _pool = new Queue<ArcInstance>();
    private Material _lineMat;

    private class ArcInstance
    {
        public GameObject Root;
        public LineRenderer Core;
        public LineRenderer Glow;
        public Coroutine Routine;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _lineMat = CreateLineMaterial();
        for (int i = 0; i < PoolSize; i++)
            _pool.Enqueue(CreateArcInstance());
    }

    public void SpawnArc(Vector2 from, Vector2 to, int jitterSeed)
    {
        if (_pool.Count == 0) return;

        var arc = _pool.Dequeue();
        arc.Root.SetActive(true);

        if (arc.Routine != null) StopCoroutine(arc.Routine);
        arc.Routine = StartCoroutine(FlashAndReturn(arc, from, to, jitterSeed));
    }

    private IEnumerator FlashAndReturn(ArcInstance arc, Vector2 from, Vector2 to, int jitterSeed)
    {
        float elapsed = 0f;
        float timeBase = jitterSeed * 0.31f;

        while (elapsed < ArcDuration)
        {
            float t = timeBase + elapsed;
            BezierLineUtil.FillLightningBolt(
                arc.Glow, from, to, t, jitterSeed,
                BoltSegments, WaveAmp, WaveFreq, WaveSpeed, -0.13f);
            BezierLineUtil.FillLightningBolt(
                arc.Core, from, to, t + 0.07f, jitterSeed + 3,
                BoltSegments, WaveAmp * 0.85f, WaveFreq, WaveSpeed, -0.12f);

            float life = 1f - (elapsed / ArcDuration);
            float alpha = Mathf.Clamp01(life * life);
            SetLayerColors(arc, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ReturnArc(arc);
    }

    private void ReturnArc(ArcInstance arc)
    {
        if (arc.Routine != null)
        {
            StopCoroutine(arc.Routine);
            arc.Routine = null;
        }
        arc.Root.SetActive(false);
        _pool.Enqueue(arc);
    }

    private static void SetLayerColors(ArcInstance arc, float alpha)
    {
        // HDR rgb 保持 > threshold，只衰减 alpha，Bloom 仍能吃到亮度
        var core = new Color(CoreHdr.r, CoreHdr.g, CoreHdr.b, alpha);
        var glow = new Color(GlowHdr.r, GlowHdr.g, GlowHdr.b, GlowHdr.a * alpha);
        arc.Core.startColor = core;
        arc.Core.endColor = core;
        arc.Glow.startColor = glow;
        arc.Glow.endColor = glow;
    }

    private ArcInstance CreateArcInstance()
    {
        var root = new GameObject("TeslaArc");
        root.transform.SetParent(transform, false);

        // 底线：略宽青蓝（色相），Bloom 会再扩一圈
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        var glow = glowGo.AddComponent<LineRenderer>();
        ConfigureLine(glow, 21, 0.11f, 0.05f);

        // 核心：极细近白 HDR
        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(root.transform, false);
        var core = coreGo.AddComponent<LineRenderer>();
        ConfigureLine(core, 22, 0.035f, 0.018f);

        root.SetActive(false);
        return new ArcInstance { Root = root, Core = core, Glow = glow };
    }

    private void ConfigureLine(LineRenderer lr, int sortingOrder, float startWidth, float endWidth)
    {
        lr.useWorldSpace = true;
        lr.material = _lineMat;
        lr.sortingOrder = sortingOrder;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 1;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.allowOcclusionWhenDynamic = false;
    }

    private static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        return new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("TeslaArcFX");
        go.AddComponent<TeslaArcFX>();
    }
}
