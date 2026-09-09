# 统一 UI 字体选型（科幻 + 中文）

目标：一套字体同时覆盖 **中文 UI** 与 **英文/数字 HUD**，风格偏赛博/科技。

> 现实约束：几乎没有「Orbitron 那种斜杠 0 + 全汉字」的单一开源字体。  
> 可选方案是找 **中西文一体、偏现代几何黑体** 的开源字族做统一 UI。

---

## 推荐首选：未来荧黑 Glow Sans SC（OFL）

| 项 | 内容 |
|----|------|
| 中文名 | **未来荧黑** |
| 英文 | Glow Sans SC |
| 许可 | **SIL OFL 1.1**（可商用、可嵌入游戏） |
| 来源 | 思源黑体 + Fira Sans / Raleway 形变 |
| 风格 | 中宫收、笔画更平直 → 比纯思源更「现代/科技」 |
| 字重 | Thin～Heavy，另有 Compressed / Normal / Wide 等宽度 |
| 下载 | https://github.com/welai/glow-sans/releases （取 `GlowSansSC-Normal-*.zip`） |
| 样张 | https://welai.github.io/glow-sans |

**建议装入工程：**
- UI 正文 / 菜单：`GlowSansSC-Normal-Regular` 或 `Medium`
- 标题 / HUD 数字：`GlowSansSC-Normal-Bold` 或 `ExtraBold`

Unity：把 `.otf` 放进 `Assets/Fonts/`，uGUI `Text` 可直接用；TMP 需 Font Asset Creator 生成 SDF（中文字库大，建议按常用字集裁剪）。

---

## 备选

| 字体 | 风格 | 许可 | 适合 |
|------|------|------|------|
| **有爱新黑 / Nowar Neo Sans** | Roboto + 思源，偏游戏客户端 | OFL | 多语言 UI，清晰稳 |
| **Orbit Gothic CJK** | 思源圆体现代化 | OFL | 圆角科技感，略软 |
| **思源黑体 / Noto Sans SC** | 标准现代黑体 | OFL | 最稳、最全，科幻感弱一点 |
| **Orbitron**（现有） | 强科幻拉丁数字 | OFL | **仅英文数字**，无汉字 |

---

## 不建议硬「一套 Orbitron 管全部」

Orbitron 没有汉字。若 HUD 数字还想保留斜杠 0，只能：

- **统一方案**：全文 Glow Sans（推荐，风格统一）
- **双字体方案**：中文 Glow Sans + 数字 Orbitron（数字更好看，两套维护）

对本项目（大量中文菜单/技能名），优先 **Glow Sans 统一**。

---

## 工程现状

- 已有：`Orbitron-Bold.ttf` + OFL（数字 HUD 临时）
- 待接入：`GlowSansSC-Normal-Bold/Regular`（需从 Release 下载 zip，约 65MB）
