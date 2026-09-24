Shader "Hidden/DirectionalCA"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "DirectionalCAPass"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float2 _BallScreenPos;
            float2 _BallVelocityDir;
            float _CAIntensity;

            half4 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                if (_CAIntensity <= 0.001)
                    return SampleSource(uv);

                float2 toBall = uv - _BallScreenPos;
                float distToBall = length(toBall);
                float alongVelocity = dot(toBall, _BallVelocityDir);

                float offsetStrength = _CAIntensity * 0.01;
                offsetStrength *= saturate(1.0 - distToBall * 2.0);
                offsetStrength *= saturate(alongVelocity * 2.0 + 0.5);

                float2 offset = _BallVelocityDir * offsetStrength;

                float r = SampleSource(uv - offset).r;
                float g = SampleSource(uv).g;
                float b = SampleSource(uv + offset).b;

                return half4(r, g, b, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
