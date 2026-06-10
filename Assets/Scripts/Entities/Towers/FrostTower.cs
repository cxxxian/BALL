using UnityEngine;
using System.Collections;

public class FrostTower : MonoBehaviour
{
    public int level = 1;
    public float attackRadius = 6.0f;
    public float baseAttackInterval = 8.0f;
    public float freezeDuration = 1.5f;

    private float _timer = 0f;

    private void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateTowerSprite();
        sr.color = new Color(0.6f, 0.9f, 1f, 1f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 3;
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = Mathf.Max(3.0f, baseAttackInterval - (level * 0.5f));
            AttackFrostNova();
        }
    }

    private void AttackFrostNova()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, attackRadius);

        float currentFreezeDuration = freezeDuration + (level * 0.5f);
        bool hitAny = false;

        foreach (var c in cols)
        {
            if (!c.CompareTag("Enemy")) continue;
            var minion = c.GetComponent<Minion>();
            if (minion == null || minion.IsDead) continue;
            hitAny = true;
            StartCoroutine(ApplyFreeze(minion, currentFreezeDuration));
        }

        JuiceRouter.TowerFire(transform.position, NeonRole.TowerFrost, hitAny);
        StartCoroutine(SpawnFrostEffect());
        StartCoroutine(TowerPulse());
    }

    private IEnumerator ApplyFreeze(Minion minion, float duration)
    {
        if (minion.moveSpeed <= 0f) yield break;

        float originalSpeed = minion.moveSpeed;
        minion.moveSpeed = 0f;

        var sr = minion.GetComponent<SpriteRenderer>();
        Color origColor = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = new Color(0.5f, 0.8f, 1f, 1f);

        yield return new WaitForSeconds(duration);

        if (minion != null && !minion.IsDead)
        {
            minion.moveSpeed = originalSpeed;
            if (sr != null) sr.color = origColor;
        }
    }

    private IEnumerator TowerPulse()
    {
        transform.localScale = new Vector3(1.3f, 1.3f, 1f);
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(new Vector3(1.3f, 1.3f, 1f), Vector3.one, t / 0.2f);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private IEnumerator SpawnFrostEffect()
    {
        GameObject nova = new GameObject("FrostNova");
        nova.transform.position = transform.position;
        var sr = nova.AddComponent<SpriteRenderer>();
        sr.sprite = CreateFrostNovaSprite();
        sr.color = new Color(0.6f, 0.9f, 1f, 0.7f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 4;

        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 targetScale = new Vector3(attackRadius * 2, attackRadius * 2, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            nova.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, easeOut);

            Color c = sr.color;
            c.a = Mathf.Lerp(0.7f, 0f, t);
            sr.color = c;

            yield return null;
        }

        Destroy(nova);
    }

    private static Sprite CreateTowerSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half) / half;
                float dy = Mathf.Abs(y - half) / half;
                if (Mathf.Max(dx, dy) <= 0.7f)
                {
                    if (Mathf.Max(dx, dy) > 0.5f) tex.SetPixel(x, y, Color.white);
                    else tex.SetPixel(x, y, new Color(0.3f, 0.6f, 0.9f, 0.8f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    private static Sprite CreateFrostNovaSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float half = size * 0.5f;
        float r = half - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half);
                float dy = (y - half);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= r)
                {
                    float alpha = 1f - (dist / r);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.5f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }
}
