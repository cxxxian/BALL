Shader "Custom/EnemyBuildStack"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrostColor ("Frost White", Color) = (0.70,0.82,0.88,1)
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _IceRoughness ("Ice Surface Frost", Range(0,1)) = 0.38
        _IceDensity ("Ice Internal Density", Range(0,2)) = 0.7
        _IceIOR ("Ice Refraction Index", Range(1.01,1.6)) = 1.31
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
            #pragma target 3.0
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _FrostColor;
            float4 _SpriteUVRect;
            float _PatternSeed;
            float _FrostStack;
            float _EventPulse;
            float _ConsumePulse;
            float _FreezeAmount;
            float _IceRoughness, _IceDensity, _IceIOR;

            float IceHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float IceNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(IceHash(cell), IceHash(cell+float2(1,0)), f.x),
                    lerp(IceHash(cell+float2(0,1)), IceHash(cell+1.0), f.x), f.y);
            }
            float IceRelief(float2 uv)
            {
                float2 p = uv + _PatternSeed * float2(2.7,5.3);
                return IceNoise(p*15.0)*0.7 + IceNoise(p*39.0)*0.3;
            }

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
                    float2(0.24, 0.22) * (1.0 + tier2 * 0.15 + tier3 * 0.15),
                    _PatternSeed * 7.0) * tier1;
                float p1 = Patch(uv, float2(0.29, 0.35) - shift,
                    float2(0.23, 0.26) * (1.0 + tier3 * 0.15),
                    _PatternSeed * 9.0 + 3.0) * tier2;
                float p2 = Patch(uv, float2(0.49, 0.78) + shift * 0.5,
                    float2(0.28, 0.20), _PatternSeed * 11.0 + 5.0) * tier3;
                float frost = saturate(max(p0, max(p1, p2)));
                // Short freeze retains a shell even when a frost burst has consumed all marks.
                frost = max(frost, saturate(_FreezeAmount) * .9);
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

                // A local Sprite ice shell: approximate optics without scene copies or raymarching.
                // Sample only inside this sprite's atlas rectangle; preserve its original silhouette.
                float relief = IceRelief(uv);
                float2 gradient = float2(IceRelief(uv+float2(.006,0))-relief,
                    IceRelief(uv+float2(0,.006))-relief)/.006;
                float3 normal = normalize(float3(-gradient * (.035 + _IceRoughness*.06), 1.0));
                float thickness = frost * (.35 + relief*.65 + tier3*.15 + saturate(_FreezeAmount)*.18);
                float3 ray = refract(float3(0,0,-1), normal, 1.0/max(_IceIOR,1.01));
                float2 localRefracted = uv + ray.xy * thickness * .075;
                float2 atlasInset = _MainTex_TexelSize.xy * .5;
                float2 refractedUV = clamp(lerp(_SpriteUVRect.xy,_SpriteUVRect.zw,localRefracted),
                    _SpriteUVRect.xy+atlasInset, _SpriteUVRect.zw-atlasInset);
                float4 refracted = tex2D(_MainTex, TRANSFORM_TEX(refractedUV,_MainTex)) * i.color;
                float3 transmitted = lerp(tex.rgb,refracted.rgb,saturate(refracted.a)*.8);
                float scattering = 1.0-exp(-thickness * _IceDensity * 1.7);
                float3 ice = lerp(transmitted*float3(.85,.95,1.0),
                    lerp(_FrostColor.rgb,float3(.95,.98,1.0),.5), scattering);
                float frostGrain = smoothstep(.38,.78,relief)*_IceRoughness;
                ice = lerp(ice,float3(.89,.95,.97),frostGrain*.48);
                float3 halfDir = normalize(float3(-.55,.75,1.6));
                float specular = pow(saturate(dot(normal,halfDir)),lerp(90.0,18.0,_IceRoughness));
                float fresnel = .018 + .982*pow(1.0-saturate(normal.z),5.0);
                ice += specular * float3(.7,.78,.8) + fresnel * float3(.45,.6,.7);
                float grainAA = 1.0-smoothstep(.035,.09,max(length(ddx(uv)),length(ddy(uv))));
                ice += smoothstep(.84,.97,relief)*grainAA*float3(.15,.2,.22);
                float3 rgb = lerp(tex.rgb,ice,saturate(frost*.92));
                rgb += float3(.18,.26,.3)*rim*(.35+saturate(_EventPulse)*.35);
                rgb = lerp(rgb,float3(.83,.94,.98),cracks*.55);
                rgb = lerp(rgb,float3(.85,.96,1.0),crystals*.62);
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
