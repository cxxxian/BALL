Shader "TA/CyberCard_Interactive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (0.05, 0.08, 0.15, 0.95)
        
        [Header(Cyber Border)]
        _BorderColor ("Border Color", Color) = (0, 0.94, 1, 1)
        _BorderWidth ("Border Width", Range(0, 0.05)) = 0.015
        
        [Header(Scanline Radar)]
        _ScanColor ("Scan Color", Color) = (0, 0.94, 1, 1)
        _ScanY ("Scan Position", Range(-0.2, 1.2)) = 1.2
        _ScanThickness ("Scan Thickness", Range(0.01, 0.5)) = 0.08
        
        [Header(Glitch Interactivity)]
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0
        
        [Header(Holographic Grid)]
        _GridColor ("Grid Color", Color) = (0, 0.94, 1, 0.1)

        // UGUI Mask 必备
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent-10" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            fixed4 _BorderColor;
            float _BorderWidth;
            
            fixed4 _ScanColor;
            float _ScanY;
            float _ScanThickness;
            
            float _GlitchAmount;
            fixed4 _GridColor;

            // 纯正的 Hash 随机数，TA 的标配
            float hash(float2 p) {
                float3 p3  = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float time = _Time.y;

                // ==========================
                // 1. 交互式 Glitch (撕裂断层)
                // ==========================
                if (_GlitchAmount > 0)
                {
                    // 把 Y 轴切成块状
                    float slice = floor(uv.y * 20.0);
                    // 块级随机抖动偏移
                    float offset = (hash(float2(slice, time * 15.0)) * 2.0 - 1.0) * 0.1 * _GlitchAmount;
                    uv.x += offset;
                }

                // ==========================
                // 2. 主底色与色散采样
                // ==========================
                // 使用色散重影 (Chromatic Aberration) 代替单调的采样
                float ca = 0.02 * _GlitchAmount;
                fixed4 baseColor = IN.color;
                
                // ==========================
                // 3. 全息背景网格 (Holographic Grid)
                // ==========================
                float2 grid = frac(uv * float2(10.0, 15.0)); // 网格密度
                if (grid.x < 0.05 || grid.y < 0.05)
                {
                    baseColor.rgb += _GridColor.rgb * _GridColor.a * (1.0 - _GlitchAmount * 0.5); // 撕裂时网格暗淡
                }

                // ==========================
                // 4. 赛博硬切角边框 (Tech Border)
                // ==========================
                float distX = min(uv.x, 1.0 - uv.x);
                float distY = min(uv.y, 1.0 - uv.y);
                float minDist = min(distX, distY);
                
                if (minDist < _BorderWidth)
                {
                    // 外发光染色
                    baseColor.rgb = lerp(baseColor.rgb, _BorderColor.rgb, _BorderColor.a);
                    if (minDist < _BorderWidth * 0.5) 
                        baseColor.rgb += _BorderColor.rgb * 0.5; // 核心超亮
                }

                // ==========================
                // 5. 交互式雷达扫描线 (Sweep Radar)
                // ==========================
                float distToScan = abs(uv.y - _ScanY);
                if (distToScan < _ScanThickness)
                {
                    // 高斯光晕衰减
                    float scanGlow = exp(-(distToScan * distToScan) / (_ScanThickness * _ScanThickness * 0.2));
                    baseColor.rgb += _ScanColor.rgb * _ScanColor.a * scanGlow * 2.0;
                    
                    // 扫描线上叠加极细的水平亮线
                    if (distToScan < _ScanThickness * 0.05)
                        baseColor.rgb += float3(1, 1, 1); // 纯白高光核心
                }

                return baseColor;
            }
            ENDCG
        }
    }
}
