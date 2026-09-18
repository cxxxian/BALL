# UI — 界面布局与 chrome

仅放 **UITK / 菜单视觉**：`.uxml` `.uss` `PanelSettings`、菜单 Textures、`UIShowcase`。

| 内容 | 说明 |
|------|------|
| `*.uxml` / `*.uss` | MainMenu、BuffSelection、Pause、Settlement 等 |
| `ProtocolNeonTheme.uss` | 共享主题 token |
| `Textures/` | 菜单 vignette / scanlines（USS `background-image`） |
| `UIShowcase/` | 面板预览场景用 |

**不要**把敌人/台面精灵放这里。

- 世界贴图 → `Assets/Art/`
- 教程终端 / 导弹 lockon（运行时 Load）→ `Assets/Resources/UI/`
- 风格规则 → `Notes/UIStyle/CyberpunkNeon_HardPrompt.md`
