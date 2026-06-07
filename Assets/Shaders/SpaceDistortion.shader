Shader "Custom/SpaceDistortion"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.1
        _RippleFrequency ("Ripple Frequency", Float) = 10.0
        _RippleSpeed ("Ripple Speed", Float) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SpaceDistortionPass"
            Tags { "LightMode" = "Universal2D" }

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _RippleFrequency;
                float _RippleSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 计算屏幕 UV
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 计算从中心向外的距离
                float2 centerOffset = input.uv - float2(0.5, 0.5);
                float distFromCenter = length(centerOffset);

                // 涟漪波纹效果：sin 波动随距离和时间变化
                float ripple = sin(distFromCenter * _RippleFrequency - _Time.y * _RippleSpeed);
                
                // 沿径向方向的扭曲偏移
                float2 distortionOffset = normalize(centerOffset) * ripple * _DistortionStrength;
                
                // 衰减：边缘扭曲更强，中心较弱
                float falloff = smoothstep(0.5, 0.0, distFromCenter);
                distortionOffset *= falloff;

                // 采样扭曲后的屏幕纹理
                float2 distortedUV = screenUV + distortionOffset;
                half4 screenColor = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, distortedUV);

                // 返回扭曲后的颜色，保持透明度
                return half4(screenColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
