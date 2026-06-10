# 新窗口实现提示词 · Arcade CRT 轻量后处理

> 复制下方 `---BEGIN PROMPT---` 到 `---END PROMPT---` 之间的全文，粘贴到新 Cursor Agent 窗口执行。

---BEGIN PROMPT---

## 任务

在 Unity URP 2D 弹球项目 **Ball** 中，实现 **全场轻量 CRT / 扫描线后处理**（TA-05 设计稿 P0+P1）。  
**不是**敌人裁切/Glitch/RGB 分离/曲面畸变；是摄像机后处理，日常有「街机屏」质感。

**设计文档（必读）**：`F:\Study\GameDesign\Ball\Docs\TA_05_Arcade_CRT_Design.md`

---

## 项目上下文

- **引擎**：Unity 2022 + URP 14 + **Renderer2D**
- **工程路径**：`F:\Study\GameDesign\Ball`
- **主场景**：`Assets/Scenes/SampleScene.unity`
- **Renderer 资产**：`Assets/Settings/Renderer2D.asset`（已挂 `DirectionalCAFeature`）
- **Bloom Volume**：`TronGlobal_Volume` → `Assets/Settings/TronGlobalProfile.asset`（**不要改**，仍只负责 Bloom）
- **动态 Volume**：`SlowMoFX_Volume` + `SlowMoFX.cs`（技能/Boss 事件 Vignette/Chroma，**不要新增第三个 Volume**）

### 现有 DirectionalCA（必须兼容、不要改其行为）

- 文件：`Assets/Scripts/VFX/DirectionalCAFeature.cs`
- Shader：`Assets/Shaders/DirectionalCA.shader`
- Pass Event：`RenderPassEvent.BeforeRenderingPostProcessing`
- 仅 `VFXDirector.IsChromaticAberrationActive == true` 时执行（Boss 击杀 ~0.5s）
- 参考其 Blit 写法：`Blitter.BlitCameraTexture` + RTHandle temp

### Boss 生成 hook 点

- `Assets/Scripts/Entities/WaveManager.cs` → `SpawnBoss()` → `boss.Initialize(...)` 之后调用 `ArcadeCRTController.Instance?.TriggerSweep()`

### Boss P2 / SlowMo 暗角互斥

- `SlowMoFX.Instance.fxVolume.enabled` 为 true 时，CRT 常驻 vignette 应 ×0.5 或置 0
- Boss 进入 P2 时（`Boss` 有 `_inPhase2`，或通过 SlowMoFX `SetBossKillVignette` 非零）同样减弱 CRT vignette，避免三重暗角

---

## 实现要求

### 1. 新增文件

| 文件 | 说明 |
|------|------|
| `Assets/Shaders/ArcadeCRT.shader` | Shader 名 `Hidden/ArcadeCRT`，URP HLSL，单 Pass 全屏 Blit |
| `Assets/Scripts/VFX/ArcadeCRTFeature.cs` | `ScriptableRendererFeature`，Pass Event = **`AfterRenderingPostProcessing`** |
| `Assets/Scripts/VFX/ArcadeCRTController.cs` | 场景单例 MonoBehaviour，暴露 TA 参数 + `TriggerSweep()` |

### 2. Shader 功能（一个 Pass 内完成）

**输入**：与 DirectionalCA 相同，用 URP Blit 的 `_BlitTexture` + `sampler_LinearClamp`（参考 DirectionalCA.shader 的 include 方式）

**常驻 - 横向扫描线（~5%）**：
- 乘性暗线，不要加性亮脏纹理
- 默认 `_ScanlineOpacity = 0.05`，`_ScanlineCount = 280`
- `col.rgb *= scanDarken`

**常驻 - 弱边缘 vignette**：
- 默认 `_VignetteStrength = 0.10`，`_VignettePower = 2.2`
- 可选轻微冷青边：`float3(0.92, 0.96, 1.0)`

**事件 - Boss 波次扫描带**：
- `_SweepT`：平时 -1；扫带动画 0→1，沿 uv.y 从上往下
- 默认 duration 0.75s，`_SweepIntensity ≈ 0.18`，颜色偏青白 `(0.7, 0.95, 1)`
- `exp(-pow(...))` 软带，不要全屏闪白

**不要做**：Glitch、RGB split、distortion、film grain 替代

### 3. ArcadeCRTFeature

- 结构对齐 `DirectionalCAFeature.cs`（Settings 挂 Shader、CreateEngineMaterial、RTHandle、CommandBufferPool）
- **常驻执行**：只要 Controller 启用且 scanline/vignette/sweep 任一有效就 Blit
- 若 `enabled == false` 或强度全 0 且 SweepT < 0 → Skip Pass（零开销）
- 仅 **Game Camera**（`cameraData.cameraType == CameraType.Game`）执行
- 在 `Renderer2D.asset` 的 `m_RendererFeatures` **追加**本 Feature（保留 DirectionalCAFeature，用 MCP 或说明需在 Unity Editor 手动拖 Shader 引用）

### 4. ArcadeCRTController

```csharp
// 必备字段（Inspector 可调）
scanlineOpacity = 0.05f
vignetteStrength = 0.10f
scanlineCount = 280
sweepDuration = 0.75f
sweepIntensity = 0.18f
enabled = true  // 总开关

public static float SweepT { get; }      // -1 表示无扫带
public static float EffectiveVignette { get; }  // 经 SlowMo/P2 互斥后的值

public void TriggerSweep()  // 协程驱动 SweepT 0→1，结束后置 -1
```

- `Awake` 单例；`LateUpdate` 或由 Feature 在 Execute 前读取 Controller 静态属性
- Feature 与 Controller 通信用 **static 属性**（避免 Feature 里 FindObjectOfType 每帧）

### 5. 改动现有文件（最小 diff）

- `WaveManager.cs`：`SpawnBoss` 末尾 `ArcadeCRTController.Instance?.TriggerSweep();`
- **不要改**：DirectionalCAFeature、VFXDirector、TronGlobalProfile Bloom 参数、JuiceRouter、GDD

### 6. 场景 setup

- SampleScene 新建空物体 `ArcadeCRT`，挂 `ArcadeCRTController`
- 或通过 `[RuntimeInitializeOnLoadMethod]` 自动创建（二选一，优先场景挂载便于 TA 调参）

---

## 渲染顺序（必须遵守）

```
2D Scene → DirectionalCA [Before PP, 短事件] → Bloom/Volume PP → ArcadeCRT [After PP, 常驻] → Screen
```

**ArcadeCRT 必须在 Post Processing 之后**，这样扫描线不被 Bloom 糊掉。

---

## 代码规范

- 匹配项目现有风格（与 DirectionalCAFeature / SlowMoFX 一致）
- 注释简洁，只解释非 obvious 的 TA 逻辑
- 不要 over-engineer（一期不需要 ScriptableObject Settings 资产，Inspector 字段够用）
- 不要提交 git，除非用户要求
- 实现后用 Unity MCP `refresh_unity` 编译，读 console 确认无 error

---

## 验收标准（Play Mode）

1. 正常游玩：几乎看不出扫描线，但整体略「电子屏」；截图 200% 可见横线
2. Bloom/Bumper 命中：霓虹仍利落，扫描线不雾化画面
3. Boss 波次出现：横向亮带扫过一次（~0.75s），不抢 Boss 视觉
4. Boss 击杀 DirectionalCA 0.5s：CA 正常，CRT 底噪仍在、不暴涨
5. 开 SlowMo 技能：暗角以 SlowMo Volume 为主，CRT vignette 自动减弱
6. `enabled=false` 或 opacity=0：Feature Skip，无额外 Blit 开销

---

## 参考文件（实现前请 Read）

- `Assets/Scripts/VFX/DirectionalCAFeature.cs` — Renderer Feature 模板
- `Assets/Shaders/DirectionalCA.shader` — Blit Shader 模板
- `Assets/Scripts/Core/SlowMoFX.cs` — Volume 互斥判断
- `Assets/Scripts/Entities/Boss.cs` — P2 vignette 调用
- `Docs/TA_05_Arcade_CRT_Design.md` — 完整 TA 设计

---

## 交付

1. 上述新文件 + 最小改动现有文件
2. 简短实现说明：Pass Event 选择理由、与 DirectionalCA 为何不冲突、TA 参数怎么调
3. 若无法通过 MCP 修改 `Renderer2D.asset`，给出 Editor 手动注册步骤（Shader 拖到哪）

---END PROMPT---
