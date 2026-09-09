#ifndef MATRIX_RAIN_COMMON_INCLUDED
#define MATRIX_RAIN_COMMON_INCLUDED

TEXTURE2D(_GlyphAtlas);
SAMPLER(sampler_GlyphAtlas);

float _GlyphsPerRow;
float _GlyphRows;
float _GlyphCount;
// 由 C# 写入 Time.unscaledTime，避免 timeScale / SlowMo 拖慢或拽回雨流
float _RainTime;

float MatrixHash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float SampleAtlasGlyph(float charIndex, float2 uv)
{
    float idx = clamp(floor(charIndex), 0.0, _GlyphCount - 1.0);
    float col = fmod(idx, _GlyphsPerRow);
    float row = floor(idx / _GlyphsPerRow);
    float2 cellUV = float2(col / _GlyphsPerRow, row / _GlyphRows);
    float2 atlasUV = cellUV + float2(1.0 / _GlyphsPerRow, 1.0 / _GlyphRows) * uv;
    return SAMPLE_TEXTURE2D(_GlyphAtlas, sampler_GlyphAtlas, atlasUV).r;
}

float3 EvaluateMatrixRain(
    float2 worldXY,
    float columnWidth,
    float charHeight,
    float columnDensity,
    float fallSpeedMin,
    float fallSpeedMax,
    float headBright,
    float trailBright,
    float glyphChangeRate,
    float rainStrength,
    float4 headColor,
    float4 trailColor,
    float combo,
    float scanBand,
    float trailFadePower)
{
    float colId = floor(worldXY.x / max(columnWidth, 0.001));
    float colHash = MatrixHash21(float2(colId, 17.3));

    if (colHash >= columnDensity)
        return float3(0.0, 0.0, 0.0);

    // 每列速度只由 Hash 决定，生命周期内恒定。
    // 禁止用 combo/scanBand 去乘 speed：stream = y + t*speed 在 speed 变小时会整段回跳（「回溯」）。
    float speedHash = MatrixHash21(float2(colId, 42.0));
    float phase = MatrixHash21(float2(colId, 91.0)) * 80.0;
    float speed = lerp(fallSpeedMin, fallSpeedMax, speedHash);

    float trailLen = floor(lerp(6.0, 22.0, MatrixHash21(float2(colId, 33.0))));
    float loopLen = max(charHeight * trailLen, charHeight);

    float rainT = _RainTime;
    float stream = worldXY.y + rainT * speed + phase;
    float qMod = fmod(stream, loopLen);
    if (qMod < 0.0)
        qMod += loopLen;

    float charRow = floor(qMod / max(charHeight, 0.001));
    float charFrac = fmod(qMod, max(charHeight, 0.001)) / max(charHeight, 0.001);

    float2 charUV = float2(
        frac(worldXY.x / max(columnWidth, 0.001)),
        1.0 - charFrac
    );

    // 换字频率可随玩法略变；不参与 stream，不会造成位置回溯
    float timeSlice = floor(rainT * glyphChangeRate);
    float cellIndex = floor(stream / max(charHeight, 0.001));
    float charIdx = floor(MatrixHash21(float2(colId, cellIndex + timeSlice * 1.73)) * _GlyphCount);

    float glyph = SampleAtlasGlyph(charIdx, charUV);
    if (glyph < 0.01)
        return float3(0.0, 0.0, 0.0);

    float fadePow = max(trailFadePower, 0.5);
    float rowFade = pow(saturate(1.0 - charRow / max(trailLen - 1.0, 1.0)), fadePow);
    float headPulse = (charRow < 0.5) ? (1.0 - smoothstep(0.0, 0.18, charFrac)) : 0.0;
    float brightness = saturate(rowFade * trailBright + headPulse * headBright);
    // Combo / ScanBand 只调亮度与存在感，不调下落位移
    brightness *= 1.0 + combo * 0.3 + scanBand * 1.6;

    float3 rainCol = lerp(trailColor.rgb, headColor.rgb, saturate(headPulse + rowFade * 0.35));
    return rainCol * glyph * brightness * rainStrength;
}

#endif
