using System.Collections;
using UnityEngine;

public class Minion : EnemyBase
{
    public MinionDefinition definition;

    private SpriteRenderer _sr;
    private Color _baseColor;

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
            _sr.sprite = def.sprite;
        else
            _sr.sprite = CyberVisualFactory.CreateMinionSprite(def.baseColor, def.isBomber);

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
