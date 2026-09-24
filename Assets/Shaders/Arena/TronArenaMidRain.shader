Shader "Custom/TronArenaMidRain"
{
    Properties
    {
        [Header(Background Base)]
        _BgColor        ("Background",     Color) = (0.002, 0.004, 0.01, 1)
        _BandColor      ("Energy Band",    Color) = (0.012, 0.04, 0.07, 1)
        _BandCenterY    ("Band Center Y",  Float) = 0.0
        _BandHalfHeight ("Band Half H",    Float) = 3.2
        _BandStrength   ("Band Strength",  Float) = 0.08

        [Header(Code Rain Matrix Style)]
        _GlyphAtlas     ("Glyph Atlas",    2D) = "white" {}
        _HeadColor      ("Head Color",     Color) = (0.28, 0.78, 0.92, 1)
        _TrailColor     ("Trail Color",    Color) = (0.04, 0.14, 0.22, 1)
        _ColumnWidth    ("Column Width",   Float) = 0.32
        _CharHeight     ("Char Height",    Float) = 0.38
        _ColumnDensity  ("Column Density", Range(0, 1)) = 0.92
        _FallSpeedMin   ("Fall Speed Min", Float) = 0.9
        _FallSpeedMax   ("Fall Speed Max", Float) = 2.0
        _HeadBright     ("Head Bright",    Float) = 0.95
        _TrailBright    ("Trail Bright",   Float) = 0.22
        _GlyphChangeRate("Glyph Change",   Float) = 8.0
        _RainStrength   ("Rain Strength",  Range(0, 2)) = 1.0
        _RainBoost      ("Rain Boost",     Float) = 1.0
        _TrailFadePower ("Trail Fade Power", Float) = 2.4

        [Header(Faint Grid Overlay)]
        _GridColor      ("Grid Lines",     Color) = (0.03, 0.12, 0.18, 1)
        _GridSize       ("Grid Cell Size", Float) = 0.72
        _LineWidth      ("Line Width",     Float) = 0.045
        _GridOverlay    ("Grid Overlay",   Range(0, 1)) = 0.05
        _GridDriftSpeed ("Grid Drift",     Float) = 0.09
        _PerspectiveFloor ("Perspective Floor", Range(0, 1)) = 0
        _FloorHorizon   ("Floor Horizon UV", Range(0.3, 0.9)) = 0.64
        _FloorCellScale ("Floor Cell Scale", Float) = 14
        _FloorScroll    ("Floor Scroll", Float) = 0.12
        _FloorLineWidth ("Floor Line Width", Float) = 0.02
        _FloorSideExtend ("Floor Side Extend", Float) = 1.25

        [Header(Scan Effects)]
        _ScanSpeed      ("Scan Speed",     Float) = 0.75
        _ScanlineStr    ("Scanline Str",   Float) = 0.12
        _ScanBandWidth  ("Scan Band Width", Float) = 0.18

        [Header(Runtime)]
        _ComboBoost     ("Combo Boost",    Float) = 0.0
        _Brightness     ("Brightness",     Float) = 1.0
        _ScanBandY      ("Scan Band World Y", Float) = -999.0
        _ScanBandActive ("Scan Band Active",  Float) = 0.0
        _GlyphsPerRow   ("Glyphs Per Row", Float) = 8
        _GlyphRows      ("Glyph Rows",     Float) = 5
        _GlyphCount     ("Glyph Count",    Float) = 36
        _WakeMap        ("Wake Map",       2D) = "black" {}
        _WakeOrigin     ("Wake Origin",    Vector) = (0, 0, 0, 0)
        _WakeSize       ("Wake Size",      Vector) = (1, 1, 0, 0)
        _WakeScale      ("Wake Scale",     Float) = 0.0
        _WakeEnabled    ("Wake Enabled",   Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "MidRainLayer"
            Tags { "LightMode" = "Universal2D" }

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MatrixRainCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _BandColor;
                float4 _HeadColor;
                float4 _TrailColor;
                float4 _GridColor;
                float  _BandCenterY;
                float  _BandHalfHeight;
                float  _BandStrength;
                float  _ColumnWidth;
                float  _CharHeight;
                float  _ColumnDensity;
                float  _FallSpeedMin;
                float  _FallSpeedMax;
                float  _HeadBright;
                float  _TrailBright;
                float  _GlyphChangeRate;
                float  _RainStrength;
                float  _RainBoost;
                float  _TrailFadePower;
                float  _GridSize;
                float  _LineWidth;
                float  _GridOverlay;
                float  _GridDriftSpeed;
                float  _PerspectiveFloor;
                float  _FloorHorizon;
                float  _FloorCellScale;
                float  _FloorScroll;
                float  _FloorLineWidth;
                float  _FloorSideExtend;
                float  _ScanSpeed;
                float  _ScanlineStr;
                float  _ScanBandWidth;
                float  _ComboBoost;
                float  _Brightness;
                float  _ScanBandY;
                float  _ScanBandActive;
                float4 _WakeOrigin;
                float4 _WakeSize;
                float  _WakeScale;
                float  _WakeEnabled;
            CBUFFER_END

            TEXTURE2D(_WakeMap);
            SAMPLER(sampler_WakeMap);

            float2 ApplyWakeField(float2 worldXY)
            {
                if (_WakeEnabled < 0.5 || _WakeScale < 0.001)
                    return worldXY;

                float2 size = max(_WakeSize.xy, float2(0.001, 0.001));
                float2 uv = (worldXY - _WakeOrigin.xy) / size + 0.5;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return worldXY;

                // 速度场（RG）直接搅动字雨：对齐流体仿真可视化方式
                float2 flowUv = SAMPLE_TEXTURE2D(_WakeMap, sampler_WakeMap, uv).rg;
                return worldXY + flowUv * size * _WakeScale;
            }

            float FloorGridLine(float2 g, float2 fw, float lineW)
            {
                float2 f = abs(frac(g) - 0.5);
                float lx = 1.0 - smoothstep(0.0, lineW + fw.x, f.x);
                float ly = 1.0 - smoothstep(0.0, lineW + fw.y, f.y);
                return max(lx, ly);
            }

            float3 ApplyPerspectiveFloor(float3 col, float2 uv)
            {
                if (_PerspectiveFloor < 0.01)
                    return col;

                float horizon = saturate(_FloorHorizon);
                if (uv.y >= horizon)
                    return col;

                float t = (horizon - uv.y) / max(horizon, 1e-4);
                t = saturate(t);

                float depth = max(0.08 + t * 0.92, 0.001);
                float invZ = 1.0 / depth;
                float2 cell = float2((uv.x - 0.5) * _FloorSideExtend * invZ, invZ + _Time.y * _FloorScroll);
                cell *= _FloorCellScale;

                float2 fw = max(fwidth(cell) * 0.75, float2(1e-4, 1e-4));
                float onLine = FloorGridLine(cell, fw, _FloorLineWidth);
                // Soft halo kept very low so floor stays atmospheric (HTML ~7% lines).
                float glow = FloorGridLine(cell, fw * 2.5, _FloorLineWidth) * 0.12;

                float mask = smoothstep(0.0, 0.25, t);
                mask *= 1.0 - smoothstep(0.78, 1.0, t);

                float floorA = saturate(onLine + glow) * _GridColor.a * mask * _PerspectiveFloor;
                // Additive tint over rain/bg — never opaque cyan wipe.
                return col + _GridColor.rgb * floorA;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 world = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXY = world.xy;
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float combo = saturate(_ComboBoost);
                float2 xy = ApplyWakeField(IN.worldXY);

                float bandT = (xy.y - _BandCenterY) / max(_BandHalfHeight, 0.01);
                float band = exp(-bandT * bandT) * _BandStrength * (1.0 + combo * 0.85);

                float3 col = _BgColor.rgb + _BandColor.rgb * band;

                float bandDist = abs(xy.y - _ScanBandY);
                float scanBand = exp(-bandDist * bandDist / max(_ScanBandWidth, 0.001)) * _ScanBandActive;

                float3 rain = EvaluateMatrixRain(
                    xy,
                    _ColumnWidth, _CharHeight, _ColumnDensity,
                    _FallSpeedMin, _FallSpeedMax,
                    _HeadBright, _TrailBright, _GlyphChangeRate, _RainStrength,
                    _HeadColor, _TrailColor,
                    combo, scanBand, _TrailFadePower);
                col = saturate(col + rain * max(_RainBoost, 0.01));

                // Flat grid (gameplay arenas). Menu sets _GridOverlay=0 and uses perspective floor.
                float2 gridDrift = float2(_Time.y * _GridDriftSpeed, _Time.y * _GridDriftSpeed * 0.37);
                float2 gridP = (xy + gridDrift) / max(_GridSize, 0.01);
                float2 gridDist = abs(frac(gridP - 0.5) - 0.5);
                float d = min(gridDist.x, gridDist.y);
                float aa = max(fwidth(d) * 1.2, 0.001);
                float halfW = _LineWidth * 0.5;
                float onGrid = 1.0 - smoothstep(halfW - aa, halfW + aa, d);
                float gridAlpha = _GridColor.a * _GridOverlay;
                col = lerp(col, _GridColor.rgb, onGrid * gridAlpha);

                col = ApplyPerspectiveFloor(col, IN.uv);

                float scanRange = _BandHalfHeight * 2.2;
                float scanY = _BandCenterY + frac(_Time.y * _ScanSpeed * 0.18) * scanRange - scanRange * 0.5;
                float scan = exp(-pow((xy.y - scanY) / 0.09, 2.0));
                col += _GridColor.rgb * scan * _ScanlineStr * (1.0 + combo * 0.5);

                col *= _Brightness;
                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
