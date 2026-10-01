# EnemySandbox 验收步骤

从 Unity Project 窗口打开 `Assets/Scenes/Gameplay/EnemySandbox.unity`，进入 Play Mode。场景沿用 BuffSandbox 的球、墙、GameManager、BuffManager 与 ComboSystem；新敌人通过正式 `Minion` 和共用生成路径创建。左侧面板只负责复现情境，不是正式 UI。场景没有加入 Build Settings。

| 测试 | 操作 | 期望 |
|---|---|---|
| Splitter 死亡 | 选 Splitter，Spawn，按 Ball hit 直到死亡 | 约 0.18 秒后出现且只出现 2 个 Mini；最近事件显示裂解 |
| Splitter 近底 | 选 Splitter，Near bottom，击杀 | Mini 出生后有短宽限，不在出生同一帧连续扣血 |
| 清场取消裂解 | 击杀 Splitter 后立即 Clear / `C` | 不再出现 Mini |
| Charger 打断 | 选 Charger，Spawn，等待 Telegraph / Dash，按 Ball hit 或用真实球撞击 | 进入 Recovery；冲锋结束后才会重新计时 |
| Charger 冰冻 | Telegraph 时按 Freeze 0.8s | 计时暂停，预警不会在冰冻期间直接跳为冲锋 |
| Conductor 推进 | 先生成 Grunt，再生成 Conductor，观察连接线与事件 | 最多 2 条线；预警后最多推 2 个目标、每次约 0.35 世界单位 |
| Conductor 反制 | Telegraph 时主球命中；或 Charge all + ignite linked | Pulse 被取消；有效电点火令核心受 1 伤并短暂硬直 |
| Jammer 生效 | 选 Jammer，Near bottom，等待 Active，Set both CD=10 后按 Combo tick | 显示倍率 0.5；两个槽各比正常少减一半 |
| 多 Jammer | 再生成 1–2 个近底 Jammer，重复 Combo tick；Clear 后重复 | 倍率仍 0.5；清场后立即回到 1.0 |
| 混编 | Mixed ×7，开/关底线伤害，反复清场与重置 | 无遗留裂解、链接、干扰；既有三种小兵仍可生成 |

首轮可以用面板 Ball hit 稳定复现，再用实际弹珠碰撞验证手感。`Electric`、`Frost`、`Combo` 按钮叠入对应 Buff；`Clear Buffs` 恢复无 Buff。敌人的形状、比例和状态标记是功能占位，正式素材与血条设计后续统一处理。

## 当前验证状态

- 2026-10-01：场景、配置与脚本已创建；用 Unity 工程的编译参数编译通过。
- 2026-10-01：Play Mode 中已验证 Splitter 死亡后生成恰好 2 个固定 1 HP 的 Mini，以及清场会取消尚未生成的 Mini。
- 其余项目、画面表现和真实弹珠碰撞仍待手动验收；下表中的「期望」不是已通过记录。
