using UnityEngine;

/// <summary>
/// 敌人霜痕视觉驱动：读取 Frost 层数并写入 Sprite Shader（MPB）。
/// 电荷由独立的 EnemyChargeArcVisual 网格绘制；两者不改战斗逻辑。
/// </summary>
[DisallowMultipleComponent]
public class EnemyBuildStackVisual : MonoBehaviour
{
    private static readonly int FrostStackId = Shader.PropertyToID("_FrostStack");
    private static readonly int EventPulseId = Shader.PropertyToID("_EventPulse");
    private static readonly int ConsumePulseId = Shader.PropertyToID("_ConsumePulse");
    private static readonly int FreezeAmountId = Shader.PropertyToID("_FreezeAmount");
    private static readonly int PatternSeedId = Shader.PropertyToID("_PatternSeed");
    private static readonly int SpriteUVRectId = Shader.PropertyToID("_SpriteUVRect");

    private static Material _sharedMat;

    private SpriteRenderer _sr;
    private MaterialPropertyBlock _mpb;
    private float _eventPulse;
    private float _consumePulse;
    private int _lastFrost = -1;
    private Sprite _lastSprite;
    private Vector4 _spriteUVRect = new Vector4(0f, 0f, 1f, 1f);
    private float _patternSeed;

    public static Material SharedMaterial
    {
        get
        {
            if (_sharedMat == null)
            {
                var sh = Shader.Find("Custom/EnemyBuildStack");
                if (sh == null)
                {
                    Debug.LogWarning("[EnemyBuildStackVisual] Shader Custom/EnemyBuildStack missing.");
                    return null;
                }
                _sharedMat = new Material(sh) { name = "EnemyBuildStack_Shared" };
            }
            return _sharedMat;
        }
    }

    /// <summary>确保敌人挂上霜痕材质和独立的电荷表现层。</summary>
    public static EnemyBuildStackVisual EnsureOn(EnemyBase enemy)
    {
        if (enemy == null) return null;
        if (!enemy.TryGetComponent(out EnemyBuildStackVisual vis))
            vis = enemy.gameObject.AddComponent<EnemyBuildStackVisual>();
        vis.EnsureMaterial();
        EnemyChargeArcVisual.EnsureOn(enemy);
        return vis;
    }

private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _patternSeed = (GetInstanceID() & 1023) / 1023f;
        EnsureMaterial();
    }

    private void OnEnable() => EnsureMaterial();

    public void EnsureMaterial()
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null) return;

        var mat = SharedMaterial;
        if (mat == null) return;
        if (_sr.sharedMaterial != mat)
            _sr.sharedMaterial = mat;
    }

private void LateUpdate()
    {
        if (_sr == null)
        {
            EnsureMaterial();
            if (_sr == null) return;
        }

        int frost = 0;
        float freeze = 0f;
        if (TryGetComponent(out EnemyFrostState frostSt))
        {
            frost = frostSt.MarkStacks;
            freeze = frostSt.IsFrozen ? 1f : 0f;
        }
        if (frost != _lastFrost)
            SetFrostLevel(frost);

        if (_eventPulse > 0f)
            _eventPulse = Mathf.Max(0f, _eventPulse - Time.deltaTime / 0.12f);
        if (_consumePulse > 0f)
            _consumePulse = Mathf.Max(0f, _consumePulse - Time.deltaTime / 0.15f);
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        UpdateSpriteUVRect();
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(FrostStackId, _lastFrost);
        _mpb.SetFloat(EventPulseId, _eventPulse);
        _mpb.SetFloat(ConsumePulseId, _consumePulse);
        _mpb.SetFloat(FreezeAmountId, freeze);
        _mpb.SetFloat(PatternSeedId, _patternSeed);
        _mpb.SetVector(SpriteUVRectId, _spriteUVRect);
        _sr.SetPropertyBlock(_mpb);
    }

public void SetFrostLevel(int level)
    {
        int next = Mathf.Clamp(level, 0, EnemyFrostState.MarkCap);
        if (_lastFrost >= 0)
        {
            if (next > _lastFrost) _eventPulse = 1f;
            if (next < _lastFrost) _consumePulse = 1f;
        }
        _lastFrost = next;
    }

private void UpdateSpriteUVRect()
    {
        Sprite sprite = _sr.sprite;
        if (_lastSprite == sprite) return;
        _lastSprite = sprite;
        _spriteUVRect = new Vector4(0f, 0f, 1f, 1f);
        if (sprite == null) return;
        Vector2[] uv = sprite.uv;
        if (uv == null || uv.Length == 0) return;
        Vector2 min = uv[0];
        Vector2 max = uv[0];
        for (int i = 1; i < uv.Length; i++)
        {
            min = Vector2.Min(min, uv[i]);
            max = Vector2.Max(max, uv[i]);
        }
        _spriteUVRect = new Vector4(min.x, min.y, max.x, max.y);
    }



    /// <summary>层数突变时立刻同步（供 DevFill / 批量灌层）。</summary>
    public void ForceRefresh()
    {
        _eventPulse = 1f;
        _lastFrost = -1;
    }
}
