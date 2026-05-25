using System.Collections;
using UnityEngine;

public class Minion : EnemyBase
{
    public MinionDefinition definition;

    private SpriteRenderer _sr;
    private Color _baseColor;

    // ── Steering 参数 ────────────────────────────────────────────────────
    private const float LookAhead   = 2.2f;   // 下方前瞻距离（CircleCast 扫射距离）
    private const float SideLook    = 1.1f;   // 侧向探针距离
    private const float ProbeAngle  = 35f;    // 斜向探针角度（度）
    private const float SepRadius   = 1.3f;   // 相互排斥检测半径
    private const float SepForce    = 1.2f;   // 分离冲力
    private const float AvoidWeight = 6.0f;   // 绕行力权重（进一步加强）
    private const float SteerLerp   = 12f;    // 速度平滑系数（加快响应，迅速滑开）
    private const float StuckDur    = 0.35f;  // 判定卡死的持续时间下调，更敏捷
    private const float EscapeForce = 3.0f;   // 卡死逃脱冲力加大

    private float _stuckTimer = 0f;
    private float _avoidBias  = 0f;  // 持久方向偏好 [-1,1]
    private float _colRadius  = 0.38f; // 扫射探针半径（略小于小兵自身 0.42f 圆形 Collider）
    private static readonly RaycastHit2D[] _rayBuf = new RaycastHit2D[8];
    private static readonly Collider2D[]   _sepBuf = new Collider2D[16];

    public void Initialize(MinionDefinition def)
    {
        definition              = def;
        maxHits                 = def.maxHP;
        moveSpeed               = def.moveSpeed;
        scoreOnHit              = def.scoreOnHit;
        scoreOnKill             = def.scoreOnKill;
        damageToPlayer          = def.damageToPlayer;
        isBomber                = def.isBomber;
        bomberDisableDuration   = def.bomberDisableDuration;
        checkBottomLine         = true;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        // 强制使用 Unlit 材质，保证 100% 亮度输出
        _sr.material = CyberVisualFactory.UnlitMaterial;

        if (def.sprite != null)
        {
            _sr.sprite = def.sprite;
            float spriteWidth = def.sprite.rect.width / def.sprite.pixelsPerUnit;
            if (spriteWidth > 0f)
            {
                float targetScale = 0.9f / spriteWidth;
                transform.localScale = new Vector3(targetScale, targetScale, 1f);

                // 配合 localScale 缩放动态调整 Collider 半径，确保在世界坐标下的实际碰撞直径始终为 0.84f (半径 0.42f)
                var circleCol = GetComponent<CircleCollider2D>();
                if (circleCol != null)
                {
                    circleCol.radius = (0.42f / 0.9f) * spriteWidth;
                }
            }
        }
        else
        {
            _sr.sprite = CyberVisualFactory.CreateMinionSprite(def.baseColor, def.isBomber);
            transform.localScale = Vector3.one;

            var circleCol = GetComponent<CircleCollider2D>();
            if (circleCol != null)
            {
                circleCol.radius = 0.42f; // 降级回退标准半径
            }
        }

        _baseColor   = def.baseColor;
        _sr.color    = _baseColor;
        _sr.sortingOrder = 2;
    }

    protected override void OnHit()
    {
        if (_sr == null) return;
        StopAllCoroutines();
        StartCoroutine(FlashWhite());
    }

    private IEnumerator FlashWhite()
    {
        _sr.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        if (_sr != null)
            _sr.color = Color.Lerp(_baseColor, Color.white, (float)CurrentHits / maxHits);
    }

    // ── 4 层转向行为 ─────────────────────────────────────────────────────

    protected override void ApplyMovement()
    {
        if (_rb == null) return;

        float speed = moveSpeed * WaveManager.MinionSpeedMultiplier;

        // 第 1 层：向下驱动（始终存在的基础方向）
        Vector2 desired = Vector2.down * speed;

        // 第 2 层：障碍探测绕行
        desired += ComputeAvoidance(speed) * AvoidWeight;

        // 限速（绕行力保留，不受分离力截断）
        desired = Vector2.ClampMagnitude(desired, speed * 3.0f);

        // 平滑合成，避免帧间速度跳变
        Vector2 vel = Vector2.Lerp(_rb.velocity, desired, SteerLerp * Time.fixedDeltaTime);
        _rb.velocity = vel;

        // 第 3 层：个体分离 — 直接 AddForce，绕过速度截断，立即作用
        ApplySeparationForce();

        // 第 4 层：卡死检测与逃脱
        HandleStuck(vel, speed);
    }

    // 障碍绕行：5 条探针（正下 + 斜向 + 纯左右），用表面法线方向精准绕过 Bumper
    private Vector2 ComputeAvoidance(float speed)
    {
        Vector2 pos = _rb.position;
        float   rad = ProbeAngle * Mathf.Deg2Rad;

        RaycastHit2D cHit  = ProbeHit(pos, Vector2.down,                                        LookAhead);
        RaycastHit2D lHit  = ProbeHit(pos, new Vector2(-Mathf.Sin(rad), -Mathf.Cos(rad)),        LookAhead);
        RaycastHit2D rHit  = ProbeHit(pos, new Vector2( Mathf.Sin(rad), -Mathf.Cos(rad)),        LookAhead);
        RaycastHit2D plHit = ProbeHit(pos, Vector2.left,                                         SideLook, 0.15f);
        RaycastHit2D prHit = ProbeHit(pos, Vector2.right,                                        SideLook, 0.15f);

        Vector2 force = Vector2.zero;

        // 侧面 Bumper：侧向检测也使用 CircleCast（半径 0.15f 的细管检测），防止贴在侧面卡边
        if (plHit && plHit.distance < SideLook)
        {
            float s = 1f - (plHit.distance / SideLook);
            force += Vector2.right * s * speed * 1.5f; // 加大侧边推开力度
        }
        if (prHit && prHit.distance < SideLook)
        {
            float s = 1f - (prHit.distance / SideLook);
            force += Vector2.left  * s * speed * 1.5f;
        }

        // 前方 Bumper：当身体扫射探测到阻碍
        if (cHit && cHit.distance < LookAhead)
        {
            float strength = 1f - (cHit.distance / LookAhead);
            float lDist    = lHit ? lHit.distance : LookAhead;
            float rDist    = rHit ? rHit.distance : LookAhead;
            float totalClr = lDist + rDist;
            float instantDir = totalClr > 0.001f
                ? (rDist - lDist) / totalClr
                : (pos.x > 0f ? -1f : 1f);

            // 方向粘滞：同向时快速跟进，异向时缓慢更新，防止探针频繁来回跳变
            bool sameSign = (Mathf.Sign(instantDir) == Mathf.Sign(_avoidBias)) || Mathf.Abs(_avoidBias) < 0.2f;
            float lerpK   = sameSign ? Time.fixedDeltaTime * 10f : Time.fixedDeltaTime * 2.0f;
            _avoidBias    = Mathf.Lerp(_avoidBias, instantDir, lerpK);

            float useDir  = Mathf.Abs(_avoidBias) > 0.05f ? _avoidBias : instantDir;

            // 纯横向推力，推力大小与距离成反比
            force += Vector2.right * useDir * strength * speed;
        }
        else
        {
            _avoidBias = Mathf.Lerp(_avoidBias, 0f, Time.fixedDeltaTime * 4f); // 无障碍时缓慢重置
        }

        return force;
    }

    // 圆体投影扫射（CircleCast），拥有与小兵几乎同等宽度的视野，100% 防止盲区
    private RaycastHit2D ProbeHit(Vector2 origin, Vector2 dir, float maxDist, float customRadius = -1f)
    {
        float castRadius = customRadius > 0f ? customRadius : _colRadius;
        int n = Physics2D.CircleCastNonAlloc(origin, castRadius, dir, _rayBuf, maxDist);
        for (int i = 0; i < n; i++)
        {
            var h = _rayBuf[i];
            if (h.collider == null || h.collider.isTrigger)  continue;
            if (h.collider.gameObject == gameObject)          continue;
            string tag = h.collider.tag;
            if (tag == "Enemy" || tag == "Ball")              continue;
            return h;
        }
        return default;
    }

    // 个体分离：直接对 Rigidbody2D 施加冲力，不经速度截断，距离越近推力越强
    private void ApplySeparationForce()
    {
        Vector2 pos  = _rb.position;
        int     n    = Physics2D.OverlapCircleNonAlloc(pos, SepRadius, _sepBuf);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            var c = _sepBuf[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (!c.CompareTag("Enemy"))                  continue;

            Vector2 diff = pos - (Vector2)c.transform.position;
            float   dist = diff.magnitude;
            if (dist < 0.001f) { push += (Vector2)Random.insideUnitCircle.normalized; continue; }

            float weight = Mathf.Clamp01(1f - dist / SepRadius);
            push += diff.normalized * weight;
        }

        if (push.sqrMagnitude > 0.001f)
            _rb.AddForce(Vector2.ClampMagnitude(push, 2f) * SepForce, ForceMode2D.Impulse);
    }

    // 卡死检测：持续低速超过阈值时给一个侧向冲力，朝画面中央方向逃脱
    private void HandleStuck(Vector2 vel, float speed)
    {
        float thresh = speed * 0.15f;
        if (vel.sqrMagnitude < thresh * thresh)
        {
            _stuckTimer += Time.fixedDeltaTime;
            if (_stuckTimer >= StuckDur)
            {
                float dir = (transform.position.x > 0f) ? -1f : 1f;
                _rb.AddForce(new Vector2(dir * EscapeForce, EscapeForce * 0.3f), ForceMode2D.Impulse);
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
    }

    public static Sprite GenerateCircleSprite(int size, Color color)
    {
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        float r    = half - 1.5f;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float dx    = (i % size) - half + 0.5f;
            float dy    = (i / size) - half + 0.5f;
            float dist  = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = dist <= r ? 1f : 0f;
            pixels[i]   = new Color(color.r, color.g, color.b, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        float ppu = size / 0.55f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
    }
}
