# Gameplay Tests

玩法/系统功能测试（非 UI 风格预览）。

| 场景 | 说明 |
|------|------|
| `BossMissileParryTest.unity` | Boss 导弹招架 |
| `BuffTowerTest.unity` | Buff 塔 |
| `BuffSandbox.unity` | Buff 构筑与战斗状态 |
| `EnemySandbox.unity` | Phase 6 新敌人单体、混编与边界验证；暂不接正式波次 |

脚本同目录：`Assets/Scripts/Test/Gameplay/`

从 Unity Project 窗口直接打开对应场景进行测试。

`EnemySandbox` 左侧面板可选择敌种和模拟波次，生成普通位置或近底目标，施加主球伤害/短冻、切换构筑、测试 Jammer 的 Combo 减 CD，并查看敌人状态与最近事件。按 `C` 清敌，右键或 `B` 重置测试球。更多验收步骤见 `EnemySandbox_TestGuide.md`。

UI 面板预览请用：`Assets/Scenes/UIShowcase/UIShowcase.unity`
