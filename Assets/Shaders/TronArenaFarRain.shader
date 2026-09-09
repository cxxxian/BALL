Shader "Custom/TronArenaFarRain"
{
    Properties
    {
        [Header(Background Base)]
        _BgColor        ("Background", Color) = (0.002, 0.005, 0.012, 1)

        [Header(Distant Code Rain)]
        _GlyphAtlas     ("Glyph Atlas",    2D) = "white" {}
        _HeadColor      ("Head Color",     Color) = (0.12, 0.38, 0.52, 1)
        _TrailColor     ("Trail Color",    Color) = (0.025, 0.08, 0.13, 1)
        _ColumnWidth    ("Column Width",   Float) = 0.48
        _CharHeight     ("Char Height",    Float) = 0.22
        _ColumnDensity  ("Column Density", Range(0, 1)) = 0.55
        _FallSpeedMin   ("Fall Speed Min", Float) = 0.45
        _FallSpeedMax   ("Fall Speed Max", Float) = 1.1
        _HeadBright     ("Head Bright",    Float) = 0.55
        _TrailBright    ("Trail Bright",   Float) = 0.12
        _GlyphChangeRate("Glyph Change",   Float) = 4.0
        _RainStrength   ("Rain Strength",  Range(0, 1)) = 0.7
        _DriftSpeed     ("Parallax Drift", Float) = 0.045

        [Header(Runtime)]
        _Brightness     ("Brightness", Float) = 1.0
        _GlyphsPerRow   ("Glyphs Per Row", Float) = 8
        _GlyphRows      ("Glyph Rows",     Float) = 5
        _GlyphCount     ("Glyph Count",    Float) = 36
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "FarRainLayer"
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BgColor;
                float4 _HeadColor;
                float4 _TrailColor;
                float  _ColumnWidth;
                float  _CharHeight;
                float  _ColumnDensity;
                float  _FallSpeedMin;
                float  _FallSpeedMax;
                float  _HeadBright;
                float  _TrailBright;
                float  _GlyphChangeRate;
                float  _RainStrength;
                float  _DriftSpeed;
                float  _Brightness;
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
                float2 drift = float2(_Time.y * _DriftSpeed, _Time.y * _DriftSpeed * 0.41);
                float2 wp = IN.worldXY + drift;

                float3 col = _BgColor.rgb;

                float3 rain = EvaluateMatrixRain(
                    wp,
                    _ColumnWidth, _CharHeight, _ColumnDensity,
                    _FallSpeedMin, _FallSpeedMax,
                    _HeadBright, _TrailBright, _GlyphChangeRate, _RainStrength,
                    _HeadColor, _TrailColor,
                    0.0, 0.0, 2.4);
                col = saturate(col + rain);

                col *= _Brightness;
                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
