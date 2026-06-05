Shader "Custom/CyberPulseSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (0.2, 0.95, 1, 1)
        _CoreColor ("Core Color", Color) = (1,1,1,1)
        _PulseSpeed ("Pulse Speed", Float) = 4.0
        _RingSharpness ("Ring Sharpness", Range(0.5, 12)) = 6.0
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.5
        _DistortAmount ("Distort Amount", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _GlowColor;
            fixed4 _CoreColor;
            float _PulseSpeed;
            float _RingSharpness;
            float _GlowIntensity;
            float _DistortAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;
                float2 p = i.uv * 2.0 - 1.0;
                float dist = length(p);

                float t = _Time.y * _PulseSpeed;
                float pulse = 0.5 + 0.5 * sin(t + dist * 14.0);
                float ring = pow(saturate(1.0 - abs(dist - 0.55) * _RingSharpness), 1.2);
                float core = saturate(1.0 - dist * 1.9);
                float scan = 0.5 + 0.5 * sin((p.y + _Time.y * 2.0) * 60.0 + dist * 20.0);
                float flare = ring * (0.55 + 0.45 * pulse) + core * 0.9;
                flare += ring * scan * 0.22;

                fixed4 col = lerp(_GlowColor, _CoreColor, core);
                col.rgb *= (1.0 + flare * _GlowIntensity);
                col.a *= saturate(tex.a * (flare + core * 0.8));
                return col;
            }
            ENDCG
        }
    }
}
