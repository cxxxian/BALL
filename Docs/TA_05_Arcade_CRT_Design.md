# TA-05 · 全场轻量 CRT / 扫描线后处理 — 设计方案

[← 返回总览](./TA_TechPoints.md)

> **目标**：给摄像机最终画面加「街机屏」质感，**不是**敌人裁切/故障风。  
> **原则**：极轻、常驻、与 DirectionalCA / 技能 Vignette **不抢戏、不叠糊**。

---

## 1. 需求摘要

| 层 | 效果 | 强度 |
|----|------|------|
| **常驻** | 横向扫描线 | ~**5%** 透明度（几乎看不见，离屏能感到「屏」） |
| **常驻** | 边缘暗角 | 很弱，比 SlowMo/Boss P2 的 Vignette 弱一个数量级 |
| **事件** | Boss 波次扫描带 | 整屏横向亮带 **扫过一次**（~0.6–1.0s），非常偶发 |
| **非目标** | 敌人裁切、Glitch、RGB 分离、曲面畸变 | 不做 |

---

## 2. 为什么不用 Volume 自带效果？

| 方案 | 结论 |
|------|------|
| URP **Film Grain** | 颗粒感，不是规律横线；调不出 CRT |
| URP **Vignette**（TronGlobal） | 与 SlowMoFX / Boss P2 **动态 Vignette 三套打架**；且 Bloom 前后行为难控 |
| **UI Overlay 扫描线** | 不进 Bloom，和 3D/2D 画面分离感强；缩放/分辨率适配麻烦 |
| **单 Pass 自定义 Blit Shader** ✅ | 扫描线 + 弱 vignette + sweep 一个 Shader 搞定；强度常量可控 |

**推荐**：新增 **`ArcadeCRTFeature`**（`ScriptableRendererFeature`），与 `DirectionalCAFeature` 同族，但 **常驻 + 极轻**。

---

## 3. 渲染管线插入点（与 DirectionalCA 不冲突）

### 当前顺序（简化）

```
① 2D Renderer 画场景（Sprite / Mesh / Line / Particle）
② DirectionalCAFeature     [BeforeRenderingPostProcessing]  ← 仅 Boss 击杀 ~0.5s，intensity=0 时 Skip
③ URP Post Processing      Bloom / （SlowMo Volume 若 enabled）
④ 【建议插入】ArcadeCRTFeature  [AfterRenderingPostProcessing]  ← 本方案
⑤ Final Blit → 屏幕
```

### 为什么放在 Post Processing **之后**？

| 若放在 Bloom 前 | 若放在 Bloom 后 ✅ |
|----------------|-------------------|
| 扫描线也会被 Bloom，易「糊成雾」 | 扫描线像 **盖在显像管玻璃上**，更街机 |
| 与 HDR 阈值策略纠缠 | 与 NeonPalette / Bloom 调参 **解耦** |
| 和 DirectionalCA 同一阶段，需合并 Pass 或争顺序 | DirectionalCA 在 ②，CRT 在 ④，**天然不冲突** |

**DirectionalCA**：Boss 击杀短窗口、Before PP、RGB 方向分离。  
**ArcadeCRT**：全程、After PP、亮度微调 + 横线。  
两者 **不同 Pass Event + 不同生命周期**，可同时存在于 `Renderer2D.asset`。

```
时间轴示例（Boss 击杀瞬间）：
  DirectionalCA ████░░░░░░░░  (0.5s)
  ArcadeCRT     ████████████████████  (全程，强度不变)
  SlowMo Vignette (若开着)  ░░████░░
  → 峰值时 CA 主导「冲击」，CRT 仍是 5% 底噪，不会糊成一团
```

---

## 4. 系统架构

```
TronGlobal_Volume          → 仍只负责 Bloom（不改）
SlowMoFX_Volume            → 仍只负责技能/Boss 事件 Vignette/Chroma（不改）

ArcadeCRTFeature (Renderer2D)
  └── ArcadeCRTPass
        Shader: Hidden/ArcadeCRT
        Material: 单例 Engine Material

ArcadeCRTController (MonoBehaviour，场景单例)
  ├── 常驻参数：scanlineOpacity, vignetteStrength
  ├── 监听 WaveManager / Boss 生成 → TriggerSweep()
  └── LateUpdate：写 Material / MPB 或 static 属性（Sweep 动画）

可选：GameConfig 或 ScriptableObject「ArcadeCRTSettings」便于 TA 调参
```

**不新增第三个 Volume**，避免与 SlowMo Profile 的 Vignette override 冲突。

---

## 5. Shader 设计（`Hidden/ArcadeCRT`）

单 Pass 全屏 Blit，输入 `_BlitTexture`（与 DirectionalCA 相同 Blit 路径）。

### 5.1 常驻横向扫描线（~5%）

```hlsl
// uv：0~1 屏幕空间
float lineFreq = _ScanlineCount;  // 建议 240~360，或 _ScreenParams.y * 0.5
float scan = frac(uv.y * lineFreq);
// 细线：仅占用 duty cycle 的一小段
float line = smoothstep(_ScanlineWidth, 0.0, abs(scan - 0.5) - (0.5 - _ScanlineWidth));
float scanDarken = lerp(1.0, 1.0 - _ScanlineOpacity, line);
col.rgb *= scanDarken;  // 乘性暗线，约 5% 对比度差
```

**TA 建议**：
- 用 **乘性略暗** 而非加性亮线 → 不像「脏纹理」，更贴近 CRT 遮光栅
- `_ScanlineOpacity = 0.05` 起调；验收：**正常游玩几乎意识不到，截图放大能看见**
- `FilterMode.Point` 可选开关：更像素；默认 Bilinear 更柔

### 5.2 常驻弱 Vignette

```hlsl
float2 d = uv - 0.5;
float dist = dot(d, d);  // 或 round vignette
float vig = saturate(1.0 - dist * _VignetteRoundness);
vig = pow(vig, _VignettePower);
col.rgb *= lerp(1.0 - _VignetteStrength, 1.0, vig);
```

**推荐初值**：
- `_VignetteStrength = 0.08 ~ 0.12`（SlowMo 用到 0.48，Boss P2 脉冲 0.26~0.50）
- 冷色偏青可选：`lerp(col.rgb, col.rgb * float3(0.92, 0.96, 1.0), edgeMask * 0.3)`

**与 SlowMo / Boss P2 共存策略**：
- CRT vignette **永远弱**
- 当 `SlowMoFX.fxVolume.enabled` 或 `Boss._inPhase2` 时，CRT Controller 将 `_VignetteStrength` **×0.5 或临时置 0**，避免暗角叠满

### 5.3 Boss 波次「扫描带扫过」（事件）

```hlsl
// _SweepT：0→1，由脚本驱动，平时 = -1 关闭
if (_SweepT >= 0.0)
{
    float bandCenter = _SweepT;  // 沿 uv.y 从上往下
    float band = exp(-pow((uv.y - (1.0 - _SweepT)) / _SweepSoftness, 2.0));
    col.rgb += _SweepColor.rgb * band * _SweepIntensity;
    // 可选：扫描带经过处 scanline 略增强
    scanDarken *= lerp(1.0, 0.92, band);
}
```

**触发**（建议）：
- `WaveManager` 生成 Boss 时 → `ArcadeCRTController.TriggerSweep()`
- 或 Boss `Initialize()` 首帧
- **每 Boss 波次 1 次**，duration 0.7s，`AnimationCurve` ease

**强度**：
- `_SweepIntensity` 0.15~0.25（HDR 可加一点青白 `(0.7, 0.95, 1)`）
- 比 P2 红 vignette 弱，比常驻扫描线 **明显一瞬** 即可

---

## 6. C# 控制接口（草案）

```csharp
public class ArcadeCRTController : MonoBehaviour
{
    public static ArcadeCRTController Instance { get; private set; }

    [Header("Always On")]
    [Range(0, 0.15f)] public float scanlineOpacity = 0.05f;
    [Range(0, 0.25f)] public float vignetteStrength = 0.10f;
    public int scanlineCount = 280;

    [Header("Boss Wave Sweep")]
    public float sweepDuration = 0.75f;
    public float sweepIntensity = 0.2f;

    public void TriggerSweep() { /* 协程 0→1 */ }

    // 供 Feature 读取
    public static float SweepT { get; private set; } = -1f;
    public static float EffectiveVignette { get; private set; }
}
```

`ArcadeCRTPass.Execute`：
- 若 `scanlineOpacity <= 0 && vignette <= 0 && SweepT < 0` → **Skip Pass**（编辑器/低配可关）
- 否则 Blit 一次（与 DirectionalCA 相同 RT 复用模式）

---

## 7. Renderer Feature 伪代码（与 DirectionalCA 对齐）

```csharp
public class ArcadeCRTFeature : ScriptableRendererFeature
{
    class ArcadeCRTPass : ScriptableRenderPass
    {
        // renderPassEvent = AfterRenderingPostProcessing

        public override void Execute(...)
        {
            if (!ShouldRun()) return;
            PushShaderParams();
            Blitter.BlitCameraTexture(cmd, source, temp, material, 0);
            Blitter.BlitCameraTexture(cmd, temp, source);
        }
    }
}
```

**性能**：
- 常驻：每帧 **1 次全屏 Blit**（1080p 移动端需实测；可设「低配关闭」）
- Boss 击杀 DirectionalCA 多 1 Blit → 峰值 2 Blit，仅 0.5s

**优化选项**（二期）：
- 每 2 帧更新一次（扫描线静止时几乎无感）
- 仅 Main Camera 执行（UI Camera 排除）

---

## 8. 与现有系统的边界

| 系统 | 关系 |
|------|------|
| **TronArenaMid** 网格扫描 | 场景内层动画；CRT 是 **屏幕级** 5% 纹理，互补不重复 |
| **Bloom** | CRT 在 Bloom 后，不提高 Threshold 压力 |
| **DirectionalCA** | 不同 Pass 阶段；Boss 击杀 CA 峰值时 CRT 保持底噪 |
| **SlowMoFX Vignette** | 事件期 CRT vignette 自动减弱 |
| **Boss P2 Vignette** | 同上，避免三重暗角 |
| **UI Canvas** | Screen Space Overlay UI **不受** Camera Blit 影响（仍在最上）→ 符合「框外 HUD 干净」 |

若希望 **UI 也带扫描线**（真·整机 CRT），二期加可选 `Screen Space - Camera` HUD 或 UI 后 Blit；**一期建议 HUD 干净**。

---

## 9. 参数表（TA 初调起点）

| 参数 | 建议值 | 说明 |
|------|--------|------|
| ScanlineOpacity | **0.05** | 用户指定 |
| ScanlineCount | 280 | 1080p 约每 3.8px 一线 |
| ScanlineWidth | 0.25 | frac 空间 duty |
| VignetteStrength | **0.10** | 常驻弱 |
| VignettePower | 2.2 | 越大越贴边 |
| SweepDuration | 0.75s | Boss 出现 |
| SweepIntensity | 0.18 | 扫带亮度 |
| SweepSoftness | 0.04 | 带宽 |

**验收清单**：
1. Idle 盯屏 30s：感觉「更电子」，说不出具体变化 ✅  
2. 截图 200%：可见横线 ✅  
3. Bumper 命中 Bloom：不被扫描线糊掉 ✅  
4. Boss 波次：扫描带扫过一次，不抢 Boss 登场 ✅  
5. Boss 击杀 CA 0.5s：CA 清晰，CRT 不消失不暴涨 ✅  
6. 开 SlowMo 技能：暗角主要来自 SlowMo，CRT vignette 已减弱 ✅  

---

## 10. 文件规划（实现阶段）

| 新增 | 路径 |
|------|------|
| Shader | `Assets/Shaders/ArcadeCRT.shader` |
| Feature | `Assets/Scripts/VFX/ArcadeCRTFeature.cs` |
| Controller | `Assets/Scripts/VFX/ArcadeCRTController.cs` |
| Settings（可选） | `Assets/ScriptableObjects/ArcadeCRTSettings.asset` |
| Renderer 注册 | `Assets/Settings/Renderer2D.asset` 追加 Feature |
| 场景挂载 | SampleScene 空物体 `ArcadeCRT` 挂 Controller |

**改动现有文件**（实现时）：
- `WaveManager.cs` 或 `Boss.Initialize` — 一行 `TriggerSweep()`
- 可选 `GameConfig` — 低配开关 `enableArcadeCRT`

---

## 11. 分期实施

| 阶段 | 内容 | 工时估 |
|------|------|--------|
| **P0** | Shader 常驻扫描线 + 弱 vignette + Feature 常驻 Blit | 0.5~1d |
| **P1** | Controller + Boss 波次 Sweep + vignette 与 SlowMo/P2 互斥 | 0.5d |
| **P2** | GameConfig 开关、低配 Skip Pass、与 Notes 作品集截图 | 0.25d |

---

## 12. 面试一句话

> 「我们用 **After PP 的单 Pass Blit** 做 5% 乘性扫描线和弱 vignette，把 Bloom 和街机质感分层；Boss 击杀的 **DirectionalCA** 放在 PP 前短窗口触发，两者 Pass Event 和生命周期错开，避免抢戏和重复 Blur。」

---

## 13. 可选扩展（非一期）

- 极弱 **rolling flicker**（scanline 相位随时间 +0.5% 亮度波动）
- 与 Combo 联动：Combo>10 时 scanline opacity 0.05→0.07
- 主菜单单独 Profile，Gameplay 再开 CRT

**不建议**：曲面 distortion、RGB 分离、block glitch —— 会和小兵裁切方案 3 混淆，且破坏可读性。
