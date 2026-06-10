# TA-02 · 自定义 Shader 拆解

[← 返回总览](./TA_TechPoints.md)

---

## Shader 清单

| Shader | 路径 | 用途 |
|--------|------|------|
| TronFlipper | `Assets/Shaders/TronFlipper.shader` | 挡板霓虹管造型 |
| TronWall | `Assets/Shaders/TronWall.shader` | 左右墙 |
| TronBall | `Assets/Shaders/TronBall.shader` | 球体 Rim（备用） |
| TronGrid | `Assets/Shaders/TronGrid.shader` | 旧网格背景 |
| TronArenaFar | `Assets/Shaders/TronArenaFar.shader` | 远层母线 |
| TronArenaMid | `Assets/Shaders/TronArenaMid.shader` | 中层扫描网格 |
| CyberPulseSprite | `Assets/Shaders/CyberPulseSprite.shader` | Tesla 脉冲 |
| UI_CyberCard | `Assets/Shaders/UI_CyberCard.shader` | Buff 卡片 UI |
| SpaceDistortion | `Assets/Shaders/SpaceDistortion.shader` | Boss 击杀扭曲 |
| DirectionalCA | `Assets/Shaders/DirectionalCA.shader` | 全屏定向色散 |

---

## 1. TronFlipper — 程序化霓虹挡板（重点）

### 造型逻辑（无 mesh 建模）

- UV 空间构建 **梯形**：`tipT = lerp(1-uv.x, uv.x, _TipAtMaxU)` 区分左右挡板
- **Pivot 倒角**：`chamferLine = 2 - _ChamferSize - pivotT - normY` + smoothstep 切角
- **分边发光**（避免 pivot 晕光进内腔）：
  - 顶/底边：`dTopBot = min(normY, 1-normY)`
  - 转轴端：`dPivotX`
  - 倒角斜边：`dChamfer`

### 发光公式

```hlsl
float tbLine = pow(saturate(1.0 - dTopBot / _LineWidth), 5.0) * _NeonIntensity;
float tbHalo = pow(saturate(1.0 - dTopBot / _GlowWidth), 1.8) * _NeonIntensity * 0.30;
// ... capLine, chLine, tipGlow
col.rgb += _NeonColor.rgb * totalGlow;  // 不 saturate → Bloom
```

### 渲染状态

- `Blend SrcAlpha One` — 加性霓虹
- **双 Pass**：`Universal2D` + `UniversalForward`（兼容 SpriteRenderer / MeshRenderer）
- 运行时 `_HitFlash` 由 `FlipperFX` 通过 MPB 驱动

**面试关键词**：SDF-like distance field、分边 glow、HDR 不 clamp、MPB 实例化优化

---

## 2. TronWall — 侧墙能量感

UV 语义：`uv.x` = 外缘→内缘，`uv.y` = 下→上

| 层 | 技法 |
|----|------|
| 边缘 glow | `edgeDist = min(uv.x, 1-uv.x) / _EdgeWidth` + pow |
| 能量 pulse | `frac(uv.y * _FlowRepeat - _Time * _FlowSpeed)` 纵向流动 |
| 电路 tick | `step` on `frac(uv.y / _TickInterval)` 横刻度 |

双 Pass 同 TronFlipper。

---

## 3. TronArenaMid / TronArenaFar — 分层背景

### Mid 层（`TronArenaMid.shader`）

- **世界坐标**网格：`worldXY` 来自 vertex transform
- **抗锯齿网格线**：`fwidth(d)` + smoothstep（避免 scroll 闪烁）
- **Combo 联动**：`_ComboBoost` 提高 grid alpha + band 亮度
- **扫描带**：`exp(-pow((y - scanY)/0.09, 2))` 平滑移动

### 运行时驱动

`TronArenaBackground.cs`：
- Far/Mid 用 **Mesh Quad** + 自定义 Shader
- Near 层用 **CPU 生成 vignette Texture2D** + Sprite-Unlit
- **Parallax**：球 Y 偏移 × `farParallaxFactor / midParallaxFactor`

**面试点**：2D 背景不用 Tilemap，全 procedural；性能友好（单 Quad draw call）

---

## 4. TronBall — 2D 伪 Fresnel

```hlsl
float2 centeredUV = uv * 2 - 1;
float dist = length(centeredUV);
float fresnel = pow(saturate(dist), _RimPower);
float pulse = 1 + _PulseAmp * sin(_Time.y * _PulseSpeed);
float3 col = lerp(_CoreColor * pulse, _RimColor * _RimIntensity, fresnel);
```

**局限**：仅 Forward Pass；实际球常用 `TronBallMat` 或 `Sprite-Unlit` + HDR color。

---

## 5. CyberPulseSprite — 径向脉冲环

Built-in CG（非 URP HLSL）：
- `dist = length(uv*2-1)`
- `ring = pow(saturate(1 - abs(dist - 0.55) * _RingSharpness), 1.2)`
- `scan = sin((p.y + time) * 60 + dist * 20)` 扫描线

用于 **TeslaTower** SpriteRenderer。

---

## 6. UI_CyberCard — UGUI 交互 Shader

路径：`Assets/Shaders/UI_CyberCard.shader`（`TA/CyberCard_Interactive`）

| 特性 | 实现 |
|------|------|
| Stencil | UGUI Mask 兼容 |
| Glitch | Y 分块 + hash 偏移 X |
| 全息网格 | `frac(uv * density)` 线框 |
| 硬切角边框 | 到边距离 min |
| 扫描雷达 | `_ScanY` 驱动水平亮带 |

Material：`Assets/Materials/UI_CyberCard.mat`

**面试点**：UI Shader 必须处理 Stencil + `unity_GUIZTestMode`；Glitch 用块噪声而非全屏随机

---

## 7. SpaceDistortion — 屏幕空间折射

```hlsl
TEXTURE2D(_CameraSortingLayerTexture);
// fragment: 按 ripple 偏移 UV 采样屏幕纹理
```

- LightMode: `Universal2D`
- Boss 击杀时 `VFXDirector` 生成 Sphere + 此 Material，scale 动画
- **依赖** `Renderer2D.m_UseCameraSortingLayersTexture = 1`

---

## 8. DirectionalCA — 全屏后处理 Shader

```hlsl
float2 offset = _BallVelocityDir * offsetStrength;
float r = Sample(uv - offset).r;
float g = Sample(uv).g;
float b = Sample(uv + offset).b;
```

- 方向性：沿球速分离 RGB
- 强度：距球屏幕位置 + 速度方向 dot 调制
- 由 `DirectionalCAFeature` 在 `BeforeRenderingPostProcessing` Blit

---

## 9. 通用 TA 模式总结

| 模式 | 本项目示例 |
|------|------------|
| UV 几何造型 | Flipper 梯形、Wall 边缘 |
| 距离场 pow 发光 | 各 Neon Shader |
| 世界坐标 procedural | ArenaMid 网格 |
| fwidth 抗锯齿 | 网格线、细线 |
| 双 Pass LightMode | Flipper/Wall 2D+Forward |
| 屏幕纹理采样 | SpaceDistortion |
| 全屏 Blit 后处理 | DirectionalCA |

---

## 10. 配套运行时贴图生成

`CyberVisualFactory.cs` — 无美术资源时的 **CPU 像素绘制 Sprite**：
- 小兵倒三角、Boss 菱形、塔、Prism 等
- 统一 `UnlitMaterial`（Sprites/Default）

**面试点**：TA 不仅写 Shader，也写 **procedural asset pipeline** 保证风格一致。
