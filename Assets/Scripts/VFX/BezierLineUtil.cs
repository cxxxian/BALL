using UnityEngine;

/// <summary>
/// 电弧折线路径：借鉴电流描边的程序性噪声抖动思路（hash + smoothNoise），
/// 在塔→目标直线上做垂直噪声偏移，形成锯齿折线而非平滑曲线。
/// Bloom 交给场景 URP Volume，这里只负责折线几何。
/// </summary>
public static class BezierLineUtil
{
    public static Vector3 SampleCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float uuu = uu * u;
        float tt = t * t;
        float ttt = tt * t;

        float x = uuu * p0.x + 3f * uu * t * p1.x + 3f * u * tt * p2.x + ttt * p3.x;
        float y = uuu * p0.y + 3f * uu * t * p1.y + 3f * u * tt * p2.y + ttt * p3.y;
        return new Vector3(x, y, 0f);
    }

    public static void FillCubic(LineRenderer lr, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments, float z = -0.12f)
    {
        int count = Mathf.Max(segments, 2);
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector3 p = SampleCubic(p0, p1, p2, p3, t);
            p.z = z;
            lr.SetPosition(i, p);
        }
    }

    /// <summary>
    /// 噪声折线电弧。waveAmp/waveFreq/waveSpeed 语义对齐电流描边文档。
    /// </summary>
    public static void FillLightningBolt(
        LineRenderer lr,
        Vector2 from,
        Vector2 to,
        float time,
        int seed,
        int segments = 18,
        float waveAmp = 0.9f,
        float waveFreq = 1.6f,
        float waveSpeed = 2.2f,
        float z = -0.12f)
    {
        int count = Mathf.Max(segments, 3);
        lr.positionCount = count;

        Vector2 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f)
        {
            for (int i = 0; i < count; i++)
                lr.SetPosition(i, new Vector3(from.x, from.y, z));
            return;
        }

        Vector2 dir = delta / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float freq = waveFreq * 8f;
        float amp = waveAmp * Mathf.Min(len * 0.22f, 1.1f);
        float seedOff = seed * 0.137f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            // 端点锁死：中间才有折线幅度
            float envelope = Mathf.Sin(t * Mathf.PI);
            envelope *= envelope;

            Vector2 basePos = Vector2.Lerp(from, to, t);

            // 对齐文档：jx 用沿程，jy 用错开相位；XY 速度比 1.0 / 0.7
            float nPerp = SmoothNoise(t * freq + seedOff, time * waveSpeed) - 0.5f;
            float nAlong = SmoothNoise(t * freq + seedOff + 5.1f, time * waveSpeed * 0.7f) - 0.5f;

            Vector2 offset = perp * (nPerp * 2f * amp * envelope)
                           + dir * (nAlong * amp * 0.35f * envelope);

            Vector2 p = basePos + offset;
            lr.SetPosition(i, new Vector3(p.x, p.y, z));
        }
    }

    public static void BuildLightningControls(Vector2 from, Vector2 to, int seed, out Vector2 c1, out Vector2 c2)
    {
        Vector2 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f)
        {
            c1 = from;
            c2 = to;
            return;
        }

        Vector2 mid = (from + to) * 0.5f;
        Vector2 perp = new Vector2(-delta.y, delta.x).normalized;
        float sag = len * RandomRangeSeeded(0.12f, 0.22f, seed);

        float signA = RandomSignSeeded(seed);
        float signB = RandomSignSeeded(seed + 17);

        c1 = mid + perp * sag * signA;
        c2 = mid - perp * sag * 0.6f * signB;
    }

    // ── 程序性噪声（对齐 edge_line.frag 的 hash + smoothNoise）────────────

    public static float Hash(Vector2 p)
    {
        p = Fract(p * new Vector2(127.1f, 311.7f));
        float d = Vector2.Dot(p, p + new Vector2(74.13f, 74.13f));
        p += new Vector2(d, d);
        return Fract(p.x * p.y);
    }

    public static float SmoothNoise(float x, float y)
    {
        var p = new Vector2(x, y);
        Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
        Vector2 f = new Vector2(p.x - i.x, p.y - i.y);
        Vector2 u = new Vector2(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));

        float a = Hash(i);
        float b = Hash(i + new Vector2(1f, 0f));
        float c = Hash(i + new Vector2(0f, 1f));
        float d = Hash(i + new Vector2(1f, 1f));
        return Mathf.Lerp(Mathf.Lerp(a, b, u.x), Mathf.Lerp(c, d, u.x), u.y);
    }

    private static Vector2 Fract(Vector2 v) =>
        new Vector2(v.x - Mathf.Floor(v.x), v.y - Mathf.Floor(v.y));

    private static float Fract(float v) => v - Mathf.Floor(v);

    private static float RandomRangeSeeded(float min, float max, int seed)
    {
        float t = Mathf.Abs(Mathf.Sin(seed * 12.9898f) * 43758.5453f) % 1f;
        return Mathf.Lerp(min, max, t);
    }

    private static float RandomSignSeeded(int seed)
    {
        return (Mathf.Abs(Mathf.Sin(seed * 78.233f) * 12345.6789f) % 1f) > 0.5f ? 1f : -1f;
    }
}
