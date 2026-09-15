// SpriteNeonHDR — 仅亮青霓虹做 HDR 增益；金属保持贴图原色（不变灰）
Shader "Custom/SpriteNeonHDR"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _NeonTint ("Neon Tint (HDR)", Color) = (0.75, 1.35, 1.8, 1)
        _NeonIntensity ("Neon Intensity", Range(1, 12)) = 5.5
        _NeonThreshold ("Neon Bright Threshold", Range(0, 0.9)) = 0.42
        _NeonSharpness ("Neon Mask Sharpness", Range(1, 8)) = 3.0
        _MetalMul ("Metal Brightness", Range(0.5, 1.2)) = 1.0

        [Header(Hit Flash)]
        _HitFlash ("Hit Flash (0-1)", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _NeonTint;
        float  _NeonIntensity;
        float  _NeonThreshold;
        float  _NeonSharpness;
        float  _MetalMul;
        float  _HitFlash;
        float4 _FlashColor;
    CBUFFER_END

    struct Attributes
    {
        float4 pos   : POSITION;
        float2 uv    : TEXCOORD0;
        float4 color : COLOR;
    };

    struct Varyings
    {
        float4 pos   : SV_POSITION;
        float2 uv    : TEXCOORD0;
        float4 color : COLOR;
    };

    Varyings vert(Attributes IN)
    {
        Varyings O;
        O.pos   = TransformObjectToHClip(IN.pos.xyz);
        O.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
        O.color = IN.color;
        return O;
    }

    float4 frag(Varyings IN) : SV_Target
    {
        float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
        float3 rgb = tex.rgb * IN.color.rgb;
        float  a   = tex.a * IN.color.a;

        // 只认「偏青 + 够亮」：灰金属 / 暗底板掩码≈0
        float cyanDom = saturate((rgb.g + rgb.b) * 0.5 - rgb.r * 0.85);
        float bright  = max(rgb.g, rgb.b);
        float gate    = saturate((bright - _NeonThreshold) / max(1.0 - _NeonThreshold, 0.001));
        float neonMask = pow(saturate(cyanDom * 2.2) * gate, _NeonSharpness);

        // 金属：贴图原色；霓虹：HDR 拉亮喂 Bloom
        float3 metal = rgb * _MetalMul;
        float3 neon  = rgb * _NeonTint.rgb * _NeonIntensity;
        float3 outRgb = lerp(metal, neon, neonMask);

        outRgb = lerp(outRgb, _FlashColor.rgb * max(_NeonIntensity * 0.45, 1.0), _HitFlash);
        a = saturate(a + _HitFlash * _FlashColor.a * tex.a);

        return float4(outRgb, a);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SpriteNeonHDR_2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "SpriteNeonHDR_Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
