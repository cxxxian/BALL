# TA-04 · 运行时 VFX · Juice 系统 · 渲染工程坑

[← 返回总览](./TA_TechPoints.md)

---

## 1. ImpactFX — 命中粒子系统

文件：`Assets/Scripts/Core/ImpactFX.cs`

### 三套 ParticleSystem

| 系统 | 用途 | 特点 |
|------|------|------|
| `_burstPS` | 主方块爆发 | 10~22 粒，lifetime 0.25~0.55s |
| `_dustPS` | 细尘 | 6~14 粒，更慢更轻 |
| `_dissolvePS` | 触底解体 | EmitParams 逐粒指定 velocity，向下飞散 |

### 公共 API

| API | 说明 |
|-----|------|
| `SpawnHit(pos, color, intensity)` | 粒子 + 扩散 Ring |
| `SpawnWallFlash(pos, normal, color, i)` | 墙边 HDR 短线 |
| `SpawnShieldRipple(y, halfW, color, i)` | 护符横向波纹 |
| `SpawnBottomDissolve(pos, color, i)` | 触底扣血专用 |

### LineRenderer 特效

- **HitRing**：33 点圆，scale 动画 0.15→1.6，alpha 衰减
- **WallFlash**：切线方向短线，0.15s
- **ShieldRipple**：护盾线位置水平 expand
- **BottomLineFlash**：全宽底线擦除感

**材质**：运行时 `Sprites/Default`；`sortingOrder` 9~14

**TA 点**：粒子用 **1×1 像素贴图** + Point Filter 保持「像素块」风格；ColorOverLifetime _hold 后 sharp fade

---

## 2. JuiceRouter — 统一反馈分档

文件：`Assets/Scripts/Core/JuiceRouter.cs`

```
Tap      → intensity 0.5, 无震
Hit      → intensity 1.0, Light  (Bumper 参考)
Skill    → intensity 1.2, Medium
Ultimate → intensity 1.5, Heavy
```

### 触点映射

| 触点 | 调用 |
|------|------|
| Bumper | `Play(Hit)` |
| 墙 | `WallHit` + 条件震 (vel>6) |
| 挡板完美接球 | `FlipperPerfectCatch` + Tap |
| Tesla/Frost | `TowerFire` |
| 护符激活 | `ShieldActivate` (ripple only) |
| 护符吸收 | `ShieldAbsorb` (ripple + 3 hit + Medium) |
| 敌人 | `EnemyJuice` → SpawnHit + Flash |

**原则**：同一类反馈同一套语言（撞=粒子+震，死亡=解体，大招=后处理）

---

## 3. EnemyJuice — 敌人受击/击杀

文件：`Assets/Scripts/Entities/EnemyJuice.cs`

- **Hit**：1 帧 HDR 白闪 (`FlashDuration=0.02`) + Light 震 + 粒子
- **Kill**：Medium 震 + 粒子（不走解体，小兵死亡简单反馈）
- **Minion 触底**：`ImpactFX.SpawnBottomDissolve`（最重档之一）

Flash 颜色：
- Boss `(6,6,6)`
- Minion `NeonPalette.GetFlash(Minion)`

---

## 4. FlipperFX — MaterialPropertyBlock

文件：`Assets/Scripts/Core/FlipperFX.cs`

```csharp
_sr.GetPropertyBlock(_mpb);
_mpb.SetFloat(_HitFlash, _flashValue);
_sr.SetPropertyBlock(_mpb);
```

| 触发 | 来源 |
|------|------|
| 按键挥击 | Update 检测 IsActivated 上升沿 |
| 完美接球 | `TriggerCatchFlash()` |

**面试点**：多实例共享 Material，零 `material` 实例化；Shader 需暴露 `_HitFlash` uniform

---

## 5. Boss P2 程序化 VFX

文件：`Assets/Scripts/Entities/Boss.cs`

| 元素 | 技术 |
|------|------|
| 双 LineRenderer | 内外菱形，反向旋转，HDR 红 |
| P2 Halo | CPU 生成 radial ring Texture → SpriteRenderer |
| 心跳 scale | sin 脉冲 + 轻微 transform scale |
| Vignette | SlowMoFX 每帧 override |

LineRenderer：`useWorldSpace=false`，顶点来自 `_sr.sprite.bounds` 菱形

---

## 6. BlockShield 视觉

文件：`Assets/Scripts/Core/BlockShield.cs`

- LineRenderer 水平护盾线，`sortingOrder=15`
- 展开/闪烁/吸收 协程驱动 width + color
- 吸收降级：JuiceRouter Skill 档（原 Heavy+7 点粒子）

---

## 7. UI / World 空间渲染

### BossHealthBar

- Screen Space Canvas（顶栏），`sortingOrder=250`
- 缓冲条 + Avatar + 阶段 `[xN]`

### MinionHealthBar（重要工程坑）

**失败方案**：World Space Canvas → URP 2D 常不显示

**现行方案**：`SpriteRenderer` 双条（bg + fill）
- 父级 scale ~0.18 时，子节点 `localScale = 1/lossyScale` **逆补偿**
- 世界恒定尺寸 ~0.62×0.09
- `sortingOrder` 12/13

文件：`Assets/Scripts/UI/MinionHealthBar.cs`

### ComboDisplay

- 纯 Text 缩放 punch，**无背景矩形**
- 里程碑 5/10：`ComboSystem.onComboMilestone` 大 punch

---

## 8. 塔 / 机关特效

| 对象 | 视觉 |
|------|------|
| TeslaTower | CyberPulseSprite + 程序化 Nova Sprite scale 动画 |
| FrostTower | 冰霜 overlay color + SpawnFrostEffect |
| Bumper | Sprite 白闪 + Glow child scale ×2.4 |
| TrailRenderer | BallController 速度色 |

---

## 9. TronArenaBackground 运行时构建

文件：`Assets/Scripts/Core/TronArenaBackground.cs`

```
Layer_Far  (sort -100) → TronArenaFar.shader, Mesh Quad
Layer_Mid  (sort -99)  → TronArenaMid.shader, Combo boost
Layer_Near (sort -98)  → CPU vignette texture 128×256
```

**Parallax**：球 Y - battlefieldCenterY × factor，clamp maxParallaxOffset

**面试点**：背景不是 Tilemap，是 **3 层独立 draw**；Mid 层与 gameplay Combo 数据联动

---

## 10. CameraShake

文件：`Assets/Scripts/Core/CameraShake.cs`

- Trauma 模型：`shake = trauma²`，Perlin 噪声
- Preset Light/Medium/Heavy → GameConfig trauma 值
- JuiceRouter / Combo / Boss 统一调用

---

## 11. 已知渲染坑汇总（面试诚实项）

| 坑 | 现象 | 解决 |
|----|------|------|
| World Canvas @ 2D URP | 血条不显示 | 改 SpriteRenderer |
| 父级极小 scale | 子 UI 1 像素高 | 逆 scale 补偿 |
| Canvas 无 overrideSorting | sortingOrder 无效 | 2D 改用 Sprite sorting |
| HDR UI | Combo 触发 Bloom 糊 | NeonPalette UI 强制 LDR |
| Material 泄漏 | 大量 `new Material` | 短生命周期 Destroy；Flipper 用 MPB |
| CyberPulseSprite CG | URP 混用 | 可迁移 HLSL Universal2D |

---

## 12. 性能粗估（2D 弹球规模）

- 背景：2 Mesh + 1 Sprite（固定）
- 粒子：Emit 制，非 loop，max ~600
- LineRenderer：短生命周期，Boss P2 仅 2 条 persistent
- 后处理：Bloom + 偶发 CA Pass（Boss 击杀 ~0.5s）

**移动端建议**：池化 LineRenderer/Particle Material；Boss P2 降 Line 更新频率；Bloom skipIterations↑

---

## 13. 扩展阅读（项目内代码跳转）

```
NeonPalette.cs          → 色彩
ImpactFX.cs             → 粒子/线
JuiceRouter.cs          → 分档
VFXDirector.cs          → Boss 击杀
DirectionalCAFeature.cs → URP Pass
TronArenaBackground.cs  → 分层背景
MinionHealthBar.cs      → 2D UI 替代方案
```
