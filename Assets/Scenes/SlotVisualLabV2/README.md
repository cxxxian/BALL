# Slot Visual Lab V2

## 法线光照试验

场景的 `SlotCabinetRelighting` 固定使用独立底色图和近似法线图。组装部件与最终机身共用同一张实时材质输出，组装结束时不会换回另一张机身图。进入 Play 后按 F8 切换固定／移动灯光，也可在 LAB 面板点击对应按钮。选中 `Protocol Array - V2 Portrait`，Inspector 的 `Slot Cabinet Relighting` 可调整 Metallic、Normal Strength、Lamp Strength、Rim Strength 与 Light Direction。Metallic 默认 0.2，可在 0.15～0.3 间微调；这是外壳的风格化金属高光强度，并非完整 PBR 材质。

开场为带倒角厚度的黑色实体启动棒，细青色灯槽点亮后两端保持横向拉开。四根实体导轨从两端接口向上下连续挤出，亮光位于生成前沿；启动握柄逐渐缩入边框，导轨随当地机身外壳生成逐段吸收。橙色三角灯在后段点亮。按 F6 重播动画。
白色硬描线由试验底色图清理，法线控制倒角与滚轮表面的光照变化。灯带遮罩在 GPU 上从原图的明亮彩色像素单独提取，最终光晕继续由 URP Bloom 处理。试验法线由图像生成工具近似制作，尚不是从精确 3D 模型烘焙的法线。素材提示词和实现边界见 `CabinetRelightingTrial.md`。

V1 的独立老虎机造型，按竖屏收窄肩部，减少背景信息；保留三滚轮、青色灯带、琥珀色锁定指示及六边形 ROLL 按钮。
弹珠台参考仅用于平滑黑玻璃的材质处理，不复用或融合弹珠台造型。

打开 `SlotVisualLabV2.unity`，进入 Play。V2 Panel Settings 使用 Scale With Screen Size，以 720×1280 为参考分辨率并按宽高居中匹配；1080×1920 Game View 会将完整竖屏画布放大到整屏。推荐用 1080×1920 验收；1080×2340 会按屏幕比例适配，核心控件保持在屏幕内。
此场景按正式老虎机的渐进流程演示：第一轮免费揭晓；提前兑现可收下当前可得 Buff，继续时第二轮投入 1 枚筹码、第三轮投入 2 枚。第二轮对子加层和三轮后的牌型预览复用正式结算规则。测试筹码为本地余额，Buff 不应用到正式对局。
LAB 展开 NORMAL / 3 EPIC / BLOOM / AUDIO / RESET。上方顶板的状态行显示当前阶段或最终牌型效果；Jackpot 规则摘要放在老虎机上方。滚轮下方三格只展示对应 Buff 的名称和稀有度，点击已揭晓的卡片可查看完整说明。卡片字号为 22px（以 720px 设计画布计），三格与滚轮逐列对齐。


## 素材与引擎发光

- `protocol_glyphs_v2.png` 及 `glyph_0..7.png`：透明图标，外侧空白 Alpha 为零，清晰色芯。
- 内置 image_gen 生成及编辑，提示词保存在 `AssetPrompts.md`。
- UI Toolkit 输出到 2D RenderTexture，单个平面交给正交相机；没有机身 3D 模型或机械滚筒。
- `SlotUIHDR.shader` 只提升明亮且有色的发光芯；黑面板和中性文字不会一起提亮。
- URP Volume 提供引擎 Bloom。默认 Intensity 0.4、Threshold 1.0、Scatter 0.55；HDR Emission Gain 2.2。
- `Protocol Array - V2 Portrait` 的 `SlotVisualLabV2Bloom` 可在 Inspector 实时调整，LAB / BLOOM 可以开关。
- 灯带的颜色、反射和少量原始高光仍属于材质图；Bloom 的柔光在 Unity 中控制。

## 验证

Unity 2022.3 / URP 14 编译成功；Play 中无新增错误。
已验证 ROLL 的屏幕到面板坐标命中、导航提交、滚动和结果锁定。
已捕获 1080×1920 的 Bloom ON/OFF 实际 Game View 对照，抽样像素中 64,346 个点有差异，证实引擎 Bloom 生效。
已验证 1080×2340 的核心控件完整落在屏幕内。
最终对照截图在 `Captures/SlotVisualLabV2/V2_GameView_BloomOn.png`、`V2_GameView_BloomOff.png`，长屏截图在 `V2_TallPhone.png`。
普通离屏相机纹理截图与实际 Game View 的后处理输出有差别，因此最终视觉检查采用 ScreenCapture 的实际 Game View 输出。

V1、原老虎机、弹珠台资源、原有渲染管线资产及玩法脚本均保持原样。

## 侧边与图标对齐优化

当前机身使用 `cabinet_albedo_trial.png` 和 `cabinet_normal_trial.png`；`terminal_portrait_clean_transparent.png` 仅提供灯带遮罩与轮廓数据。两侧滚轮外缘改成连续黑色导轨，清理碎反光并移除中部外凸菱形。减速动画以待揭晓 Buff 图标作为目标帧，因此滚轮停止和结算提交使用同一图标，不会在终止瞬间跳格。
图标从完整的 384×512 图集单元切出，避免原方形裁剪截断图形。`glyphBounds` 保存 Alpha ≥32 的可见轮廓范围，运行时生成居中的 Sprite，在统一 92px 方框内等比显示；三个锁定结果的中心共用设计坐标 y=584。
1080×1920 实际 Game View 截图：`Captures/SlotVisualLabV2/V2_CleanRails_Aligned.png`。

## 在 Editor 中编辑布局

菜单 `Rebound Protocol / Slot Visual Lab / Edit V2 UI in UI Builder` 打开 Unity 原生 UI Builder。V2 设计画布为 720×1280（9:16）；此 UXML 已开启 `Match Game View`，当前 Game View 设为 1080×1920（9:16），所以 Builder 会跟随游戏窗口比例。若在另一台机器首次打开时没有匹配，先把 Game View 切到 9:16，再选中 UI Builder Hierarchy 顶部的 UXML 文档，在 Canvas 设置勾选 `Match Game View`，最后点 Viewport 工具栏的 `Fit Canvas`。UI Builder 会为每份 UXML 单独保存这些预览设置。

固定设计画布在 UI Builder 中默认居中显示；游戏运行时由场景控制器按屏幕与安全区缩放、居中。编辑时拖动对应的控件节点，或在 Inspector 修改 Left、Top、Width、Height 和 Rotate，然后保存 UXML/USS。三张 Buff 卡与三个滚轮中心列保持逐列对应。

- 选中 `rollControl`：整体移动 ROLL 图片、文字和点击区域。Inspector 的 Position 改 Left / Top，Size 改 Width / Height，Transform 改 Rotate / Scale；也可用画布拖拽和缩放手柄。
- `rollFrame`：独立透明外框素材 `roll_frame.png`。`rollButton`：文字和点击响应，填充父容器。
- `labButton`、`testDock`、`normalButton`、`specialButton`、`bloomButton`、`audioButton`、`resetButton`、`cashOutButton`：分别编辑位置、尺寸、文字、旋转和样式。`testDock` 默认隐藏，编辑时暂时设 Display 为 Flex，完成后恢复 None。
- `brandLabel`、`titleLabel`、`statusLabel`、`result1..3`、资源文字与 `reel1..3` 窗口也都保存到 UXML。
- `statusLabel` 显示当前阶段或最终组合摘要；`result1..3` 是与三根滚轮逐列对应的 Buff 卡，点击后由 `inspectOverlay` 显示完整说明。牌型规则摘要不再占用滚轮下方的金属内嵌区。
- Ctrl+S 保存 UXML/USS 后，Play 使用保存后的布局。程序不再清空或重建静态节点，也不会重设按钮的位置、大小、旋转。
- `stage` 是整张设计画布，运行时缩放与居中适配屏幕；请调整它下面的节点。滚轮符号的滚动位置由动画控制，间距可在 Controller 的 Reel Pitch 中调整。


白边处理：按钮各状态清除默认 background-image 与边框，取消矩形悬停底色；机身图移除了原 ROLL 外框，独立素材去掉了白色金属边。Bloom 仍由引擎负责。

## 统一老虎机音效

`Assets/Audio/SlotMachine/` 内原始音效与创战纪风格的 V2 都由 `Tools/generate_slot_audio.py` 可复现合成（48 kHz、单声道 PCM），通过干脆打击、FM 伺服音、紧凑噪声和冷色电子泛音塑造机械科幻感。V2 更新按钮、启动、锁定、确认和特殊结果；`slot_reel_tick_01..03.wav` 是保留的原滚动声，二进制内容未修改。无外部音频依赖。

`SlotMachineSfx` 是可复用组件，可挂在任意槽机对象上，并在 Inspector 里指定 AudioSource、片段和音量；`PlayHover`、`PlayPress`、`PlaySpinStart`、`PlayReelTick`、`PlayReelStop`、`PlayRollConfirm`、`PlaySpecialReveal` 可由玩法事件复用。V2 已连接按钮反馈、抽取开始、逐轮节拍/锁定和特殊结果；测试面板 AUDIO 顺序试听整套 V2，并保留滚动声原样。


后半段保留像素电路扩散与像素块材质成型，前沿使用分簇噪声逐块生成，最终 UI 采用平滑交接。
