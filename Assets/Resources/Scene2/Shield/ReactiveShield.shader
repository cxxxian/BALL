Shader "Scene2/ReactiveShield"
{
    Properties
    {
        _DetailTex ("Energy detail", 2D) = "gray" {}
        [HDR] _Tint ("Cold cyan", Color) = (0.05, 0.95, 1.55, 1)
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
    CBUFFER_START(UnityPerMaterial)
    float4 _Tint;
    CBUFFER_END
    float _Clock, _Formation, _Dissolve, _Charge, _IdleMotion;
    float4 _Impact0, _Impact1, _Impact2;
    struct Attributes { float4 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float3 cell:TEXCOORD1; };
    struct Varyings
    {
        float4 position:SV_POSITION;
        float3 positionWS:TEXCOORD0;
        float3 normalWS:TEXCOORD1;
        float3 normalOS:TEXCOORD2;
        float edge:TEXCOORD3;
        float3 cell:TEXCOORD4;
    };
    float3 HitNormal(float4 hit)
    {
        // The game camera faces +Z: the visible shell projects onto its -Z half.
        return float3(hit.xy, -sqrt(max(0.001,1-dot(hit.xy,hit.xy))));
    }
    float Ripple(float3 normal, float4 hit)
    {
        if (hit.z < 0 || hit.z > 0.48) return 0;
        float distance = acos(clamp(dot(normal,HitNormal(hit)),-0.9999,0.9999));
        float life = saturate(1-hit.z/0.48);
        float front = hit.z * 5.5;
        float width = 0.07 + hit.z * 0.22;
        float ring = exp(-pow((distance-front)/width,2));
        float flash = exp(-distance*distance*85) * exp(-hit.z*24);
        return (ring * 1.6 + flash * 2.2) * life * life;
    }
    Varyings vert(Attributes v)
    {
        Varyings o;
        float3 n = normalize(v.normal);
        float turn = 0.23 + sin(_Clock*0.22)*0.12*_IdleMotion;
        n = float3(n.x*cos(turn)+n.z*sin(turn),n.y,-n.x*sin(turn)+n.z*cos(turn));
        float breathing = sin(_Clock*1.27)*0.004 + sin(_Clock*2.03+1.4)*0.002;
        float tension = sin(dot(n,float3(3,5,2))+_Clock*0.61)*0.0025;
        float dent = 0;
        if (_Impact0.z >= 0 && _Impact0.z < 0.3)
            dent += exp(-pow(1-dot(n,HitNormal(_Impact0)),2)*600)*exp(-_Impact0.z*14)*0.025;
        if (_Impact1.z >= 0 && _Impact1.z < 0.3)
            dent += exp(-pow(1-dot(n,HitNormal(_Impact1)),2)*600)*exp(-_Impact1.z*14)*0.025;
        if (_Impact2.z >= 0 && _Impact2.z < 0.3)
            dent += exp(-pow(1-dot(n,HitNormal(_Impact2)),2)*600)*exp(-_Impact2.z*14)*0.025;
        float3 p = n * (1+(breathing+tension)*_IdleMotion-dent+_Charge*0.01);
        o.positionWS = TransformObjectToWorld(p);
        o.position = TransformWorldToHClip(o.positionWS);
        o.normalWS = TransformObjectToWorldNormal(n);
        o.normalOS = n;
        o.edge = v.uv.x;
        o.cell = v.cell;
        return o;
    }
    float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
    float4 frag(Varyings i):SV_Target
    {
        float3 n = normalize(i.normalOS);
        float3 nw = normalize(i.normalWS);
        float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
        float facing = dot(nw,view);
        float front = step(0,facing);
        float fresnel = pow(1-saturate(abs(facing)),2.2);
        float ripple = min(2.5,Ripple(n,_Impact0)+Ripple(n,_Impact1)+Ripple(n,_Impact2))*front;
        // Detail texture only uses spherical UV; geometric cell borders have no UV seams or poles.
        float tilt = 0.38 + sin(_Clock*0.24)*0.07*_IdleMotion;
        float3 mapped = float3(n.x,n.y*cos(tilt)-n.z*sin(tilt),n.y*sin(tilt)+n.z*cos(tilt));
        float yaw = 0.18 + sin(_Clock*0.19)*0.09*_IdleMotion;
        mapped = float3(mapped.x*cos(yaw)+mapped.z*sin(yaw),mapped.y,-mapped.x*sin(yaw)+mapped.z*cos(yaw));
        float2 spherical = float2(atan2(mapped.x,-mapped.z),asin(clamp(mapped.y,-0.999,0.999)));
        float edge = i.edge;
        float aa = max(fwidth(edge),0.008);
        float grid = smoothstep(0.95-aa,0.95+aa,edge);
        float seed = Hash(i.cell.xy*13+i.cell.z*7);
        float detail = SAMPLE_TEXTURE2D(_DetailTex,sampler_DetailTex,spherical*0.5+float2(_Clock*0.01,0)).r;
        // Directional illumination and two soft reflections establish rounded volume.
        float3 key = normalize(float3(-0.45,0.62,-0.64));
        float diffuse = saturate(dot(nw,key));
        float3 halfVector = normalize(key+view);
        float specular = pow(saturate(dot(nw,halfVector)),36)*front;
        float reflection = exp(-pow((dot(nw,normalize(float3(0.68,0.35,-0.64)))-0.93)/0.055,2))*front;
        reflection *= smoothstep(-0.2,0.4,nw.y);
        float angle = atan2(n.y,n.x);
        float sweepPhase = _Clock*0.43+sin(_Clock*0.31)*0.55;
        float sweep = pow(saturate(cos(angle-sweepPhase)),12)*_IdleMotion;
        float patch = pow(saturate(sin(spherical.x*2+spherical.y*3-_Clock*0.7+seed*0.15)),12)*_IdleMotion;
        float threshold = seed*0.58+saturate(i.cell.x*0.4+0.5)*0.42;
        float gone = _Dissolve*1.12;
        float alive = 1-smoothstep(threshold,threshold+0.07,gone);
        float burn = (1-smoothstep(0.015,0.09,abs(threshold-gone)))*step(0.001,_Dissolve);
        // The rear shell stays faint, readable as a second depth layer at the silhouette.
        float rearFade = lerp(0.08+fresnel*0.10,1,front);
        float centreFade = lerp(0.08,1,smoothstep(0.20,0.85,length(n.xy)));
        float gridLight = grid*(0.016+fresnel*0.45+diffuse*0.13+patch*0.14+ripple*1.4+burn*1.7);
        gridLight *= lerp(0.75,1.15,detail)*centreFade*rearFade;
        float membrane = front*(pow(fresnel,0.7)*0.028+diffuse*0.012+specular*0.20+reflection*0.075);
        float rim = pow(fresnel,2.7)*(0.38+sweep*0.42+_Charge*0.85)*rearFade;
        float breath = 0.92+(sin(_Clock*1.27)*0.05+sin(_Clock*2.03+1.4)*0.03)*_IdleMotion;
        float brightness = breath*(1+_Charge*0.8)*alive*_Formation;
        float3 color = _Tint.rgb*(gridLight+membrane+rim)*brightness;
        color += float3(0.55,0.85,1)*(specular*0.14+rim*0.10+ripple*0.10+burn*grid*0.25)*alive*_Formation;
        float opacity = min(0.12,membrane*0.18+fresnel*0.025*front)*alive*_Formation;
        return float4(color,opacity);
    }
    ENDHLSL
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
