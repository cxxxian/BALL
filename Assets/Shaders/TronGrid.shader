Shader "Custom/TronGrid"
{
    Properties
    {
        _GridColor  ("Grid Line Color",   Color)  = (0.05, 0.18, 0.45, 1)
        _BgColor    ("Background Color",  Color)  = (0.005, 0.008, 0.025, 1)
        _GridSize   ("Grid Cell Size",    Float)  = 1.0
        _LineWidth  ("Line Width",        Float)  = 0.04
        _Brightness ("Brightness",        Float)  = 1.0
        _PulseSpeed ("Pulse Speed",       Float)  = 0.6
        _PulseAmp   ("Pulse Amplitude",   Float)  = 0.18
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "TronGridPass"
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
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // 传世界 XY 坐标用于网格计算（不用 UV，保证全局对齐）
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXY = worldPos.xy;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.worldXY / _GridSize;

                // frac + min 计算到格线的最近距离
                float2 grid = abs(frac(p - 0.5) - 0.5);

                // 抗锯齿：用 fwidth 做软化
                float2 fw   = fwidth(p);
                float2 edge = smoothstep(_LineWidth * 0.5 + fw, _LineWidth * 0.5, grid);
                float  line = max(edge.x, edge.y);

                // 缓慢脉冲（整体亮度呼吸）
                float pulse = 1.0 + _PulseAmp * sin(_Time.y * _PulseSpeed);

                float4 col = lerp(_BgColor, _GridColor * pulse, line);
                col.rgb *= _Brightness;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
