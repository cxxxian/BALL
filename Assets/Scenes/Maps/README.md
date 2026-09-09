# Maps — 机关编排地图

基于 `SampleScene` 完整玩法底板，只重排场景机关。

路径：`Assets/Scenes/Maps/`  
重新生成：菜单 **Ball → Build Mechanism Maps**

| 场景 | 主题 |
|------|------|
| `Map_BumperCluster` | Bumper 密巢 |
| `Map_PortalCircuit` | 传送门回路（高可玩参考） |
| `Map_OrbitFarm` | **环轨** 加速甩出 |
| `Map_DivertorRails` | **分流闸** + **滑轨** |
| `Map_PlayfulToys` | Portal + Orbit + Divertor + Rail 合演 |

旧版 Prism / Cannon 场景已从生成列表移除（脚本仍保留）。

---

## 为什么 Portal 好玩？新机关怎么对齐

传送门的乐趣 ≈ **空间惊喜 + 回路可读 + 立刻想再玩一次**。

| 机关 | 核心乐趣 | 和 Portal 的差别 |
|------|----------|------------------|
| **Portal** | 配对瞬移、8 字回路 | — |
| **OrbitRing 环轨** | 看得见的公转加速，再切线甩出 | 不瞬移；同地转圈提速 |
| **FlipDivertor 分流闸** | **Z/← 左出口，X/→ 右出口** 主动选路 | 短轨送达，玩家决策 |
| **SlideRail 滑轨** | 沿曲线「骑行」到出口 | 全程可见位移，非瞬移 |

棱镜 / 炮塔偏「改方向工具」，缺少回路惊喜，所以新地图不再主推它们。
