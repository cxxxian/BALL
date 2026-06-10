# Ball 项目 · 渲染向 TA 技术点总览

> **用途**：面试前快速回顾本项目的图形管线、Shader、后处理与运行时 VFX 设计。  
> **引擎**：Unity 2022 + **URP 14** + **2D Renderer**  
> **美术方向**：Tron / 赛博朋克霓虹 —— **HDR 超亮 + Bloom 选择性发光 + 程序化几何**

---

## 一句话项目定位（面试开场）

这是一个 **URP 2D 弹球 Roguelike**，没有依赖大量美术贴图，而是用 **程序化 Shader（SDF/UV 几何）+ HDR 色彩体系 + Bloom + 自定义 Renderer Feature** 做出「电路板 + 霓虹管」可读性。TA 工作集中在：**色彩语义、Bloom 阈值分层、2D 多层背景、Hit Juice 统一语言、Boss 击杀多级后处理**。

---

## 文档地图

| 文档 | 内容 | 面试重点 |
|------|------|----------|
| [TA_01_Pipeline_HDR_Bloom.md](./TA_01_Pipeline_HDR_Bloom.md) | URP 2D 管线、Volume、Bloom、HDR 分层 | 「怎么让霓虹亮但不糊」 |
| [TA_02_Custom_Shaders.md](./TA_02_Custom_Shaders.md) | 全部 Custom Shader 技法拆解 | 「程序化造型 + 双 Pass」 |
| [TA_03_VFX_PostProcess.md](./TA_03_VFX_PostProcess.md) | 后处理、Renderer Feature、Boss 击杀序列 | 「URP 扩展与事件驱动 FX」 |
| [TA_04_Runtime_VFX_Systems.md](./TA_04_Runtime_VFX_Systems.md) | 粒子、LineRenderer、Juice、UI/World 渲染坑 | 「Gameplay Juice 工程化」 |
| [TA_05_Arcade_CRT_Design.md](./TA_05_Arcade_CRT_Design.md) | 全场轻量 CRT/扫描线后处理（设计稿） | 「After PP Blit 与 CA 错层」 |

---

## 核心架构图

```
┌─────────────────────────────────────────────────────────────┐
│  NeonPalette (ScriptableObject) — 色彩单一事实来源           │
│  HDR 绝对值 (Ball/Bumper) │ LDR (UI/背景) │ 粒子倍率        │
└───────────────────────────┬─────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
  SpriteRenderer.color   ImpactFX 粒子      UI Text (LDR)
  + Custom Shaders       LineRenderer       不触发 Bloom
        │                   │
        └─────────┬─────────┘
                  ▼
     URP 2D Renderer (HDREmulationScale=4)
                  │
                  ▼
     TronGlobal_Volume → Bloom (Threshold 1.1 / Intensity 0.45)
                  │
                  ▼
     DirectionalCAFeature (Boss 击杀窗口)
                  │
                  ▼
              屏幕输出
```

---

## 技术点清单（按面试可讲维度）

### A. 管线与色彩（必讲）

- [x] URP **2D Renderer**，非 Forward 3D；Sprite 默认 `Sprite-Unlit-Default`
- [x] **HDREmulationScale = 4**（`Renderer2D.asset`）—— 2D HDR 与 Bloom 衔接
- [x] **双 Volume 分工**：`TronGlobal_Volume`（常驻 Bloom） vs `SlowMoFX_Volume`（技能/Boss 动态 Vignette/Chroma）
- [x] **NeonPalette** 三套色彩约定：HDR 绝对值 / Entity 色相×倍率 / LDR UI
- [x] Bloom 调参哲学：Threshold↑ 减少全屏雾，Scatter↓ 光晕更利落

→ 详见 [TA_01](./TA_01_Pipeline_HDR_Bloom.md)

### B. 程序化 Shader（必讲 2～3 个）

- [x] **TronFlipper**：UV 梯形 + 倒角遮罩 + 分边霓虹管；`Blend SrcAlpha One` 加性；**不 clamp HDR**
- [x] **TronWall**：双侧 edge glow + 纵向 flow pulse + 电路 tick
- [x] **TronArenaMid/Far**：世界坐标网格 + `fwidth` 抗锯齿 + Combo 驱动 `_ComboBoost`
- [x] **TronBall**：2D 伪 Fresnel（距中心 UV）+ sin 呼吸
- [x] **CyberPulseSprite**：径向 ring + 扫描线（Tesla 塔）
- [x] **UI_CyberCard**：UGUI Stencil + Glitch + 全息网格
- [x] **SpaceDistortion**：采样 `_CameraSortingLayerTexture` 屏幕扭曲

→ 详见 [TA_02](./TA_02_Custom_Shaders.md)

### C. 后处理与 URP 扩展（差异化亮点）

- [x] **DirectionalCAFeature**：`ScriptableRendererFeature` + 全屏 Blit
- [x] 定向色散：沿 **球速度方向** 分离 RGB，强度与屏幕空间位置相关
- [x] **VFXDirector** Boss 击杀时间轴：HitStop → Vignette → 扭曲球膨胀 → 恢复 → CA → Hold
- [x] **SlowMoFX**：Canvas 闪屏/ tint + Volume 三件套（Chroma/Vignette/Saturation）

→ 详见 [TA_03](./TA_03_VFX_PostProcess.md)

### D. 运行时 VFX 与工程坑（体现 TA+程序协作）

- [x] **ImpactFX**：双 ParticleSystem + EmitParams 手动发射 + LineRenderer 扩散环
- [x] **JuiceRouter**：Tap/Hit/Skill/Ultimate 分档，统一「撞=粒子+震」
- [x] **FlipperFX**：`MaterialPropertyBlock` 写 `_HitFlash`，零 Material 实例化
- [x] **Boss P2**：LineRenderer 双环反向旋转 + 程序化 Halo Sprite + Volume 脉冲
- [x] **MinionHealthBar**：World Canvas 失败 → **SpriteRenderer + 父级 scale 逆补偿**
- [x] **TronArenaBackground**：三层 parallax（Far/Mid Shader + Near 像素 vignette 纹理）

→ 详见 [TA_04](./TA_04_Runtime_VFX_Systems.md)

---

## 关键资产路径速查

| 类型 | 路径 |
|------|------|
| URP 资产 | `Assets/Settings/UniversalRP.asset`, `Renderer2D.asset` |
| 全局 Bloom | `Assets/Settings/TronGlobalProfile.asset` |
| 色彩配置 | `Assets/ScriptableObjects/NeonPalette.asset` |
| Shader 目录 | `Assets/Shaders/` |
| 材质 | `Assets/Materials/` |
| VFX 脚本 | `Assets/Scripts/Core/ImpactFX.cs`, `JuiceRouter.cs`, `Assets/Scripts/VFX/` |
| 背景系统 | `Assets/Scripts/Core/TronArenaBackground.cs` |

---

## 面试常见问题 · 本项目怎么答

**Q：Bloom 怎么调才不糊？**  
A：Background/UI 严格 LDR（≤1）；Gameplay 霓虹写 2～6 HDR；Bloom Threshold 提到 ~1.1，Intensity ~0.45，Scatter ~0.6；用 Bumper 命中做验收画面。

**Q：2D 怎么做「发光管」？**  
A：Fragment 里算到边的距离做 pow 衰减，核心填暗色，边缘叠 HDR；Blend 用加性；配合 Bloom 而不是靠贴图 bloom map。

**Q：怎么避免 Material Instance 爆炸？**  
A：挡板挥击/接球闪用 MPB 写 `_HitFlash`；共享 Material；动态 LineRenderer/粒子用 `new Material` 但短生命周期 Destroy。

**Q：World Space UI 在 2D URP 的坑？**  
A：Canvas 常不渲染或 scale 错误；小兵 health bar 改用 SpriteRenderer，并对父级 `lossyScale` 做逆缩放保持世界尺寸恒定。

**Q：Renderer Feature 做了什么？**  
A：DirectionalCA 只在 `VFXDirector.IsChromaticAberrationActive` 时 Blit 全屏；RGB 沿球速方向偏移；Pass 挂在 Post Processing 前。

**Q：Juice 怎么统一语言？**  
A：JuiceRouter 分档；Bumper 为 Hit 参考；墙/塔/护符/挡板各映射到 Tap/Hit/Skill；死亡走 EnemyJuice + 解体粒子。

---

## 已知限制 & 可改进方向（诚实加分项）

1. **TronBall.shader** 仅 `UniversalForward` Pass，球体实际多用 `Sprite-Unlit-Default` + HDR color
2. **CyberPulseSprite** 仍为 Built-in CG，与 URP HLSL 混用
3. **SpaceDistortion** 依赖 2D Camera Sorting Layer Texture，需注意 Renderer 开关
4. **Boss P2** LineRenderer 方案可行但美术上限有限，后续可换序列帧/Shader ring
5. **无 SRP Batcher 深度优化文档**——大量运行时 `new Material`，移动端需池化

---

## 版本记录

| 日期 | 说明 |
|------|------|
| 2026-06 | 初版：Bloom 接入、JuiceRouter 全触点、Minion 血条 Sprite 方案、Combo 去矩形边框 |
