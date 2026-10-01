Shader "Hidden/MenuWakeField"
{
    // Stable Fluids + Vorticity Confinement（思路对齐 MagicStones23 / GPU Gems）
    // Pass: 0 Advect  1 Curl  2 Vorticity  3 Divergence  4 Jacobi  5 Project  6 Damp  7 Splat  8 Transport
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
        float4 _Domain; // xy domain size in screen-height units
        float4 _SplatStart; // previous pointer UV
        float _Recovery;
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
            float2 texel = abs(_VelocityTex_TexelSize.xy);
            return float2(
                SAMPLE_TEXTURE2D(_VelocityTex, sampler_VelocityTex, saturate(uv - float2(0.5 * texel.x, 0))).r,
                SAMPLE_TEXTURE2D(_VelocityTex, sampler_VelocityTex, saturate(uv - float2(0, 0.5 * texel.y))).g);
        }

        float2 SampleVelMain(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv)).rg;
        }

        float SampleR(Texture2D tex, SamplerState s, float2 uv)
        {
            return SAMPLE_TEXTURE2D(tex, s, saturate(uv)).r;
        }

        // Face velocities: x on the right face, y on the top face.
        // Backward divergence + forward gradient compose the Jacobi Laplacian.
        float2 CellSize() { return abs(_MainTex_TexelSize.xy) * _Domain.xy; }
        float2 WallVelocity(float2 uv, float2 v)
        {
            float2 edge = 1.0 - 0.5 * abs(_MainTex_TexelSize.xy);
            if (uv.x >= edge.x - 1e-6) v.x = 0;
            if (uv.y >= edge.y - 1e-6) v.y = 0;
            return v;
        }

        float2 ClampVel(float2 v)
        {
            float m = length(v);
            if (m > 3.0)
                v *= 3.0 / m;
            return v;
        }
        ENDHLSL

        // 0: 速度自平流
        Pass
        {
            Name "Advect"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragAdvect
            float4 FragAdvect(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 face = SampleVelMain(IN.uv);
                float2 vx = float2(face.x, SampleVelMain(IN.uv + float2(0.5 * texel.x, -0.5 * texel.y)).y);
                float2 vy = float2(SampleVelMain(IN.uv + float2(-0.5 * texel.x, 0.5 * texel.y)).x, face.y);
                float2 outV = float2(
                    SampleVelMain(IN.uv - _Dt * vx / _Domain.xy).x,
                    SampleVelMain(IN.uv - _Dt * vy / _Domain.xy).y) * _Dissipation;
                return float4(WallVelocity(IN.uv, outV), 0, 1);
            }
            ENDHLSL
        }

        // 1: curl = ∂vy/∂x - ∂vx/∂y
        Pass
        {
            Name "Curl"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragCurl
            float4 FragCurl(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 h = CellSize();
                float2 L = SampleVelMain(IN.uv - float2(texel.x, 0));
                float2 R = SampleVelMain(IN.uv + float2(texel.x, 0));
                float2 B = SampleVelMain(IN.uv - float2(0, texel.y));
                float2 T = SampleVelMain(IN.uv + float2(0, texel.y));
                return float4((R.y - L.y) / (2 * h.x) - (T.x - B.x) / (2 * h.y), 0, 0, 1);
            }
            ENDHLSL
        }

        // 2: vorticity confinement → 漩涡持久、可搅动
        Pass
        {
            Name "Vorticity"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragVorticity
            float4 FragVorticity(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 h = CellSize();
                float L = SampleR(_CurlTex, sampler_CurlTex, IN.uv - float2(texel.x, 0));
                float R = SampleR(_CurlTex, sampler_CurlTex, IN.uv + float2(texel.x, 0));
                float B = SampleR(_CurlTex, sampler_CurlTex, IN.uv - float2(0, texel.y));
                float T = SampleR(_CurlTex, sampler_CurlTex, IN.uv + float2(0, texel.y));
                float C = SampleR(_CurlTex, sampler_CurlTex, IN.uv);
                float2 gradient = float2(abs(R) - abs(L), abs(T) - abs(B)) / (2 * h);
                float2 n = gradient / max(length(gradient), 1e-5);
                float2 force = _Vorticity * min(h.x, h.y) * C * float2(n.y, -n.x);
                return float4(WallVelocity(IN.uv, ClampVel(SampleVelMain(IN.uv) + _Dt * force)), 0, 1);
            }
            ENDHLSL
        }

        // 3: divergence
        Pass
        {
            Name "Divergence"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragDiv
            float4 FragDiv(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 h = CellSize();
                float2 C = WallVelocity(IN.uv, SampleVelMain(IN.uv));
                float2 L = SampleVelMain(IN.uv - float2(texel.x, 0));
                float2 B = SampleVelMain(IN.uv - float2(0, texel.y));
                if (IN.uv.x < texel.x) L.x = 0;
                if (IN.uv.y < texel.y) B.y = 0;
                return float4((C.x - L.x) / h.x + (C.y - B.y) / h.y, 0, 0, 1);
            }
            ENDHLSL
        }

        // 4: Jacobi pressure
        Pass
        {
            Name "Jacobi"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragJacobi
            float4 FragJacobi(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 h = CellSize();
                float2 w = 1.0 / (h * h);
                float L = SampleR(_MainTex, sampler_MainTex, IN.uv - float2(texel.x, 0));
                float R = SampleR(_MainTex, sampler_MainTex, IN.uv + float2(texel.x, 0));
                float B = SampleR(_MainTex, sampler_MainTex, IN.uv - float2(0, texel.y));
                float T = SampleR(_MainTex, sampler_MainTex, IN.uv + float2(0, texel.y));
                float b = SampleR(_DivTex, sampler_DivTex, IN.uv);
                return float4(((L + R) * w.x + (B + T) * w.y - b) / (2 * (w.x + w.y)), 0, 0, 1);
            }
            ENDHLSL
        }

        // 5: project u = u - ∇p
        Pass
        {
            Name "Project"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragProject
            float4 FragProject(Varyings IN) : SV_Target
            {
                float2 texel = abs(_MainTex_TexelSize.xy);
                float2 h = CellSize();
                float C = SampleR(_PressureTex, sampler_PressureTex, IN.uv);
                float R = SampleR(_PressureTex, sampler_PressureTex, IN.uv + float2(texel.x, 0));
                float T = SampleR(_PressureTex, sampler_PressureTex, IN.uv + float2(0, texel.y));
                float2 vel = SampleVelMain(IN.uv) - float2(R - C, T - C) / h;
                return float4(WallVelocity(IN.uv, vel), 0, 1);
            }
            ENDHLSL
        }

        // 6: velocity damping
        Pass
        {
            Name "Damp"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragDamp
            float4 FragDamp(Varyings IN) : SV_Target
            {
                return float4(WallVelocity(IN.uv, SampleVelMain(IN.uv) * _Damping), 0, 1);
            }
            ENDHLSL
        }

        // 7: mouse splat — 只沿滑动方向推水（椭圆笔刷），避免圆形汇聚造成“吸向鼠标”
        Pass
        {
            Name "Splat"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragSplat
            float4 FragSplat(Varyings IN) : SV_Target
            {
                float2 a = _SplatStart.xy * _Domain.xy;
                float2 b = _SplatUV.xy * _Domain.xy;
                float2 x = IN.uv * _Domain.xy;
                float2 segment = b - a;
                float t = saturate(dot(x - a, segment) / max(dot(segment, segment), 1e-8));
                float2 d = x - lerp(a, b, t);
                float r = max(_SplatUV.z, 1e-4);
                float weight = exp(-dot(d, d) / (r * r));
                float2 vel = SampleVelMain(IN.uv) + _SplatVel.xy * weight * _SplatUV.w;
                return float4(WallVelocity(IN.uv, ClampVel(vel)), 0, 1);
            }
            ENDHLSL
        }
        // Inverse material map: d(x,t+dt) = d(x-dt*u,t) + dt*u.
        // Relaxation is an artistic return to the original digital rain.
        Pass
        {
            Name "Transport"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragTransport
            float4 FragTransport(Varyings IN) : SV_Target
            {
                float2 velUv = SampleVel(IN.uv) / _Domain.xy;
                float2 back = IN.uv - _Dt * velUv;
                float2 displacement = (SampleVelMain(back) + _Dt * velUv) * exp(-_Recovery * _Dt);
                float2 physical = displacement * _Domain.xy;
                displacement *= min(1.0, 0.12 / max(length(physical), 1e-6));
                float2 edge = min(IN.uv, 1.0 - IN.uv) * _Domain.xy;
                displacement *= smoothstep(0.0, 0.03, min(edge.x, edge.y));
                return float4(displacement, 0, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
