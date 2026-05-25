// TronWall.shader
// 用于左右墙壁的程序化霓虹边框效果。
// UV.x = 宽度方向（外缘→内缘），UV.y = 高度方向（下→上）。
// 效果：双侧边缘青色发光 + 向上流动的能量脉冲 + 电路板横向刻度线。
// Pass: Universal2D (SpriteRenderer/URP2D) + UniversalForward (MeshRenderer)
Shader "Custom/TronWall"
{
    Properties
    {
        [Header(Edge Glow)]
        _EdgeColor      ("Neon Edge Color",         Color)        = (0.10, 0.85, 1.00, 1.0)
        _CoreColor      ("Core Fill Color",         Color)        = (0.00, 0.20, 0.35, 0.25)
        _EdgeWidth      ("Edge Glow Width",         Range(0.05, 0.6)) = 0.28
        _EdgeIntensity  ("Edge Intensity",          Range(1.0, 6.0))  = 3.2

        [Header(Energy Flow)]
        _FlowColor      ("Flow Pulse Color",        Color)        = (0.50, 1.00, 1.00, 0.90)
        _FlowSpeed      ("Flow Speed (u/s)",        Float)        = 1.0
        _FlowRepeat     ("Pulses on Wall",          Range(1, 5))  = 2.0
        _FlowWidth      ("Pulse Width",             Range(0.01, 0.15)) = 0.04

        [Header(Circuit Ticks)]
        _TickColor      ("Tick Mark Color",         Color)        = (0.20, 0.90, 1.00, 0.55)
        _TickInterval   ("Tick Interval (UV)",      Range(0.02, 0.3)) = 0.07
        _TickThickness  ("Tick Thickness (UV)",     Range(0.002, 0.02)) = 0.006
        _TickInset      ("Tick Inset from Edge",    Range(0.0, 0.5))    = 0.18

        [Header(Tint)]
        _WallTint       ("Overall Tint",            Color)        = (1.0, 1.0, 1.0, 1.0)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _EdgeColor;
        float4 _CoreColor;
        float  _EdgeWidth;
        float  _EdgeIntensity;

        float4 _FlowColor;
        float  _FlowSpeed;
        float  _FlowRepeat;
        float  _FlowWidth;

        float4 _TickColor;
        float  _TickInterval;
        float  _TickThickness;
        float  _TickInset;

        float4 _WallTint;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
    };

    Varyings vert(Attributes IN)
    {
        Varyings OUT;
        OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
        OUT.uv         = IN.uv;
        return OUT;
    }

    float4 frag(Varyings IN) : SV_Target
    {
        float2 uv = IN.uv;

        // ── 1. 双侧边缘发光 ──────────────────────────────────────
        float edgeDist = min(uv.x, 1.0 - uv.x) / max(_EdgeWidth, 0.001);
        float edgeGlow = pow(saturate(1.0 - edgeDist), 2.5) * _EdgeIntensity;

        float4 col = _CoreColor;
        col.rgb   += _EdgeColor.rgb * edgeGlow;
        col.a      = saturate(_CoreColor.a + _EdgeColor.a * saturate(edgeGlow * 0.5));

        // ── 2. 流动能量脉冲（沿 UV.y 向上） ────────────────────
        float flowPhase = frac(uv.y * _FlowRepeat - _Time.y * _FlowSpeed);
        float flowDist  = min(flowPhase, 1.0 - flowPhase);
        float flow      = smoothstep(_FlowWidth, 0.0, flowDist);
        float edgeMask  = saturate(1.0 - edgeDist * 0.6);
        col.rgb        += _FlowColor.rgb * (flow * _FlowColor.a * edgeMask);
        col.a           = saturate(col.a + flow * _FlowColor.a * edgeMask * 0.4);

        // ── 3. 电路板横向刻度线 ──────────────────────────────────
        float tickPhase = frac(uv.y / _TickInterval);
        float tick      = smoothstep(_TickThickness * 0.5, 0.0,
                                    abs(tickPhase - 0.5) - (0.5 - _TickThickness * 0.5));
        float tickMask  = step(uv.x, _TickInset) + step(1.0 - _TickInset, uv.x);
        tickMask        = saturate(tickMask);
        col.rgb        += _TickColor.rgb * tick * tickMask * _TickColor.a;
        col.a           = saturate(col.a + tick * tickMask * _TickColor.a * 0.5);

        // ── 4. 整体色调 ───────────────────────────────────────────
        col *= _WallTint;

        return col;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        // ── SpriteRenderer 在 URP 2D Renderer 下使用 Universal2D pass ──
        Pass
        {
            Name "TronWall_2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }

        // ── MeshRenderer / 标准 URP Forward Renderer 备用 ──────────────
        Pass
        {
            Name "TronWall_Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
