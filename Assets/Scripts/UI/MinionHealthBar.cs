using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MinionHealthBar : MonoBehaviour, IEnemyHealthBar
{
    [Header("Visibility")]
    [SerializeField] private float showDuration = 1.8f;

    [Header("Style")]
    [SerializeField] private Color fillColor = new Color(0f, 2.4f, 2f, 1f);
    [SerializeField] private Color bgColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);
    [SerializeField] private Color flashColor = Color.white;

    private const float WorldGapAboveSprite = 0.07f;

    private EnemyBase _enemy;
    private Transform _root;
    private SpriteRenderer _bgSr;
    private SpriteRenderer _fillSr;

    private float _visibleTimer;
    private float _hpPct = 1f;
    private float _displayAlpha;
    private float _barWidth = 0.62f;
    private float _barHeight = 0.09f;
    private Coroutine _flashRoutine;
    private bool _persistVisible;
    private float _persistAlpha = 0.95f;
    private float _widthScale = 1f;

    private bool ShouldPersistBar =>
        _persistVisible || (_enemy != null && _enemy.maxHits > 1);

    public void Configure(bool showOnSpawn, float visibleDuration, float yOffset, float widthScale)
    {
        _persistVisible = showOnSpawn;
        showDuration = Mathf.Max(0.1f, visibleDuration);
        _widthScale = widthScale > 0.01f ? widthScale : 1f;
        ApplyLayout();

        if (ShouldPersistBar)
            _displayAlpha = _persistAlpha;
        ApplySpriteAlpha(_displayAlpha);
    }

    private void ApplyLayout()
    {
        bool multiHp = _enemy != null && _enemy.maxHits > 1;
        _barWidth = (multiHp ? 0.82f : 0.62f) * _widthScale;
        _barHeight = multiHp ? 0.11f : 0.09f;
        _persistAlpha = multiHp ? 1f : 0.92f;

        SyncRootTransform();
        ApplyBarGeometry(_hpPct);
        ApplySpriteAlpha(_displayAlpha);
    }

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        BuildSprites();
        ApplyLayout();
    }

    public void Bind(EnemyBase enemy) => _enemy = enemy;

    private void BuildSprites()
    {
        var legacy = transform.Find("HealthBarCanvas");
        if (legacy != null) Destroy(legacy.gameObject);

        var existing = transform.Find("HealthBar");
        if (existing != null) Destroy(existing.gameObject);

        var rootObj = new GameObject("HealthBar");
        rootObj.transform.SetParent(transform, false);
        _root = rootObj.transform;

        _bgSr = CreateBarPart("Background", _root, bgColor, 20);
        _fillSr = CreateBarPart("Fill", _root, fillColor, 21);
    }

    private static SpriteRenderer CreateBarPart(string name, Transform parent, Color color, int sortOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.color = color;
        sr.sortingOrder = sortOrder;
        return sr;
    }

    private void LateUpdate()
    {
        if (_enemy == null || _enemy.IsDead || _root == null)
        {
            if (_root != null) _root.gameObject.SetActive(false);
            return;
        }

        _root.gameObject.SetActive(true);
        SyncRootTransform();

        float targetPct = _enemy.maxHits <= 0
            ? 0f
            : Mathf.Clamp01(1f - (float)_enemy.CurrentHits / _enemy.maxHits);
        _hpPct = Mathf.Lerp(_hpPct, targetPct, Time.deltaTime * 20f);
        ApplyBarGeometry(_hpPct);

        bool showBar = ShouldPersistBar || _visibleTimer > 0f;
        if (_visibleTimer > 0f) _visibleTimer -= Time.deltaTime;

        float targetAlpha = showBar
            ? (ShouldPersistBar
                ? (_enemy.CurrentHits == 0 ? _persistAlpha * 0.9f : _persistAlpha)
                : 1f)
            : 0f;

        if (ShouldPersistBar)
            _displayAlpha = Mathf.Max(_displayAlpha, targetAlpha);
        else
            _displayAlpha = Mathf.Lerp(_displayAlpha, targetAlpha, Time.deltaTime * 14f);

        ApplySpriteAlpha(_displayAlpha);
    }

    /// <summary>
    /// 抵消父级缩放，使血条始终以固定世界尺寸显示在小兵头顶。
    /// </summary>
    private void SyncRootTransform()
    {
        if (_root == null) return;

        var ls = transform.lossyScale;
        float invX = ls.x > 0.0001f ? 1f / ls.x : 1f;
        float invY = ls.y > 0.0001f ? 1f / ls.y : 1f;
        _root.localScale = new Vector3(invX, invY, 1f);

        float topLocal = 0.42f;
        var sr = _enemy != null ? _enemy.MainSR : null;
        if (sr != null && sr.sprite != null)
            topLocal = sr.sprite.bounds.extents.y;

        float gapLocal = ls.y > 0.0001f ? WorldGapAboveSprite / ls.y : WorldGapAboveSprite;
        _root.localPosition = new Vector3(0f, topLocal + gapLocal, -0.01f);
    }

    private void ApplyBarGeometry(float pct)
    {
        if (_bgSr == null || _fillSr == null) return;

        _bgSr.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        _bgSr.transform.localPosition = Vector3.zero;

        float fillW = Mathf.Max(_barWidth * pct, _barHeight * 0.4f);
        _fillSr.transform.localScale = new Vector3(fillW, _barHeight * 0.82f, 1f);
        _fillSr.transform.localPosition = new Vector3(-(_barWidth - fillW) * 0.5f, 0f, 0f);
    }

    private void ApplySpriteAlpha(float alpha)
    {
        if (_bgSr == null || _fillSr == null) return;

        var bg = bgColor;
        bg.a *= alpha;
        _bgSr.color = bg;

        var fill = fillColor;
        fill.a *= alpha;
        _fillSr.color = fill;
    }

    public void OnEnemyHit()
    {
        _visibleTimer = showDuration;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void OnEnemyDeath()
    {
        if (_root != null) _root.gameObject.SetActive(false);
    }

    private static Sprite _whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }

    private IEnumerator FlashRoutine()
    {
        if (_fillSr != null) _fillSr.color = flashColor;
        yield return new WaitForSeconds(0.08f);
        ApplySpriteAlpha(_displayAlpha);
        _flashRoutine = null;
    }
}
