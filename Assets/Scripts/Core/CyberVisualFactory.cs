using UnityEngine;

/// <summary>
/// 赛博朋克极简发光符号贴图（运行时像素级绘制）
/// 彻底告别“全是圆球”，赋予每个对象独特的发光科技纹理。
/// </summary>
public static class CyberVisualFactory
{
    private static Material _unlitMaterial;
    public static Material UnlitMaterial
    {
        get
        {
            if (_unlitMaterial == null)
                _unlitMaterial = new Material(Shader.Find("Sprites/Default"));
            return _unlitMaterial;
        }
    }

    // ── 1. 敌方小兵：警告倒三角 (32x32) ───────────────────────────
    public static Sprite CreateMinionSprite(Color baseColor, bool isBomber)
    {
        int sz = 48;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;

                // 爆弹兵：画核辐射警告标志
                if (isBomber)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > half - 2f)
                    {
                        px[y * sz + x] = Color.clear;
                    }
                    else if (dist < 5f)
                    {
                        px[y * sz + x] = Color.white; // 中心核
                    }
                    else if (dist > 8f && dist < half - 5f)
                    {
                        // 扇区扇形 (120度分成三瓣)
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f; // 0-360
                        float m = angle % 120f;
                        if (m < 60f)
                            px[y * sz + x] = baseColor;
                        else
                            px[y * sz + x] = Color.clear;
                    }
                    else
                    {
                        px[y * sz + x] = Color.clear;
                    }
                }
                else
                {
                    // 普通兵：画空心外红、内亮色倒三角
                    // 三角形边界检测
                    float normalizedY = (float)y / sz; // 0 to 1
                    float xLimit = (1f - normalizedY) * half * 1.1f; // 上宽下窄

                    if (y > 6 && Mathf.Abs(dx) <= xLimit)
                    {
                        // 边界粗线
                        float innerLimit = (1f - ((float)(y - 4) / sz)) * half * 1.1f;
                        if (y < 12 || Mathf.Abs(dx) >= innerLimit - 3f)
                            px[y * sz + x] = baseColor;
                        else if (Mathf.Abs(dx) <= 3f || Mathf.Abs(dy) <= 3f)
                            px[y * sz + x] = baseColor * 0.4f; // 核心十字准星
                        else
                            px[y * sz + x] = Color.clear;
                    }
                    else
                    {
                        px[y * sz + x] = Color.clear;
                    }
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 0.5f);
    }

    // ── 2. Bumper (反弹器)：赛博科技发光双层环 (64x64) ────────────────
    public static Sprite CreateBumperSprite(Color neonColor)
    {
        int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 外环 (R = 26~30)
                if (dist >= 25f && dist <= 30f)
                {
                    px[y * sz + x] = neonColor;
                }
                // 内环 (R = 14~18)
                else if (dist >= 13f && dist <= 17f)
                {
                    px[y * sz + x] = neonColor * 1.3f; // 稍亮
                }
                // 放射状刻度线
                else if (dist > 18f && dist < 25f)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                    if (Mathf.Repeat(angle, 45f) < 8f)
                        px[y * sz + x] = neonColor * 0.7f;
                    else
                        px[y * sz + x] = Color.clear;
                }
                // 核心实心圆
                else if (dist <= 6f)
                {
                    px[y * sz + x] = Color.white;
                }
                else
                {
                    px[y * sz + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 0.75f);
    }

    // ── 3. SpringBoard (弹簧板)：斑马警告黄绿条纹 (64x32) ──────────────
    public static Sprite CreateSpringBoardSprite(Color neonColor)
    {
        int w = 64, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 绘制斜斑马线
                bool isStripe = ((x + y * 2) / 8) % 2 == 0;

                if (y == 0 || y == h - 1 || x == 0 || x == w - 1)
                {
                    px[y * w + x] = neonColor * 1.5f; // 强边框
                }
                else
                {
                    px[y * w + x] = isStripe ? neonColor : new Color(0f, 0.1f, 0.05f, 0.6f);
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w / 1.4f);
    }

    // ── 4. BoostGear (加速齿轮)：黄金飞齿形 (64x64) ─────────────────
    public static Sprite CreateBoostGearSprite(Color neonColor)
    {
        int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 算出极角以绘制齿轮齿
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                bool inTooth = Mathf.Repeat(angle, 30f) < 15f; // 12个齿

                float maxR = inTooth ? 28f : 21f;

                if (dist <= maxR && dist >= 16f)
                {
                    px[y * sz + x] = neonColor;
                }
                else if (dist >= 10f && dist <= 12f)
                {
                    px[y * sz + x] = Color.white; // 中心金色轴承圈
                }
                else
                {
                    px[y * sz + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 0.9f);
    }

    // ── 5. Portal (传送门)：太空中空旋涡环 (64x64) ─────────────────
    public static Sprite CreatePortalSprite(Color neonColor)
    {
        int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > half - 1f)
                {
                    px[y * sz + x] = Color.clear;
                }
                else
                {
                    // 绘制旋涡线：r 与 angle 联动
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                    float swirl = Mathf.Repeat(dist * 5f - angle, 120f);

                    if (dist >= 26f && dist <= 29f)
                    {
                        px[y * sz + x] = neonColor * 1.5f; // 精美外光边
                    }
                    else if (dist > 8f && swirl < 22f)
                    {
                        px[y * sz + x] = Color.Lerp(neonColor, Color.clear, (dist / half));
                    }
                    else
                    {
                        px[y * sz + x] = Color.clear;
                    }
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 0.9f);
    }

    // ── 6. ReflectivePrism (反射棱镜)：赛博双重晶体边框 (32x96) ────────
    public static Sprite CreatePrismSprite(Color neonColor)
    {
        int w = 24, h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 渐缩成尖顶折射晶体
                float edgeX = Mathf.Min(y * 0.5f, (h - y) * 0.5f);
                edgeX = Mathf.Clamp(edgeX, 0f, w * 0.5f);

                float dx = Mathf.Abs(x - (w * 0.5f));

                if (dx <= edgeX)
                {
                    // 外部边缘
                    if (dx >= edgeX - 2.5f)
                    {
                        px[y * w + x] = neonColor;
                    }
                    // 核心线
                    else if (dx <= 1.5f)
                    {
                        px[y * w + x] = Color.white;
                    }
                    else
                    {
                        px[y * w + x] = new Color(neonColor.r * 0.1f, neonColor.g * 0.1f, neonColor.b * 0.1f, 0.4f);
                    }
                }
                else
                {
                    px[y * w + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h / 1.2f);
    }

    // ── 7. EnergyCannon (能量炮台)：同心正八边形堡垒 (64x64) ────────────
    public static Sprite CreateCannonSprite(Color neonColor)
    {
        int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;

                // 转换成正八边形距离
                float d8 = Mathf.Max(
                    Mathf.Abs(dx) * 0.9238f + Mathf.Abs(dy) * 0.3826f,
                    Mathf.Abs(dy) * 0.9238f + Mathf.Abs(dx) * 0.3826f
                );

                if (d8 >= 26f && d8 <= 30f)
                {
                    px[y * sz + x] = neonColor * 1.5f; // 外围八边框
                }
                else if (d8 >= 14f && d8 <= 18f)
                {
                    px[y * sz + x] = neonColor; // 内八角
                }
                else if (d8 <= 6f)
                {
                    px[y * sz + x] = Color.white; // 核心射口
                }
                else
                {
                    px[y * sz + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 1.0f);
    }
}
