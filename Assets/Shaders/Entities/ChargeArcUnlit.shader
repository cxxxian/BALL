Shader "Custom/ChargeArcUnlit"
{
    Properties
    {
        _NoiseScale ("Turbulence Scale", Range(4, 40)) = 18
        _NoiseSpeed ("Turbulence Speed", Range(0, 8)) = 2.2
        _Wobble ("Core Wobble", Range(0, 0.6)) = 0.34
        _GlowWidth ("Glow Width", Range(0.05, 0.8)) = 0.48
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPos : TEXCOORD1;
            };

            float _NoiseScale;
            float _NoiseSpeed;
            float _Wobble;
            float _GlowWidth;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Turbulence(float2 p)
            {
                float n0 = ValueNoise(p);
                float n1 = ValueNoise(p * 2.03 + 13.7);
                float n2 = ValueNoise(p * 4.11 - 7.9);
                return n0 * 0.58 + n1 * 0.29 + n2 * 0.13;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.localPos = v.vertex.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _NoiseSpeed;
                float seed = dot(i.localPos, float2(17.13, 9.17)) * 0.07;
                float2 flow = float2(i.uv.x * _NoiseScale + t,
                    i.uv.x * 3.7 - t * 0.73 + seed);

                float broad = Turbulence(flow);
                float fine = Turbulence(flow * 1.71 + 4.3);
                float center = 0.5
                    + (broad - 0.5) * _Wobble
                    + (fine - 0.5) * (_Wobble * 0.34);

                float distanceToCore = abs(i.uv.y - center);
                float core = 1.0 - smoothstep(0.045, 0.17, distanceToCore);
                float glow = 1.0 - smoothstep(0.10, _GlowWidth, distanceToCore);

                float filamentCenter = center
                    + (fine - 0.5) * 0.31;
                float filament = 1.0 - smoothstep(0.018, 0.065,
                    abs(i.uv.y - filamentCenter));
                float flicker = 0.68 + 0.32 * Turbulence(
                    flow * 0.63 + float2(0, t * 1.9));

                float energy = saturate(glow * 0.27 + core * 0.74
                    + filament * 0.24) * flicker;
                float3 plasma = lerp(i.color.rgb,
                    float3(0.49, 0.72, 1.0), saturate(glow * 0.38));
                plasma = lerp(plasma, float3(0.83, 0.98, 1.0),
                    saturate(core * 0.72 + filament * 0.30));

                return float4(plasma, saturate(i.color.a * energy));
            }
            ENDCG
        }
    }
}
