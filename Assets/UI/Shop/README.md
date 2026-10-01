# 可编辑协议商店

场景：`Assets/Scenes/MainMenu.unity`
预制体：`Assets/UI/Shop/ShopCanvas.prefab`

## 在 Editor 中调整

退出 Play，展开 `ShopCanvas / EditableLayout`。Scene 视图打开 2D，按 **T** 使用 Rect Tool。
选中区块或其子节点即可拖动位置、调整边界；Inspector 的 RectTransform 可精确修改尺寸。
文字的 Text 组件提供 Font Size 和 Color。Play 模式的修改不会自动保存。

主要节点：

- `Header`：返回、标题、余额。
- `Navigation`：直购弹珠与协议宝箱按钮。
- `DirectPage/Hero/BallPreview`：弹珠图片与尺寸。
- `DirectPage/Hero/NeonIdentityDisc`：独立光盘，透视、线宽、旋转速度、颜色均可编辑。
- `DirectPage/BallDetails`：名称与稀有度。
- `DirectPage/BoundSkills/SkillSlot1`、`SkillSlot2`：技能名、冷却与模式、效果说明。
- `DirectPage/PurchaseArea`：价格与操作按钮。
- `DirectPage/OfferCarousel`：鼠标拖动列表。ScrollRect 可调整惯性、弹性和滚轮速度。
- `OfferCardTemplate`：运行时卡片的模板，编辑时可暂时启用查看，完成后关闭。
- `CratePage`：编辑宝箱页时启用它，同时关闭 DirectPage；完成后恢复默认直购页。

列表 Content 使用 HorizontalLayoutGroup，子项的位置由布局管理；调整卡片尺寸请改模板 RectTransform，间距请改 Content 的 Spacing。
运行时只填充数据、创建列表卡片和执行过渡，不重设作者编辑的区块坐标与字号。

## 结构与限制

以 1080 × 1920 竖屏为本次验收基准。Canvas 按高度缩放；横屏当前保留居中的竖向布局，尚未另做横屏排版。
商店主体使用 uGUI；原 UI Toolkit 开箱仪式与结果弹层继续复用，展示结果时暂时隐藏 Canvas，关闭后恢复。
字体为 Noto Sans CJK SC，许可证见 `Assets/Fonts/NotoSansCJK-LICENSE.txt`。
本项目尚未安装 TMP，当前使用 uGUI Text 和独立中文字体。

创建工具菜单 `Rebound Protocol / UI / Create editable shop` 只在不存在商店实例时创建，不覆盖已有设计。
