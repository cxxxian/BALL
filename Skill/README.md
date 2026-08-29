# BALL 项目 Skills 目录

本目录存放从 [skills.sh](https://skills.sh) 下载的社区 Agent Skills，供 Cursor 使用。

## 分类

| 目录 | 说明 | 数量 |
|------|------|------|
| [balance-economy/](balance-economy/) | 数值 / 经济 / 平衡 | 2 |
| [game-design-gdd/](game-design-gdd/) | 游戏策划 / GDD / 关卡 | 3 |
| [shader-unity/](shader-unity/) | URP / HLSL 着色器 | 3 |
| [ui-design/](ui-design/) | 游戏 UI 设计 | 2 |
| [ui-unity/](ui-unity/) | Unity uGUI / UITK | 2 |
| **合计** | | **12** |

## Cursor 集成

安装脚本会将每个 skill 通过目录联接（junction）链接到项目根目录的 `.cursor/skills/`，Cursor Agent 会自动发现这些 skill。

## 重新安装

```powershell
cd F:\Study\GameDesign\Ball\Skill
.\install-design-skills.ps1    # 策划 + 数值 5 个
.\install-shader-skills.ps1    # 着色器 3 个
.\install-ui-skills.ps1        # UI 设计 + Unity UI 4 个
```

## 完整清单

详见 [Skills下载清单.md](Skills下载清单.md)。
