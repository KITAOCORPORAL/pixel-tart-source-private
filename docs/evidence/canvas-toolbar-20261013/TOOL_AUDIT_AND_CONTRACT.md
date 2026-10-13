# 逐工具审计与处理合同

基线：3c5a5ff。PASS_CODE 仅指代码接线与自动测试，不是桌面批准。

证据根 N:/pixart/kitao-canvas-toolbar-20261013。
- Toolbar: src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.CanvasToolbar.cs
- Input: src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs
- Zoom: src/RAWSelectionAssistant/Views/ColorStudioZoomPanState.cs
- Range: src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.RangeSelection.cs
- Editor: src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs
- PV: src/RAWSelectionAssistant.Core/Services/Projects/ProfessionalRawPipeline.cs

|工具|当前证据与本轮|字段与历史|保存重开|取消与导出|状态|
|---|---|---|---|---|---|
|选择 V|Input 分界线拖动；Toolbar 常驻选择|ActiveCanvasTool 临时；不写历史|默认选择|切工具清取样 revision；输出不变|PASS_CODE|
|抓手 H|Zoom.PanBy；常驻左键、Space及中键|Pan 视图状态|不保存手势|Esc 释放 capture；输出不变|PASS_CODE|
|缩放 Z|ZoomAbout；点击、Alt点击、Ctrl+0/1、双击|Zoom 视图状态|不保存临时比例|并排右半坐标转换、物理DPI换算；输出不变|PASS_CODE|
|裁切 C|无通用裁切；InstantCrop 仅胶片|缺 Crop、坐标域、算法版本和历史|缺迁移|需全尺寸裁切与块映射；不暴露入口|DEFERRED|
|旋转拉直 R|EXIF 转正不是用户编辑|缺 Angle、重采样、历史|缺存储|需尺寸、透明边界及导出测试|DEFERRED|
|透视 Shift+R|无通用透视|缺四点、矩阵、版本|缺历史及存储|需逆采样、halo、取消|DEFERRED|
|修复 J|无像素修复器|缺源、笔触、算法、蒙版|缺序列化和撤销|需确定性像素与块接缝测试|DEFERRED|
|克隆 S|对象 Clone 不是像素克隆|缺源点、笔触、对齐模式|缺存储和历史|需源身份与跨尺寸空间对应|DEFERRED|
|画笔 K|WPF Brush 不是笔触调整|缺 Stroke、BrushMask、羽化|缺存储和撤销|需真实分块像素处理|DEFERRED|
|橡皮 E|无笔触擦除|缺蒙版擦除语义|缺历史和存储|不暴露假入口|DEFERRED|
|线性渐变 G|无几何蒙版|缺端点、组合、版本|缺存储和历史|需跨尺寸及全尺寸导出|DEFERRED|
|径向渐变 Shift+G|无径向蒙版|缺椭圆、羽化、反转|缺存储和历史|需空间一致及分块处理|DEFERRED|
|蒙版显示 O|Range.ShowSelection 仅颜色范围|通用 MaskLayer 缺失|范围参数可存，显示临时|保留原面板，不冒充通用蒙版|EXISTING_LIMITED；工具栏 DEFERRED|
|白平衡取样 W|WB 参数存在，中性估计器不存在|缺区域、输入域及gain映射|未加字段|不得重复作用RAW白平衡|DEFERRED|
|颜色取样 I|Range 在所选节点输入域取样；不创建空节点|Samples/NegativeSamples；ChangeStack历史|Session和Version保存栈|Esc、切工具、切源清revision；Shift增加/Alt减少；同栈输出|PASS_CODE；独立RAW及光片禁用|
|原图比较 B|HoldOriginal；点击锁定并恢复前模式|ViewMode/held 临时；不写像素历史|不保存按键状态|失焦、卸载、切源释放；输出不变|PASS_CODE|
|撤销重做|现有 Undo/RedoAdjustmentCommand|专业调整栈共用历史|结果存储；undo栈不落会话|文本编辑保有撤销；独立RAW及光片使用版本恢复|PASS_CODE；旧栈限定|

## 处理与保存合同

未修改 Core、PV4/PV5/PV6、RAW 转换、白平衡、胶片、快速导出、Publishing。独立 RawDefinition/InstantInput 不允许接旧 ColorRange，工具栏禁用并解释。观察状态不写处理定义、不新增版本、不迁移历史。

取样只改选中 ColorRange 节点；未选中则禁用。进入取样不生成空节点、不污染历史。取消不添加样本。导出仍读取冻结定义，不用屏幕截图。

100% 只表示源像素与屏幕物理像素比例，不代表视口已补齐。独立 RAW 继续由 RawPipelineStatus 报告覆盖与代理；尺寸未就绪加代理标记。

## 第一段与完整设计的差异

未交付五组菜单、全部工具折叠、选项条、草稿应用或丢弃、通用失败重试、右键菜单、独立触控板手势、源像素坐标读数及上述像素工具。当前五项文字工具使用现有主题选中边框、无障碍名称及横向滚动工具带。不重排全局外壳或其余页面，不把 HTML Toast 当功能。停止处理仍沿用旧命令。
