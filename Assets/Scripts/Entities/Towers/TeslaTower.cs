using UnityEngine;
using System.Collections;

public class TeslaTower : MonoBehaviour
{
    public int level = 1;
    public float attackRadius = 4.0f;
    public float baseAttackInterval = 5.0f;
    public int baseDamage = 3;

    private float _timer = 0f;
    private Material _novaMat;

    private void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateTeslaSprite();
        sr.color = new Color(0f, 0.95f, 1f, 1f);
        var cyberShader = Shader.Find("Custom/CyberPulseSprite");
        sr.material = cyberShader != null ? new Material(cyberShader) : new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 3;

        _novaMat = new Material(Shader.Find("Sprites/Default"));
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = Mathf.Max(2.0f, baseAttackInterval - (level * 0.4f));
            AttackElectricNova();
        }
    }

    private void AttackElectricNova()
    {
        int damage = baseDamage + level * 2;
        float actualRadius = attackRadius + level * 0.3f;

        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, actualRadius);
        bool hitAny = false;
        foreach (var c in cols)
        {
            if (c.CompareTag("Enemy"))
            {
                var enemy = c.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeHit(damage);
                    hitAny = true;
                }
            }
        }

        if (hitAny)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);

        StartCoroutine(SpawnNovaEffect(actualRadius));
    }

    private IEnumerator SpawnNovaEffect(float radius)
    {
        GameObject novaObj = new GameObject("TeslaNova");
        novaObj.transform.position = transform.position;
        var sr = novaObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateNovaSprite();
        sr.color = new Color(0f, 0.95f, 1f, 1f);
        sr.material = _novaMat;
        sr.sortingOrder = 4;

        float duration = 0.35f;
        float elapsed = 0f;

        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * (radius * 2f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scaleT = 1f - Mathf.Pow(1f - t, 3f);
            novaObj.transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);

            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;

            yield return null;
        }

        Destroy(novaObj);
    }

    private static Sprite CreateTeslaSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= half - 2f)
                {
                    if (dist > half - 6f)
                    {
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360f;
                        if (angle % 90f < 70f)
                            tex.SetPixel(x, y, Color.white);
                        else
                            tex.SetPixel(x, y, new Color(0f, 0.4f, 0.6f, 0.8f));
                    }
                    else if (dist < 8f)
                        tex.SetPixel(x, y, Color.white);
                    else if (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f)
                        tex.SetPixel(x, y, new Color(0.6f, 1f, 1f, 0.9f));
                    else
                        tex.SetPixel(x, y, new Color(0f, 0.1f, 0.2f, 0.6f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    private static Sprite CreateNovaSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float ringDist = Mathf.Abs(dist - (half - 4f));
                if (dist <= half && ringDist < 8f)
                {
                    float alpha = 1f - (ringDist / 8f);
                    float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                    alpha *= (0.5f + noise * 0.5f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }
}
