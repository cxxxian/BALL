Shader "ReboundProtocol/SlotVisualLabV2/UIHDR"
{
    Properties
    {
        _BaseMap("Live UI", 2D) = "black" {}
        _EmissionStrength("Emitter HDR gain", Range(0, 4)) = 2.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "LiveUI"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _EmissionStrength;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 rgb = sample.rgb;
                half peak = max(rgb.r, max(rgb.g, rgb.b));
                half low = min(rgb.r, min(rgb.g, rgb.b));
                half saturation = (peak - low) / max(peak, 0.0001);
                // Only bright colored cores emit. Dark panels and neutral text stay below HDR.
                half emitter = smoothstep(0.35, 0.65, saturation) * smoothstep(0.4, 0.8, peak);
                return half4(rgb * (1 + emitter * _EmissionStrength), sample.a);
            }
            ENDHLSL
        }
    }
}
