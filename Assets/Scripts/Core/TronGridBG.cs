using UnityEngine;

/// <summary>
/// 运行时程序生成 Tron 风格电路格栅背景纹理，应用到 SpriteRenderer。
/// 支持慢速脉冲发光效果。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TronGridBG : MonoBehaviour
{
    [Header("Grid Settings")]
    public int   texWidth    = 512;
    public int   texHeight   = 1024;
    public int   cellPixels  = 32;     // 每格大小（px）
    public int   lineWidth   = 2;      // 格线宽（px）

    [Header("Colors")]
    public Color bgColor   = new Color(0.003f, 0.005f, 0.015f, 1f);
    public Color lineColor = new Color(0.022f, 0.08f,  0.25f,  1f);

    [Header("Pulse")]
    public float pulseSpeed = 0.5f;
    public float pulseAmp   = 0.22f;   // 格线亮度呼吸幅度

    private SpriteRenderer _sr;
    private Material       _mat;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = -100;   // 最后面，不遮挡任何游戏对象

        // 生成格栅纹理
        var tex = GenerateGrid();
        _sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0.5f), 100f);

        // 实例化材质（用于脉冲颜色动画）
        _mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        _sr.sharedMaterial = _mat;
    }

    private void Update()
    {
        // 缓慢脉动：调整 SpriteRenderer 颜色亮度
        float pulse = 1f + pulseAmp * Mathf.Sin(Time.time * pulseSpeed);
        _sr.color = new Color(pulse, pulse, pulse, 1f);
    }

    private Texture2D GenerateGrid()
    {
        var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;

        Color32 bg   = bgColor;
        Color32 line = lineColor;

        for (int y = 0; y < texHeight; y++)
        for (int x = 0; x < texWidth;  x++)
        {
            int lx = x % cellPixels;
            int ly = y % cellPixels;
            bool isLine = lx < lineWidth || ly < lineWidth;
            tex.SetPixel(x, y, isLine ? line : bg);
        }
        tex.Apply();
        return tex;
    }
}
