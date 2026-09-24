Shader "Hidden/MenuWakeField"
{
    // Stable Fluids + Vorticity Confinement（思路对齐 MagicStones23 / GPU Gems）
    // Pass: 0 Advect  1 Curl  2 Vorticity  3 Divergence  4 Jacobi  5 Project  6 Damp  7 Splat
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_VelocityTex);
        SAMPLER(sampler_VelocityTex);
        TEXTURE2D(_PressureTex);
        SAMPLER(sampler_PressureTex);
        TEXTURE2D(_CurlTex);
        SAMPLER(sampler_CurlTex);
        TEXTURE2D(_DivTex);
        SAMPLER(sampler_DivTex);

        float4 _MainTex_TexelSize;
        float4 _VelocityTex_TexelSize;
        float _Dt;
        float _Dissipation;
        float _Vorticity;
        float _Damping;
        float4 _SplatUV;   // xy center, z radius, w strength
        float4 _SplatVel;  // xy velocity inject

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

        Varyings Vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = IN.uv;
            return OUT;
        }

        float2 SampleVel(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_VelocityTex, sampler_VelocityTex, saturate(uv)).rg;
        }

        float2 SampleVelMain(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).rg;
        }

        float SampleR(Texture2D tex, SamplerState s, float2 uv)
        {
            return SAMPLE_TEXTURE2D(tex, s, saturate(uv)).r;
        }

        float2 ClampVel(float2 v)
        {
            float m = length(v);
            if (m > 8.0)
                v *= 8.0 / m;
            return v;
        }
        ENDHLSL

        // 0: 速度自平流
        Pass
        {
            Name "Advect"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAdvect
            float4 FragAdvect(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 vel = SampleVelMain(IN.uv);
                float2 coord = IN.uv - _Dt * vel * texel * 80.0;
                float2 outV = SampleVelMain(coord) * _Dissipation;
                return float4(ClampVel(outV), 0, 1);
            }
            ENDHLSL
        }

        // 1: curl = ∂vy/∂x - ∂vx/∂y
        Pass
        {
            Name "Curl"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCurl
            float4 FragCurl(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 L = SampleVelMain(IN.uv - float2(texel.x, 0));
                float2 R = SampleVelMain(IN.uv + float2(texel.x, 0));
                float2 B = SampleVelMain(IN.uv - float2(0, texel.y));
                float2 T = SampleVelMain(IN.uv + float2(0, texel.y));
                float curl = R.y - L.y - (T.x - B.x);
                return float4(curl * 0.5, 0, 0, 1);
            }
            ENDHLSL
        }

        // 2: vorticity confinement → 漩涡持久、可搅动
        Pass
        {
            Name "Vorticity"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVorticity
            float4 FragVorticity(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float L = SampleR(_CurlTex, sampler_CurlTex, IN.uv - float2(texel.x, 0));
                float R = SampleR(_CurlTex, sampler_CurlTex, IN.uv + float2(texel.x, 0));
                float B = SampleR(_CurlTex, sampler_CurlTex, IN.uv - float2(0, texel.y));
                float T = SampleR(_CurlTex, sampler_CurlTex, IN.uv + float2(0, texel.y));
                float C = SampleR(_CurlTex, sampler_CurlTex, IN.uv);

                float2 force = float2(abs(T) - abs(B), abs(R) - abs(L));
                float len = length(force) + 1e-5;
                force = force / len * _Vorticity;
                // 2D cross: force × curl
                force = float2(force.y * C, -force.x * C);

                float2 vel = SampleVelMain(IN.uv);
                vel += force * _Dt;
                return float4(ClampVel(vel), 0, 1);
            }
            ENDHLSL
        }

        // 3: divergence
        Pass
        {
            Name "Divergence"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDiv
            float4 FragDiv(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 L = SampleVelMain(IN.uv - float2(texel.x, 0));
                float2 R = SampleVelMain(IN.uv + float2(texel.x, 0));
                float2 B = SampleVelMain(IN.uv - float2(0, texel.y));
                float2 T = SampleVelMain(IN.uv + float2(0, texel.y));
                float div = 0.5 * (R.x - L.x + T.y - B.y);
                return float4(div, 0, 0, 1);
            }
            ENDHLSL
        }

        // 4: Jacobi pressure
        Pass
        {
            Name "Jacobi"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragJacobi
            float4 FragJacobi(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float L = SampleR(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0));
                float R = SampleR(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0));
                float B = SampleR(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y));
                float T = SampleR(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y));
                float b = SampleR(_DivTex, sampler_DivTex, IN.uv);
                float p = (L + R + B + T - b) * 0.25;
                return float4(p, 0, 0, 1);
            }
            ENDHLSL
        }

        // 5: project u = u - ∇p
        Pass
        {
            Name "Project"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragProject
            float4 FragProject(Varyings IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 vel = SampleVelMain(IN.uv);
                float L = SampleR(_PressureTex, sampler_PressureTex, IN.uv - float2(texel.x, 0));
                float R = SampleR(_PressureTex, sampler_PressureTex, IN.uv + float2(texel.x, 0));
                float B = SampleR(_PressureTex, sampler_PressureTex, IN.uv - float2(0, texel.y));
                float T = SampleR(_PressureTex, sampler_PressureTex, IN.uv + float2(0, texel.y));
                float2 grad = float2(R - L, T - B) * 0.5;
                return float4(ClampVel(vel - grad), 0, 1);
            }
            ENDHLSL
        }

        // 6: velocity damping
        Pass
        {
            Name "Damp"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDamp
            float4 FragDamp(Varyings IN) : SV_Target
            {
                float2 vel = SampleVelMain(IN.uv) * _Damping;
                // 边界轻微贴壁，减少边缘炸开
                float edge = min(min(IN.uv.x, 1.0 - IN.uv.x), min(IN.uv.y, 1.0 - IN.uv.y));
                float wall = smoothstep(0.0, 0.02, edge);
                return float4(ClampVel(vel * wall), 0, 1);
            }
            ENDHLSL
        }

        // 7: mouse splat — 只沿滑动方向推水（椭圆笔刷），避免圆形汇聚造成“吸向鼠标”
        Pass
        {
            Name "Splat"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSplat
            float4 FragSplat(Varyings IN) : SV_Target
            {
                float2 vel = SampleVelMain(IN.uv);
                float strength = _SplatUV.w;
                float2 inj = _SplatVel.xy;
                float injSpeed = length(inj);

                if (strength > 1e-5 && injSpeed > 1e-5)
                {
                    float2 d = IN.uv - _SplatUV.xy;
                    float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                    d.x *= aspect;

                    float2 fwd = inj / injSpeed;
                    float2 side = float2(-fwd.y, fwd.x);
                    float along = dot(d, fwd);
                    float lateral = dot(d, side);
                    float r = max(_SplatUV.z, 1e-4);

                    // 细长尾迹：顺着运动方向拉长，横向收窄（像拨水，而不是圆盘）
                    float fall = exp(-(along * along) / (r * r * 2.8) - (lateral * lateral) / (r * r * 0.28));

                    // 主要推前方与脚下，背后减弱，减少回流“吸进去”的错觉
                    float forwardBias = smoothstep(-0.35, 0.65, along / r);

                    vel += inj * (fall * strength * forwardBias);
                }

                return float4(ClampVel(vel), 0, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
