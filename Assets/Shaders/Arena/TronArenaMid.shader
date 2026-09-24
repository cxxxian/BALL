Shader "Custom/TronArenaMid"
{
    Properties
    {
        _BgColor        ("Background",     Color) = (0.003, 0.006, 0.014, 1)
        _BandColor      ("Scan Band",      Color) = (0.015, 0.05, 0.09, 1)
        _GridColor      ("Grid Lines",     Color) = (0.03, 0.12, 0.18, 1)
        _GridSize       ("Grid Cell Size", Float) = 0.72
        _LineWidth      ("Line Width",     Float) = 0.045
        _BandCenterY    ("Band Center Y",  Float) = 0.0
        _BandHalfHeight ("Band Half H",    Float) = 3.2
        _BandStrength   ("Band Strength",  Float) = 0.14
        _ScanSpeed      ("Scan Speed",     Float) = 0.75
        _ScanlineStr    ("Scanline Str",   Float) = 0.18
        _GridDriftSpeed ("Grid Drift",     Float) = 0.09
        _ComboBoost     ("Combo Boost",    Float) = 0.0
        _Brightness     ("Brightness",     Float) = 1.0
        _ScanBandY      ("Scan Band World Y", Float) = -999.0
        _ScanBandActive ("Scan Band Active",  Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "MidLayer"
            Tags { "LightMode" = "Universal2D" }

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY      : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _BandColor;
                float4 _GridColor;
                float  _GridSize;
                float  _LineWidth;
                float  _BandCenterY;
                float  _BandHalfHeight;
                float  _BandStrength;
                float  _ScanSpeed;
                float  _ScanlineStr;
                float  _GridDriftSpeed;
                float  _ComboBoost;
                float  _Brightness;
                float  _ScanBandY;
                float  _ScanBandActive;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 world = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXY = world.xy;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float combo = saturate(_ComboBoost);

                // Horizontal battlefield energy band
                float bandT = (IN.worldXY.y - _BandCenterY) / max(_BandHalfHeight, 0.01);
                float band = exp(-bandT * bandT) * _BandStrength * (1.0 + combo * 0.85);

                float3 col = _BgColor.rgb + _BandColor.rgb * band;

                // Fine grid — smooth scroll (anti-aliased lines, no strobe)
                float2 gridDrift = float2(_Time.y * _GridDriftSpeed, _Time.y * _GridDriftSpeed * 0.37);
                float2 gridP = (IN.worldXY + gridDrift) / _GridSize;
                float2 gridDist = abs(frac(gridP - 0.5) - 0.5);
                float d = min(gridDist.x, gridDist.y);
                float aa = max(fwidth(d) * 1.2, 0.001);
                float bandDist = abs(IN.worldXY.y - _ScanBandY);
                float scanBand = exp(-bandDist * bandDist / 0.18) * _ScanBandActive;

                float halfW = _LineWidth * 0.5 * (1.0 + scanBand * 0.45);
                float onGrid = 1.0 - smoothstep(halfW - aa, halfW + aa, d);

                float bandMask = saturate(exp(-bandT * bandT * 0.65) * 1.4);
                float gridAlpha = _GridColor.a * (0.35 + bandMask * 0.55 + combo * 0.35);
                col = lerp(col, _GridColor.rgb * (1.0 + scanBand * 2.2), onGrid * gridAlpha * (1.0 + scanBand));

                // Moving horizontal scan sweep (smooth band, not flicker)
                float scanRange = _BandHalfHeight * 2.2;
                float scanY = _BandCenterY + frac(_Time.y * _ScanSpeed * 0.18) * scanRange - scanRange * 0.5;
                float scan = exp(-pow((IN.worldXY.y - scanY) / 0.09, 2.0));
                col += _GridColor.rgb * scan * _ScanlineStr * (1.0 + combo * 0.5);

                col *= _Brightness;
                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
