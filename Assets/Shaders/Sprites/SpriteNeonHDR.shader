// SpriteNeonHDR — 仅亮青霓虹做 HDR 增益；金属保持贴图原色（不变灰）
// 额外：墙框沿弧长扩散脉冲（UV2.x = 弧长 s），Bumper/Slingshot 的 UV2=0 时脉冲无效
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

        [Header(Wall Arc Pulse)]
        _PulseSpread ("Pulse Spread Speed", Float) = 16
        _PulseWidth ("Pulse Band Width", Float) = 0.7
        _PulseDuration ("Pulse Duration", Float) = 0.65
        _WallPulse0 ("Wall Pulse 0-1", Vector) = (0, -1, 0, -1)
        _WallPulse1 ("Wall Pulse 2-3", Vector) = (0, -1, 0, -1)
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
    CBUFFER_END

    // CBUFFER 外：MaterialPropertyBlock 逐实例写入
    float  _HitFlash;
    float4 _FlashColor;
    float  _PulseSpread;
    float  _PulseWidth;
    float  _PulseDuration;
    // 每条脉冲：xy = (弧长 s, 年龄 age)；age < 0 表示关闭
    float4 _WallPulse0; // (s0, age0, s1, age1)
    float4 _WallPulse1; // (s2, age2, s3, age3)

    struct Attributes
    {
        float4 pos   : POSITION;
        float2 uv    : TEXCOORD0;
        float2 uv2   : TEXCOORD1;
        float4 color : COLOR;
    };

    struct Varyings
    {
        float4 pos   : SV_POSITION;
        float2 uv    : TEXCOORD0;
        float  arcS  : TEXCOORD1;
        float4 color : COLOR;
    };

    Varyings vert(Attributes IN)
    {
        Varyings O;
        O.pos   = TransformObjectToHClip(IN.pos.xyz);
        O.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
        O.arcS  = IN.uv2.x;
        // Mesh 无顶点色时 Unity 可能给 0；墙框需要白 tint
        O.color = any(IN.color) ? IN.color : float4(1, 1, 1, 1);
        return O;
    }

    float PulseMask(float arcS, float pulseS, float age, float spread, float width, float duration)
    {
        if (age < 0.0 || duration < 0.0001)
            return 0.0;

        float life = saturate(1.0 - age / duration);
        life = life * life; // 前半段更亮，尾段更快收
        float front = spread * age;
        float d = abs(arcS - pulseS);
        // 扩散前沿亮带（更宽、更软）
        float band = 1.0 - saturate(abs(d - front) / max(width, 0.001));
        band = pow(saturate(band), 1.35);
        // 撞击点残留核心
        float core = 1.0 - saturate(d / max(width * 2.2, 0.001));
        core = pow(saturate(core), 1.5) * saturate(1.0 - age / max(duration * 0.7, 0.001));
        // 前沿内侧拖尾：从撞击点到前沿之间略亮
        float wake = (d < front) ? (1.0 - saturate(d / max(front, 0.001))) * 0.45 * life : 0.0;
        return saturate(max(max(band, core), wake) * (0.55 + 0.45 * life));
    }

    float4 frag(Varyings IN) : SV_Target
    {
        float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
        float3 rgb = tex.rgb * IN.color.rgb;
        float  a   = tex.a * IN.color.a;

        // 偏青 或 偏金黄霓虹槽：暗金属（含黄铜高光）进不来
        float cyanDom   = saturate((rgb.g + rgb.b) * 0.5 - rgb.r * 0.85);
        float cyanBright = max(rgb.g, rgb.b);
        float cyanGate  = saturate((cyanBright - _NeonThreshold) / max(1.0 - _NeonThreshold, 0.001));
        float cyanMask  = pow(saturate(cyanDom * 2.2) * cyanGate, _NeonSharpness);

        float yellowDom  = saturate(min(rgb.r, rgb.g) - rgb.b - 0.12);
        float yellowGate = saturate((max(rgb.r, rgb.g) - 0.62) / 0.38);
        float yellowMask = pow(saturate(yellowDom * 3.0) * yellowGate, _NeonSharpness);

        float neonMask = max(cyanMask, yellowMask);

        float hit = saturate(_HitFlash);

        float pulse =
            PulseMask(IN.arcS, _WallPulse0.x, _WallPulse0.y, _PulseSpread, _PulseWidth, _PulseDuration) +
            PulseMask(IN.arcS, _WallPulse0.z, _WallPulse0.w, _PulseSpread, _PulseWidth, _PulseDuration) +
            PulseMask(IN.arcS, _WallPulse1.x, _WallPulse1.y, _PulseSpread, _PulseWidth, _PulseDuration) +
            PulseMask(IN.arcS, _WallPulse1.z, _WallPulse1.w, _PulseSpread, _PulseWidth, _PulseDuration);
        pulse = saturate(pulse);

        float pulseMask = saturate(neonMask + pulse * cyanDom * 0.4);

        float neonBoost = 1.0 + hit * 3.4 + pulse * 2.2;
        float3 metal = rgb * _MetalMul; // 金属不吃 HitFlash
        float3 neon  = rgb * _NeonTint.rgb * (_NeonIntensity * neonBoost);
        float3 outRgb = lerp(metal, neon, pulseMask);
        outRgb += pulseMask * _FlashColor.rgb * (hit * 1.15 + pulse * 0.85);
        a = saturate(a + pulseMask * (hit * 0.25 + pulse * 0.22) * _FlashColor.a * tex.a);

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
