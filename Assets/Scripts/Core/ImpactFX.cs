using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactFX : MonoBehaviour
{
    public static ImpactFX Instance { get; private set; }

    private ParticleSystem _burstPS;   // 主方块爆发
    private ParticleSystem _dustPS;    // 细小漂散尘埃
    private ParticleSystem _dissolvePS; // 触底像素解体（方案 1）
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
        Color hdr = NeonColors.Active.ForParticle(neonColor, intensity);

        // 保持原本清爽、克制且粒粒分明的数量
        int burstCount = Mathf.RoundToInt(Mathf.Lerp(10f, 22f, intensity));
        int dustCount  = Mathf.RoundToInt(Mathf.Lerp(6f,  14f, intensity));

        EmitAt(_burstPS, worldPos, hdr,          burstCount);
        EmitAt(_dustPS,  worldPos, hdr * 0.6f,   dustCount);
    }

    /// <summary>墙边短 HDR 闪条（沿墙切线方向）。</summary>
    public void SpawnWallFlash(Vector2 worldPos, Vector2 normal, Color neonColor, float intensity = 1f)
    {
        StartCoroutine(WallFlashRoutine(worldPos, normal, neonColor, intensity));
    }

    /// <summary>护心符横向 cyan 波纹（比触底解体弱一档）。</summary>
    public void SpawnShieldRipple(float shieldY, float halfWidth, Color neonColor, float intensity = 1f)
    {
        StartCoroutine(ShieldRippleRoutine(shieldY, halfWidth, neonColor, intensity));
    }

    /// <summary>
    /// 触底扣血：密集像素块向下飞散（方案 1，不裁切 Sprite）。
    /// </summary>
    public void SpawnBottomDissolve(Vector2 worldPos, Color neonColor, float intensity = 1f)
    {
        EmitBottomDissolve(worldPos, neonColor, intensity, 1f, false);
    }

    /// <summary>Boss 击杀：金色像素解体，数量 ×2，轻微上飘。</summary>
    public void SpawnBossDissolve(Vector2 worldPos, Color bossBaseColor, float intensity = 1.5f)
    {
        Color gold = Color.Lerp(bossBaseColor, new Color(1f, 0.82f, 0.15f), 0.65f);
        EmitBottomDissolve(worldPos, gold, intensity, 2f, true);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
    }

    private void EmitBottomDissolve(Vector2 worldPos, Color neonColor, float intensity, float countMult, bool upwardBias)
    {
        Color hdr = NeonColors.Active.ForParticle(neonColor, intensity);

        int count = Mathf.RoundToInt(Mathf.Lerp(32f, 52f, intensity) * countMult);
        float spread = 0.32f + intensity * 0.12f;
        float sizeMul = upwardBias ? 1.15f : 1f;

        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            Vector2 offset = Random.insideUnitCircle * spread;
            ep.position = new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, -0.18f);
            ep.startColor = hdr * Random.Range(0.8f, 1f);
            ep.startSize = Random.Range(0.07f, 0.14f) * intensity * sizeMul;
            ep.startLifetime = Random.Range(0.26f, 0.38f);
            float vx = Random.Range(-2.8f, 2.8f);
            float vy = upwardBias
                ? Random.Range(-2.5f, 4.5f)
                : Random.Range(-6f, -1.2f);
            ep.velocity = new Vector3(vx, vy, 0f);
            _dissolvePS.Emit(ep, 1);
        }

        if (!upwardBias)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    // ── 内部工具 ──────────────────────────────────────────────────────────
    private void EmitAt(ParticleSystem ps, Vector2 pos, Color color, int count)
    {
        var ep      = new ParticleSystem.EmitParams();
        ep.position = new Vector3(pos.x, pos.y, -0.2f);
        ep.startColor = color;
        ps.Emit(ep, count);
    }

    // ── Bumper 里程碑脉冲波（见 SpawnBumperPulseWave）────────────────────

    /// <summary>Bumper 里程碑脉冲：金色八角冲击波 + 径向射线；伤害随波前半径同步。</summary>
    public void SpawnBumperPulseWave(Vector2 worldPos, float worldRadius, Color neonColor, float duration = 1f, int pulseDamage = 0)
    {
        StartCoroutine(BumperPulseWaveRoutine(worldPos, worldRadius, neonColor, duration, pulseDamage));
    }

    private static readonly Collider2D[] PulseOverlapBuf = new Collider2D[48];

    private IEnumerator BumperPulseWaveRoutine(Vector2 pos, float worldRadius, Color neonColor, float duration, int pulseDamage)
    {
        var root = new GameObject("BumperPulseWave");
        root.transform.position = new Vector3(pos.x, pos.y, -0.11f);

        Color hdr = NeonColors.Active.ForParticle(neonColor, 1.5f);
        Color core = new Color(hdr.r * 1.2f, hdr.g * 1.1f, hdr.b, 1f);

        // 半透明核心闪光（与普通 Hit 粒子区分）
        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(root.transform, false);
        var coreSr = coreGo.AddComponent<SpriteRenderer>();
        coreSr.sprite = MakeDiscSprite(32);
        coreSr.material = new Material(Shader.Find("Sprites/Default"));
        coreSr.color = new Color(core.r, core.g, core.b, 0.55f);
        coreSr.sortingOrder = 17;

        // 八角冲击环
        var ringGo = new GameObject("OctRing");
        ringGo.transform.SetParent(root.transform, false);
        var ringLr = BuildLoopLine(ringGo, 8, 0.16f, 18);
        ringLr.material = new Material(Shader.Find("Sprites/Default"));

        // 6 条径向射线
        const int spokeCount = 6;
        var spokes = new LineRenderer[spokeCount];
        for (int s = 0; s < spokeCount; s++)
        {
            var spokeGo = new GameObject("Spoke" + s);
            spokeGo.transform.SetParent(root.transform, false);
            spokeGo.transform.localRotation = Quaternion.Euler(0f, 0f, s * (360f / spokeCount));
            spokes[s] = spokeGo.AddComponent<LineRenderer>();
            spokes[s].useWorldSpace = false;
            spokes[s].positionCount = 2;
            spokes[s].sortingOrder = 19;
            spokes[s].material = new Material(Shader.Find("Sprites/Default"));
            spokes[s].SetPosition(0, Vector3.zero);
            spokes[s].SetPosition(1, Vector3.right);
        }

        float startScale = 0.12f;
        float endScale = Mathf.Max(worldRadius, 0.8f);
        var hitEnemies = pulseDamage > 0 ? new HashSet<EnemyBase>() : null;
        bool shook = false;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            float ringScale = Mathf.Lerp(startScale, endScale, p);
            ringGo.transform.localScale = Vector3.one * ringScale;

            if (hitEnemies != null)
            {
                int count = Physics2D.OverlapCircleNonAlloc(pos, ringScale, PulseOverlapBuf);
                for (int i = 0; i < count; i++)
                {
                    var col = PulseOverlapBuf[i];
                    if (col == null) continue;

                    var enemy = col.GetComponentInParent<EnemyBase>();
                    if (enemy == null || hitEnemies.Contains(enemy)) continue;
                    if (!ComboMilestoneRewards.IsGruntPulseTarget(enemy)) continue;

                    float dist = Vector2.Distance((Vector2)enemy.transform.position, pos);
                    if (dist > ringScale + 0.1f) continue;

                    enemy.TakeHit(pulseDamage, isFromBall: false, pos);
                    hitEnemies.Add(enemy);
                    if (!shook)
                    {
                        shook = true;
                        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
                    }
                }
            }

            float coreScale = Mathf.Lerp(0.25f, endScale * 0.45f, Mathf.Min(1f, p * 1.4f));
            coreGo.transform.localScale = Vector3.one * coreScale;

            float alpha = (1f - p) * (1f - p);
            coreSr.color = new Color(core.r, core.g, core.b, 0.55f * alpha);

            float ringW = Mathf.Lerp(0.22f, 0.05f, p);
            ringLr.startWidth = ringW;
            ringLr.endWidth = ringW;
            var ringC = new Color(hdr.r, hdr.g, hdr.b, alpha * 0.95f);
            ringLr.startColor = ringC;
            ringLr.endColor = ringC;

            float spokeLen = ringScale * (0.55f + p * 0.5f);
            float spokeW = Mathf.Lerp(0.12f, 0.03f, p);
            var spokeC = new Color(core.r, core.g, core.b, alpha * 0.75f);
            for (int s = 0; s < spokeCount; s++)
            {
                spokes[s].startWidth = spokeW;
                spokes[s].endWidth = spokeW * 0.4f;
                spokes[s].startColor = spokeC;
                spokes[s].endColor = new Color(spokeC.r, spokeC.g, spokeC.b, 0f);
                spokes[s].SetPosition(1, new Vector3(spokeLen, 0f, 0f));
            }

            yield return null;
        }

        Destroy(root);
    }

    private static LineRenderer BuildLoopLine(GameObject go, int sides, float width, int sortOrder)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = sides + 1;
        lr.sortingOrder = sortOrder;
        lr.startWidth = width;
        lr.endWidth = width;
        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }
        return lr;
    }

    private static Sprite MakeDiscSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
            float a = Mathf.Clamp01(1f - d);
            a = a * a;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private IEnumerator WallFlashRoutine(Vector2 worldPos, Vector2 normal, Color neonColor, float intensity)
    {
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        if (tangent.sqrMagnitude < 0.001f) tangent = Vector2.right;
        tangent.Normalize();

        float halfLen = 0.22f + intensity * 0.12f;
        var go = new GameObject("WallFlash");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sortingOrder = 11;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.06f * intensity;
        lr.endWidth = lr.startWidth;

        Color flash = NeonColors.Active.ForParticle(neonColor, 1f + intensity * 0.2f);
        const float dur = 0.15f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float alpha = 1f - p;
            var c = new Color(flash.r, flash.g, flash.b, alpha);
            lr.startColor = c;
            lr.endColor = c;
            lr.startWidth = Mathf.Lerp(0.08f * intensity, 0.02f, p);
            lr.endWidth = lr.startWidth;

            Vector2 center = worldPos + normal * 0.04f;
            lr.SetPosition(0, center - tangent * halfLen);
            lr.SetPosition(1, center + tangent * halfLen);
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator ShieldRippleRoutine(float shieldY, float halfWidth, Color neonColor, float intensity)
    {
        var go = new GameObject("ShieldRipple");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sortingOrder = 14;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        Color flash = NeonColors.Active.ForParticle(neonColor, 0.95f + intensity * 0.15f);
        const float dur = 0.28f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float expand = Mathf.SmoothStep(0f, 1f, p);
            float half = halfWidth * expand;
            float alpha = (1f - p) * (1f - p);

            lr.SetPosition(0, new Vector3(-half, shieldY, -0.1f));
            lr.SetPosition(1, new Vector3( half, shieldY, -0.1f));

            var c = new Color(flash.r, flash.g, flash.b, alpha);
            lr.startColor = c;
            lr.endColor = c;
            float w = Mathf.Lerp(0.04f, 0.14f, expand) * intensity;
            lr.startWidth = w;
            lr.endWidth = w;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>底线位置短横线闪白（创战纪式擦除感）。</summary>
    private IEnumerator BottomLineFlashRoutine(float y, Color neonColor, float intensity)
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        float halfW = cfg != null ? cfg.worldWidth * 0.48f : 4.3f;

        var go = new GameObject("BottomLineFlash");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.sortingOrder = 12;
        lr.startWidth = 0.07f * intensity;
        lr.endWidth   = lr.startWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.SetPosition(0, new Vector3(-halfW, y, -0.12f));
        lr.SetPosition(1, new Vector3( halfW, y, -0.12f));

        Color flash = NeonColors.Active.ForParticle(Color.Lerp(Color.white, neonColor, 0.35f), 1f + intensity * 0.15f);

        const float dur = 0.14f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float alpha = 1f - p;
            var c = new Color(flash.r, flash.g, flash.b, alpha);
            lr.startColor = c;
            lr.endColor   = c;
            lr.startWidth = Mathf.Lerp(0.09f * intensity, 0.02f, p);
            lr.endWidth   = lr.startWidth;
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
        _dissolvePS = BuildDissolvePS();
    }

    private ParticleSystem BuildDissolvePS()
    {
        var go = new GameObject("ImpactPS_Dissolve");
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0.15f;
        main.maxParticles = 600;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        // 保持块大小，末尾快速消失（避免拖尾感）
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.78f, 1f), new Keyframe(1f, 0f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 0.9f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = _particleMat;
        rend.sortingOrder = 11;

        return ps;
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
        main.gravityModifier = 0.15f;
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
