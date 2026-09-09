Shader "Custom/MenuPerspectiveGrid"
{
    // HTML demos/main-menu .grid-floor:
    //   perspective(500px) rotateX(58deg) + cyan grid + vertical fade mask
    Properties
    {
        _GridColor ("Grid Color", Color) = (0, 0.85, 0.95, 0.42)
        _Horizon ("Horizon (UV Y)", Range(0.35, 0.85)) = 0.62
        _CellScale ("Cell Scale", Float) = 14
        _LineWidth ("Line Width", Range(0.005, 0.08)) = 0.018
        _Scroll ("Scroll Speed", Float) = 0.12
        _FadeNearHorizon ("Fade Near Horizon", Range(0.05, 0.5)) = 0.22
        _FadeNearCamera ("Fade Near Camera", Range(0.05, 0.5)) = 0.18
        _Glow ("Line Glow", Range(0, 2)) = 0.65
        _SideExtend ("Side Extend", Float) = 1.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "PerspectiveFloor"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GridColor;
                float _Horizon;
                float _CellScale;
                float _LineWidth;
                float _Scroll;
                float _FadeNearHorizon;
                float _FadeNearCamera;
                float _Glow;
                float _SideExtend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float GridLine(float2 g, float2 fw)
            {
                float2 f = abs(frac(g) - 0.5);
                float lx = 1.0 - smoothstep(0.0, _LineWidth + fw.x, f.x);
                float ly = 1.0 - smoothstep(0.0, _LineWidth + fw.y, f.y);
                return max(lx, ly);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float horizon = saturate(_Horizon);

                // Mesh UV: y=0 bottom, y=1 top. Floor sits below the horizon (HTML inset ~35% from top).
                if (uv.y >= horizon)
                    return float4(0, 0, 0, 0);

                float t = (horizon - uv.y) / max(horizon, 1e-4); // 0 at horizon → 1 at screen bottom
                t = saturate(t);

                // Approximate CSS perspective(500) rotateX(58): foreshorten toward horizon.
                float depth = max(0.08 + t * 0.92, 0.001);
                float invZ = 1.0 / depth;

                float gx = (uv.x - 0.5) * _SideExtend * invZ;
                float gz = invZ + _Time.y * _Scroll;
                float2 cell = float2(gx, gz) * _CellScale;

                float2 fw = fwidth(cell) * 0.75;
                float onLine = GridLine(cell, max(fw, float2(1e-4, 1e-4)));

                // Soft secondary glow on lines
                float glow = GridLine(cell, fw * (2.5 + _Glow)) * 0.35 * _Glow;

                // HTML mask: transparent at horizon + soft fade near bottom
                float mask = smoothstep(0.0, _FadeNearHorizon, t);
                mask *= 1.0 - smoothstep(1.0 - _FadeNearCamera, 1.0, t);

                float alpha = saturate(onLine + glow) * _GridColor.a * mask;
                float3 col = _GridColor.rgb * (0.75 + onLine * 0.55 + glow * 0.4);
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
