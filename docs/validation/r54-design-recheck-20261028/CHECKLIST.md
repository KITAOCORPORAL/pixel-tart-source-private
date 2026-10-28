# R5.4 逐项核验清单

符合＝本条实现/证据成立，不是全产品通过；部分＝已有实现但仍有明确差距；缺口＝确认缺失或与稿不一致；未验证＝本轮不足以判断。未来AI等保留在完整稿，但禁用不计完成。

## 全局和外壳

### G01 简易专业以第9章及v2图优先
- 条款：§0/9；状态：**符合**；优先：P1；范围：本批界面。
- 实际：实际读取346段和11张PNG；简易/专业v2及右栏/菜单/剧照图已打开。右栏备选只辨识不实施。
- 证据：`DESIGN_INPUT.json`；`N:/UI5.4.zip`。

### G02 深色/青绿/11–12DIP文字
- 条款：§0；状态：**部分**；优先：P2；范围：本批界面。
- 实际：整体深色与11DIP字体成立；混合卡顶条用#BD9C67金色，类型边框没有逐类用指定青绿。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:1`；`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:54-60`。

### G03 主界面不放说明文字
- 条款：§0/7；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：节点头固定显示“仅显示所选节点…”；相纸区固定同源缩略说明。应移tooltip/帮助，错误/断线状态另区分。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.Layout.cs:63`；`screenshots/professional-1366-OFFSCREEN.png`。

### G04 全部图标按钮tooltip和AutomationProperties.Name
- 条款：§0；状态：**部分**；优先：P2；范围：本批界面。
- 实际：已有按钮有名字；顶栏空Content的pointer/hand/undo等仅tooltip，动态节点画布无细粒度AutomationPeer。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:21-26`；`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:11-26`。

### G05 40导航/34顶栏/264资源/304参数/26状态栏
- 条款：§2.1；状态：**符合**；优先：P1；范围：本批界面。
- 实际：代码对应；宿主避免重复导航轨；1366/1920实际控件复测。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:20-28`；`test-results/design-recheck.trx`。

### G06 窄窗资源/参数恢复入口
- 条款：§2.1；状态：**符合**；优先：P1；范围：本批界面。
- 实际：资源抽屉/40DIP参数轨能恢复，显式开关不被下一次布局抹掉。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:47-69`；`test-results/design-recheck.trx`。

### G07 保存状态在标题栏右侧
- 条款：§2.1；状态：**部分**；优先：P2；范围：本批界面。
- 实际：主保存文案目前在底部；上方保存标题行Height0，不是稿上的黄/红点+重试标题位置。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:18-19`；`screenshots/simple-1366-OFFSCREEN.png`。

### G08 B画笔和反斜杠对比焦点规则
- 条款：§2.8；状态：**符合**；优先：P1；范围：本批界面。
- 实际：沿已确认B画笔/反斜杠切对比；DOCX旧段“按住B”与V/H/C/B/I冲突，不回退。文本/密码/ComboBox焦点不抢快捷键。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:20-44`。

### G09 完整工具I取色/F全屏/F9标注层
- 条款：§0/2.8；状态：**未验证**；优先：P2；范围：本批界面。
- 实际：本次没做完整快捷键真机逐键与F9视觉验收；不能仅按按钮/字符串算完成。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:20-44`。

### G10 无旧品牌泄露
- 条款：§0；状态：**未验证**；优先：P2；范围：本批界面。
- 实际：当前截图使用光镜；未全模块所有对话框逐一核验旧品牌，本轮不作全局通过。
- 证据：`screenshots/simple-1366-OFFSCREEN.png`。

## 简易层栈

### S01 简易线性层栈每行38DIP
- 条款：§9.1/9.10；状态：**部分**；优先：P1；范围：本批界面。
- 实际：普通栈有38DIP真实层行；复杂图路径隐藏ListBox换101.94DIP独立专业组面板，不是层栈中的同款行。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:2`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.R54.cs:31-44`；`OBSERVATIONS.json`。

### S02 普通行显隐/输出/蒙版/名称/混合强度/锁
- 条款：§2.2；状态：**部分**；优先：P2；范围：本批界面。
- 实际：大部分已有；类型图标未单列，未有蒙版时按钮仍固定占位，不是仅悬停虚线+。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:2`；`src/RAWSelectionAssistant/Views/StudioLayerThumbnail.cs:19-22`。

### S03 新建/蒙版/复制/删除
- 条款：§2.2；状态：**符合**；优先：P2；范围：本批界面。
- 实际：已有生产命令入口，回归未改底层路由。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:49`；`test-results/design-recheck.trx`。

### S04 专业组显隐和强度/切专业无损
- 条款：§9.7；状态：**符合**；优先：P1；范围：本批界面。
- 实际：现有整栈保守专业组可显隐/强度并保留图；前后序列化断言通过。
- 证据：`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Graph.cs:66-69`；`test-results/design-recheck.trx`。

### S05 并行部分成组并保留其他串行层顺序
- 条款：§9.7；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：整栈GroupEnabled/GroupAmount，没有精确子图段和普通层同栈展示；保守不压平是正确边界但不是已完成。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R54.cs:35-44`；`src/RAWSelectionAssistant.Core/Services/Projects/StudioNodeGraph.cs:6-7`。

### S06 基础/RGB曲线/HSL简易页
- 条款：§2.2/9.1；状态：**部分**；优先：P1；范围：本批界面。
- 实际：基础和RGB曲线能展示；HSL/ColorRange为空工具区，不可因页签存在记通过。
- 证据：`OBSERVATIONS.json`；`screenshots/tool-ColorRange-1366-OFFSCREEN.png`。

## 节点卡与类型

### N01 调色/透明/填充/混合/外部/即时胶片六类
- 条款：§9.2；状态：**部分**；优先：P2；范围：后续能力不计本批完成。
- 实际：库列六类；只有调色与并行混合可添加；其他明确禁用。符合当前不伪装实现约束，但不等于完整六类型。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:164-167`；`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs:9`。

### N02 类型图标及同色系专属边框
- 条款：§9.2；状态：**部分**；优先：P2；范围：本批界面。
- 实际：调色用三滑杆而非◐；混合双圆；卡外边框统一#6E9B91，混合未用#B5D3CC。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:54-61`。

### N03 每节点自身真实输出缩略
- 条款：§9.2；状态：**符合**；优先：P1；范围：本批界面。
- 实际：有界192px生产节点输出，当前图路由节点都有独立ID输出；非最终成片复制到全部节点。只确认已支持栅格测试，RAW特定路径另有限制。
- 证据：`src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.LayerThumbnails.cs`；`test-results/design-recheck.trx`。

### N04 已有蒙版缩略
- 条款：§9.2；状态：**符合**；优先：P2；范围：本批界面。
- 实际：蒙版源坐标真实生成，图卡仅LocalMask存在时绘制；当前蒙版测试已复跑。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:63`；`test-results/design-recheck.trx`。

### N05 单击蒙版缩略编辑/无蒙版悬停+蒙版
- 条款：§2.3；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：Down只判端口/整卡，没有蒙版Rect点击；只能菜单请求编辑；无蒙版hover+未实现。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:85-98`。

### N06 顶部3DIP色条/可选颜色标签
- 条款：§9.2；状态：**部分**；优先：P2；范围：本批界面。
- 实际：绘制3DIP条已有；没有标签字段或颜色菜单，mix固定金色不是用户色标签。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:56`；`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:149-161`。

### N07 选中白边/旁路45%/锁定
- 条款：§9.2；状态：**符合**；优先：P1；范围：本批界面。
- 实际：白边、.45透明与锁文字已有；锁限制参数位置/边，仍可显隐。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:54-68`；`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Graph.cs:14-16`；`test-results/design-recheck.trx`。

### N08 编号/可重命名/名称/混合强度
- 条款：§9.2；状态：**部分**；优先：P2；范围：本批界面。
- 实际：序号/F2/名称已有；卡没有混合模式或强度读数，需要看右栏。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:61-68`；`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:141-146`。

### N09 新增节点名称反映类型
- 条款：§9.2；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：串行添加Levels/ColorBalance/SkinTone/Details均命名“颜色范围”；已按实际SelectedType/Name和头绑定复测，不是选择错位。
- 证据：`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs:514-524`；`OBSERVATIONS.json`。

### N10 新并行卡不重叠输出
- 条款：§9.3；状态：**符合**；优先：P1；范围：本批界面。
- 实际：14bbb60d修复，当前复跑默认卡无相交断言。
- 证据：`test-results/design-recheck.trx`；`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Graph.cs:39-53`。

## 节点画布

### C01 图在照片与胶片条之间不在右栏
- 条款：§9.3；状态：**符合**；优先：P1；范围：本批界面。
- 实际：Grid.Row2节点、Row3胶片条，否决右栏方案未启用。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:35-36`；`screenshots/professional-1366-OFFSCREEN.png`。

### C02 22DIP标题和独立折叠箭头
- 条款：§9.3；状态：**部分**；优先：P2；范围：本批界面。
- 实际：22DIP高度和独立折叠通过；按钮文字“节点图/胶片条”无▾/▸当前状态图形。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:35-36`；`test-results/design-recheck.trx`。

### C03 标题+库/缩放/适应/高亮
- 条款：§9.3；状态：**部分**；优先：P2；范围：本批界面。
- 实际：库/缩放/适应已有；没有高亮按钮，Shift+H只给ColorRange旧选区开关。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:35`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:26`。

### C04 1366默认22收起/大屏默认展开
- 条款：§9.3；状态：**符合**；优先：P1；范围：本批界面。
- 实际：1366初始22断言通过；按ActualWidth和Nullable历史选择决定，用户选择不反复覆盖。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:53`；`OBSERVATIONS.json`；`test-results/design-recheck.trx`。

### C05 高度/视图恢复/顶部拖高
- 条款：§9.3；状态：**符合**；优先：P1；范围：本批界面。
- 实际：保存height/collapsed/pan/zoom，跨宽度/重挂DataContext保留；DragDelta实际事件，未做物理鼠标。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R54.cs:17-30`；`test-results/design-recheck.trx`。

### C06 平移/滚轮缩放/适应
- 条款：§9.3；状态：**符合**；优先：P2；范围：本批界面。
- 实际：生产graph有对应坐标变换，Fit实际用于截图；原生滚轮指针未验。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:29-38`；`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:100-126`。

### C07 端口拖线/断开/串行插入
- 条款：§9.3；状态：**部分**；优先：P2；范围：本批界面。
- 实际：核心事件与路由API存在、VM回归通过；没有实际鼠标拖线验收，不将API等同原生交互。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:108-122`；`test-results/design-recheck.trx`。

### C08 拖到空白新分支
- 条款：§9.3；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：卡拖空白目前只是Move位置，没有创建新分支/新节点。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:119-122`。

### C09 节点库搜索分组可拖入
- 条款：§9.3；状态：**部分**；优先：P2；范围：本批界面。
- 实际：搜索六类分组点选可用；没有DragDrop/AllowDrop处理，不能拖放到指定空白位置。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:164-167`。

### C10 1366示波22DIP竖条/波形分量矢量
- 条款：§9.1；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：实际是胶片条下面104DIP横向面板，非照片右侧22DIP竖条。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:40`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs`。

### C11 1366照片高度592/354/570
- 条款：§9.9；状态：**部分**；优先：P2；范围：本批界面。
- 实际：同一生产页1366实测：ViewerArea简易594/专业展开378/收起572；扣12DIP×2图像内边距后PhotoViewport分别570/354/548。草图约592/354/570使用边界并不完全一致：展开354吻合，其余差22DIP。不得混用控件边界宣布三态完全符合。
- 证据：`OBSERVATIONS.json`；`DIP_LAYOUT_RESULTS.json`。

### C12 窄窗保留已选状态并可恢复
- 条款：§9.3/2.10；状态：**部分**；优先：P1；范围：本批界面。
- 实际：用户展开状态保留正确；911×512有效DIP下图展开216使照片高98、宽539，需要恢复/折叠引导，但不能反复强制覆盖用户选择。
- 证据：`OBSERVATIONS.json`。

## 右键菜单

### M01 右键添加节点同类型分组
- 条款：§9.4；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：OpenMenu没有“添加节点▸”五类子菜单，和节点库不是同一模型。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:149-161`。

### M02 串行/并行/混合/外部快捷键
- 条款：§9.4；状态：**部分**；优先：P2；范围：本批界面。
- 实际：Alt+S/P/L/O事件存在；P/L都调用同一AddParallelGraphBranch，O仅tooltip未实现，菜单未列Alt提示。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:127-136`。

### M03 局部蒙版子菜单/Shift+Q
- 条款：§9.4；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：仅编辑画笔/反相和合并AI禁用项；径向/渐变子项与Shift+Q没有接线。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:149-161`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:20-34`。

### M04 Ctrl+D/复位/复制粘贴/F2/Del
- 条款：§9.4；状态：**符合**；优先：P2；范围：本批界面。
- 实际：当前功能入口/焦点守卫和VM命令已有；仅本节点同类型粘贴，锁定保护。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:127-161`；`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Graph.cs:88-91`。

### M05 存预设/LUT导出/颜色标签
- 条款：§9.4；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：本节点菜单无这三组；其他模块有相关旧功能不等于节点菜单完成。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:149-161`。

### M06 F2节点与胶片条按焦点区分
- 条款：§9.4；状态：**符合**；优先：P1；范围：本批界面。
- 实际：graph焦点优先ExecuteGraphKey，filmstrip焦点F2从原片新建；文本编辑不触发。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:22-33`；`test-results/design-recheck.trx`。

## 专业参数

### R01 直方图→节点头→六页签→工具参数
- 条款：§9.5；状态：**部分**；优先：P2；范围：本批界面。
- 实际：顺序已改到右栏，图下无工具页签；节点头冗长/固定说明，3D不是胶囊开关。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.Layout.cs:42-76`；`screenshots/professional-1366-OFFSCREEN.png`。

### R02 仅显示选中节点参数不改其他节点
- 条款：§9.5；状态：**符合**；优先：P1；范围：本批界面。
- 实际：BasicTone/WhiteBalance/Levels/Curve/ColorBalance/Details实测对应section；SetToolParameter图模式拒绝跨类型写。
- 证据：`OBSERVATIONS.json`；`src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Tools.cs:26-35`。

### R03 四轮同屏暗/中/高/偏移及下方控制
- 条款：§9.1/草图4；状态：**部分**；优先：P2；范围：本批界面。
- 实际：有四个实际色轮，但垂直堆叠需滚动；1366只显示约两个，不是草图横排紧凑布局，只有色相/强度8参数。
- 证据：`screenshots/tool-ColorBalance-1366-OFFSCREEN.png`；`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolCatalog.cs:31-33`。

### R04 曲线RGB/R/G/B+色相对色相/饱和度
- 条款：§9.1/草图4；状态：**部分**；优先：P2；范围：本批界面。
- 实际：实际RGB/R/G/B曲线可用；后二者没有入口/算法。
- 证据：`src/RAWSelectionAssistant/Views/StudioCurveEditor.cs`；`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolCatalog.cs:37-39`。

### R05 范围/HSL不能空壳
- 条款：§9.1/9.5；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：ColorRange目录参数0；StudioToolPanel只从catalog生成，观察为0 sliders/0 textInputs。范围和HSL均引用这同一空section。
- 证据：`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolCatalog.cs`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:57`；`OBSERVATIONS.json`。

### R06 所有可添加节点可编辑或明确禁用
- 条款：§9.5；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：SkinTone有9个数学参数且库允许添加，hierarchy switch遗漏导致visibleSections=[]；不是未来AI分割，本来就是已有肤色均匀化。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.Layout.cs:86-96`；`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolCatalog.cs:44-47`；`OBSERVATIONS.json`。

### R07 扭曲未实现明确禁用
- 条款：§9.5；状态：**符合**；优先：P2；范围：后续能力不计本批完成。
- 实际：页签禁用，未冒称实现；仍是完整定稿未来缺项。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.Layout.cs:45-73`。

### R08 锁定参数置灰显隐仍可用
- 条款：§9.5；状态：**符合**；优先：P1；范围：本批界面。
- 实际：选中锁定时section/composite禁用，显隐保留，图节点锁边位置；VM回归可用。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.Layout.cs:60-100`；`test-results/design-recheck.trx`。

## 蒙版

### K01 画笔/擦除/羽化流量密度/反相
- 条款：§9.1/9.6；状态：**符合**；优先：P2；范围：本批界面。
- 实际：现有单蒙版编辑实际按钮滑块；非持续喷涂语义明确。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.LocalMask.cs:35-41`。

### K02 径向/渐变/组合加减交集
- 条款：§9.1/9.6；状态：**缺口**；优先：P2；范围：后续能力不计本批完成。
- 实际：只有笔迹序列与erase累积，没有径向渐变和通用多蒙版布尔组合。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.LocalMask.cs:35-41`；`src/RAWSelectionAssistant.Core/Services/Projects/StudioLayerComposite.cs`。

### K03 蒙版精修羽化/去噪/收紧黑白
- 条款：§9.6；状态：**部分**；优先：P2；范围：后续能力不计本批完成。
- 实际：笔刷边缘羽化不等于蒙版整体精修；整体去噪与黑白收紧未有。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.LocalMask.cs:41`。

### K04 色彩范围限定和灰底高亮
- 条款：§9.6；状态：**缺口**；优先：P2；范围：本批界面。
- 实际：旧ColorRange采样选区开关不等于新HSL限定器+灰底mask-only高亮；工具页为空。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:26`；`OBSERVATIONS.json`。

### K05 AI主体/天空/皮肤及共享/外部反向
- 条款：§9.1；状态：**部分**；优先：P2；范围：后续能力不计本批完成。
- 实际：AI/外部已禁用，未做共享蒙版引用；禁用正确，但完整功能未实现。
- 证据：`src/RAWSelectionAssistant/Views/StudioNodeGraphView.cs:158-166`。

## 剧照和对比

### T01 剧照左栏独立标签/Ctrl+Alt+G
- 条款：§9.8；状态：**符合**；优先：P2；范围：本批界面。
- 实际：独立标签和捕获快捷键；最多8张随项目保存。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.xaml:27`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:24`；`src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Stills.cs:7-15`。

### T02 剧照两列卡片与操作
- 条款：§9.8；状态：**部分**；优先：P2；范围：本批界面。
- 实际：实际单列112px高逐张列表；当前照片应用入口有，未做到多选目标批量。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.Stills.cs:10-16`。

### T03 W可拖划像并共用主对比视图
- 条款：§9.8；状态：**部分**；优先：P1；范围：本批界面。
- 实际：可拖划像已做，但每次新建900×650独立Window，非主照片共用视图；W重复可开多个。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.Stills.cs:19-24`。

### T04 2–4变体与剧照共用同视图
- 条款：§9.8/2.5；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：变体对比另一个1100×780窗口，有同步缩放平移；与剧照不是同一个窗口/控制状态。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.MultiCompare.cs:9-18`；`src/RAWSelectionAssistant/Views/PhotoGradePageView.Stills.cs:24`。

## 即时胶片和继承模块

### F01 显影冻结配方和系统锁定节点
- 条款：§2.6/9.7；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：显影配方冻结存在；本轮显影后专业图只输入→输出，developedNodes=[]，没有系统锁定即时胶片节点。两者不能混算。
- 证据：`OBSERVATIONS.json`；`screenshots/developed-professional-1366-OFFSCREEN.png`；`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs:9`。

### F02 左唯一相纸/右参数和底部显影
- 条款：§2.6；状态：**符合**；优先：P1；范围：本批界面。
- 实际：右相纸隐藏，左相纸唯一；1366右栏全高滚动且footer贴底复测通过。
- 证据：`test-results/design-recheck.trx`；`screenshots/film-draft-1366-OFFSCREEN.png`。

### F03 完整相纸组和3列
- 条款：§2.6；状态：**部分**；优先：P1；范围：本批界面。
- 实际：当前仅5个不同响应而非3彩/3黑白/10实验；卡为2列。没有复制卡伪造响应；仍不符合完整稿。
- 证据：`screenshots/film-draft-1366-OFFSCREEN.png`。

### F04 相机实拍图/详情/动画
- 条款：§2.6；状态：**部分**；优先：P1；范围：本批界面。
- 实际：当前相机仅i-2/SX-70文字，授权素材没接；不能按型号按钮存在计整体移植完成。
- 证据：`screenshots/film-draft-1366-OFFSCREEN.png`。

### F05 原创真实框/当前照片缩略
- 条款：§2.6；状态：**符合**；优先：P1；范围：本批界面。
- 实际：12已授权原创框和当前照片实际缩略存在，同框hash/窗口合同保持；不包含未授权联名资源。
- 证据：`screenshots/film-draft-1366-OFFSCREEN.png`；`test-results/design-recheck.trx`。

### F06 已显影左相纸禁用0.38
- 条款：§2.6；状态：**部分**；优先：P2；范围：本批界面。
- 实际：禁用且保持深色已复跑，图中是本地低透明呈现；本次未单独量0.38及cursor值，不把disabled当精确视觉通过。
- 证据：`screenshots/developed-1366-OFFSCREEN.png`；`test-results/design-recheck.trx`。

### F07 176项整体移植/完整瑕疵与动画
- 条款：§2.6；状态：**未验证**；优先：P1；范围：本批界面。
- 实际：读取原有HTML和instant-port-check，未在本次逐项执行176项，不复述原型175通过为产品通过。
- 证据：`R53_HTML_READBACK.json`。

### F08 3D胶囊开关/默认不遮图/完整控制
- 条款：§2.4；状态：**部分**；优先：P2；范围：本批界面。
- 实际：已有可拖缩放非模态窗口和返回；不是胶囊高亮状态，CenterOwner可能遮照片，当前仅点云/重建/返回，不等于整套R3控制。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.R53.cs:135-164`。

### F09 变体Ctrl多选主画面2–4/胶片条入口
- 条款：§2.5；状态：**部分**；优先：P2；范围：本批界面。
- 实际：已有独立同步对比窗；从菜单另外选变体，不是稿上的Ctrl+点胶片条和主画面网格。
- 证据：`src/RAWSelectionAssistant/Views/PhotoGradePageView.MultiCompare.cs:9-18`。

### F10 联名需授权不随产品
- 条款：§2.6；状态：**符合**；优先：P1；范围：本批界面。
- 实际：未擅自引入无授权相机/联名资源；授权边界优先，不因设计写“移植”就许可复制。
- 证据：`R53_HTML_READBACK.json`；`../home-r54-darkroom-20261028/FINAL_REPORT.md`。

## 范围和验收边界

### X01 自由画布布局/全部交互
- 条款：§3；状态：**未验证**；优先：P2；范围：独立模块本轮不重做。
- 实际：本次仅源码定位和已存回归边界，未重跑全画布与原型逐状态，不因R5.4没改就写符合。
- 证据：`R53_HTML_READBACK.json`。

### X02 策划中心五栏/拼贴预览与关联
- 条款：§4；状态：**未验证**；优先：P2；范围：独立模块本轮不重做。
- 实际：HTML已读取；本次没有策划15逐屏/关联保存重开验收，全模块保留不等于设计全部实现。
- 证据：`R53_HTML_READBACK.json`。

### X03 跨模块引用/更新/保存撤销
- 条款：§5；状态：**未验证**；优先：P1；范围：本批界面。
- 实际：没有本轮完整暗室→拼贴→策划真实鼠标闭环；保留历史证据但不作这次新通过。
- 证据：`../home-r54-darkroom-20261028/FINAL_REPORT.md`。

### X04 125/150物理DPI/ACM/HDR
- 条款：§2.10；状态：**未验证**；优先：P1；范围：本批界面。
- 实际：只做有效DIP离屏布局，未ComputerUse/桌面操作/物理显示验收。
- 证据：`DIP_LAYOUT_RESULTS.json`。

### X05 新宝丽来候选接入前的四状态界面门槛
- 条款：§接续边界；状态：**缺口**；优先：P1；范围：本批界面。
- 实际：原包已补齐，不能再说缺资料；现在阻点是已证实界面差距，先收尾再按显式版本接候选，不重做未来AI。
- 证据：`OBSERVATIONS.json`。

