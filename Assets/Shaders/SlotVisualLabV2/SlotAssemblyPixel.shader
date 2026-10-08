Shader "ReboundProtocol/SlotVisualLabV2/AssemblyPixel"
{
    Properties { _Gain("Pixel emission",Float)=3 _WhiteGeometry("White voxel geometry",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Gain,_WhiteGeometry;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;float shade:TEXCOORD0;};
            V Vert(A v)
            {
                V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;
                float3 n=TransformObjectToWorldNormal(v.normalOS);
                float basic=.55+.45*abs(dot(normalize(n),normalize(float3(.3,.6,-.8))));
                float solid=.20+.80*saturate(dot(normalize(n),normalize(float3(-.35,.55,-.76))));
                o.shade=lerp(basic,solid,_WhiteGeometry);return o;
            }
            half4 Frag(V i):SV_Target {return half4(lerp(i.color.rgb,half3(1,1,1),_WhiteGeometry)*_Gain*i.shade,i.color.a);}
            ENDHLSL
        }
    }
}
