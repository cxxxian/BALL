using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactFX : MonoBehaviour
{
    public static ImpactFX Instance { get; private set; }

    private ParticleSystem _burstPS;         // 主方块爆发
    private ParticleSystem _dustPS;          // 细小漂散尘埃
    private ParticleSystem _dissolvePS;      // Boss 升解（无地面碰撞）
    private ParticleSystem _bottomShatterPS; // 触底解构（底线平面反弹）
    private Transform      _bouncePlane;
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

    private enum ShatterMode { Bottom, Boss }

    /// <summary>
    /// 触底扣血：网格像素解构 → 列错位下坠（数字雨式，不裁切 Sprite）。
    /// </summary>
    public void SpawnBottomDissolve(Vector2 worldPos, Color neonColor, float intensity = 1f)
    {
        StartCoroutine(PixelShatterRoutine(worldPos, neonColor, intensity, ShatterMode.Bottom, compact: false));
    }

    /// <summary>Boss 击杀清场等：同冲线语汇，飞散略紧于完整冲线。</summary>
    public void SpawnBottomDissolveCompact(Vector2 worldPos, Color neonColor, float intensity = 0.72f)
    {
        StartCoroutine(PixelShatterRoutine(worldPos, neonColor, intensity, ShatterMode.Bottom, compact: true));
    }

    /// <summary>Boss 击杀：金色网格解构 → 冻结裂开 → 上飘飞散。</summary>
    public void SpawnBossDissolve(Vector2 worldPos, Color bossBaseColor, float intensity = 1.5f)
    {
        Color gold = Color.Lerp(bossBaseColor, new Color(1f, 0.82f, 0.15f), 0.65f);
        StartCoroutine(PixelShatterRoutine(worldPos, gold, intensity, ShatterMode.Boss, compact: false));
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
    }

    /// <summary>
    /// 导弹相关空中解构（无底线反弹）：Miss 打球 / 弹刀截断导弹共用。
    /// </summary>
    public void SpawnMissileShatter(Vector2 worldPos, Color neonColor, float intensity = 1.15f)
    {
        StartCoroutine(PixelShatterRoutine(worldPos, neonColor, intensity, ShatterMode.Boss, compact: false));
    }

    /// <summary>弹刀瞬间定向爆裂（沿回击方向多喷一撮）。</summary>
    public void SpawnParryClash(Vector2 worldPos, Vector2 towardBoss, Color neonColor, float intensity = 1.35f)
    {
        SpawnMissileShatter(worldPos, neonColor, intensity);
        SpawnHit(worldPos, neonColor, intensity * 1.15f);
        // 沿回击方向额外一撮，强化「弹回去」方向感
        if (_burstPS == null) return;
        Color hdr = NeonColors.Active.ForParticle(neonColor, intensity);
        Vector2 dir = towardBoss.sqrMagnitude > 0.01f ? towardBoss.normalized : Vector2.up;
        int n = Mathf.RoundToInt(Mathf.Lerp(8f, 16f, intensity));
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            Vector2 jitter = dir + Random.insideUnitCircle * 0.35f;
            emit.position = worldPos;
            emit.velocity = jitter.normalized * Random.Range(3.5f, 7.5f) * intensity;
            emit.startSize = Random.Range(0.06f, 0.14f);
            emit.startColor = hdr;
            emit.startLifetime = Random.Range(0.25f, 0.55f);
            _burstPS.Emit(emit, 1);
        }
        CameraShake.Instance?.Shake(intensity > 1.2f ? CameraShake.Preset.Heavy : CameraShake.Preset.Medium);
    }

    /// <summary>
    /// 像素解构三拍：裂纹闪 → 网格按列错位弹出 → 细尘收尾。
    /// Boss 用 realtime（躲过顿帧）；触底用 scaled time + 底线平面反弹。
    /// compact：清场用紧凑飞散，不刷整条底线闪。
    /// </summary>
    private IEnumerator PixelShatterRoutine(
        Vector2 worldPos, Color neonColor, float intensity, ShatterMode mode, bool compact)
    {
        bool boss = mode == ShatterMode.Boss;
        ParticleSystem ps = boss ? _dissolvePS : _bottomShatterPS;

        // 触底：对齐攻击线 Y，打开平面碰撞；Boss 升解不碰地
        if (!boss && !compact)
            SyncBouncePlane(MinionLineRules.GetAttackLineY());

        // 触底略降 HDR，减轻 Bloom 糊团；Boss 保持亮
        float hdrMul = boss ? intensity : intensity * 0.78f;
        Color hdr = NeonColors.Active.ForParticle(neonColor, hdrMul);
        Color flash = Color.Lerp(Color.white, hdr, 0.35f);
        flash = NeonColors.Active.ForParticle(flash, hdrMul * (boss ? 1.35f : 1.05f));

        float cell = boss ? 0.13f : (compact ? 0.11f : 0.14f);
        int cols = boss ? 7 : (compact ? 4 : 4);
        int rows = boss ? 7 : (compact ? 4 : 4);
        float sizeMul = boss ? 1.2f : (compact ? 1.05f : 1.28f);
        // 触底寿命加长，好让反弹/缓冲被看见
        float lifeMul = boss ? 1.55f : (compact ? 1.4f : 1.85f);
        float spread = compact ? 0.68f : 1f;

        StartCoroutine(CrackFlashRoutine(worldPos, flash, intensity, boss));
        if (!boss && !compact)
        {
            StartCoroutine(BottomLineFlashRoutine(MinionLineRules.GetAttackLineY(), neonColor, intensity));
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        }

        int midCol = cols / 2;
        for (int wave = 0; wave <= midCol; wave++)
        {
            EmitShatterColumn(ps, worldPos, midCol - wave, rows, cols, cell, hdr, flash, intensity, sizeMul, lifeMul, boss, spread);
            if (wave > 0)
                EmitShatterColumn(ps, worldPos, midCol + wave, rows, cols, cell, hdr, flash, intensity, sizeMul, lifeMul, boss, spread);

            float stagger = boss ? 0.028f : 0.018f;
            if (boss)
                yield return new WaitForSecondsRealtime(stagger);
            else
                yield return new WaitForSeconds(stagger);
        }

        int dustCount = Mathf.RoundToInt(Mathf.Lerp(10f, 18f, intensity) * (boss ? 1.6f : (compact ? 0.4f : 0.55f)));
        EmitShatterDust(ps, worldPos, hdr, dustCount, boss, intensity, spread);
    }

    private void SyncBouncePlane(float lineY)
    {
        if (_bouncePlane == null) return;
        // Plane 的 up = 法线；identity → +Y，即水平底线
        _bouncePlane.position = new Vector3(0f, lineY, 0f);
        _bouncePlane.rotation = Quaternion.identity;
    }

    private void EmitShatterColumn(
        ParticleSystem ps, Vector2 center, int col, int rows, int cols, float cell,
        Color hdr, Color flash, float intensity, float sizeMul, float lifeMul, bool boss, float spread)
    {
        if (col < 0 || col >= cols) return;

        float originX = -(cols - 1) * 0.5f * cell;
        float originY = -(rows - 1) * 0.5f * cell;
        float colX = originX + col * cell;

        for (int r = 0; r < rows; r++)
        {
            float edge = Mathf.Abs(r - (rows - 1) * 0.5f) / (rows * 0.5f);
            if (edge > 0.85f && Random.value > 0.55f) continue;

            float px = center.x + colX + Random.Range(-cell * 0.08f, cell * 0.08f);
            float py = center.y + originY + r * cell + Random.Range(-cell * 0.08f, cell * 0.08f);
            // 触底：避免粒子生成在碰撞面下方而穿模
            if (!boss && spread >= 0.99f)
                py = Mathf.Max(py, MinionLineRules.GetAttackLineY() + 0.06f);
            float rowNorm = r / (float)Mathf.Max(1, rows - 1);

            EmitShatterPixel(ps, px, py, center, hdr, flash, intensity, sizeMul, lifeMul, boss, true, rowNorm, spread);

            // Boss 保留碎屑；触底少叠碎屑，靠大块可读
            float crumbChance = boss ? 0.34f : 0.14f;
            if (Random.value < crumbChance)
            {
                EmitShatterPixel(
                    ps,
                    px + Random.Range(-0.04f, 0.04f),
                    py + Random.Range(-0.04f, 0.04f),
                    center, hdr, flash, intensity, sizeMul * 0.55f, lifeMul * 0.85f, boss,
                    false, rowNorm, spread);
            }
        }
    }

    private void EmitShatterPixel(
        ParticleSystem ps, float px, float py, Vector2 center, Color hdr, Color flash, float intensity,
        float sizeMul, float lifeMul, bool boss, bool isChunk, float rowNorm, float spread)
    {
        var ep = new ParticleSystem.EmitParams();
        ep.position = new Vector3(px, py, -0.18f);

        Color c = Color.Lerp(flash, hdr, Random.Range(0.35f, 0.85f));
        c *= Random.Range(0.82f, 1.05f);
        c.a = 1f;
        ep.startColor = c;

        float baseSize = isChunk
            ? (boss ? Random.Range(0.065f, 0.11f) : Random.Range(0.095f, 0.15f))
            : (boss ? Random.Range(0.03f, 0.05f) : Random.Range(0.04f, 0.065f));
        ep.startSize = baseSize * intensity * sizeMul;
        ep.startLifetime = Random.Range(0.38f, 0.58f) * lifeMul;
        ep.rotation = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        ep.angularVelocity = Random.Range(-360f, 360f);

        float vx, vy;
        if (boss)
        {
            Vector2 radial = new Vector2(px - center.x, py - center.y);
            if (radial.sqrMagnitude < 0.0001f)
                radial = Random.insideUnitCircle;
            radial.Normalize();
            float spd = Random.Range(1.8f, 5.2f) * (isChunk ? 1f : 1.25f);
            vx = radial.x * spd;
            vy = radial.y * spd * 0.7f + Random.Range(1.4f, 4.6f);
            if (vy < 0f) vy *= 0.4f;
        }
        else
        {
            // 扇形下坠：横飞加大、竖速克制，撞线后能弹开而不是糊成一团
            float down = Mathf.Lerp(1.6f, 4.2f, rowNorm) * spread;
            vx = Random.Range(-3.4f, 3.4f) * (isChunk ? 0.9f : 1.25f) * spread;
            vy = -down * Random.Range(0.9f, 1.15f) + Random.Range(0.2f, 1.1f) * spread;
        }

        ep.velocity = new Vector3(vx, vy, 0f);
        ps.Emit(ep, 1);
    }

    private void EmitShatterDust(ParticleSystem ps, Vector2 worldPos, Color hdr, int count, bool boss, float intensity, float spread = 1f)
    {
        Color dust = hdr * 0.65f;
        dust.a = 1f;
        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            Vector2 o = Random.insideUnitCircle * (boss ? 0.45f : 0.32f * spread);
            ep.position = new Vector3(worldPos.x + o.x, worldPos.y + o.y, -0.16f);
            ep.startColor = dust * Random.Range(0.7f, 1f);
            ep.startSize = Random.Range(0.018f, 0.042f) * intensity * (boss ? 1f : 1.35f);
            ep.startLifetime = Random.Range(0.55f, 1.05f) * (boss ? 1.15f : 1.4f) * Mathf.Lerp(0.7f, 1f, spread);
            if (boss)
                ep.velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 3.2f), 0f);
            else
                ep.velocity = new Vector3(Random.Range(-2.6f, 2.6f) * spread, Random.Range(-3.2f, -0.6f) * spread, 0f);
            ps.Emit(ep, 1);
        }
    }

    private IEnumerator CrackFlashRoutine(Vector2 worldPos, Color flash, float intensity, bool boss)
    {
        var go = new GameObject("ShatterCrackFlash");
        go.transform.position = new Vector3(worldPos.x, worldPos.y, -0.2f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeDiscSprite(24);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 13;
        float peak = (boss ? 0.7f : 0.45f) * Mathf.Lerp(0.85f, 1.15f, intensity);
        float dur = boss ? 0.16f : 0.1f;
        float scalePeak = (boss ? 0.85f : 0.5f) * Mathf.Lerp(0.9f, 1.1f, intensity);

        for (float t = 0f; t < dur;)
        {
            t += boss ? Time.unscaledDeltaTime : Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            // 快亮 → 慢收，避免硬闪一下就没
            float envelope = p < 0.25f
                ? Mathf.SmoothStep(0f, 1f, p / 0.25f)
                : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.25f) / 0.75f);
            sr.color = new Color(flash.r, flash.g, flash.b, peak * envelope);
            float s = Mathf.Lerp(0.15f, scalePeak, envelope);
            go.transform.localScale = Vector3.one * s;
            yield return null;
        }
        Destroy(go);
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

    /// <summary>
    /// 球心震爆扩散环：光滑圆环 + 柔光盘（无八角/无射线，区别于 Combo Bumper 脉冲）。
    /// </summary>
    public void SpawnCorePulseWave(Vector2 worldPos, float worldRadius, Color neonColor, float duration = 0.4f)
    {
        StartCoroutine(CorePulseWaveRoutine(worldPos, worldRadius, neonColor, duration));
    }

    private IEnumerator CorePulseWaveRoutine(Vector2 pos, float worldRadius, Color neonColor, float duration)
    {
        var root = new GameObject("CorePulseWave");
        root.transform.position = new Vector3(pos.x, pos.y, -0.12f);

        Color hdr = NeonColors.Active.ForParticle(neonColor, 1.65f);
        Color ice = Color.Lerp(hdr, Color.white, 0.35f);

        // 柔光填充盘（由中心胀开后淡出）
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        var fillSr = fillGo.AddComponent<SpriteRenderer>();
        fillSr.sprite = MakeDiscSprite(48);
        fillSr.material = new Material(Shader.Find("Sprites/Default"));
        fillSr.color = new Color(ice.r, ice.g, ice.b, 0.4f);
        fillSr.sortingOrder = 16;

        // 主圆环（高分段，看起来是圆不是八角）
        var ringGo = new GameObject("SoftRing");
        ringGo.transform.SetParent(root.transform, false);
        var ringLr = BuildLoopLine(ringGo, 48, 0.2f, 18);
        ringLr.material = new Material(Shader.Find("Sprites/Default"));

        // 滞后外环
        var outerGo = new GameObject("OuterRing");
        outerGo.transform.SetParent(root.transform, false);
        var outerLr = BuildLoopLine(outerGo, 48, 0.08f, 17);
        outerLr.material = new Material(Shader.Find("Sprites/Default"));

        // 中心爆发粒子
        SpawnHit(pos, neonColor, 1.35f);

        float startScale = 0.08f;
        float endScale = Mathf.Max(worldRadius, 0.9f);
        duration = Mathf.Max(0.12f, duration);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / duration);
            float ringScale = Mathf.Lerp(startScale, endScale, p);
            ringGo.transform.localScale = Vector3.one * ringScale;

            float outerP = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t / duration) * 0.85f));
            float outerScale = Mathf.Lerp(startScale * 0.6f, endScale * 1.08f, outerP);
            outerGo.transform.localScale = Vector3.one * outerScale;

            float fillScale = Mathf.Lerp(0.15f, endScale * 0.95f, p);
            fillGo.transform.localScale = Vector3.one * fillScale;

            float alpha = 1f - p * p;
            fillSr.color = new Color(ice.r, ice.g, ice.b, 0.38f * alpha);

            float ringW = Mathf.Lerp(0.28f, 0.04f, p);
            ringLr.startWidth = ringW;
            ringLr.endWidth = ringW;
            var ringC = new Color(hdr.r, hdr.g, hdr.b, alpha);
            ringLr.startColor = ringC;
            ringLr.endColor = ringC;

            float outerW = Mathf.Lerp(0.12f, 0.03f, outerP);
            outerLr.startWidth = outerW;
            outerLr.endWidth = outerW;
            var outerC = new Color(ice.r, ice.g, ice.b, alpha * 0.65f);
            outerLr.startColor = outerC;
            outerLr.endColor = outerC;

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
        _dissolvePS = BuildDissolvePS(withFloorBounce: false);
        _bottomShatterPS = BuildDissolvePS(withFloorBounce: true);
    }

    private ParticleSystem BuildDissolvePS(bool withFloorBounce)
    {
        string name = withFloorBounce ? "ImpactPS_BottomShatter" : "ImpactPS_Dissolve";
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = !withFloorBounce; // Boss 躲顿帧；触底跟玩法时间走
        main.startLifetime = withFloorBounce ? 0.9f : 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.1f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        // 触底重力略大，落地后弹跳弧可读
        main.gravityModifier = withFloorBounce ? 0.85f : 0.35f;
        main.maxParticles = withFloorBounce ? 900 : 800;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        if (withFloorBounce)
        {
            // 保持速度倍率，避免把碰撞反弹也压没
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.06f, 0.2f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0.85f)));
        }
        else
        {
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.05f),
                    new Keyframe(0.08f, 0.05f),
                    new Keyframe(0.22f, 1.15f),
                    new Keyframe(0.55f, 0.55f),
                    new Keyframe(1f, 0.08f)));
        }

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            withFloorBounce
                ? new AnimationCurve(
                    new Keyframe(0f, 0.9f),
                    new Keyframe(0.1f, 1.05f),
                    new Keyframe(0.55f, 0.95f),
                    new Keyframe(0.82f, 0.55f),
                    new Keyframe(1f, 0f))
                : new AnimationCurve(
                    new Keyframe(0f, 0.85f),
                    new Keyframe(0.12f, 1.05f),
                    new Keyframe(0.7f, 1f),
                    new Keyframe(1f, 0f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        if (withFloorBounce)
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.2f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(0.7f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.15f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0.85f, 0.78f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        if (withFloorBounce)
            SetupFloorBounceCollision(ps);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = _particleMat;
        rend.sortingOrder = 11;

        return ps;
    }

    /// <summary>
    /// 底线无限平面碰撞：轻弹 + 阻尼 + 每次撞线削寿命 → 缓冲后消失。
    /// </summary>
    private void SetupFloorBounceCollision(ParticleSystem ps)
    {
        if (_bouncePlane == null)
        {
            var planeGo = new GameObject("ImpactBouncePlane");
            planeGo.transform.SetParent(transform);
            planeGo.transform.position = new Vector3(0f, MinionLineRules.GetAttackLineY(), 0f);
            planeGo.transform.rotation = Quaternion.identity;
            _bouncePlane = planeGo.transform;
        }

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.bounce = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
        collision.dampen = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        // 每次落地削掉剩余寿命，跳一两下后自然消掉
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        collision.minKillSpeed = 0.15f;
        collision.maxKillSpeed = 10000f;
        collision.radiusScale = 0.65f;
        collision.enableDynamicColliders = false;
        collision.SetPlane(0, _bouncePlane);
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
        main.useUnscaledTime = true; // 顿帧时粒子仍正常消亡
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
