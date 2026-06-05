using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MinionHealthBar : MonoBehaviour, IEnemyHealthBar
{
    [Header("Visibility")]
    [SerializeField] private float showDuration = 1.2f;
    [SerializeField] private float fadeSpeed = 10f;

    [Header("Style")]
    [SerializeField] private Color fillColor = new Color(0f, 1f, 0.95f, 1f); // Neon Cyan
    [SerializeField] private Color bgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color flashColor = Color.white;

    private EnemyBase _enemy;
    private CanvasGroup _canvasGroup;
    private RectTransform _fillRect;
    private Image _fillImage;
    
    private float _visibleTimer;
    private float _hpPct = 1f;
    private Coroutine _flashRoutine;
    private bool _visibleOnSpawn = false;

    public void Configure(bool showOnSpawn, float visibleDuration, float yOffset, float widthScale)
    {
        _visibleOnSpawn = showOnSpawn;
        showDuration = Mathf.Max(0.1f, visibleDuration);
        
        // 动态修改尺寸和位置
        if (_canvasGroup != null)
        {
            var canvasObj = _canvasGroup.gameObject;
            canvasObj.transform.localPosition = new Vector3(0f, yOffset, 0f);
            var rootRect = canvasObj.GetComponent<RectTransform>();
            if (rootRect != null)
                rootRect.sizeDelta = new Vector2(1.2f * widthScale, 0.08f);
        }
    }

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        BuildUI();
    }

    public void Bind(EnemyBase enemy)
    {
        _enemy = enemy;
    }

    private void BuildUI()
    {
        // 制作一个 Canvas (World Space)
        GameObject canvasObj = new GameObject("HealthBarCanvas");
        canvasObj.transform.SetParent(transform, false);
        
        // 挂载在小兵模型正下方或正上方（跟随移动）。这里选择正下方
        canvasObj.transform.localPosition = new Vector3(0f, -0.65f, 0f);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        RectTransform rootRect = canvasObj.GetComponent<RectTransform>();
        // 极简线条：高度 4-6 px。这里世界坐标大小设置一下
        rootRect.sizeDelta = new Vector2(1.2f, 0.08f);

        // 扣除的血量留下一条暗灰色的底槽
        var bg = CreateImage("Background", rootRect, bgColor);
        Stretch(bg.rectTransform);

        // 剩余血量用高亮青色 (Cyan) / 荧光绿
        var fill = CreateImage("Fill", bg.transform, fillColor);
        _fillImage = fill;
        _fillRect = fill.rectTransform;
        Stretch(_fillRect);
        _fillRect.pivot = new Vector2(0f, 0.5f); // 左对齐
    }

    private void LateUpdate()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            return;
        }

        // 满血时隐藏 (CurrentHits == 0)
        if (_enemy.CurrentHits == 0)
        {
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
            return;
        }

        // 更新血量百分比
        float targetPct = _enemy.maxHits <= 0 ? 0f : Mathf.Clamp01(1f - (float)_enemy.CurrentHits / _enemy.maxHits);
        _hpPct = Mathf.Lerp(_hpPct, targetPct, Time.deltaTime * 15f);

        if (_fillRect != null)
            _fillRect.anchorMax = new Vector2(_hpPct, 1f);

        if (_visibleTimer > 0f || _visibleOnSpawn)
        {
            if (_visibleTimer > 0f) _visibleTimer -= Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
        }
        else
        {
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 0f, Time.deltaTime * (fadeSpeed * 0.5f));
        }
    }

    public void OnEnemyHit()
    {
        _visibleTimer = showDuration;
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void OnEnemyDeath()
    {
        // 死亡时伴随特效碎裂消失。这里直接隐藏。可以在此处接入粒子特效
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.type = Image.Type.Sliced;
        return img;
    }

    private static Sprite _whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator FlashRoutine()
    {
        // 受击瞬间血条整体闪白 (Flash White) 0.1秒
        if (_fillImage != null) _fillImage.color = flashColor;
        yield return new WaitForSeconds(0.1f);
        if (_fillImage != null) _fillImage.color = fillColor;
        _flashRoutine = null;
    }
}
