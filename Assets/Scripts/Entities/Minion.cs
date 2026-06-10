using UnityEngine;

public class Minion : EnemyBase
{
    public MinionDefinition definition;

    private SpriteRenderer _sr;
    private Color _baseColor;

    // ── Steering 参数 ────────────────────────────────────────────────────
    private const float LookAhead   = 2.2f;
    private const float SideLook    = 1.1f;
    private const float ProbeAngle  = 35f;
    private const float SepRadius   = 1.8f;
    private const float SepForce    = 2.2f;
    private const float SepInDesiredWeight = 2.5f;
    private const float AvoidWeight = 6.0f;
    private const float SteerLerp   = 12f;
    private const float StuckDur    = 0.35f;
    private const float EscapeForce = 3.5f;
    private const float PeerLookAhead = 1.5f;
    private const float PeerLookRadius = 1.2f;

    private float _stuckTimer = 0f;
    private float _avoidBias  = 0f;
    private float _colRadius  = 0.38f;
    private static readonly RaycastHit2D[] _rayBuf = new RaycastHit2D[8];
    private static readonly Collider2D[]   _sepBuf = new Collider2D[20];

    public void Initialize(MinionDefinition def, int waveIndex = 0)
    {
        definition              = def;
        float hpMult            = EndlessWaveScaling.GetMinionHpMultiplier(waveIndex);
        float spdMult           = EndlessWaveScaling.GetMinionSpeedMultiplier(waveIndex);
        maxHits                 = Mathf.Max(1, Mathf.RoundToInt(def.maxHP * hpMult));
        moveSpeed               = def.moveSpeed * spdMult;
        scoreOnHit              = def.scoreOnHit;
        scoreOnKill             = def.scoreOnKill;
        damageToPlayer          = def.damageToPlayer;
        isBomber                = def.isBomber;
        bomberDisableDuration   = def.bomberDisableDuration;
        checkBottomLine         = true;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        _sr.material = CyberVisualFactory.UnlitMaterial;

        if (def.sprite != null)
        {
            _sr.sprite = def.sprite;
            float spriteWidth = def.sprite.rect.width / def.sprite.pixelsPerUnit;
            if (spriteWidth > 0f)
            {
                float targetScale = 0.9f / spriteWidth;
                transform.localScale = new Vector3(targetScale, targetScale, 1f);

                var circleCol = GetComponent<CircleCollider2D>();
                if (circleCol != null)
                    circleCol.radius = (0.42f / 0.9f) * spriteWidth;
            }
        }
        else
        {
            _sr.sprite = CyberVisualFactory.CreateMinionSprite(def.baseColor, def.isBomber);
            transform.localScale = Vector3.one;

            var circleCol = GetComponent<CircleCollider2D>();
            if (circleCol != null)
                circleCol.radius = 0.42f;
        }

        _baseColor   = NeonColors.ApplyMinionBase(def.baseColor);
        BaseColor    = _baseColor;
        MainSR       = _sr;
        _sr.color    = _baseColor;
        _sr.sortingOrder = 2;

        var healthBar = GetComponent<MinionHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<MinionHealthBar>();
        float barW = def.healthBarWidthScale > 0.01f ? def.healthBarWidthScale : (maxHits > 1 ? 0.88f : 0.58f);
        healthBar.Configure(true, def.healthBarVisibleDuration, 0f, barW);

        if (GetComponent<MinionFallPreview>() == null)
            gameObject.AddComponent<MinionFallPreview>();
    }

    protected override void ApplyMovement()
    {
        if (_rb == null) return;

        float speed = moveSpeed * WaveManager.MinionSpeedMultiplier;

        Vector2 desired = Vector2.down * speed;
        desired += ComputeAvoidance(speed) * AvoidWeight;
        desired += ComputeSeparationVelocity(speed) * SepInDesiredWeight;

        desired = Vector2.ClampMagnitude(desired, speed * 3.5f);

        Vector2 vel = Vector2.Lerp(_rb.velocity, desired, SteerLerp * Time.fixedDeltaTime);
        _rb.velocity = vel;

        ApplySeparationForce();
        HandleStuck(vel, speed);
    }

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

        if (plHit && plHit.distance < SideLook)
        {
            float s = 1f - (plHit.distance / SideLook);
            force += Vector2.right * s * speed * 1.5f;
        }
        if (prHit && prHit.distance < SideLook)
        {
            float s = 1f - (prHit.distance / SideLook);
            force += Vector2.left  * s * speed * 1.5f;
        }

        if (cHit && cHit.distance < LookAhead)
        {
            float strength = 1f - (cHit.distance / LookAhead);
            float lDist    = lHit ? lHit.distance : LookAhead;
            float rDist    = rHit ? rHit.distance : LookAhead;
            float totalClr = lDist + rDist;
            float instantDir = totalClr > 0.001f
                ? (rDist - lDist) / totalClr
                : (pos.x > 0f ? -1f : 1f);

            bool sameSign = (Mathf.Sign(instantDir) == Mathf.Sign(_avoidBias)) || Mathf.Abs(_avoidBias) < 0.2f;
            float lerpK   = sameSign ? Time.fixedDeltaTime * 10f : Time.fixedDeltaTime * 2.0f;
            _avoidBias    = Mathf.Lerp(_avoidBias, instantDir, lerpK);

            float useDir  = Mathf.Abs(_avoidBias) > 0.05f ? _avoidBias : instantDir;
            force += Vector2.right * useDir * strength * speed;
        }
        else
        {
            _avoidBias = Mathf.Lerp(_avoidBias, 0f, Time.fixedDeltaTime * 4f);
        }

        force += ComputePeerAvoidance(speed);

        return force;
    }

    private Vector2 ComputePeerAvoidance(float speed)
    {
        Vector2 pos = _rb.position;
        Vector2 steer = Vector2.zero;

        RaycastHit2D peerDown = ProbeHitPeers(pos, Vector2.down, PeerLookAhead);
        if (peerDown.collider != null)
        {
            float strength = 1f - (peerDown.distance / PeerLookAhead);
            float side = Mathf.Sign(pos.x - peerDown.collider.transform.position.x);
            if (Mathf.Abs(side) < 0.01f) side = pos.x > 0f ? 1f : -1f;
            steer += Vector2.right * side * strength * speed * 1.4f;
        }

        int n = Physics2D.OverlapCircleNonAlloc(pos, PeerLookRadius, _sepBuf);
        for (int i = 0; i < n; i++)
        {
            var c = _sepBuf[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (!c.CompareTag("Enemy")) continue;

            Vector2 toPeer = (Vector2)c.transform.position - pos;
            float dist = toPeer.magnitude;
            if (dist < 0.001f || dist > PeerLookRadius) continue;

            if (toPeer.y < 0.4f)
            {
                float w = 1f - dist / PeerLookRadius;
                steer += new Vector2(Mathf.Sign(toPeer.x) * w * speed * 0.8f, 0f);
            }
        }

        return steer;
    }

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

    private RaycastHit2D ProbeHitPeers(Vector2 origin, Vector2 dir, float maxDist)
    {
        float castRadius = _colRadius;
        int n = Physics2D.CircleCastNonAlloc(origin, castRadius, dir, _rayBuf, maxDist);
        RaycastHit2D best = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = _rayBuf[i];
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.collider.gameObject == gameObject) continue;
            if (!h.collider.CompareTag("Enemy")) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
            }
        }
        return best;
    }

    private Vector2 ComputeSeparationVelocity(float speed)
    {
        Vector2 pos  = _rb.position;
        int     n    = Physics2D.OverlapCircleNonAlloc(pos, SepRadius, _sepBuf);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            var c = _sepBuf[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (!c.CompareTag("Enemy")) continue;

            Vector2 diff = pos - (Vector2)c.transform.position;
            float   dist = diff.magnitude;
            if (dist < 0.001f) { push += (Vector2)Random.insideUnitCircle.normalized; continue; }

            float weight = Mathf.Clamp01(1f - dist / SepRadius);
            push += diff.normalized * weight;
        }

        if (push.sqrMagnitude < 0.001f) return Vector2.zero;
        return Vector2.ClampMagnitude(push.normalized * speed, speed * 2.2f);
    }

    private void ApplySeparationForce()
    {
        Vector2 pos  = _rb.position;
        int     n    = Physics2D.OverlapCircleNonAlloc(pos, SepRadius, _sepBuf);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            var c = _sepBuf[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (!c.CompareTag("Enemy")) continue;

            Vector2 diff = pos - (Vector2)c.transform.position;
            float   dist = diff.magnitude;
            if (dist < 0.001f) { push += (Vector2)Random.insideUnitCircle.normalized; continue; }

            float weight = Mathf.Clamp01(1f - dist / SepRadius);
            push += diff.normalized * weight;
        }

        if (push.sqrMagnitude > 0.001f)
            _rb.AddForce(Vector2.ClampMagnitude(push, 2.8f) * SepForce, ForceMode2D.Impulse);
    }

    private int CountNearbyEnemies(float radius)
    {
        int n = Physics2D.OverlapCircleNonAlloc(_rb.position, radius, _sepBuf);
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            var c = _sepBuf[i];
            if (c == null || c.gameObject == gameObject) continue;
            if (c.CompareTag("Enemy")) count++;
        }
        return count;
    }

    private void HandleStuck(Vector2 vel, float speed)
    {
        float thresh = speed * 0.15f;
        int crowded = CountNearbyEnemies(SepRadius);
        float stuckThreshold = crowded >= 3 ? StuckDur * 0.45f : StuckDur;

        if (vel.sqrMagnitude < thresh * thresh)
        {
            _stuckTimer += Time.fixedDeltaTime;
            if (_stuckTimer >= stuckThreshold)
            {
                float dir = (transform.position.x > 0f) ? -1f : 1f;
                float push = crowded >= 3 ? EscapeForce * 1.4f : EscapeForce;
                _rb.AddForce(new Vector2(dir * push, push * 0.35f), ForceMode2D.Impulse);
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
