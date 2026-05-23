Shader "Custom/TronGrid"
{
    Properties
    {
        _GridColor   ("Grid Line Color",    Color) = (0.04, 0.13, 0.35, 1)
        _BgColor     ("Background Color",   Color) = (0.027, 0.031, 0.059, 1)
        _GridSize    ("Grid Cell Size",     Float) = 1.0
        _LineWidth   ("Line Width (0~0.5)", Float) = 0.0625
        _Brightness  ("Brightness",         Float) = 1.0
        _PulseSpeed  ("Pulse Speed",        Float) = 0.5
        _PulseAmp    ("Pulse Amplitude",    Float) = 0.12
        _PixelSnap   ("Pixel Snap (px/u)",  Float) = 16.0
        _ScanlineStr ("Scanline Strength",  Float) = 0.22
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "PixelGridPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float4 _BgColor;
                float  _GridSize;
                float  _LineWidth;
                float  _Brightness;
                float  _PulseSpeed;
                float  _PulseAmp;
                float  _PixelSnap;
                float  _ScanlineStr;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                float3 world   = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXY    = world.xy;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // ── 像素对齐：坐标吸附到逻辑像素格（消除亚像素模糊）────────
                float2 snapped  = floor(IN.worldXY * _PixelSnap + 0.5) / _PixelSnap;
                float2 p        = snapped / _GridSize;

                // ── 到格线的距离（值=0 在格线上，值=0.5 在格中心）──────────
                float2 grid     = abs(frac(p - 0.5) - 0.5);

                // ── 硬边格线（step，无抗锯齿，真像素风）─────────────────────
                float halfW     = _LineWidth * 0.5;
                float onGridX   = step(grid.x, halfW);
                float onGridY   = step(grid.y, halfW);
                float onGrid    = max(onGridX, onGridY);   // 1 = 在格线上

                // ── 呼吸脉冲 ─────────────────────────────────────────────────
                float pulse     = 1.0 + _PulseAmp * sin(_Time.y * _PulseSpeed);

                // ── 基础颜色混合 ──────────────────────────────────────────────
                float4 col      = lerp(_BgColor, _GridColor * pulse, onGrid);

                // ── CRT 扫描线：每隔一个逻辑像素行暗一行 ─────────────────────
                float scanRow   = floor(snapped.y * _PixelSnap);
                float scanDark  = fmod(abs(scanRow), 2.0);   // 0 或 1
                col.rgb        *= 1.0 - _ScanlineStr * scanDark;

                col.rgb        *= _Brightness;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
