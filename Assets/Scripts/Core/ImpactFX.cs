using System.Collections;
using UnityEngine;

public class ImpactFX : MonoBehaviour
{
    public static ImpactFX Instance { get; private set; }

    private ParticleSystem _burstPS;   // 主方块爆发
    private ParticleSystem _dustPS;    // 细小漂散尘埃
    private Material       _particleMat;
    private Texture2D      _squareTex;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildSystems();
    }

    // ── 公共 API ─────────────────────────────────────────────────────────
    /// <summary>在 worldPos 产生 Tron 风格像素粒子爆发。color 取碰撞物体的 Neon 颜色。</summary>
    public void SpawnHit(Vector2 worldPos, Color neonColor, float intensity = 1f)
    {
        // 降低辉光过载倍率，防止颜色过曝粘连成“一坨”，保持原汁原味的霓虹色彩
        Color hdr = neonColor * (2.8f * intensity);
        hdr.a = 1f;

        // 保持原本清爽、克制且粒粒分明的数量
        int burstCount = Mathf.RoundToInt(Mathf.Lerp(10f, 22f, intensity));
        int dustCount  = Mathf.RoundToInt(Mathf.Lerp(6f,  14f, intensity));

        EmitAt(_burstPS, worldPos, hdr,          burstCount);
        EmitAt(_dustPS,  worldPos, hdr * 0.6f,   dustCount);
        StartCoroutine(RingRoutine(worldPos, neonColor, intensity));
    }

    // ── 内部工具 ──────────────────────────────────────────────────────────
    private void EmitAt(ParticleSystem ps, Vector2 pos, Color color, int count)
    {
        var ep      = new ParticleSystem.EmitParams();
        ep.position = new Vector3(pos.x, pos.y, -0.2f);
        ep.startColor = color;
        ps.Emit(ep, count);
    }

    // ── 扩散光环（LineRenderer 圆） ───────────────────────────────────────
    private IEnumerator RingRoutine(Vector2 pos, Color neonColor, float intensity)
    {
        var go = new GameObject("HitRing");
        go.transform.position = new Vector3(pos.x, pos.y, -0.15f);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = false;
        lr.loop           = false;
        lr.positionCount  = 33;
        lr.sortingOrder   = 9;

        float w = 0.055f * intensity;
        lr.startWidth = w; lr.endWidth = w;

        // 画圆
        for (int i = 0; i <= 32; i++)
        {
            float a = (float)i / 32 * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        var ringMat = new Material(Shader.Find("Sprites/Default"));
        lr.material = ringMat;

        Color hdrRing = neonColor * 3.5f;
        hdrRing.a = 1f;

        float dur = 0.20f;
        float endScale = 1.6f + intensity * 0.8f;

        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, endScale, p);
            float alpha = 1f - p;
            lr.startColor = new Color(hdrRing.r, hdrRing.g, hdrRing.b, alpha);
            lr.endColor   = lr.startColor;
            yield return null;
        }
        Destroy(go);
    }

    // ── 粒子系统构建 ─────────────────────────────────────────────────────
    private void BuildSystems()
    {
        _squareTex   = MakeSquareTex(8);
        _particleMat = new Material(Shader.Find("Sprites/Default"));
        _particleMat.mainTexture = _squareTex;

        _burstPS = BuildPS("Burst", 0.25f, 0.55f, 2.5f, 9f, 0.05f, 0.18f, 500);
        _dustPS  = BuildPS("Dust",  0.4f,  0.85f, 0.5f, 3f,  0.02f, 0.07f, 300);
    }

    private ParticleSystem BuildPS(string goName,
        float lifeMin, float lifeMax,
        float spdMin,  float spdMax,
        float sizeMin, float sizeMax,
        int maxParticles)
    {
        var go = new GameObject("ImpactPS_" + goName);
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        // Main
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(spdMin, spdMax);
        main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startColor      = Color.white;
        main.gravityModifier = 0f;
        main.maxParticles    = maxParticles;

        // Emission off (manual)
        var em = ps.emission;
        em.enabled = false;

        // Shape: point
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.01f;

        // Speed over lifetime: decelerate
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.05f)));

        // Size over lifetime: hold then shrink
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0f)));

        // Color over lifetime: hold opacity, then sharp fade
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Renderer
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode  = ParticleSystemRenderMode.Billboard;
        rend.material    = _particleMat;
        rend.sortingOrder = 10;

        return ps;
    }

    private static Texture2D MakeSquareTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return tex;
    }
}
