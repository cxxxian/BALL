# TA-01 · URP 2D 管线 · HDR 色彩 · Bloom

[← 返回总览](./TA_TechPoints.md)

---

## 1. 渲染管线选型

| 项 | 本项目 |
|----|--------|
| RP | Universal Render Pipeline (URP) 14.x |
| Renderer | **Renderer2D**（非 3D Forward Renderer） |
| 相机 | Orthographic，`orthographic size ≈ 9` |
| 主场景 Volume | `TronGlobal_Volume` → `TronGlobalProfile` |
| 动态 Volume | `SlowMoFX_Volume`（技能/Boss 事件） |

**面试点**：2D 项目仍可用 HDR + Post Processing；关键是 `Renderer2D` 的 **HDREmulationScale** 与 Sprite 默认 Unlit 材质。

---

## 2. Renderer2D 关键配置

文件：`Assets/Settings/Renderer2D.asset`

| 参数 | 值 | 含义 |
|------|-----|------|
| `m_HDREmulationScale` | **4** | 2D 渲染 HDR  emulation，影响超亮像素如何进入后处理 |
| `m_UseCameraSortingLayersTexture` | **1** | 开启后可采样 `_CameraSortingLayerTexture`（SpaceDistortion 用） |
| `m_RendererFeatures` | DirectionalCAFeature | 自定义全屏 Pass |
| Default Unlit | `Sprite-Unlit-Default` | 大部分运行时 Sprite 回退材质 |

**Renderer Feature 注册**：`DirectionalCAFeature` → Shader `Hidden/DirectionalCA`

---

## 3. 双 Volume 架构

```
TronGlobal_Volume (Global, 常驻)
  └── Bloom: Threshold 1.1, Intensity 0.45, Scatter 0.6, Tint 冷青

SlowMoFX_Volume (Global, 按需 enabled)
  └── ChromaticAberration / Vignette / ColorAdjustments
      由 SlowMoFX、VFXDirector、Boss P2 动态写 intensity
```

### 为什么拆两个 Volume？

- **Bloom** 是「画面基调」，应全程一致，策划/TA 调一次即可
- **Vignette/Chroma/Saturation** 是「事件反馈」，和 Time.timeScale、Boss 阶段绑定，需要运行时开关 `fxVolume.enabled`

相关脚本：
- `SlowMoFX.cs` — 技能时缓进出场
- `VFXDirector.cs` — Boss 击杀 vignette
- `Boss.cs` EnterPhase2 — 红色威胁 vignette 脉冲

---

## 4. Bloom 调参（TronGlobalProfile）

文件：`Assets/Settings/TronGlobalProfile.asset`

| 参数 | 当前值 | TA 意图 |
|------|--------|---------|
| **Threshold** | 1.1 | 只有真正 HDR 的像素发光；背景网格/UI 不贡献 |
| **Intensity** | 0.45 | 中等发光；Bumper/命中够亮但不洗屏 |
| **Scatter** | 0.6 | 比默认略紧，减少「柔焦雾」 |
| **Tint** | (0.9, 0.95, 1) | 整体 Bloom 偏冷，贴合 Tron |
| **Clamp** | 65472 | 防止极端 HDR 烧屏 |

### 验收方法（面试可描述）

1. **静止场景**：网格/黑底不应雾蒙蒙
2. **Bumper 命中**：青边清晰发光，非一团糊
3. **UI Combo 数字**：LDR 色，边缘不应出现 Bloom  halo
4. **Boss P2 红环**：有压迫感但不全屏过曝

---

## 5. NeonPalette — HDR 色彩体系

文件：
- `Assets/Scripts/Core/NeonPalette.cs`
- `Assets/ScriptableObjects/NeonPalette.asset`

### 三套约定（核心 TA 规则）

```
┌─────────────────────────────────────────────────────────┐
│ 1. HDR 绝对值 — 直接写入 SpriteRenderer.color           │
│    Ball ~(3.5,3.5,3.8)  Bumper ~(0,1.8,2.5)            │
│    Flash 瞬间 ~(6,6,6)                                  │
├─────────────────────────────────────────────────────────┤
│ 2. Entity 色相 × entityHdrMultiplier (2.5)              │
│    Minion/Boss Definition.baseColor 是 0~1 色相         │
├─────────────────────────────────────────────────────────┤
│ 3. LDR ≤ 1 — UI / Background                          │
│    Combo UI、Danger、Background 禁止 >1                 │
└─────────────────────────────────────────────────────────┘
```

### API 速查

| 方法 | 用途 |
|------|------|
| `GetBase(NeonRole)` | 实体常态色 |
| `GetFlash(NeonRole)` | 受击 1 帧白闪 (~×6) |
| `ForParticle(color, intensity)` | 粒子 × `particleHdrMultiplier`(3) |
| `ApplyEntityHue(defColor)` | 小兵/Boss 初始化 |
| `Dim(color, factor)` | Bumper passthrough 暗化 |

`NeonColors.Active` 为运行时全局访问入口。

---

## 6. 混合模式策略

| 对象 | Blend | 原因 |
|------|-------|------|
| TronFlipper | `SrcAlpha One` | 暗部不叠亮，亮部强叠加 = 霓虹管 |
| 普通 Sprite / 粒子 | `SrcAlpha OneMinusSrcAlpha` | 标准透明 |
| TronBall | Alpha blend | 球体需要软边 |

**面试点**：加性混合物体数量要控制，否则 Bloom 叠加过量；所以 Background 用 LDR 且不加性。

---

## 7. Sorting Order 分层（2D 深度语义）

| Order | 内容 |
|-------|------|
| -100 ~ -98 | 背景 Far / Mid / Near |
| 1 | Boss P2 Halo |
| 2 | 实体 Sprite（Minion/Boss） |
| 6~9 | 粒子 / Hit Ring |
| 8~14 | LineRenderer（盾/墙闪/护符波纹） |
| 12~21 | Minion 血条 |
| 15 | BlockShield 线 |
| 250 | Boss 顶栏 UI Canvas |

**原则**：Gameplay 反馈（粒子/线）在实体之上；UI 最高。

---

## 8. 与 Bloom 联动的代码入口

```csharp
// 写入 HDR → Bloom 拾取
_sr.color = NeonColors.Active.GetBase(NeonRole.Bumper);

// 粒子：显式放大亮度
Color hdr = NeonColors.Active.ForParticle(neonColor, intensity);

// UI：保持 LDR
public Color combo = new Color(1f, 0.88f, 0.18f, 1f); // ≤1
```

---

## 9. 面试延伸答法

- **为什么不用 Bloom Map？** 2D 程序化霓虹，亮度来自 Shader/color 通道，不需要第二张贴图；TA 工作流是 Palette + Threshold 分区。
- **HDR 和 Gamma？** URP Linear 空间；Sprite color 可 >1；最终 Tonemapping 由 URP 处理（本项目未 heavily 调 Tonemapping，靠 Bloom Threshold 分区）。
- **如果要移动端优化？** 降 Bloom Iterations、降 Intensity；减少加性混合层；粒子数量已在 ImpactFX 控制 (10~22 burst)。
