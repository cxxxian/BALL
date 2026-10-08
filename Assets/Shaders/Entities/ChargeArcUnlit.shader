// Electro-style profile: continuous displaced ridge and nonlinear HDR falloff.
// Noise below is independently written lattice value noise, not the supplied Simplex implementation.
Shader "Custom/ChargeArcUnlit"
{
    Properties
    {
        [HDR] _PlasmaColor ("Electro Color", Color) = (1.70,1.48,1.78,1)
        _NoiseScale ("Turbulence Scale", Range(4,40)) = 12
        _NoiseSpeed ("Turbulence Speed", Range(0,8)) = 0.4
        _Wobble ("Core Wobble", Range(0,0.6)) = 0.30
        _GlowWidth ("Glow Width", Range(0.05,0.8)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 noiseSeed:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float seed:TEXCOORD1; };
            float4 _PlasmaColor;
            float _NoiseScale, _NoiseSpeed, _Wobble, _GlowWidth;
            float _NoiseSeed;
            float Hash3(float3 p)
            {
                p=frac(p*float3(.1031,.11369,.13787));
                p+=dot(p,p.yzx+19.19);
                return frac((p.x+p.y)*p.z);
            }
            float Noise3(float3 p)
            {
                float3 c=floor(p), f=frac(p);
                f=f*f*(3.0-2.0*f);
                float a=lerp(lerp(Hash3(c),Hash3(c+float3(1,0,0)),f.x),
                    lerp(Hash3(c+float3(0,1,0)),Hash3(c+float3(1,1,0)),f.x),f.y);
                float b=lerp(lerp(Hash3(c+float3(0,0,1)),Hash3(c+float3(1,0,1)),f.x),
                    lerp(Hash3(c+float3(0,1,1)),Hash3(c+1.0),f.x),f.y);
                return lerp(a,b,f.z)*2.0-1.0;
            }
            float Turbulence(float3 p)
            {
                return Noise3(p)*.533333 + Noise3(p*2.0)*.266667
                    + Noise3(p*4.0)*.133333 + Noise3(p*8.0)*.066667;
            }
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.seed=v.noiseSeed.x+_NoiseSeed;return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float x=i.uv.x*2.0-1.0;
                // One center value across the ribbon width keeps the filament continuous.
                float intensity=Turbulence(float3(i.uv.x*_NoiseScale+12.0,12.0+i.seed,_Time.y*_NoiseSpeed*12.0));
                float envelope=saturate(.15-x*x*.16);
                float center=intensity*envelope*(_Wobble/.10);
                float y=abs((i.uv.y-.5)/max(_GlowWidth,.05)-center);
                // Pixel integration around the cusp avoids a disappearing subpixel bright core.
                float aa=max(fwidth(y)*.10,.0001);
                y=max(y-aa,0.0);
                float g=pow(saturate(y),.2);
                float3 col=_PlasmaColor.rgb*(1.0-g);
                col*=col; col*=col;
                float edge=1.0-smoothstep(.42,.5,abs(i.uv.y-.5));
                return float4(col*i.color.rgb*i.color.a*edge,0);
            }
            ENDCG
        }
    }
}
