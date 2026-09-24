Shader "Custom/TronBall"
{
    Properties
    {
        _CoreColor    ("Core Color (HDR)",   Color)  = (2.5, 2.5, 3.0, 1)
        _RimColor     ("Rim Color (HDR)",    Color)  = (0.1, 2.8, 4.0, 1)
        _RimPower     ("Rim Falloff Power",  Float)  = 2.8
        _RimIntensity ("Rim Intensity",      Float)  = 3.5
        _PulseSpeed   ("Pulse Speed",        Float)  = 2.2
        _PulseAmp     ("Pulse Amplitude",    Float)  = 0.25
        _MainTex      ("Sprite Texture",     2D)     = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "TronBallPass"
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
                float2 uv         : TEXCOORD0;
                // 用 UV 模拟 Fresnel（2D sprite 没有法线，用距中心距离代替）
                float2 centeredUV : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _CoreColor;
                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;
                float  _PulseSpeed;
                float  _PulseAmp;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.centeredUV = IN.uv * 2.0 - 1.0;   // [-1, 1] 居中 UV
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                if (tex.a < 0.01) discard;

                // 用到中心的距离模拟 Fresnel（距中心越远 = 越边缘）
                float dist    = length(IN.centeredUV);            // [0, ~1.4]
                float fresnel = pow(saturate(dist), _RimPower);   // 中心=0, 边缘=1

                // 呼吸脉冲（给 Core 颜色）
                float pulse   = 1.0 + _PulseAmp * sin(_Time.y * _PulseSpeed);

                // 核心色 + 边缘发光色叠加
                float3 col = lerp(_CoreColor.rgb * pulse,
                                  _RimColor.rgb * _RimIntensity,
                                  fresnel);

                return float4(col, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
