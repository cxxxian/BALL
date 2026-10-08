# Slot Visual Lab

打开 `Assets/Scenes/SlotVisualLab/SlotVisualLab.unity`，进入 Play，点击 ROLL。
推荐横屏：1536×1024 或 1920×1080。竖屏完整缩放并留黑边，目前不是竖屏专用布局。

- NORMAL / ROLL：演示滚动、减速、三个通道依次锁定。
- SPECIAL：固定琥珀色特殊图标，测试结果强调。
- GLOW / GLASS：开关独立选择光层和边缘暗化层。底板自带的灯光不会随此按钮关闭。
- RESET：恢复参考组合。
- Tab / Enter：选择并触发按钮。

这是一套 2D UI Toolkit 视觉演示，不接入 BuffManager、概率、筹码、Reroll 或结算。
资源数值是演示标签；原老虎机的 UXML、USS、脚本和场景均未修改。

## 分层

1. `terminal_backplate.png`：环境、机身、空白窗口及按钮底板，合并成一张静态图，用于首轮视觉验证。
2. `protocol_glyphs.png`：AI 生成的透明图标图集，八种符号。
3. `glyph_0..7.png`：Unity 编辑器切出的独立图标，可复用。
4. 三个 `reel-window`：UI Toolkit overflow hidden；图标纵向移动，按距离压缩 Y 和降低 Alpha，模拟纵深。
5. `glass_edge_shade.png`：程序生成上下和两侧暗化。
6. `selection_glow.png`：程序生成柔和选中光，独立覆盖。
7. 实时文字、图例和可点击按钮：不烘焙在底板内。

脚本在 `Assets/Scripts/SlotVisualLab`；场景生成器在 `Assets/Editor/SlotVisualLab`。
菜单 `Rebound Protocol > Slot Visual Lab > Create isolated scene` 可重建场景。
当前底板的环境、框架、按钮光边尚未拆成独立图片；适合验证参考效果，后续接入旧老虎机前再拆分。

## 验证记录

Unity 2022.3.41f1c1 编译成功，新增代码没有编译错误。
已进入 Play 检查 UI，并捕获横屏、竖屏及面板离屏预览。
MCP 相机截图过程中出现 Unity PlayerLoop 递归错误；离屏面板截图用于避开该截图路径。
截图位于 `Captures/SlotVisualLab`。
原 SampleScene2 的未保存内容保存在 `Recovery` 下的副本中。

## 素材生成

使用内置 image_gen 工具。生成提示词见同目录 `AssetPrompts.md`。
图标使用额外透明背景修正，并验证 PNG 空白角落 Alpha = 0。
