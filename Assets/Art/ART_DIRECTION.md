# Ball — UI 美术方向（AIGC 生成规范）

> 与 gameplay 程序化 Tron 霓虹世界对齐，不做「独立画风」的 UI 插画。

## 1. 视觉锚点（必须一致）

| 维度 | 值 | 来源 |
|------|-----|------|
| 背景 | `#0A0A0F` | `MainMenu.uss --neon-bg` |
| 面板底 | `#030712` | `--neon-panel` |
| 主强调 | `#00FFFF` cyan | `--neon-cyan` / Bumper |
| 次强调 | `#FF00FF` fuchsia | `--neon-fuchsia` |
| 奖励/Combo | `#FFFF00` yellow | `--neon-yellow` |
| 线条 | 1–2px 硬边，圆角 ≤ 2px | Tron 像素风 |
| 发光 | 内发光为主，不外溢大光晕 | StyleKit micro-glow |

**禁止：** 暖金/棕色机械风、写实 3D、渐变圆角 iOS 风、复杂角色插画。

## 2. 生成工作流

```
Phase 0  风格锚点（1 张面板框）→ 人工确认 OK
Phase 1  框架类（9-slice 面板 / 按钮 / 转轮框）
Phase 2  图标类（Buff / Skill / Ball，64×64）
Phase 3  装饰类（扫描线 tile、lock-on 环）
```

每批最多 3 张，确认风格锁定后再扩量。**不要一次生成 20 张。**

## 3. AIGC Prompt 模板

### 面板框（9-slice）

```
flat 2D game UI panel frame, cyberpunk terminal aesthetic,
dark navy background #030712, thin cyan #00FFFF neon border 2px,
sharp corners radius 2px, subtle horizontal scanlines inside panel,
holographic glass feel, NO text, NO icons, NO characters,
transparent PNG outside frame, center area flat and empty for 9-slice stretch,
1024x1024, orthographic, UI asset sheet
```

### 按钮（normal + hover 分开生成）

```
flat 2D game UI button, cyberpunk neon, dark fill #030712,
cyan #00FFFF border glow, rectangular sharp corners,
small inner highlight line, NO text,
transparent background, 512x256, UI sprite
```

### Buff 图标（64×64）

```
flat 2D game icon, cyberpunk neon line art, single symbol centered,
cyan and fuchsia palette only, dark transparent background,
64x64 pixel-perfect, readable at small size, NO text, UI icon
```

## 4. 目录结构

```
Assets/Art/UI/
├── Frames/      # 9-slice 面板框（BuffSelection, Settlement, MainMenu）
├── Buttons/     # 按钮 normal / hover / pressed
├── Icons/
│   ├── Buffs/
│   ├── Skills/
│   └── Balls/
├── Fx/          # lock-on 环、扫描线 tile、装饰 overlay
└── _Anchor/     # Phase 0 风格锚点（确认前不要删）
```

## 5. Unity 导入设置

| 类型 | Texture Type | Filter | Compression |
|------|-------------|--------|-------------|
| 9-slice 框 | Sprite (2D) | Point (no filter) | None |
| 图标 64px | Sprite (2D) | Point | None |
| 装饰 tile | Sprite (2D) | Bilinear | Normal |

9-slice 框必须在 Sprite Editor 里设 Border（通常四角 64–128px）。

## 6. UITK 接入方式

```css
/* USS — 9-slice 面板 */
.slot-panel {
    background-image: url("project://database/Assets/Art/UI/Frames/slot_panel.png");
    -unity-background-scale-mode: stretch-to-fill;
    -unity-slice-left: 64;
    -unity-slice-right: 64;
    -unity-slice-top: 64;
    -unity-slice-bottom: 64;
    border-width: 0; /* 贴图自带边框，去掉 USS border */
}
```

图标用 `<ui:VisualElement class="buff-icon" />` + `background-image` 或 Image 元素。

## 7. 资产清单（按优先级）

| P | 文件 | 尺寸 | 用途 |
|---|------|------|------|
| 0 | `_Anchor/panel_anchor.png` | 1024² | 风格锁定 |
| 1 | `Frames/slot_panel.png` | 1024² | BuffSelection 主面板 |
| 1 | `Frames/slot_reel.png` | 512² | 转轮框 |
| 1 | `Buttons/btn_primary_n.png` | 512×128 | 确认/领取按钮 |
| 2 | `Icons/Buffs/*.png` | 64² | 各 Buff 图标 ×N |
| 2 | `Icons/Skills/*.png` | 64² | 各 Skill 图标 ×N |
| 3 | `Fx/ball_lockon.png` | 256² | Boss 导弹锁定环 |

## 8. 后处理（生成后必做）

1. 去背景 → 纯透明（remove.bg 或 PS 魔棒）
2. 色偏校正 → cyan 对齐 `#00FFFF`（可选 LUT/色相）
3. 尺寸规范化 → 1024 / 512 / 64 整数倍
4. 命名 `snake_case`，无空格
