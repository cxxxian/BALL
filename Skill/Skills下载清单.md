# BALL 项目 Skills 下载清单

> 整理日期：2026-08-19  
> 来源网站：[https://skills.sh](https://skills.sh)  
> **这些不是 Claude / Anthropic 官方 skill**，而是社区作者在 GitHub 上发布的开源 skill 包。

---

## 一、安装方式（回自己电脑后）

### 前置条件

```bash
# 需要 Node.js，然后直接用 npx 即可
npx -y skills add <owner/repo@skill-name> -y --copy
```

### 安装到项目目录（推荐）

在 BALL 项目根目录执行，skill 会装到 `.agents/skills/`：

```bash
cd /path/to/BALL
npx -y skills add lvtd-llc/skills@game-balance-economy -y --copy
```

### 一键批量安装（策划 + 数值，5 个）

```powershell
cd F:\Study\GameDesign\Ball\Skill
.\install-design-skills.ps1
```

---

## 二、策划 / 数值 Skill（5 个，已精简）

> 从 20 个精简到 **5 个**。BALL 已有 GDD v4.0 和 `Notes/06_Balance/` 数值文档，不需要重复的理论 skill 和新项目启动器。

### 2.1 数值 / 平衡（2 个）

| Skill | 干什么 | 对应 BALL | 页面 |
|---|---|---|---|
| **balance-check** | 分析数值文件，找异常/失衡/退化策略 | `Notes/06_Balance/` | [链接](https://skills.sh/donchitos/claude-code-game-studios/balance-check) |
| **game-balance-economy** | 调优方法论：难度曲线、奖励节奏、经济平衡 | 老虎机 Buff、塔/敌人数值 | [链接](https://skills.sh/lvtd-llc/skills/game-balance-economy) |

### 2.2 游戏策划（3 个）

| Skill | 干什么 | 对应 BALL | 页面 |
|---|---|---|---|
| **design-game-design-fundamentals** | 核心循环、MDA、动机、难度/奖励设计 | 整体玩法框架 | [链接](https://skills.sh/fcsouza/agent-skills/design-game-design-fundamentals) |
| **design-review** | 审查设计文档的完整性、一致性、可实施性 | `Notes/` 下所有设计文档 | [链接](https://skills.sh/donchitos/claude-code-game-studios/design-review) |
| **level-design** | 关卡节奏、遭遇设计、张力/休息曲线 | 波次/敌人组合/竞技场 | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/level-design) |

### 2.3 已删除的 15 个（及原因）

| 删除的 Skill | 原因 |
|---|---|
| game-design-core / game-design-theory / game-design | 3 个重复的设计理论，fundamentals 已覆盖 |
| game-mechanics-designer | 与 fundamentals 重叠 |
| game-architect | 新项目 MVP 启动器，BALL 已在开发中 |
| game-design-document | 400+ 行 GDD 生成器，已有 GDD v4.0 |
| game-designer / design-game | 浏览器游戏视觉 polish，BALL 是 Unity + TA 文档 |
| design-game-encounters | Three.js 引擎，BALL 是 Unity |
| game-world-design | 叙事/世界观设计，BALL 是街机向 |
| rpg | RPG 系统（任务/对话/装备），不是 RPG |
| game-balance / game-balance-check | 与 balance-check 重叠，后者更直接 |
| game-economy-designer | F2P/战令/monetization，BALL 不涉及 |
| design-game-economy-design | 与 game-balance-economy 重叠 |

---

## 三、Unity 开发相关 Skill（skills.sh 有，推荐额外安装）

> BALL 是 Unity 2D 弹球项目，下面按优先级排列。

### 3.1 Unity 官方（unity-technologies/skills）— 最靠谱

仓库：https://github.com/unity-technologies/skills  
一键装全部：`npx skills add unity-technologies/skills --all -y --copy`

| Skill 名 | 用途 | 安装量 | 页面 |
|---|---|---|---|
| unity-cli | Unity CLI 工具 | ~1.7K | [链接](https://skills.sh/unity-technologies/skills/unity-cli) |
| unity-package-management | Package Manager | ~1.2K | [链接](https://skills.sh/unity-technologies/skills/unity-package-management) |
| new-unity-project | 新建 Unity 项目 | ~958 | [链接](https://skills.sh/unity-technologies/skills/new-unity-project) |
| build-live-game | 联机 / LiveOps | ~877 | [链接](https://skills.sh/unity-technologies/skills/build-live-game) |
| ui / ui-ugui / ui-uitk | UI 系统 | ~400-500 | [链接](https://skills.sh/unity-technologies/skills/ui) |
| urp-postprocessing | URP 后处理 | ~234 | [链接](https://skills.sh/unity-technologies/skills/urp-postprocessing) |
| shader-graph-create-custom-node | Shader Graph 自定义节点 | ~226 | [链接](https://skills.sh/unity-technologies/skills/shader-graph-create-custom-node) |
| validate-urp-render-graph-renderer-feature | URP Render Graph | ~220 | [链接](https://skills.sh/unity-technologies/skills/validate-urp-render-graph-renderer-feature) |
| optimize-audio | 音频优化 | ~199 | [链接](https://skills.sh/unity-technologies/skills/optimize-audio) |
| sprite-editor | Sprite 切片编辑 | ~170 | [链接](https://skills.sh/unity-technologies/skills/sprite-editor) |
| initialize-ai-navigation | NavMesh 导航 | — | [链接](https://skills.sh/unity-technologies/skills/initialize-ai-navigation) |

### 3.2 Unity 开发综合（高安装量）

| Skill 名 | 用途 | 安装量 | 安装命令 | 页面 |
|---|---|---|---|---|
| unity-developer | 通用 Unity 开发 | ~2.8K | `npx skills add rmyndharis/antigravity-skills@unity-developer` | [链接](https://skills.sh/rmyndharis/antigravity-skills/unity-developer) |
| unity-ecs-patterns | ECS 架构模式 | ~10K | `npx skills add wshobson/agents@unity-ecs-patterns` | [链接](https://skills.sh/wshobson/agents/unity-ecs-patterns) |
| unity-csharp-scripting | C# 脚本 | ~1.2K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-csharp-scripting` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/unity-csharp-scripting) |
| unity-animation | 动画系统 | ~1.2K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-animation` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/unity-animation) |
| unity-tilemap-2d | 2D Tilemap | ~1.2K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-tilemap-2d` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/unity-tilemap-2d) |
| unity-gamedev-skill-pack | Unity 开发技能包 | ~239 | `npx skills add akillness/jeo-skills@unity-gamedev-skill-pack` | [链接](https://skills.sh/akillness/jeo-skills/unity-gamedev-skill-pack) |

### 3.3 besty0728/unity-skills — 最全 Unity 自动化仓库（81 个 skill）

仓库：https://github.com/besty0728/unity-skills  
**可通过 REST API 直接操作 Unity Editor**，适合 AI 驱动编辑器自动化。

一键装全部：

```bash
npx skills add besty0728/unity-skills --all -y --copy
```

与 BALL 项目最相关的子 skill：

| Skill 名 | 用途 | 页面 |
|---|---|---|
| unity-skills | 编辑器 REST API 总入口 | [链接](https://skills.sh/besty0728/unity-skills/unity-skills) |
| unity-script | C# 脚本创建/分析 | [链接](https://skills.sh/besty0728/unity-skills/unity-script) |
| unity-material | 材质/Shader 属性编辑 | [链接](https://skills.sh/besty0728/unity-skills/unity-material) |
| unity-shader | HLSL/ShaderLab .shader 文件 | [链接](https://skills.sh/besty0728/unity-skills/unity-shader) |
| unity-shadergraph | Shader Graph 资产 | [链接](https://skills.sh/besty0728/unity-skills/unity-shadergraph) |
| unity-shadergraph-design | Shader Graph 设计规范 | [链接](https://skills.sh/besty0728/unity-skills/unity-shadergraph-design) |
| unity-urp | URP 管线管理 | [链接](https://skills.sh/besty0728/unity-skills/unity-urp) |
| unity-terrain | Terrain 地形操作 | [链接](https://skills.sh/besty0728/unity-skills/unity-terrain) |
| unity-scene | 场景管理 | [链接](https://skills.sh/besty0728/unity-skills/unity-scene) |
| unity-probuilder | ProBuilder 白盒建模 | [链接](https://skills.sh/besty0728/unity-skills/unity-probuilder) |
| unity-physics | 物理查询/配置 | [链接](https://skills.sh/besty0728/unity-skills/unity-physics) |
| unity-performance | 性能红线建议 | [链接](https://skills.sh/besty0728/unity-skills/unity-performance) |
| unity-2d 相关 | 见 nice-wolf-studio 仓库 | — |

### 3.4 nice-wolf-studio/unity-claude-skills — Unity 6 文档向（35 个 skill）

仓库：https://github.com/nice-wolf-studio/unity-claude-skills  
基于 Unity 6.3 LTS 官方文档，偏概念 + 正确性模式。

一键装全部：

```bash
npx skills add nice-wolf-studio/unity-claude-skills --all -y --copy
```

| Skill 名 | 用途 | 页面 |
|---|---|---|
| unity-2d | 2D 游戏开发（Sprite/Tilemap/2D 物理） | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-2d) |
| unity-graphics | 渲染管线/Shader/材质 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-graphics) |
| unity-level-design | 关卡设计转代码 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-level-design) |
| unity-procedural-gen | 程序化生成/地形 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-procedural-gen) |
| unity-physics | 3D/2D 物理 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-physics) |
| unity-scripting | C# 脚本生命周期 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-scripting) |
| unity-performance | 性能分析优化 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-performance) |
| unity-lighting-vfx | 灯光/VFX/后处理 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-lighting-vfx) |
| unity-ui | UI Toolkit / uGUI | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-ui) |
| unity-game-loop | 核心循环/难度/节奏 | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-game-loop) |

### 3.5 cryptorabea/claude_unity_dev_plugin — Unity 开发插件集

仓库：https://github.com/cryptorabea/claude_unity_dev_plugin

| Skill 名 | 用途 | 安装量 | 页面 |
|---|---|---|---|
| unity-fundamentals | Unity 基础 | ~79 | [链接](https://skills.sh/cryptorabea/claude_unity_dev_plugin/unity-fundamentals) |
| unity-architecture | 架构设计 | ~136 | [链接](https://skills.sh/cryptorabea/claude_unity_dev_plugin/unity-architecture) |
| unity-performance | 性能优化 | ~149 | [链接](https://skills.sh/cryptorabea/claude_unity_dev_plugin/unity-performance) |
| unity-workflows | 工作流 | ~100 | [链接](https://skills.sh/cryptorabea/claude_unity_dev_plugin/unity-workflows) |

---

## 四、着色器 Skill（3 个，已精简安装）

> Skill 越多，Cursor 每次对话加载的描述文本越长，Agent 选 skill 也越容易犹豫。
> 已从 18 个精简到 **3 个**，覆盖 BALL 项目（Unity 2D URP + 手写 HLSL）的全部需求。
>
> 重装：`Skill/install-shader-skills.ps1`

| # | Skill | 干什么 | 对应 BALL 资产 | 页面 |
|---|---|---|---|---|
| 1 | **urp-hlsl-templates** | URP HLSL/ShaderLab 模板、SRP Batcher 规范 | `TronWall` `TronFlipper` `TronGrid` | [链接](https://skills.sh/adevra/unity-shader-agent-skills/urp-hlsl-templates) |
| 2 | **shader-programming** | Shader 基础：UV、rim light、溶解、描边等效果数学 | `CyberPulseSprite` `SpaceDistortion` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/shader-programming) |
| 3 | **shader-techniques** | 高级 VFX：霓虹发光、后处理、材质特效 | `ArcadeCRT` `UI_CyberCard` | [链接](https://skills.sh/pluginagentmarketplace/custom-plugin-game-developer/shader-techniques) |

**三个的分工：**
- `urp-hlsl-templates` → **怎么写**（URP 语法、Pass 结构、CBUFFER）
- `shader-programming` → **为什么**（效果背后的 GPU 数学）
- `shader-techniques` → **做什么**（霓虹/CRT/发光等具体特效方案）

### 已删除的 15 个（及原因）

| 删除的 Skill | 原因 |
|---|---|
| unity-shader / unity-material（besty0728） | 需要 Unity Editor REST API 插件，BALL 未安装 |
| unity-shadergraph / shader-graph-*（4 个） | 项目用手写 `.shader`，不用 Shader Graph |
| unity-shaders-rendering / unity-graphics | 与上面 3 个高度重叠，还混入了 HDRP 内容 |
| mobile-shader-optimization / mobile-post-processing | 偏移动端优化，BALL 当前是 PC 向 |
| water-fluid-shaders | 弹球游戏不需要流体 |
| texture-packing-variant-stripping / webgl-shader-constraints | 过于细分，用到时再查文档即可 |
| shader-dev / threejs-shaders | 跨引擎/Three.js，与 Unity 无关 |

> 如果以后需要某个被删的 skill，在 [skills.sh](https://skills.sh) 搜名字单独装即可。

---

## 五、地编 / 关卡 / 地形 Skill

| Skill 名 | 类型 | 安装量 | 安装命令 | 页面 |
|---|---|---|---|---|
| level-design | 通用关卡设计 | ~1.7K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@level-design` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/level-design) |
| unity-level-design | Unity 关卡设计转代码 | ~40 | `npx skills add nice-wolf-studio/unity-claude-skills@unity-level-design` | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-level-design) |
| unity-terrain | Unity Terrain 操作 | ~48 | `npx skills add besty0728/unity-skills@unity-terrain` | [链接](https://skills.sh/besty0728/unity-skills/unity-terrain) |
| unity-probuilder | ProBuilder 白盒/地编 | — | `npx skills add besty0728/unity-skills@unity-probuilder` | [链接](https://skills.sh/besty0728/unity-skills/unity-probuilder) |
| unity-procedural-gen | 程序化生成/地形 | — | `npx skills add nice-wolf-studio/unity-claude-skills@unity-procedural-gen` | [链接](https://skills.sh/nice-wolf-studio/unity-claude-skills/unity-procedural-gen) |
| unity-tilemap-2d | 2D Tilemap | ~1.2K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-tilemap-2d` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/unity-tilemap-2d) |
| design-level-design | 关卡设计（fcsouza） | ~40 | `npx skills add fcsouza/agent-skills@design-level-design` | [链接](https://skills.sh/fcsouza/agent-skills/design-level-design) |
| team-level | 关卡团队协作 | ~271 | `npx skills add donchitos/claude-code-game-studios@team-level` | [链接](https://skills.sh/donchitos/claude-code-game-studios/team-level) |
| procedural-gen | 程序化生成（通用） | ~1.6K | `npx skills add gamedev-skills/awesome-gamedev-agent-skills@procedural-gen` | [链接](https://skills.sh/gamedev-skills/awesome-gamedev-agent-skills/procedural-gen) |

---

## 六、针对 BALL 项目的推荐安装组合

BALL 是 **Unity 2D + URP + 弹球/塔防**，建议按优先级装：

### 必装（策划 + 数值，5 个，已在 `Skill/` 目录）

见第二节。写设计文档、调数值、设计波次时用。

### 必装（着色器，3 个，已在 `Skill/shader-unity/`）

见第四节。写/改 `Assets/Shaders/` 里的 `.shader` 时会自动用到。

### 强烈推荐（Unity 开发，尚未安装）

```bash
# Unity 官方
npx skills add unity-technologies/skills@unity-cli -y --copy
npx skills add unity-technologies/skills@sprite-editor -y --copy
npx skills add unity-technologies/skills@urp-postprocessing -y --copy

# Unity 2D + 物理 + 脚本
npx skills add nice-wolf-studio/unity-claude-skills@unity-2d -y --copy
npx skills add nice-wolf-studio/unity-claude-skills@unity-physics -y --copy
npx skills add nice-wolf-studio/unity-claude-skills@unity-scripting -y --copy
npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-csharp-scripting -y --copy

# 性能
npx skills add nice-wolf-studio/unity-claude-skills@unity-performance -y --copy
```

### 可选（地编 / 关卡）

```bash
npx skills add nice-wolf-studio/unity-claude-skills@unity-level-design -y --copy
npx skills add gamedev-skills/awesome-gamedev-agent-skills@level-design -y --copy
npx skills add gamedev-skills/awesome-gamedev-agent-skills@unity-tilemap-2d -y --copy
```

### 土豪版（整仓库全装）

```bash
npx skills add unity-technologies/skills --all -y --copy
npx skills add besty0728/unity-skills --all -y --copy
npx skills add nice-wolf-studio/unity-claude-skills --all -y --copy
npx skills add gamedev-skills/awesome-gamedev-agent-skills --all -y --copy
npx skills add adevra/unity-shader-agent-skills --all -y --copy
```

---

## 七、搜索更多 Skill

```bash
# 关键词搜索
npx skills find "unity"
npx skills find "shader"
npx skills find "level design"
npx skills find "game balance"

# 浏览网站
# https://skills.sh
```

---

## 八、迁移到正式电脑

1. 把整个 `Skill/` 文件夹拷过去
2. 重新运行安装脚本重建 Cursor 链接：
   ```powershell
   cd Skill
   .\install-design-skills.ps1    # 策划 + 数值 5 个
   .\install-shader-skills.ps1    # 着色器 3 个
   ```

### 当前已安装汇总（8 个）

| 分类 | 数量 | 目录 | 核心 Skill |
|---|---|---|---|
| 数值 / 平衡 | 2 | `Skill/balance-economy/` | balance-check, game-balance-economy |
| 游戏策划 | 3 | `Skill/game-design-gdd/` | fundamentals, design-review, level-design |
| 着色器 | 3 | `Skill/shader-unity/` | urp-hlsl-templates, shader-programming, shader-techniques |
| **合计** | **8** | | |
