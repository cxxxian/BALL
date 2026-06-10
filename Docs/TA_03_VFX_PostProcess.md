# TA-03 · 后处理 · URP Renderer Feature · Boss 击杀序列

[← 返回总览](./TA_TechPoints.md)

---

## 1. 后处理组件地图

| 系统 | 文件 | 触发时机 |
|------|------|----------|
| 全局 Bloom | `TronGlobalProfile` | 常驻 |
| 技能时缓 FX | `SlowMoFX.cs` | Execute / Shield 技能 |
| Boss 击杀总控 | `VFXDirector.cs` | Boss 被球击杀 |
| Boss P2 威胁 | `Boss.cs` + `SlowMoFX.SetBossKillVignette` | HP < 50% |
| 定向色散 | `DirectionalCAFeature.cs` | Boss 击杀窗口 |

---

## 2. SlowMoFX — 技能级后处理

文件：`Assets/Scripts/Core/SlowMoFX.cs`

### 三层结构

```
Canvas 层
  ├── flashOverlay  — 瞬间全屏亮闪 (unscaledDeltaTime)
  └── tintOverlay   — 暗蓝 tint 覆盖

URP Volume (SlowMoFX_Volume)
  ├── ChromaticAberration  — 进入时缓时 0→0.45
  ├── Vignette             — 0→0.48
  └── ColorAdjustments     — Saturation 0→-45
```

### 关键 API

| 方法 | 作用 |
|------|------|
| `Activate(timeScale)` | 技能瞄准：快速进入时缓 + 后处理 |
| `Deactivate()` | 退出：ease-in 恢复 |
| `PulseFlash(color, alpha, dur)` | 短闪（Boss P2 进场等） |
| `SetBossKillVignette(intensity, color)` | **只动 Vignette**，不动 Chroma/Sat |

**设计点**：`SetBossKillVignette` 与技能时缓 Volume **复用同一 Profile**，但 Boss P2 不改动 Saturation/Chroma，避免与 SlowMo 冲突。

---

## 3. VFXDirector — Boss 击杀时间轴

文件：`Assets/Scripts/VFX/VFXDirector.cs`

### 完整序列（unscaled time）

```
T=0.00  Time.timeScale → hitStopTimeScale (0.02)
        Vignette → 0.8
        生成 SpaceDistortion 球 (scale 0)

T=0~0.05  扭曲球 scale 0 → distortionMaxRadius (AnimationCurve)

T=0.05~0.10  等待 hitStop 后半

T=0.10  Time.timeScale → 1
        Vignette → 0
        扭曲球快速放大淡出 Destroy

T=0.10+  DirectionalCA 窗口 chromaticAberrationDuration (0.5s)
        VFXDirector.IsChromaticAberrationActive = true

T=end   postEffectHoldDuration (0.5s) — 延迟 Buff 选择 UI
        _effectActive = false
```

### 与玩法耦合

- `WaveManager` 调用 `WaitForEffectComplete()` 再弹 Buff
- `TRIGGER_COOLDOWN = 0.3s` 防重复触发
- 仅 **Boss 球击杀** 走完整序列（非 Tesla 等非球击杀）

**面试点**：Gameplay 节奏由 TA 时间轴 gate；`unscaledDeltaTime` 保证 hit stop 不受 timeScale 影响

---

## 4. DirectionalCAFeature — URP 扩展实现

文件：`Assets/Scripts/VFX/DirectionalCAFeature.cs`

### 类结构

```
DirectionalCAFeature : ScriptableRendererFeature
  └── DirectionalCAPass : ScriptableRenderPass
        renderPassEvent = BeforeRenderingPostProcessing
        ConfigureInput(Color)
        Execute: Blit camera → tempRT → camera (material pass 0)
```

### 启用条件

```csharp
if (!VFXDirector.IsChromaticAberrationActive) return false;
```

**刻意设计**：普通高速球 **不** 开 CA，避免廉价感；只有 Boss 击杀仪式感。

### Shader 参数注入

- `_BallScreenPos` — Viewport 空间
- `_BallVelocityDir` — 归一化 2D 速度
- `_CAIntensity` — Feature settings

### 注册位置

`Assets/Settings/Renderer2D.asset` → `m_RendererFeatures` → DirectionalCAFeature

**面试关键词**：ScriptableRenderPass、RTHandle、Blitter.BlitCameraTexture、RenderPassEvent 顺序

---

## 5. SpaceDistortion 材质

- Material：`Assets/Materials/SpaceDistortion.mat`
- Shader 采样 `_CameraSortingLayerTexture` 做 ripple UV 偏移
- Boss 击杀：动态 Sphere + scale 动画；`sortingOrder = 10`

**注意**：这是 **2D 屏幕纹理扭曲**，不是 3D 折射 Snell 定律；性能友好。

---

## 6. Boss P2 后处理（非 Boss 击杀）

文件：`Assets/Scripts/Entities/Boss.cs`

| 反馈 | 实现 |
|------|------|
| 进场闪 | `SlowMoFX.PulseFlash` 红色 |
| 持续威胁 | `SetBossKillVignette(0.26~0.50 脉冲)` |
| 环 + Halo | LineRenderer + SpriteRenderer（见 TA-04） |
| 震屏 | Enter Heavy + 周期 Light |

**Scheme B**：Boss 本体黄色 HDR **不改色**，压迫感靠环境后处理 + 外环。

---

## 7. Volume 优先级与冲突处理

| 场景 | TronGlobal Bloom | SlowMo Volume |
|------|------------------|---------------|
| 正常游玩 | ✅ 生效 | disabled |
| 技能 SlowMo | ✅ 生效 | enabled + 全套 |
| Boss P2 | ✅ 生效 | Vignette only override |
| Boss 击杀 | ✅ 生效 | Vignette + CA Feature |

**坑**：多个 Global Volume 时看 **Priority** 与 **Weight**；本项目 SlowMo Volume 按需 enable 而非混 weight。

---

## 8. 面试：如何扩展下一个全屏效果？

1. 在 Volume Profile 加新 Override（如 Film Grain）→ 零代码
2. 需要方向/物体绑定 → 写 `ScriptableRendererFeature` + Blit Shader
3. 需要 2D 局部扭曲 → 采样 CameraSortingLayerTexture + 世界空间 Quad
4. 事件驱动 → 静态 flag + Director 协程（与 VFXDirector 同模式）

---

## 9. 相关场景对象

| 对象名 | 作用 |
|--------|------|
| `TronGlobal_Volume` | 常驻 Bloom |
| `SlowMoFX_Volume` | 动态后处理 |
| `Main Camera` | Post Processing enabled |

SampleScene：`Assets/Scenes/SampleScene.unity`
