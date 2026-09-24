Shader "Custom/ChargeArcUnlit"
{
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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = abs(i.uv.y * 2.0 - 1.0);
                float body = 1.0 - smoothstep(0.55, 1.0, d);
                float hot = 1.0 - smoothstep(0.0, 0.43, d);
                float3 rgb = i.color.rgb * (0.85 + 0.25 * hot);
                return float4(rgb, saturate(i.color.a * body));
            }
            ENDCG
        }
    }
}