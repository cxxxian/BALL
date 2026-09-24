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

            #define ARCADE_CRT_TWO_PI 6.28318530718

            float _ScanlineDark;
            float _ScanlineBright;
            float _ScanlineSharpness;
            float4 _PhosphorTint;
            float _NeonBoost;
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

            float ComputePhosphorMask(float screenY)
            {
                float linePhase = frac(screenY * 0.5);
                float wave = 0.5 + 0.5 * cos(linePhase * ARCADE_CRT_TWO_PI);
                return pow(saturate(wave), _ScanlineSharpness);
            }

            float DetectNeonContent(half3 rgb)
            {
                float luma = dot(rgb, half3(0.299, 0.587, 0.114));
                float cyan = saturate(rgb.g - rgb.r * 0.7);
                float gold = saturate(rgb.r - rgb.b * 0.8) * step(0.2, rgb.r);
                return saturate(luma * 1.8 + cyan * 1.2 + gold * 0.8);
            }

            float ComputeContentBoost(half3 rgb)
            {
                float neon = DetectNeonContent(rgb);
                float hdr = saturate(max(rgb.r, max(rgb.g, rgb.b)) - 1.0);
                return saturate(neon * 0.6 + hdr * 1.2);
            }

            void ApplyPhosphorScanlines(inout half3 col, float screenY)
            {
                float scanMask = ComputePhosphorMask(screenY);
                float contentBoost = ComputeContentBoost(col);

                float effectiveDark = _ScanlineDark * lerp(0.7, 1.4 * _NeonBoost, contentBoost);
                float effectiveBright = _ScanlineBright * lerp(0.5, 1.8 * _NeonBoost, contentBoost);

                float darkAmt = effectiveDark * scanMask;
                float brightAmt = effectiveBright * (1.0 - scanMask);

                col *= (1.0 - darkAmt);
                col += brightAmt * _PhosphorTint.rgb;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 col = SampleSource(uv);
                float master = saturate(_EventMaster);
                float screenY = uv.y * _ScreenParams.y;

                ApplyPhosphorScanlines(col.rgb, screenY);

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
