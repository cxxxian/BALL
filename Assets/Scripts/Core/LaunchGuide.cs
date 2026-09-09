using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaunchGuide : MonoBehaviour
{
    public static LaunchGuide Instance { get; private set; }

    private LineRenderer _lr;
    private bool _visible;
    private bool _urgent;

    private static readonly Color NormalStart = Color.white;
    private static readonly Color NormalEnd   = new Color(0.5f, 0.8f, 1f);
    private static readonly Color UrgentStart = new Color(1f, 0.75f, 0.2f);
    private static readonly Color UrgentEnd   = new Color(1f, 0.35f, 0.15f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _lr = GetComponent<LineRenderer>();
        SetupRenderer();
        Hide();
    }

    private void SetupRenderer()
    {
        _lr.useWorldSpace = true;
        _lr.startWidth = 0.07f;
        _lr.endWidth = 0.02f;
        _lr.numCapVertices = 4;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;

        // 虚线纹理（4px 白 + 4px 透明，循环铺贴）
        var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        for (int i = 0; i < 8; i++)
            tex.SetPixel(i, 0, i < 5 ? Color.white : Color.clear);
        tex.Apply();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        _lr.material = mat;
        _lr.textureMode = LineTextureMode.Tile;

        ApplyGradient(false);
    }

    /// <summary>决定窗末段：瞄准线切到暖色紧迫态。</summary>
    public void SetUrgency(bool urgent)
    {
        if (_urgent == urgent) return;
        _urgent = urgent;
        ApplyGradient(urgent);
    }

    private void ApplyGradient(bool urgent)
    {
        var start = urgent ? UrgentStart : NormalStart;
        var end   = urgent ? UrgentEnd   : NormalEnd;
        float a0  = urgent ? 1f : 0.9f;

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(0f, 1f) }
        );
        _lr.colorGradient = grad;
    }

    public void Show(Vector2 origin, Vector2 direction)
    {
        _visible = true;
        _lr.enabled = true;
        Rebuild(origin, direction);
    }

    public void UpdateDirection(Vector2 origin, Vector2 direction)
    {
        if (_visible) Rebuild(origin, direction);
    }

    public void Hide()
    {
        _visible = false;
        _lr.enabled = false;
        SetUrgency(false);
    }

    private void Rebuild(Vector2 origin, Vector2 dir)
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        int count  = cfg != null ? cfg.launchGuideDots : 20;
        float len  = cfg != null ? cfg.launchGuideLength : 6f;

        _lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector2 p = origin + dir.normalized * (len * t);
            _lr.SetPosition(i, new Vector3(p.x, p.y, -0.5f));
        }

        // 铺贴缩放：控制虚线密度
        _lr.material.mainTextureScale = new Vector2(len * 1.5f, 1f);
    }
}
