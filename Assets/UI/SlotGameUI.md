# 正式游戏老虎机界面

正式布局：Assets/UI/BuffSelection.uxml；样式：Assets/UI/BuffSelection.uss；面板：Assets/UI/SlotGamePanelSettings.asset。用 UI Workbench 调整正式界面；V2 实验场景样式只影响实验预览。SampleScene、SampleScene2 和地图变体已配置新版组件。

BuffSelectionController 负责正式抽取、筹码、重转、结算、教学限制和塔放置。SlotVisualLabV2Controller 的 gameplayMode 只负责显示、动画和详情。机柜按适配后的 84% 居中，可用 gameplayScale 调整。

转场沿用 V2 测试场景的 SlotAssemblyTransition / SlotMechanicalAssembly3D：中心棒子 → 电路扩展 → 像素蔓延 → 机柜与控件显现。已移除 SlotWaveTransition，游戏挡板不参与转场，也不被隐藏或移动。打开奖励面板时等待有效布局，避免首帧初始化跳过动画。转场使用独立时间，暂停游戏时仍播放，期间锁住抽取。

背景截取冻结的游戏画面，在半分辨率下进行两轮水平/垂直 Gaussian 模糊。中间纹理使用双线性采样与 Clamp，避免原先的规则条纹；额外进行低饱和度、压暗和柔和暗角处理。backdropBrightness 默认 .15。资源 shader：Assets/Resources/SlotBackdropGaussian.shader。关闭时释放背景纹理和合成遮罩，不改变主相机后处理。

正式场景的发光参数：emissionStrength=.85；bloomIntensity=.16；bloomThreshold=1.2；bloomScatter=.28。V2 实验场景保留自身配置。

验证过正式抽取/扣费/重转/结算、界面按钮命中与点击、暂停恢复、塔放置、转场各阶段实际画面、关闭与退出 Play。最近视觉调整后控制台无错误。旧 SlotVisualLab 的过时资源已清理，仍使用的贴图已迁移且保留 GUID；恢复场景在 Scenes/Recovery。
