# Art — 世界 / 局内精灵

世界侧贴图（`SpriteRenderer`、机关、敌人）。**不要**把菜单 UITK 素材放这里。

| 目录 | 用途 |
|------|------|
| `Table/` | 台面：墙、挡板、Bumper、齿轮等 |
| `Enemies/` | Boss / 小兵精灵（由 `ScriptableObjects/Enemies` 引用） |

硬编码路径示例：`Assets/Art/Table/...`（见 `PlayfieldSideShoulder`、`BuildMechanismMaps`）。

界面布局 → `Assets/UI/`  
运行时 `Resources.Load` → `Assets/Resources/UI/`
