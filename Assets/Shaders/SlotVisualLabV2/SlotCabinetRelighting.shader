Shader "ReboundProtocol/SlotVisualLabV2/CabinetRelighting"
{
    Properties
    {
        _BaseMap("Cabinet albedo", 2D) = "black" {}
        _SourceMap("Original emitter source / alpha", 2D) = "black" {}
        _NormalMap("Raw RGB cabinet normals", 2D) = "bump" {}
        _EmissionMask("Separate lamp mask", 2D) = "black" {}
        _NormalStrength("Normal strength", Range(0,2)) = .65
        _Metallic("Metallic response", Range(0,1)) = .2
        _LampStrength("Lamp strength", Range(0,2)) = .85
        _RimStrength("Fresnel rim strength", Range(0,1)) = .18
        _RimPower("Fresnel power", Range(1,8)) = 3
        _LightDirection("Tangent-space light", Vector) = (-.4,.6,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_EmissionMask); SAMPLER(sampler_EmissionMask);
        TEXTURE2D(_SourceMap); SAMPLER(sampler_SourceMap);
        CBUFFER_START(UnityPerMaterial)
            float _NormalStrength, _LampStrength, _RimStrength, _RimPower;
            float _Metallic;
            float4 _LightDirection;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = v.uv;
            return o;
        }
        half4 Relight(Varyings i) : SV_Target
        {
            half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
            half4 original = SAMPLE_TEXTURE2D(_SourceMap, sampler_SourceMap, i.uv);
            // The trial map stores raw RGB directions. Suppress invalid dark contour pixels
            // from image generation rather than interpreting them as sideways-facing normals.
            half3 raw = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv).rgb * 2 - 1;
            half valid = smoothstep(.05h,.35h,raw.z);
            half3 n = normalize(lerp(half3(0,0,1), normalize(raw + half3(0,0,.001h)), valid));
            n.xy *= _NormalStrength;
            n.z = sqrt(max(.05h, 1 - dot(n.xy,n.xy)));
            n = normalize(n);
            half3 l = normalize(_LightDirection.xyz);
            half3 h = normalize(l + half3(0,0,1));
            half diffuse = (.72h + .32h * saturate(dot(n,l))) * (1 - .12h * _Metallic);
            half specular = pow(saturate(dot(n,h)), 48) * lerp(.045h, .18h, _Metallic);
            half rim = pow(1 - saturate(n.z), _RimPower) * _RimStrength;
            half3 lamps = SAMPLE_TEXTURE2D(_EmissionMask, sampler_EmissionMask, i.uv).rgb;
            half lampArea = saturate(max(lamps.r, max(lamps.g, lamps.b)) * 2);
            half3 body = lerp(base.rgb, base.rgb * .15h, lampArea);
            half3 highlightTint = lerp(half3(.55h,.8h,.85h), lerp(body,half3(.55h,.8h,.85h),.45h),_Metallic);
            half3 rgb = body * diffuse + specular * highlightTint * (1-lampArea)
                + rim * half3(.55h,.8h,.85h) + lamps * _LampStrength;
            return half4(rgb, original.a);
        }
        half4 Mask(Varyings i) : SV_Target
        {
            half4 source = SAMPLE_TEXTURE2D(_SourceMap, sampler_SourceMap, i.uv);
            half peak = max(source.r, max(source.g, source.b));
            half low = min(source.r, min(source.g, source.b));
            half saturation = (peak-low)/max(peak,.0001h);
            half mask = smoothstep(.4h,.75h,saturation) * smoothstep(.3h,.75h,peak);
            return half4(source.rgb * mask, 1);
        }
        ENDHLSL
        Pass
        {
            Name "NormalLighting"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Relight
            ENDHLSL
        }
        Pass
        {
            Name "LampMask"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Mask
            ENDHLSL
        }
    }
}
