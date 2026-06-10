Shader "Hidden/ArcadeCRT"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ArcadeCRTPass"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ScanlineOpacity;
            float _ScanlineCount;
            float _ScanlineWidth;
            float _VignetteStrength;
            float _VignettePower;
            float _VignetteRoundness;

            float _EventMaster;
            float _EventHeadY;
            float _EventTime;
            float _EventLineIntensity;
            float _EventWakePx;
            float _EventRevealDim;
            float _EventInteractBoost;
            float4 _EventColor;

            half4 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            float ComputeCrtMask(float uvY)
            {
                float scan = frac(uvY * _ScanlineCount);
                float halfDuty = _ScanlineWidth * 0.5;
                return smoothstep(halfDuty, 0.0, abs(scan - 0.5) - (0.5 - halfDuty));
            }

            float DetectNeonContent(half3 rgb)
            {
                float luma = dot(rgb, half3(0.299, 0.587, 0.114));
                float cyan = saturate(rgb.g - rgb.r * 0.7);
                float gold = saturate(rgb.r - rgb.b * 0.8) * step(0.2, rgb.r);
                return saturate(luma * 1.8 + cyan * 1.2 + gold * 0.8);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 col = SampleSource(uv);
                float master = saturate(_EventMaster);

                float crtMask = ComputeCrtMask(uv.y);
                float crtStrength = lerp(_ScanlineOpacity, _ScanlineOpacity * 0.25, master);
                col.rgb *= lerp(1.0, 1.0 - crtStrength, crtMask);

                float2 vigD = uv - 0.5;
                float vig = pow(saturate(1.0 - dot(vigD, vigD) * _VignetteRoundness), _VignettePower);
                col.rgb *= lerp(1.0 - _VignetteStrength, 1.0, vig);

                if (master > 0.001)
                {
                    float pxDist = abs(uv.y - _EventHeadY) * _ScreenParams.y;
                    float breathe = 0.86 + 0.14 * sin(_EventTime * 5.0);

                    float coreMask = smoothstep(1.1, 0.0, pxDist);
                    float glowMask = smoothstep(4.5, 1.0, pxDist) * (1.0 - coreMask);

                    col.rgb += _EventColor.rgb * coreMask * _EventLineIntensity * master * breathe;
                    col.rgb += _EventColor.rgb * glowMask * _EventLineIntensity * 0.38 * master * breathe;

                    float wake = smoothstep(_EventWakePx, 0.0, pxDist) * (1.0 - coreMask);
                    col.rgb *= 1.0 + wake * 0.06 * master;

                    float revealBand = smoothstep(0.0, 48.0, (uv.y - _EventHeadY) * _ScreenParams.y);
                    float unrevealed = 1.0 - revealBand;
                    col.rgb *= lerp(1.0, 1.0 - _EventRevealDim, unrevealed * master);

                    float interactBand = smoothstep(10.0, 0.0, pxDist);
                    float neon = DetectNeonContent(col.rgb);
                    col.rgb += _EventColor.rgb * neon * interactBand * _EventInteractBoost * master;
                }

                return half4(col.rgb, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
