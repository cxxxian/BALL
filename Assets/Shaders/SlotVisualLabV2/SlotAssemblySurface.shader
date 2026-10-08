Shader "ReboundProtocol/SlotVisualLabV2/AssemblySurface"
{
    Properties
    {
        _BaseMap("Clean device surface",2D)="white"{}
        _UseTexture("Textured face",Float)=1
        _Reveal("Materialization",Range(0,1))=0
        _EmissionStrength("Shared emission gain",Float)=2.4
        _Region("Panel UV bounds",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _UseTexture, _Reveal, _EmissionStrength;
                float4 _Region;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD0;};
            V Vert(A v) {V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                // Native image remains sharp; a clustered digital matte affects only the advancing tips.
                float2 cell=floor(i.uv*float2(64,114));
                float2 pixelUV=(cell+.5)/float2(64,114);
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half3 n=normalize(i.normalWS);
                half light=saturate(dot(n,normalize(float3(-.4,.6,-.8))));
                half spec=pow(light,18)*.20;
                half3 side=half3(.017,.027,.04)+light*.045+spec;
                half3 rgb=lerp(side,tex.rgb,_UseTexture);
                half alpha=lerp(1,tex.a,_UseTexture);
                clip(alpha-.05);
                float2 cluster=floor(cell/2);
                float clusterNoise=frac(sin(dot(cluster,float2(12.9898,78.233)))*43758.5453);
                float fineNoise=frac(sin(dot(cell,float2(39.346,11.135)))*47453.5453);
                float arrival=abs(pixelUV.y-.5)*1.64+(clusterNoise-.5)*.065+(fineNoise-.5)*.018;
                float age=_Reveal-arrival;
                float solid=smoothstep(-.008,.010,age)*smoothstep(0,.035,_Reveal);
                clip(solid-.005);
                float peak=max(rgb.r,max(rgb.g,rgb.b));
                float saturation=(peak-min(rgb.r,min(rgb.g,rgb.b)))/max(.0001,peak);
                float emit=smoothstep(.35,.65,saturation)*smoothstep(.35,.8,peak);
                rgb=rgb*(1+emit*_EmissionStrength);
                float edge=(1-smoothstep(.045,.115,age))*(1-smoothstep(.92,1,_Reveal));
                float detail=.12+.88*smoothstep(.06,.5,peak);
                float2 pixelCenterUV=(cell+.5)/float2(64,114);
                half3 pixelColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,pixelCenterUV).rgb;
                float pixelPeak=max(pixelColor.r,max(pixelColor.g,pixelColor.b));
                float highlight=detail*(.65+.35*fineNoise);
                float3 glow=float3(.7,1.55,3.0)*highlight;
                glow+=float3(1.1,1.1,1.1)*smoothstep(.4,.8,pixelPeak)*highlight;
                rgb=lerp(rgb,glow,edge);
                return half4(rgb,alpha*solid);
            }
            ENDHLSL
        }
    }
}
