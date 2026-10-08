Shader "Hidden/ReboundProtocol/SlotBackdropGaussian"
{
    Properties { _MainTex ("Scene", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Direction;
            half4 frag(v2f_img i) : SV_Target
            {
                float2 stepUV = _MainTex_TexelSize.xy * _Direction.xy;
                half3 color = tex2D(_MainTex, i.uv).rgb * .227027;
                color += (tex2D(_MainTex, i.uv + stepUV * 1.384615).rgb +
                          tex2D(_MainTex, i.uv - stepUV * 1.384615).rgb) * .316216;
                color += (tex2D(_MainTex, i.uv + stepUV * 3.230769).rgb +
                          tex2D(_MainTex, i.uv - stepUV * 3.230769).rgb) * .070270;
                return half4(color, 1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment grade
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Brightness;
            half4 grade(v2f_img i) : SV_Target
            {
                half3 rgb = tex2D(_MainTex, i.uv).rgb;
                half luminance = dot(rgb, half3(.2126, .7152, .0722));
                rgb = lerp(luminance.xxx, rgb, .68);
                float radius = length((i.uv - .5) * float2(1, 1.15));
                float vignette = lerp(1, .55, smoothstep(.18, .72, radius));
                return half4(rgb * _Brightness * vignette, 1);
            }
            ENDCG
        }
    }
}
