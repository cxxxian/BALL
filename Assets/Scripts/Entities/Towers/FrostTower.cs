using UnityEngine;
using System.Collections;

/// <summary>
/// Frost Rare 建筑分支：周期给附近小兵叠霜痕（主职），不再做纯减速/长冻 nova。
/// 有霜爆时，塔叠痕可顺带触发冰爆。短冻由 FrostCombat 负责（≤1s）。
/// </summary>
public class FrostTower : MonoBehaviour
{
    public int level = 1;
    public float attackRadius = 3.5f;
    public float baseAttackInterval = 6.5f;

    private float _timer;

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
        if (_timer > 0f) return;

        float interval = Mathf.Max(3.5f, baseAttackInterval - 0.5f * (level - 1));
        if (DebuffManager.Instance != null)
            interval *= DebuffManager.Instance.TowerAttackIntervalMultiplier;
        _timer = interval;
        PulseFrostMarks();
    }

    private void PulseFrostMarks()
    {
        float radius = attackRadius + 0.25f * (level - 1);
        // L1=1 层/跳；L2+=偶尔 2 层，保持塔非主 DPS
        int marks = level >= 2 ? 2 : 1;

        FrostCombat.OnTowerPulse(transform.position, radius, marks, level);

        bool anyMarked = HasEnemyInRadius(radius);
        JuiceRouter.TowerFire(transform.position, NeonRole.TowerFrost, anyMarked);
        StartCoroutine(SpawnFrostEffect(radius));
        StartCoroutine(TowerPulse());
    }

    private bool HasEnemyInRadius(float radius)
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var c in cols)
        {
            if (!c.CompareTag("Enemy")) continue;
            var minion = c.GetComponent<Minion>();
            if (minion != null && !minion.IsDead) return true;
        }
        return false;
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

    private IEnumerator SpawnFrostEffect(float radius)
    {
        GameObject nova = new GameObject("FrostMarkPulse");
        nova.transform.position = transform.position;
        var sr = nova.AddComponent<SpriteRenderer>();
        sr.sprite = CreateFrostNovaSprite();
        sr.color = new Color(0.6f, 0.9f, 1f, 0.55f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 4;

        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 targetScale = new Vector3(radius * 2, radius * 2, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            nova.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, easeOut);

            Color c = sr.color;
            c.a = Mathf.Lerp(0.55f, 0f, t);
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
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r)
                {
                    float alpha = 1f - dist / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.5f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }
}
