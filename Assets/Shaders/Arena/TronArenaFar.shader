Shader "Custom/TronArenaFar"
{
    Properties
    {
        _BgColor     ("Background", Color) = (0.004, 0.008, 0.02, 1)
        _BusColor    ("Bus Lines",  Color) = (0.02, 0.07, 0.12, 0.35)
        _NodeColor   ("Nodes",      Color) = (0.04, 0.14, 0.22, 0.55)
        _BusSpacing  ("Bus Spacing", Float) = 2.8
        _BusWidth    ("Bus Width",   Float) = 0.035
        _NodeSpacing ("Node Spacing", Float) = 2.8
        _NodeRadius  ("Node Radius",  Float) = 0.11
        _DriftSpeed  ("Drift Speed",  Float) = 0.045
        _Brightness  ("Brightness",   Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        LOD 100

        Pass
        {
            Name "FarLayer"
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
                float4 _BusColor;
                float4 _NodeColor;
                float  _BusSpacing;
                float  _BusWidth;
                float  _NodeSpacing;
                float  _NodeRadius;
                float  _DriftSpeed;
                float  _Brightness;
            CBUFFER_END

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

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

                // Sparse coarse bus grid
                float2 busCell = floor(wp / _BusSpacing);
                float2 busLocal = abs(frac(wp / _BusSpacing - 0.5) - 0.5) * _BusSpacing;
                float busLine = max(step(busLocal.y, _BusWidth), step(busLocal.x, _BusWidth));
                float busGate = step(0.28, Hash21(busCell));
                col = lerp(col, _BusColor.rgb, busLine * busGate * _BusColor.a);

                // Nodes at selected intersections
                float2 nodeCell = floor(wp / _NodeSpacing);
                float2 nodeLocal = frac(wp / _NodeSpacing) - 0.5;
                float nodeDist = length(nodeLocal);
                float nodeGate = step(0.62, Hash21(nodeCell + 19.7));
                float node = nodeGate * (1.0 - smoothstep(_NodeRadius * 0.35, _NodeRadius, nodeDist));
                col = lerp(col, _NodeColor.rgb, node * _NodeColor.a);

                col *= _Brightness;
                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
