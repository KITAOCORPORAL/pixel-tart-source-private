# 2026-10-04 晚间 R01–R12 修复与复验记录

## 仓库与证据边界

- START_HEAD：`4d875833f6a588537bb97d0c597120f571213c28`；分支 `integration/pixel-tart-developer-preview`；起点 CLEAN，已 fetch。
- 审计源码 `756541d` 到起点只有只读审计文档。审计报告不是修复证据。
- 当前 Production SourceHead：`63cd21e6cd13e4d2982be1b45676fefa68226c6f`（r4）；r3 为下文诊断历史。
- EXE：`artifacts/releases/evening-feedback-2026-10-04-r4/publish/win-x64/KitaoPhotoSelector.exe`。
- r4 DLL ProductVersion：`2.3.0`，生产项目默认不在版本文本附加 SHA。SourceHead 由构建参数、日志和完整发布文件 SHA256 清单关联；不能只靠 EXE apphost 哈希认定源码版本。
- r4 manifest：`artifacts/releases/evening-feedback-2026-10-04-r4/release-manifest.json`；EXE SHA256：`6476D024DA74E2754A88C39304214BD8E8F428BEFD841247B06AC8E31E2C373F`。
- 图1–7、11–12为用户反馈；图8/9/10为参考界面。用户反馈 EXE HEAD 仍 UNKNOWN，没有构建 sidecar，不归因于旧版。
- `VisualApproved=false`；`UserVerified=false`；`USER_ACCEPTANCE=NOT_APPROVED`。
- 本文 RUNTIME 仅接受上述同一 Production Release 的实际操作；UI Review/自动测试图另列，不混作 After。

## 逐项修复（RUNTIME 以当前 r4 为准；r3 不折算为 r4）

| 项 / 图 | 旧版重放步骤 | 已确认源码根因 / 修改 | CODE | TEST | RELEASE_RUNTIME |
|---|---|---|---|---|---|
| R01 / 1 | 智能文件夹→嵌套规则→降低窗口高度→寻找关闭/保存 | 宿主随 Gallery；操作混在滚动内容。改有最大尺寸的宿主、固定关闭/草稿保护头与保存/取消底，中间滚动 | IMPLEMENTED | PASS；新280/360/500 DIP可达性与草稿保护行为测试 | PARTIAL |
| R02 / 2 | 扩展名→条件树上滚轮→空白上滚轮→键盘至末项 | 旧 Release 中树上滚轮无位移，空白可部分滚动（48 DIP）；树消费 wheel。外层统一 PreviewMouseWheel，禁树内滚动；PageUp/Down、CtrlHome/End | IMPLEMENTED | PASS；实际树事件至ScrollViewer末端 | PARTIAL |
| R03 / 3 | 加载主题→画廊正向/反向拖框 | ApplyAccent把Subtle画刷变成不透明；框选局部使用alpha18填充，原选择算法未替换 | IMPLEMENTED | PASS；真实ApplyAccent后alpha<40、原多选测试 | PARTIAL |
| R04 / 4 | 三图放大平移→Fit→旋转→resize→再Fit | 按钮原已接线。旧边界未算旋转，未保留Fit模式，固定140留白/2倍上限。新旋转外接框+有限视口+resize维持Fit；单一Zoom | IMPLEMENTED；用户原次无反应原因未完整实测归因 | PASS；旋转/resize/幂等/空态边界；真实操作另记录 | PARTIAL |
| R05 / 5 | 对比三个组织标题并点智能文件夹 | 普通TextBlock与SectionTitle不一致。统一文字样式与行距，保留集合菜单；移除误导的永久展开箭头 | IMPLEMENTED | PASS；原组织Query回归；视觉仍待验 | PARTIAL |
| R06 / 6 | 文件夹搜索输入长中文/英文→焦点切换 | 固定30高减14 padding与4边框，只剩12 DIP。局部MinHeight36、Padding8,3、文字垂直居中 | IMPLEMENTED | PASS；主题后真实TextBox内容高度测量+原筛树回归 | PARTIAL |
| R07 / 7 | 进入Studio→展开两栏→窄窗/专注/折叠 | 旧需求方向与新图相反。移动实际EditingRail到右、ContextRail到左，更新响应式；同一Editor | IMPLEMENTED | PASS；控件几何及数据绑定、窄窗测试；废止旧方向断言 | PARTIAL |
| R08 / 7/8/12 | 中央顶部展开分析→RGB/R/G/B/Y→原图/结果/切图 | 计算已有，位置/通道/网格缺失。复用PreviewHistogram与HistogramDrawing；中央可收起分析组+线性Y区间标签 | IMPLEMENTED | PASS；真实分析缓存/切图/空态；布局测量 | PARTIAL |
| R09 / 9参考 | 悬停0–X色块→主图对应区域→离开/Esc/切图 | ToneZoneDrawing以前只画比例。新增hover事件→相同量化线性Y成员→既有预览Overlay | IMPLEMENTED | PASS；全256灰阶、端点、成员与统计相等、Esc/切图、编辑栈不变 | PARTIAL |
| R10 / 10参考 | 右侧父组展开→调子参数→复位→保存/重载/同步/导出 | 缺真实独立影调节点。新增Develop线性RGB处理；局部复用ColorRange；高位深选区修复/Film共用float | IMPLEMENTED；无新画笔蒙版 | PASS；实际像素效果、identity、持久化、同步、TIFF16预览/导出一致 | PARTIAL |
| R11 / 11 | 顶部快速导出→选目录→检查格式/数量/路径/逐张结果 | 非RAW以前固定jpg。复用选中集/冻结目标/取消/失败报告；按源JPEG/PNG/TIFF编码，RAW→TIFF16；统一ICC→sRGB | IMPLEMENTED | PASS；PNG alpha/TIFF16精度及真实导出；JPEG8与批量回归 | PARTIAL |
| R12 / 12 | 展开3D→Orbit/Pan/Zoom/Reset/Fit→双向取色/Esc | 原OKLab坐标云没有球形结构。增加归一化球形线框，点坐标不投壳；Fit包含球/真实点边界 | IMPLEMENTED | PASS；原相机/映射测试及球形resize等向性、样本模型不改 | PARTIAL |

R04 诊断仅在显式设置 `PIXEL_TART_FIT_DIAGNOSTICS` 时写实际操作的 Zoom/Pan、ActualWidth/Height、旋转 Bounds、可见像素边界和 DPI；不自动点击、无需周期观察器。

完整参数/数学/边界映射：`2026-10-04_DEVELOP_PARAMETER_MAP.md`。

## 自动验证

### 当前 r4 最终结果

| Gate | 通过 / 失败 / 跳过 | 证据 |
|---|---|---|
| Core full（重新编译） | 1541 / 0 / 4 | `core-full-r4-built.trx` |
| WPF full serial | 1449 / 0 / 11 | `wpf-full-r4-final.trx`；正常退出，8分23秒，无 testhost crash |
| DPI | 91 / 0 / 0 | `dpi-r4.trx` |
| Esc 定向 | 2 / 0 / 0 | `escape-r4-final.trx` |
| Clean Release x64 | 成功 | `artifacts/evening-r4-clean.log`、`artifacts/evening-r4-publish.log` |

TRX 均在 `artifacts/evening-feedback/tests/`；哈希、时间和类名归组见 `2026-10-04_EVENING_TEST_INDEX.json`。
Asset 385/0/2；Color/Studio 98/0/1；Reference/Match 22/0/3；ColorSpace3D 3/0/0；Canvas 23/0/0；Guardian 10/0/0；本轮 EveningFeedback WPF 行为测试 10/0/0。归组可能重叠，不能当作独立总数或完整实机覆盖。

r4 第一次 WPF 全量为1448/1/11：失败是旧的 Escape 源码字符串断言仍要求页面内直接关闭 Loupe；`1485e6d` 改为共享入口约定，并在真实 MainWindow 路由测试中验证 Loupe 被关闭。行为没有改回旧代码。旧失败 TRX 保留。`core-full-r4.trx` 是 stale `--no-build` 产物（1537/0/4），排除出最终结果；重新编译的 `core-full-r4-built.trx` 才是当前 Core 结果。

当前自动视觉 manifest：`artifacts/r12x/current-run-visual-evidence.json`，SourceHead=`63cd21e6cd13e4d2982be1b45676fefa68226c6f`，106 captures、32 DPI states（100/125/150/200）。这是 UiReview 的逻辑缩放，未替代 Production After 或 Windows 物理 DPI 操作。没有依赖 rc12 旧产物。

### 历史候选与失败记录

- Core full r3：1541 passed / 0 failed / 4 skipped；`artifacts/evening-feedback/tests/core-full-r3.trx`，处理核心未再修改。
- WPF首次full：1442 passed / 4 failed / 11 skipped；正常退出，无testhost crash。失败为两个无障碍/旧断言问题和新增主题测试STA隔离问题；详细TRX保留。
- 修复后针对性：15 passed / 0 failed；`repair-focused.trx`。
- WPF第二次full：1445 passed / 1 failed / 11 skipped；既有 VisualPhase2 测试重复创建 Application，改为套件共享 STA/Application。
- WPF第三次full：1443 passed / 3 failed / 11 skipped；正常退出。两个独立 evidence 验证器从 Git blob 解码后保留 UTF-8 BOM，使 PowerShell XML 转换失败；仅解析文本去除 BOM，原始字节哈希不变。Quick Loupe 测试的隐藏 HwndSource 无法稳定维持 StaysOpen=false 鼠标捕获，改用活动 Window + finally 关闭；原行为断言保留。
- 三个针对性 gate-repair：3 passed / 0 failed；实机布局修复后的 runtime-layout-focused：5 passed / 0 failed。
- WPF r2 full：1447 passed / 0 failed / 11 skipped，正常退出；`wpf-full-r2.trx`。
- Release x64：clean+publish成功；不是Installer/UiReview/Harness。
- 自动视觉首次生成：`artifacts/evening-feedback/current-run-visual`在OrganizeNoOverlap报长输出路径文字裁切；失败未删除。
- `artifacts/r12v` 短路径生成成功：106 captures / 32 DPI captures，SourceHead=e148caf。完整生成的 manifest 原样复制到 current-run provider，引用真实绝对路径；原 provider 已备份，未改图/哈希/SourceHead。DPI 91 passed / 0 failed。
- r3 自动视觉已刷新到6a52e83：106 captures / 32 DPI captures；DPI91/0/0。r4结果见上方当前清单。
- 自动视觉不能代表Windows物理DPI操作或用户认可。

## 当前状态

NOT_READY_FOR_USER_RETEST：r4已完成31张同一EXE的实际操作截图，但完整实机矩阵仍为PARTIAL；逐项缺口见文末。自动测试当前失败数为0。r1/r2/r3仅保留诊断历史，未拼入r4。

## 首个候选实机发现（不是 r2 的 After）

同一 e148caf EXE 在隔离素材库实际启动，无启动异常。Windows 缩放未更改。

- 1180×720 窗口：新建智能文件夹→两层嵌套组→树上滚轮，底部可达，关闭/保存/取消固定；关闭触发未保存保护，点击保存后定义出现在侧栏。`artifacts/evening-feedback/after/R01-nested-scroll.png`、`R01-draft-guard.png`。
- 扩展名：进入通用编辑器→树上滚轮到底→空白滚轮返回顶部→Ctrl+End 到末端；可看到历史/保存区域。`R02-tree-scroll-bottom.png`。窄规则行仍需横向滚动；没有把滚轮改善解释为完整 DPI 通过。
- Color Studio：加载合法合成 PNG 后，1180×720 中间照片被顶部分析/底部胶片条挤到不可见；收起的左栏仍占固定宽度。`R07-R08-candidate-failure.png` 明确为 FAIL。
- b5bd051 修复：分析组在低矮工作区默认折叠，展开内容有限高并滚动；可折叠胶片条，批次动作/视图操作单行横向滚动；空状态文本不占高度；左栏收起归还宽度。新增真实载图后的几何行为测试，不能只检查空态 XAML。
- 所有上述 e148caf 图仅保留诊断历史。当前 R01–R12 RUNTIME 只采用 r4 63cd21e 同一 EXE，r1/r2/r3 均不计入当前 After。


## r2 实机暴露的后台预览失败

r2 在当前桌面实际载入合成横图，1180×720 主图可见；最大化后左侧参考/3D、中央上方分析、右侧编辑均出现。影调 IV 悬停产生对应灰阶条和彩色区域高亮，Esc 清除且留在 Studio。操作曝光从 0 到 1，出现“处理失败，请重试”，没有有效新预览。以下图片均为 r2 历史诊断，不是 r3 After：

- `artifacts/evening-feedback/after-r2/R07-R08-layout-analysis.png`
- `artifacts/evening-feedback/after-r2/R09-zone-IV-highlight.png`
- `artifacts/evening-feedback/after-r2/R09-Esc-clear.png`
- `artifacts/evening-feedback/after-r2/R10-exposure-runtime-failure.png`

新增真实加载 PNG、绑定 Slider、等待 debounce 的测试准确复现了异常：冻结的 BitmapFrame 仍保留原解码线程的 BitmapDecoder；后台 FormatConvertedBitmap.Freeze 访问它时抛 InvalidOperationException。直接调用同一处理器的旧测试没有覆盖这个线程跳转。

`6a52e83` 将 StudioQuickExport.Load 的最终结果复制成独立 BitmapSource 像素缓冲，保留像素格式、调色板、alpha 和精度。内部 LastRenderFailure 保存当前异常供行为测试读取，不将异常技术细节加入产品页面。

同一个载图窗口测试还发现：3D 视口尺寸临时为零时，Project 返回空点集，DrawAxes 索引越界导致新回归测试 testhost 中止。增加零视口绘制防护；不是跳过断言。失败日志 `live-develop-repro.log`、`live-develop-debounce.log` 保留。修复后 40 个相关测试通过（0 fail），含真实滑杆链、格式编码、批量冻结参数和点云边界。

r3 clean Release x64 已生成，manifest 逐文件 SHA256 已写入。r3 全量 WPF 已正常结束：1448 passed / 0 failed / 11 skipped。r3 首次桌面操作受到多显示器坐标不一致阻挡：点击前目标检查指向非目标窗口，停止输入；用户已手动将窗口还原并移到主显示器，已重新识别并继续同一r3实机验证。

## 修改文件与类型（源码提交）

| 类型 | 文件 |
|---|---|
| 交互/渲染/处理适配 | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs` |
| WPF布局与绑定 | `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml` |
| WPF布局与绑定 | `src/PixelTart.Modules.AssetLibrary/AssetQueryComposerView.xaml` |
| WPF布局与绑定 | `src/PixelTart.Modules.AssetLibrary/AssetSmartFolderEditorView.xaml` |
| 交互/渲染/处理适配 | `src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasSurface.cs` |
| 交互/渲染/处理适配 | `src/PixelTart.Modules.AssetLibrary/HistogramDrawing.cs` |
| 交互/渲染/处理适配 | `src/PixelTart.Modules.AssetLibrary/NestedScrollBehavior.cs` |
| 交互/渲染/处理适配 | `src/PixelTart.Modules.AssetLibrary/ToneZoneDrawing.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorSpaceRendererContract.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioDevelop.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioRenderPipeline.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/PixelTartFilm.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/RawMatchTiff16ProductPipeline.cs` |
| 处理数学/数据契约 | `src/RAWSelectionAssistant.Core/Services/Projects/ReferenceColorCoreV2.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/MainWindow.xaml.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Services/ColorStudioAcceptanceFixture.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Services/ReferenceLookPreviewService.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Services/StudioQuickExport.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.Analysis.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.Develop.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Views/ColorSpace3DViewport.cs` |
| WPF布局与绑定 | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml` |
| 交互/渲染/处理适配 | `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.Tests/EveningFeedbackCoreTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.Tests/RawMatchTiff16ProductPipelineTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/AssetLibraryEagleLayoutReconstructionTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/AssetLibrarySmartFolderEditorWpfTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/AssetLibraryVisualPhase2Tests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/BatchExportProcessedPixelsTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/ColorSpace3DViewportTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/EmbeddedAssetLibraryWpfTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/EveningFeedbackWpfTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/ReferenceWorkspaceWideRatioTests.cs` |
| 行为/回归测试 | `tests/RAWSelectionAssistant.WpfTests/RuntimeCorrectionWpfTests.cs` |
| 证据验证工具 | `tools/AssetLibraryP1AutomatedAcceptance/Test-P1AssetLibraryAutomatedEvidence.ps1` |


## 本轮提交（源码/测试）

- `e148caf97b897c53e0790e9010ed170ac7a0b705`：R01–R12 UI、处理与回归测试。
- `d95a2f36de59bfe18c73a45f205b0d68a1c53a63`：完整套件复用 WPF Application 生命周期。
- `f88cbc186282a6d1b91ef900c3d57443cceab9da`：证据验证器只在 XML 解析文本时处理 BOM，哈希仍取原始字节。
- `b5bd0519cb510d09f5ddde911380938397c143d1`：真实低矮窗口照片被挤空的修复及载图几何测试。
- `6a52e83ab1d424bf93f2e38a7727ae5366278b3f`：真实滑杆后台解码对象跨线程失败及零尺寸点云绘制修复。
- `63cd21e6cd13e4d2982be1b45676fefa68226c6f`：素材库局部弹层先消费Esc，再由Shell处理路由返回；r4生产构建起点。
- `1485e6d1c835d676fadcab425a7fc33d46512825`：共享Esc入口的回归测试；仅测试修改，不改变r4生产源码。

## 同一候选证据规则

最终运行记录只收 r4，目录 `artifacts/evening-feedback/after-r4/`。r1/r2/r3 均保留为诊断历史，不能拼入 r4 After。
每个候选发布清单均逐文件关联构建。r4的287文件重新计算缺失/不匹配=0；应用DLL SHA256=`6B4F4CFFA8EE6C5E97C498EFC6C1E5B7717C0381B3DAF6C5A8A8CD90B0F9FA49`。不能只凭apphost EXE哈希比较构建。
本轮未修改系统缩放。真实 Free Canvas Fit 诊断在当前显示器测得 DPI=1.5；200% 物理环境尚未操作。截图由桌面工具缩放输出，CaptureWidth/Height 不能直接当作屏幕物理像素；实际 DIP 视口另见 Fit JSONL。logical simulation、WPF测量和 Production截图分别报告。

## r3 Production 实机记录（历史候选，不是 r4 After）

所有下列 PNG 和同名 JSON 位于 `artifacts/evening-feedback/after-r3/`。原图保留在本机，未经改图；包含素材库已有照片的截图不上传 Git。SourceHead、EXE 哈希与 ProductVersion 由 JSON 绑定。

| 项 | 新版实际操作与结果 | After 基名 | 尚未覆盖 |
|---|---|---|---|
| R01 | 打开已保存两层规则；1173×722工具截图下树上滚动到底，关闭/保存/取消固定；右上关闭有效 | R01-reopen-nested-editor；R01-short-window-nested-scroll | 200%（未保存保护见补充记录） |
| R02 | 条件树滚轮到底、空白滚轮回顶部、Ctrl+End到保存/历史；窄行可横滚 | R02-tree-wheel-bottom；R02-keyboard-bottom | Esc出现返回Studio，不能声称弹层Esc关闭通过；完整DPI |
| R03 | 从画廊空白反向拖到前两张合成图，右侧准确显示选中2项；从Gallery外起拖无选择属命中范围 | R03-reverse-selection | 工具drag只返回释放后的截图，未留拖动中alpha证据；Ctrl/Shift |
| R04 | 三个合成图对象（两源图，第三为复制）；433%并平移→点击Fit 275%；旋转后Fit 245%；重复不变；缩小后100%且四角都可见 | R04-before-fit-433；R04-after-fit-275；R04-rotated-fit；R04-resize-fit | 空画布见补充记录；旧用户截图没有诊断，不宣称证明原次无反应全部原因 |
| R05/06 | 三标题相同层级；本地文件夹搜索输入“旅途 English 长中文名称”，聚焦文字完整 | R05-R06-sidebar-search-focused | 200%、非聚焦矩阵 |
| R07/08 | 左参考/3D、右实际调色、中央分析与图像；曝光+1后图像和统计更新，未见处理失败 | R07-R08-R10-before-exposure；R10-exposure-preview-success | 全DPI/窗口组合（参考图、通道、切图、收起见补充记录） |
| R09 | 处理结果IV色块映射到相应灰带，Esc清除，曝光未变 | R09-result-zone-IV；R09-Escape-cleared | 主图空格/中键平移（其他补充见下） |
| R10 | 调色父组展开，真实滑杆曝光0→1，照片变亮，完成后普通状态恢复 | R10-exposure-preview-success | 其余明度/端点/细节逐参数实机（复位/重启/同步见下） |
| R11 | 顶栏选目录显示1张PNG；实际生成 tone-landscape_仿色.png，1/1成功并显示路径 | R11-quick-export-format-count；R11-PNG-export-completed | 多图失败/取消、非sRGB真实样本（JPEG/TIFF成功见下） |
| R12 | 展开球形，Orbit旋转，点云青色点→照片对应区域；吸管黄色→两视图对应点；Esc清除；Pan/滚轮/Fit/Reset可见变化 | R12-expanded-sphere；R12-cloud-to-image-rotated；R12-image-to-cloud；R12-camera-reset-after-pan-zoom-fit | 完整场景组合（Mini/密度/透明度见下）；Reset默认比例比Fit大，不将其混为同一命令 |

### Fit 数值读回

真实点击产生 `artifacts/evening-feedback/fit-runtime-r3.jsonl`，无自动调用 Fit：

- 视口2497.333×1319.333 DIP，Before Zoom=4.3335888744 / Pan=(235.3809,-5.37665)，Fit Zoom=2.7540740741 / Pan=(271.68,40)，Bounds=709.484669×450，屏幕边界=(271.68,40)–(2225.653333,1279.333333)。
- 旋转后 Bounds=734.308971×506.362704；Fit Zoom=2.4475209616，重复 Fit 完全相同。
- 缩小到实际画布1116×588 DIP，Fit Zoom=1.0032334442，边界=(189.658341,40)–(926.341659,548)。
- 上述记录 Dpi=1.5。未更改 Windows 缩放。

### 最终自动结果补充

- Core r3 full：1541/0/4；WPF r3 full serial：1448/0/11（通过/失败/跳过），正常退出。
- WPF类名归组（可重叠）：Asset 385通过/2跳过；Reference或Match 22/3；Color或Studio 98/1；ColorSpace3D 3/0；Canvas 23/0；Guardian 10/0。这不是各项完整实机覆盖计数。
- Guardian静态扫描仍有P1=5、P2=37、EXEMPT=2；历史报告4/34/3。新增报告条目包括此前拆出的AssetColorFilterPicker，不能把测试绿解释为静态审查清零。原始报告保留待审。

## r3 补充重放与 r4 修复原因

- R01：关闭未保存嵌套规则触发保存/放弃/取消保护；取消保留草稿，保存后再次关闭成功。`R01-draft-close-guard`。
- R04：空画布 Fit 保持100%，重复 Fit 的 Zoom/Pan相同。`R04-empty-fit`。
- R07：参考图100%后真实拖动平移，Fit回到23%；收起左栏归还中央宽度。`R07-reference-100-pan`、`R07-context-rail-collapsed`。
- R08：RGB、R/G/B、亮度切换；TIFF16切图、原图/结果统计更新；滚动可达X，收起归还高度。`R08-TIFF16-R`、`R08-TIFF16-luma`、`R08-original-analysis`、`R08-analysis-scroll-to-X`、`R08-analysis-collapsed`。
- R09：原图和结果的IV区域、100%图像下对应区域，离开和Esc清除。主图空格+拖动及中键拖动未测：桌面API没有按住修饰键拖动或中键拖动接口。`R09-original-TIFF-zone-IV`、`R09-leave-clears`、`R09-zone-IV-at-100`。
- R10：曝光0.5、对比54.74、结构56.37、组复位及同步到三张选中；保存方案，正常关闭并重启同一r3后应用方案，对比/结构数值恢复。不是声称临时Target会话自动恢复。`R10-TIFF16-exposure-preview`、`R10-contrast-preview`、`R10-structure-settled`、`R10-group-reset`、`R10-sync-selected-JPEG`、`R10-scheme-saved`、`R10-restart-parameters-restored`。
- R11：同一批次3/3成功；JPEG1200×800/Bgr24、PNG1200×800/Bgra32、TIFF256×192/Rgba64，均1个颜色上下文。全部为明确标注的合法合成输入；不冒充真实RAW。`runtime-export-formats-r3/verification.json`保存大小、像素格式与SHA。真实非sRGB/透明PNG/RAW运行语料未覆盖。
- R12：Mini Orbit/Pan/Zoom/Fit/Reset；密度Standard→Dense、点大小2.6→5.0、不透明度70→33%，点云确有变化。`R12-mini-orbit-pan-zoom`、`R12-mini-reset`、`R12-density-point-opacity`。

### 已确认的 r3 Esc 失败

R02扩展名面板内获得焦点，按Esc仍离开素材库返回Studio；再次进入素材库面板仍打开。`R02-before-Escape`与`R02-Escape-FAIL`。这是实际FAIL，不能用已有Page的Esc代码覆盖。

根因：MainWindow.Window_PreviewKeyDown在WPF隧道路由先于AssetLibraryPage消费Esc；TryCloseActiveInputPopup仅覆盖ComboBox/DatePicker，遗漏素材库自身的筛选/智能文件夹。

`63cd21e6cd13e4d2982be1b45676fefa68226c6f`：AssetLibraryPage.TryCloseTransientSurface复用原关闭/草稿逻辑，MainWindow在路由返回前调用可见页面；页面自身仍复用同一入口。新增真实MainWindow承载AssetLibraryPage的按键路由测试：关闭筛选、智能文件夹未保存保护及再次Esc取消关闭，均不丢草稿。focused 1/0。

r4 clean Release发布成功。r3全部图片仍在原目录，r4不复制这些图片冒充重放。本轮所有用户批准状态保持false/NOT_APPROVED。

## r4 Production 实机记录（当前唯一 After）

当前已保存31张原始截图及同名来源JSON，目录 `artifacts/evening-feedback/after-r4/`。索引 `2026-10-04_EVENING_AFTER_INDEX.json` 记录逐图SHA256、来源、尺寸和操作；旧r3索引另存 `2026-10-04_EVENING_AFTER_R3_HISTORY.json`，明确为历史。

隔离运行库用于样本和草稿操作；未修改用户素材库。合成样本为PNG1200×800、JPEG1200×800、TIFF16 256×192，未将其称为真实RAW或真实摄影语料。Windows缩放未更改；Fit直接测得150%。工具截图1182×722或2560×1392等尺寸仅为工具输出尺寸，不冒充窗口物理尺寸。

下表是实际观察范围，不是用户验收通过。CODE=IMPLEMENTED；TEST=PASS（本轮列明的自动行为与回归范围）；RELEASE_RUNTIME=PARTIAL（仍有未覆盖条件）。

| 项 | r4 新版操作结果 | 当前 After 基名 | 剩余实机缺口 |
|---|---|---|---|
| R01 | 嵌套规则滚轮到底；顶部关闭与底部保存/取消固定。Esc进入未保存保护，再Esc取消关闭保留草稿；保存后关闭 | R01-nested-scroll-footer；R01-Escape-draft-guard；R01-Escape-preserves-draft | 同一r4低矮编辑器和200%组合尚未重放 |
| R02 | 条件树滚轮到底可见底部内容；Esc只关闭筛选，素材库路由保留 | R02-wheel-bottom；R02-Escape-closes-filter | r4空白区域滚轮、键盘至末项、拖动滚动条尚未逐项留证；旧r3不补算 |
| R03 | 反向拖框释放后准确选中两张合成图 | R03-reverse-select-two | drag接口只返回释放后画面，缺少拖动中alpha截图；Ctrl/Shift组合未在r4重放 |
| R04 | 三对象先433%并平移，再点击Fit得到275%，全部对象落在可用画布内；记录真实Zoom/Pan/Bounds/DPI | R04-before-Fit-433-pan；R04-after-Fit-275 | r4旋转对象、空态、重复Fit和resize重放未完成；用户旧图缺少诊断，不能完整归因原次无反应 |
| R05 | 三个组织标题同层级；智能文件夹入口可打开实际编辑器 | R05-R06-folder-search-focused；R01-nested-scroll-footer | 完整窗口/DPI组合未覆盖 |
| R06 | 文件夹局部搜索输入“旅途 English 长中文名称”，聚焦与失焦均完整可见 | R05-R06-folder-search-focused；R06-search-unfocused | 200%和特殊字符组合尚未实测 |
| R07 | 实际参考/3D在左，编辑在右，主图中央。收起左栏归还宽度；小窗口收起胶片条后Fit77%→134%，照片保持可见 | R07-R08-layout-original；R07-context-collapsed；R07-short-window-three-rails；R07-short-window-filmstrip-collapsed | 当前r4参考原图Fit/100%/Pan未重放；完整窗口/DPI矩阵未完成 |
| R08 | PNG结果与TIFF原图切换后统计、主图、点云同步；逐个切RGB/R/G/B/亮度；滚动可达X；收起分析主图Fit343%→452% | R08-TIFF-original-RGB；R08-channel-R/G/B/luma；R08-R09-bottom-X-TIFF-mapping；R08-analysis-collapsed | 全DPI/不同图像的运行性能矩阵未完成；无目标图空态不视为故障 |
| R09 | PNG处理结果IV区域和TIFF原图VII区域有对应临时高亮；离开/收起/Esc清除；切图刷新 | R09-zone-IV-preview；R09-Esc-cleared；R08-R09-bottom-X-TIFF-mapping | 缩放/平移组合、所有端点的实机留证尚缺；边界自动测试通过不替代实机 |
| R10 | 右侧明度/对比与端点/结构与细节父组及真实参数；曝光0→0.71完成预览，无后台异常，统计和像素变化 | R10-exposure-preview | 其余逐参数效果、局部选区、组复位、同步、方案保存/重启的同一r4重放未完成；r3历史成功不补算 |
| R11 | 目录选择器显示3张和PNG/JPG/TIF；实际3/3成功，显示路径；只PNG带曝光，其他目标没有套用活动图参数 | R11-export-all-format-count；R11-three-formats-completed | r4顶部所选入口、取消/逐张失败、透明PNG/非sRGB真实输入运行矩阵未完成；RAW真实语料未跑 |
| R12 | 展开球形旋转后点云→图像；图像黄色取样→两点云高亮；Esc清除；Pan/Zoom/Fit/Reset实际可操作，切到TIFF点云更新 | R12-cloud-to-image-rotated；R12-image-to-cloud；R12-Esc-cleared；R12-reset-after-pan-zoom-fit | Mini相机全套、密度/大小/透明度、不同原图/结果组合未在r4全部重放 |

### r4 Fit 实际数值

`artifacts/evening-feedback/fit-runtime-r4.jsonl` 来自真实按钮点击，未从测试代码自动调用Fit：

- 画布视口2497.333×1319.333 DIP；DPI=1.5。
- 点击前Zoom=4.3335888744、Pan=(-51.732765,-129.287470)。
- 点击后Zoom=2.7540740741、Pan=(356.346667,40)；对象Bounds=648×450。
- 屏幕边界=(356.346667,40)–(2140.986667,1279.333333)，完全位于可用视口。

### r4 导出文件读回

真实产品导出完成后使用WIC只读解码文件，未通过解码器代替产品导出。路径 `artifacts/evening-feedback/runtime-export-formats-r4/verification.json`；完整SHA256已并入当前After索引。

| 文件 | 尺寸 | 解码像素格式 | 颜色上下文 |
|---|---|---|---|
| tone-jpeg_仿色.jpg | 1200×800 | Bgr24，RGB各8位 | 1 |
| tone-landscape_仿色.png | 1200×800 | Bgra32，RGBA各8位 | 1 |
| tone-tiff16_仿色.tif | 256×192 | Rgb48，RGB各16位 | 1 |

TIFF目标未加编辑，保存RGB48；不能沿用r3调整后Rgba64的结果描述当前文件。PNG alpha与高精度数学另有自动测试，不扩大为本次真实透明样本已通过。

### 仍阻止完整关闭的事项

1. 上表r4实机缺口尚存，尤其Windows200%、R03拖动过程、R04边界场景和R10全参数重启链。未授权改变系统缩放，桌面接口也不提供按住修饰键的drag或中键drag；这些条件不造证据。
2. 用户原12图没有可核对的EXE来源清单，构建HEAD仍UNKNOWN，不能断言全因旧Release。当前候选自身来源已核实。
3. Guardian静态扫描P1=5/P2=37/EXEMPT=2尚未逐条处理。与旧报告的差异涉及已经拆出的颜色筛选组件；不能把增加的扫描条目直接称为新功能回归，也不能写静态检查清零。

最终保留 **NOT_READY_FOR_USER_RETEST**。没有把任何R01–R12完整实机项或用户验收升级为PASS/APPROVED。

## 交付索引

- 源码/测试变更40个文件，逐文件类型：`2026-10-04_EVENING_CHANGED_FILES.json`。
- 当前测试及历史失败/排除记录：`2026-10-04_EVENING_TEST_INDEX.json`。
- r4实机31图、导出与Fit：`2026-10-04_EVENING_AFTER_INDEX.json`。原始截图留本机，含既有素材的图片未上传Git。
- 四个候选各自manifest保留；当前只使用r4。候选历史不拼接为一个Release。
- 用户入口：`artifacts/user-retest/latest/00_START_HERE.md`。未制作Installer，未进入下一阶段。
- 本文的提交SHA和push后LOCAL/REMOTE在最终消息回传，避免文档为记录自身SHA反复产生新提交。
