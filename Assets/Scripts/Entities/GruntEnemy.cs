using UnityEngine;

public class GruntEnemy : EnemyBase
{
    private void Awake()
    {
        maxHits = 2;
        moveSpeed = 0.5f;
        scoreOnHit = 10;
        scoreOnKill = 50;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = CreateCircleSprite(64, new Color(0.9f, 0.3f, 0.3f));
    }

    public static Sprite CreateCircleSprite(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float half = size * 0.5f;
        float r = half - 2f;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float dx = (i % size) - half;
            float dy = (i / size) - half;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01((r - dist) * 2f);
            pixels[i] = new Color(color.r, color.g, color.b, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        float ppu = size / 0.6f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
    }

    protected override void OnHit()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.Lerp(new Color(0.9f, 0.3f, 0.3f), Color.white, (float)CurrentHits / maxHits);
    }
}
