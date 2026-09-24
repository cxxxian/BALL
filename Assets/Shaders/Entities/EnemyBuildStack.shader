Shader "Custom/EnemyBuildStack"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrostColor ("Frost White", Color) = (0.70,0.82,0.88,1)
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 texUV : TEXCOORD0;
                float2 spriteUV : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _FrostColor;
            float4 _SpriteUVRect;
            float _PatternSeed;
            float _FrostStack;
            float _EventPulse;
            float _ConsumePulse;
            float _FreezeAmount;

            float SegmentLine(float2 p, float2 a, float2 b, float width)
            {
                float2 ab = b - a;
                float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.00001));
                float d = length(p - (a + h * ab));
                return 1.0 - smoothstep(width, width + 0.007, d);
            }

            float Cross2(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

            float TriangleFill(float2 p, float2 a, float2 b, float2 c)
            {
                float ab = Cross2(b - a, p - a);
                float bc = Cross2(c - b, p - b);
                float ca = Cross2(a - c, p - c);
                return step(0.0, ab * bc) * step(0.0, bc * ca);
            }

            float Patch(float2 uv, float2 center, float2 radius, float seed)
            {
                float2 q = (uv - center) / radius;
                float d = length(q);
                d += sin(q.x * 9.0 + q.y * 6.0 + seed) * 0.075;
                d += sin(q.y * 13.0 - q.x * 5.0 + seed * 1.7) * 0.045;
                return 1.0 - smoothstep(0.79, 1.08, d);
            }

            float PatchRim(float patch)
            {
                return smoothstep(0.12, 0.40, patch) *
                    (1.0 - smoothstep(0.65, 0.93, patch));
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texUV = TRANSFORM_TEX(v.uv, _MainTex);
                o.spriteUV = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texUV) * i.color;
                clip(tex.a - 0.01);

                float2 uv = saturate((i.spriteUV - _SpriteUVRect.xy) /
                    max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.0001, 0.0001)));
                float tier1 = step(0.5, _FrostStack);
                float tier2 = step(1.5, _FrostStack);
                float tier3 = step(2.5, _FrostStack);
                float2 shift = float2(sin(_PatternSeed * 18.0),
                    cos(_PatternSeed * 21.0)) * 0.025;

                float p0 = Patch(uv, float2(0.70, 0.64) + shift,
                    float2(0.16, 0.14) * (1.0 + tier2 * 0.10 + tier3 * 0.10),
                    _PatternSeed * 7.0) * tier1;
                float p1 = Patch(uv, float2(0.29, 0.35) - shift,
                    float2(0.15, 0.17) * (1.0 + tier3 * 0.10),
                    _PatternSeed * 9.0 + 3.0) * tier2;
                float p2 = Patch(uv, float2(0.49, 0.78) + shift * 0.5,
                    float2(0.18, 0.12), _PatternSeed * 11.0 + 5.0) * tier3;
                float frost = saturate(max(p0, max(p1, p2)));
                float rim = max(PatchRim(p0), max(PatchRim(p1), PatchRim(p2)));

                float cracks = 0.0;
                cracks = max(cracks, SegmentLine(uv, float2(0.63, 0.67) + shift,
                    float2(0.74, 0.59) + shift, 0.011) * tier1);
                cracks = max(cracks, SegmentLine(uv, float2(0.27, 0.29) - shift,
                    float2(0.37, 0.39) - shift, 0.011) * tier2);
                cracks = max(cracks, SegmentLine(uv, float2(0.45, 0.79) + shift * 0.5,
                    float2(0.55, 0.72) + shift * 0.5, 0.012) * tier3);
                cracks *= frost;

                float crystals = 0.0;
                crystals = max(crystals, TriangleFill(uv,
                    float2(0.39, 0.41) - shift, float2(0.45, 0.48) - shift,
                    float2(0.40, 0.51) - shift) * tier2);
                crystals = max(crystals, TriangleFill(uv,
                    float2(0.59, 0.82) + shift * 0.5,
                    float2(0.66, 0.88) + shift * 0.5,
                    float2(0.57, 0.91) + shift * 0.5) * tier3);

                float3 frostSurface = _FrostColor.rgb + tex.rgb * 0.18;
                float strength = frost * (0.70 + tier2 * 0.05 + tier3 * 0.05);
                float3 rgb = lerp(tex.rgb, frostSurface, saturate(strength));
                rgb += float3(0.24, 0.31, 0.33) * rim *
                    (0.25 + saturate(_EventPulse) * 0.20);
                rgb = lerp(rgb, float3(0.83, 0.94, 0.98), cracks * 0.62);
                rgb = lerp(rgb, float3(0.77, 0.91, 0.96), crystals * 0.58);
                rgb = lerp(rgb, frostSurface,
                    saturate(_FreezeAmount) * frost * 0.07);
                rgb += float3(0.08, 0.12, 0.14) * saturate(_ConsumePulse) * rim;

                fixed4 outC;
                outC.rgb = rgb * tex.a;
                outC.a = tex.a;
                return outC;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}