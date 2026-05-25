// TronFlipper.shader — 赛博朋克霓虹弹板
// 形状：六边形轮廓（梯形 + 转轴端倒角），近黑内腔，霓虹管外框发光
// Pass: Universal2D (URP2D SpriteRenderer) + UniversalForward (MeshRenderer)
Shader "Custom/TronFlipper"
{
    Properties
    {
        [Header(Shape)]
        _TipAtMaxU   ("Tip at UV.x=1  (L flipper=1, R=0)", Range(0,1))     = 1.0
        _TipTopY     ("Top edge Y at tip",                  Range(0.1,0.9)) = 0.60
        _TipBotY     ("Bot edge Y at tip",                  Range(0.1,0.9)) = 0.40
        _ChamferSize ("Pivot corner chamfer (0=none)",      Range(0,0.5))   = 0.22

        [Header(Neon Tube)]
        [HDR]_NeonColor   ("Neon Color (HDR)",  Color)      = (1.00, 0.55, 0.00, 1.0)
        _CoreColor   ("Dark Interior",     Color)           = (0.08, 0.03, 0.00, 0.12)
        _LineWidth   ("Hot Line Width",    Range(0.01,0.15))= 0.045
        _GlowWidth   ("Soft Halo Width",   Range(0.05,0.5)) = 0.28
        _NeonIntensity("Neon Intensity",   Range(1,10))     = 6.0

        [Header(Hit Flash)]
        _HitFlash    ("Hit Flash (0-1)",   Range(0,1))      = 0.0
        _FlashColor  ("Flash Color",       Color)           = (1,1,1,1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float  _TipAtMaxU; float  _TipTopY; float  _TipBotY; float _ChamferSize;
        float4 _NeonColor; float4 _CoreColor;
        float  _LineWidth; float  _GlowWidth; float _NeonIntensity;
        float  _HitFlash;  float4 _FlashColor;
    CBUFFER_END

    struct Attributes { float4 pos:POSITION; float2 uv:TEXCOORD0; float4 col:COLOR; };
    struct Varyings   { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 col:COLOR; };

    Varyings vert(Attributes IN)
    {
        Varyings O;
        O.pos = TransformObjectToHClip(IN.pos.xyz);
        O.uv  = IN.uv;
        O.col = IN.col;
        return O;
    }

    float4 frag(Varyings IN) : SV_Target
    {
        float2 uv  = IN.uv;

        // ── 1. 梯形形状 ─────────────────────────────────────────
        float tipT    = lerp(1.0 - uv.x, uv.x, _TipAtMaxU);   // 0=转轴, 1=尖端
        float topB    = lerp(1.0,    _TipTopY, tipT);
        float botB    = lerp(0.0,    _TipBotY, tipT);
        float shapeH  = max(topB - botB, 0.001);
        float normY   = (uv.y - botB) / shapeH;                // 0=底边, 1=顶边

        // ── 2. 转轴端倒角遮罩（切去 pivot 侧顶角） ──────────────
        // chamferLine = 0 时恰好在对角线上，< 0 时在被切掉的角区域
        // pivotT: 0=尖端, 1=转轴端
        float pivotT     = 1.0 - tipT;
        // 倒角对角线：过 (pivotT=1, normY=1-cs) 和 (pivotT=1-cs, normY=1)
        // chamferLine > 0 → 保留区；< 0 → 转轴顶角被切掉
        float chamferLine = 2.0 - _ChamferSize - pivotT - normY;
        float chamferMask = smoothstep(-0.02, 0.02, chamferLine); // 0=切除区, 1=保留区

        // 整体形状 alpha（含倒角）
        float shapeAlpha = smoothstep(-0.04, 0.04, normY)
                         * smoothstep(1.04, 0.96, normY)
                         * chamferMask;

        // ── 3. 各边独立发光（避免 pivot 晕光渗入内腔中心） ─────
        // 顶/底边：在形状归一化 Y 空间
        float dTopBot  = min(normY, 1.0 - normY);          // 0=边缘, 0.5=中心
        float tbLine   = pow(saturate(1.0 - dTopBot  / max(_LineWidth,  0.001)), 5.0) * _NeonIntensity;
        float tbHalo   = pow(saturate(1.0 - dTopBot  / max(_GlowWidth,  0.001)), 1.8) * _NeonIntensity * 0.30;

        // 转轴端面：仅在 UV.x 方向靠近端面时发光（不蔓延到内腔）
        float dPivotX  = lerp(uv.x, 1.0 - uv.x, _TipAtMaxU);  // 0=端面, 1=尖端
        float capLine  = pow(saturate(1.0 - dPivotX / max(_LineWidth * 0.6, 0.001)), 5.0) * _NeonIntensity * 0.9;
        float capHalo  = pow(saturate(1.0 - dPivotX / 0.10),   1.8) * _NeonIntensity * 0.20;

        // 倒角斜边
        float dChamfer = abs(chamferLine) * 0.6;
        float chLine   = pow(saturate(1.0 - dChamfer / max(_LineWidth * 0.8, 0.001)), 5.0) * _NeonIntensity * 0.8;
        float chHalo   = pow(saturate(1.0 - dChamfer / max(_GlowWidth * 0.4, 0.001)), 1.8) * _NeonIntensity * 0.18;

        // 尖端收窄自然辉光
        float narrow   = 1.0 - shapeH;
        float tipGlow  = narrow * tipT * _NeonIntensity * 0.35;

        // 不截断 → col.rgb 超过 1.0 → Bloom 才会散射发光
        float totalGlow = tbLine + tbHalo + capLine + capHalo + chLine + chHalo + tipGlow;

        // ── 5. 合成颜色（霓虹管：暗内腔 + 亮轮廓 HDR）──────────
        float4 col  = _CoreColor * shapeAlpha;          // 极暗的内腔填充
        col.rgb    += _NeonColor.rgb * totalGlow;       // HDR：不截断，Bloom 使用超亮值
        col.a       = saturate(_CoreColor.a * shapeAlpha
                    + _NeonColor.a  * saturate(tbLine * 0.9 + tbHalo * 0.5
                    + capLine * 0.8 + chLine * 0.7) * shapeAlpha);

        // ── 6. 击打闪光 ─────────────────────────────────────────
        col.rgb = lerp(col.rgb, _FlashColor.rgb, _HitFlash);
        col.a   = saturate(col.a + _HitFlash * _FlashColor.a * shapeAlpha);

        col *= IN.col;
        return col;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One   // 加性-alpha：暗处不叠加，亮处强叠加 = 真实霓虹管发光
        ZWrite Off
        Cull Off

        Pass
        {
            Name "TronFlipper_2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Name "TronFlipper_Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
