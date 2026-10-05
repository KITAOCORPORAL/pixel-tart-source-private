# Color Studio 工作区源码审计（实施前）

## 证据边界

- 审计日期：2026-10-05（Asia/Shanghai）。实际工作树：`N:/pixart/pixel-tart-source-private`。
- 本次读取 HEAD：`5427406c4ef3c805ef56fbb814251df69365032d`；审计开始时 `git status --short` 无输出。
- 本文只记录实际读取的源码、已有测试及待修改范围，未执行本轮测试、未操作桌面、未修改生产代码。
- 上轮 r4 的 SourceHead 为 `63cd21e6cd13e4d2982be1b45676fefa68226c6f`。上轮报告 R01–R12 的 RELEASE_RUNTIME 均为 PARTIAL；不可作为本次新布局或新调色能力的验收证据。
- 当前 `CODE=审计状态`、`TEST=既有测试可复用但本轮未运行`、`RELEASE_RUNTIME=NOT_RUN`、`USER_VISUAL_REVIEW=NOT_APPROVED`。用户原反馈 EXE HEAD 无可核对 sidecar，仍 UNKNOWN。

## 现状、调用链与拟修改范围

路径以仓库根为基准。表中“存在”不代表视觉质量或效果已通过。

| 功能 | UI / ViewModel 调用链 | 算法 / 状态 / 存储 / 导出 | 已有测试 | 已确认差距与拟修改文件 |
|---|---|---|---|---|
| 整体工作区 | `Views/ReferenceColorWorkspaceView.xaml` 的 `EditingRail` 列2、`ContextRail` 列0；`.xaml.cs:UpdateResponsiveLayout` | 单一 `ReferenceColorWorkspaceViewModel.Editor`；无独立左右模式状态 | `ReferenceWorkspaceWideRatioTests`、`RuntimeCorrectionWpfTests` | 目前右侧5个中文分区，左侧两个连续 Expander；需要左4模式、右7工具页、紧凑顶栏。更新 view、code-behind、明确模式ID的 VM；不能复制 Editor |
| 双直方图 | `CentralAnalysis` 位于中央照片上方；`PreviewHistogram`→`HistogramDrawing`；`ShowAnalysisLuma` 叠加Y轮廓 | `Analysis.cs:RefreshPreviewAnalysisAsync` 120ms合并，bitmap引用+tier缓存4项，revision防旧结果；当前原片/结果分阶段标签 | `RuntimeCorrectionWpfTests.AnalysisInvalidatesOnTargetChangeAndMappingDoesNotEditParameters` 等、`EveningFeedbackWpfTests` | 新图要求右顶双图，当前只有单图/通道切换；无悬停bin读数。`UpdateAnalysisSpace` 明写中央上方约束，必须重做；复用计算而非另算第二套 |
| 影调区间 | `ToneZoneDrawing.ZoneHovered`→`HighlightToneZone`→`ImageHighlightOverlay` | `VisualAnalysisEngine.ToneZoneMembers` 与统计相同线性Y等分11区；不写入stack | `EveningFeedbackCoreTests.AllQuantizedToneBoundariesAndMembersMatchStatistics`；WPF hover/Esc/切图测试 | 保留并移入右侧分析区。需在新双图收起/滚动条件验证映射，不能称摄影Zone System |
| 中央照片 | `ColorStudioImageViewport` 绘制 `Original/Matched`；`ColorStudioZoomPanState` 单一Zoom；viewport与overlay共享state | 对比模式是中文字符串：原片、仿色结果、左右对比、并排对比；`ColorStudioSampleMapping` 处理letterbox/代理/左右区域 | `ColorStudioZoomPanTests`、`ColorStudioSampleMappingTests` | Fit/100%工具当前在照片下方；移顶部紧凑行，保留Zoom。翻译不能直接改变模式判断字符串 |
| 参考导航 | `ReferenceNavigator.xaml/.cs`；`CurrentReferencePath`；独立只读viewport，Fit/100%/wheel/pan | BitmapImage读入，revision防旧文件覆盖；不改变目标stack | `RuntimeCorrectionWpfTests` navigator用例、旧实机证据 | 左Reference模式内整合参考来源、原有仿色控制/入口，避免移出后遗失功能；当前来源信息分散在编辑栏参考列表 |
| 3D 当前照片云 | `ColorSpace3DViewport`、`CloudInspectionControls`；Analysis生成model；view订阅selection | `ColorStudioColorSpace`/`ColorSpaceProjection`：OKLab点保留L/a/b与源坐标；现球形为线框参考，不是连续彩色表面 | Core ColorSpace相关测试、WPF `ColorStudioSampleMappingTests`、`BatchExportProcessedPixelsTests.ColorInspectionOnlyChangesPreviewAndClearsOnTargetSwitch` | 需新增真正连续可旋转表面、显示转换、背景/网格/轴/色域控制及slice/范围/rotation参数。只能改查看状态，不得隐式改stack |
| 3D双向映射 | 图片取色→`HighlightDisplayedImageSampleAsync`→最近点；cloud→`HighlightCloudSelection`→像素成员 | `ColorSpaceLinking.ToPixelMembership`按完整OKLab球半径二值成员；当前容差.01–.15；同一preview的proxy buffer | 以上mapping和runtime correction tests | 现无柔和选区权重；分析buffer丢alpha，透明像素仍可能计入；全buffer颜色转换同步执行有UI成本。需缓存颜色/支持软权重/alpha有效像素 |
| 基础调色 | `TetherReferenceModeViewModel.Develop.cs`→`SetDevelop`→`ChangeStack`→`RenderAsync` | `ColorStudioDevelop.Apply`线性RGB luminance处理；单一Develop节点；scheme保存NumericParameters；export同stack | `EveningFeedbackCoreTests`、`EveningFeedbackWpfTests.DevelopResetRestoreAndRealTiffPreviewExportAgree` | 只有10项，无WB、全局饱和、色彩平衡、Levels、Curve、专用降噪。亮度直接与exposure相加(±1EV)，HDR是重叠EV权重+硬clamp，质量需实图审计 |
| 颜色范围 | SelectedAdjustmentNode及正/负取样；RangeHue/Saturation/Chroma/Lightness等 | `ColorStudioRenderPipeline.SelectionWeight` 在a/b平面按range+smoothstep羽化；保L可选；原8bit及float路径分别实现 | `ColorStudioStackTests` range羽化/负取样/顺序；`EveningFeedbackCoreTests`高精度范围 | 无基础颜色范围preset、独立肤色均匀化模型、蒙版层/空间刷选。3D选择用完整Lab距离而调色范围只用ab，必须向用户说明区别或统一显式模式 |
| 节点 | `AdjustmentNodeList`位于右调色/仿色下；拖动、启停、rename、reset、duplicate、undo | `ColorAdjustmentStack` Version2/OKLabD65、唯一GUID、有限值校验；VM `_undoStacks/_redoStacks`；scheme序列化 | `ColorStudioStateClosureTests` node/undo/drag tests；`ColorStudioStackTests` | 移至左Nodes但右参数绑定同一SelectedNode。切图时undo栈未切换/清空，存在A操作撤销到B风险；节点组单独reset与新增类型需规则 |
| 方案与胶片 | 右“预设”含ColorSchemes、XMP、ReferenceLooks；右“胶片”含FilmProfiles/参数 | `ColorStudioSchemeStore`原子替换catalog v2；`PixelTartFilmSettings`、`PixelTartFilmPipeline`；预设hover可取消 | scheme restart/unsaved tests、Film pipeline tests | 左Film Preset应浏览/应用；右Film只调当前film。不能复制值；现胶片参数在节点视图和胶片分区重复呈现 |
| 目标切换/预览 | `ActivateTargetAsync`保存前target snapshot→加载source→ApplyTargetSnapshot→SetSourceAsync | activation revision/cancellation；最多3个或128MiB源缓存；editor render revision+cache，旧帧不能覆盖 | `BatchExportProcessedPixelsTests` rapid activation/cache/cancel/failed job等 | 仍需新大栈持续拖动、线程/显存实测。Develop每次ChangeStack直接RenderAsync，未统一进100ms debounce |
| 胶片条 | `Filmstrip ItemsSource=Targets`；卡片52px图+文件名+状态+独立评分行；view事件接Ctrl/Shift/箭头 | `ReferenceTargetItem.IsActive`与`IsSelected`分离；`SelectFilmstripTarget`按Targets索引 | `BatchExportProcessedPixelsTests.FilmstripClickModifiersKeepAnchorSelectionAndActiveIndependent` | 无rating/color/scope过滤序列；按钮行过宽、折叠标题和状态占高。新可见序列需统一鼠标/键盘/range/Ctrl+A/菜单/export语义 |
| 评分/色标 | 双向评分控件+右键色标；workspace FIFO metadata queue | AssetId有效则repository.UpdateAssetMetadataAsync或AssetPresentationMetadataStore；MainWindow回调刷新AssetLibrary；无AssetId仅session | `ColorStudioStateClosureTests.FilmstripHydratesAndPersistsAssetLibraryMetadata` | 筛选后改评分/色标会移出可见列表，需明确active/selection保留。不能重新维护持久化副本 |
| 同步 | SyncSelected直接`Editor.CopyCurrentLookTo(SelectedTargets)`；另有node与type选择popup | snapshots深拷贝；不复制rating/color/EXIF。Type同步在Core实现 | `BatchExportProcessedPixelsTests.CopyApplyAdjustmentsUsesSelectedCategoriesAndProtectsAssetFields`、`ColorStudioStackTests.SelectedTypeSync...` | source与targets人数不清；无批量Undo。多个同type节点会使ToDictionary重复键，需修。node/type同步未刷新active editor；必须排除source或明确处理 |
| 快速导出 | 顶部ExportSelected+底部selected/all；`ExportAsync`freeze每目标→WPF或RAW pipeline→temp→move | StudioQuickExport保JPEG/PNG/TIFF，RAW→TIFF16；ICC读入normalize sRGB，输出embed sRGB；取消与逐张失败 | `BatchExportProcessedPixelsTests`冻结非active/取消/真实像素parity；`EveningFeedbackWpfTests`alpha/TIFF16 | 没有Quick Export格式/输出profile选择；发布配方有独立格式配置。RAW导出分支仍读取active Editor.IsMatchV4Beta，需核查target engine冻结 |
| 发布配方 | `PreparePublishingAsync`先处理冻结目标至临时PNG/TIFF，再OpenPublishing | `PublishingExportViewModel`进行format/size/metadata/ICC等输出设置 | PublishingReceivesProcessedFrozenTargets... | 与快速导出职责不同必须保留，且对高精度TIFF输入中间PNG位深/alpha/ICC继续验证 |
| 项目重开 | `ColorStudioSchemeStore`只存scheme；`Targets`为VM集合，session snapshot存在target对象 | `ColorStudioSchemeV2`存stack，不存target路径/active/selected/filter；`ReferenceLookStore`另存reference | Scheme重启测试只证scheme可还原，不证batch project | “保存并重开多图项目”需明确会话/项目文档持久化，不能用scheme通过替代。需新project/session schema及兼容测试 |
| 汉字优先/语言 | XAML和VM大量硬编码中文；`AppSettings`无UILanguage | `Resources/DesignSystem`为样式，无locale词典；全文检索未找到UI语言服务/切换设置 | 尚未定位语言行为测试 | 不能声称已有主流语言切换。新增稳定模式ID/字符串资源、设置持久化、fallback；保留已有中文和全局设置其它字段 |

## 已有参数清单（源码读取，非效果批准）

| 组 | UI绑定与范围 | 对应真实处理/存储 |
|---|---|---|
| Develop | Exposure −3..3 EV；Brightness/Highlights/Midtones/Shadows/DevelopContrast/Whites/Blacks/Structure/Detail −100..100 | Develop NumericParameters `exposure/brightness/highlights/midtones/shadows/contrast/whites/blacks/structure/detail`；`ColorStudioDevelop.Apply` |
| 参考 | ReferenceSources.WeightPercent .1..100；MatchStrength/ToneStrength/ColorStrength/ContrastStrength/SaturationStrength 0..100；SkinProtection/HighlightProtection/NeutralProtection 0..100；KeepOriginalTone bool | ReferenceLook与ReferenceMatch节点双向同步；matcher处理/旧LUT仅此链 |
| 范围 | RangeStrength 0..100；RangeRadius/RangeSoftness 0..50；RangeHue −180..180；RangeSaturation/RangeChroma/RangeLightness −100..100；KeepOriginalLuminance、ShowSelection | ColorRange节点：`strength/range/softness/hue/saturation/chroma/lightness/keep_original_luminance`及正负Samples；ShowSelection仅预览 |
| 过渡 | TransitionAmount 0..100 | `TransitionBlend.amount`；当前是固定比例色度衰减，不能作为完整color balance |
| 预设 | PresetStrengthPercent 0..100 | `Preset`节点；`exposure/contrast/saturation/preset_strength`；XMP仅支持声明过的字段 |
| 胶片 | Enabled、ProfileId、ProfileAmount/GrainAmount/GrainSize/HalationAmount/BloomAmount/VignetteAmount/SurfaceAmount/TextureAmount 0..100；TextureId、Seed及reroll | `PixelTartFilmSettings`；现有真实FilmPipeline，不增加Film Lab |
| 检查 | SamplingTier 1024/4096/16384点；PointSize1..6；PointOpacity .15..1；SelectionTolerance .01...15（即0.01至0.15） | VM transient inspection；不存调整节点，不导出 |
| 视口 | Fit、100%、wheel、pan、compare、split；ref独立Fit/100%/pan | ColorStudioZoomPanState，照片像素不变 |

以上参数只有部分自动输入/输出覆盖。尚未做本轮逐值、肤色、真实RAW/JPEG、导出与全位深视觉评价，不能写“所有按钮效果合格”。

## 必须解决的状态风险

1. **同类节点同步会抛异常**：`ColorStudioModels.cs:SyncSelectedByTypeFrom` 用 `selected.ToDictionary(node => node.Type)`；允许多个ColorRange/Transition/Preset节点时重复键。目标存在多个同类节点时逐个替换成同一个incoming ID，又与Normalize唯一ID冲突。测试只覆盖单同类节点。
2. **跨照片撤销历史**：`TetherReferenceModeViewModel.ApplyTargetSnapshot`不重置/恢复undo/redo、edit transaction或持久scheme上下文。需按target保存history或实现明确的target-local操作事务。
3. **同步与当前预览分离**：node/type同步改snapshot但不更新active Editor；batch history缺失。实现时应明确以active为source且目标排除source，导出前冻结同一状态。
4. **过滤后的索引**：现鼠标/键盘全部读Targets顺序。只给ListBox加CollectionView.Filter将导致Shift range与视觉顺序不一致；隐藏的selected targets可能被错误同步/导出。推荐StableId锚点、可见序列统一API，保留全体选择但明确“可见选中/总选中”执行范围。
5. **8bit逐节点量化**：`ColorStudioBitmapRenderer.Render`只有BitsPerPixel>32走float；普通JPEG/PNG走VisualPixelBuffer，每节点写8bit。需将普通输入也升float，经完整stack后才显示量化；alpha必须独立保留。
6. **分析透明像素**：`ToVisualBuffer`转Rgb24后丢弃alpha，统计和选区可能对全透明RGB计数。加入显式有效像素定义，不能统计不可见颜色而不说明。
7. **长图分析内存/UI成本**：分析proxy长边限制768，但每次生成Build(buffer,buffer,identity)包含三份cloud与migration。按需要只建当前图模型；成员查询缓存Lab，避免滑动容差每次全图计算。
8. **程序状态语言耦合**：`WorkspaceSection/ViewMode`等中文标签兼作逻辑枚举。资源翻译前建立稳定ID或在DisplayLabel层转换，避免切语言导致空页/错误对比。
9. **保存方案并非保存项目**：scheme stack重载测试不含Target资产集合、源reference文件绑定、active、selection、filter。新项目状态需要独立schema，保留现有v2 scheme与旧ReferenceLook迁移。
10. **参考/胶片入口重排**：当前源选择Popup挂在右“仿色”，色彩方案挂在右“预设”，节点列表挂在右“调色/仿色”。移动前应按能力整体拆分一次；禁止留下旧入口不可达或保存/输出失去绑定。

## 旧方向测试必须有意更新

- `ReferenceWorkspaceWideRatioTests.AnalysisIsLeftEditingIsRightAndHistogramAboveTarget` 明确断言histogram在中央上方。
- `RuntimeCorrectionWpfTests.RightEditingAndCentralHistogramRemainBoundedAcrossViewportSizes` 同样绑定旧位置。
- `EveningFeedbackWpfTests.LoadedPngDevelopRendersThroughBoundWorkspace`和低矮视口测试直接查找`CentralAnalysis`。
- `StudioDesignSystemContractTests`含源码字符串检查；保留必要设计约束，但本次不能只改字符串使其绿色。
- 新行为测试应量测双histogram父容器/屏幕相对位置、目标可视面积、各模式真实可达性，验证实际binding/pixel结果；Windows物理DPI实测另列。

## 建议文件所有权与集成边界

| 模块 | 建议主要修改文件 | 公共契约 |
|---|---|---|
| 工作区与布局 | `Views/ReferenceColorWorkspaceView.xaml/.xaml.cs`、ReferenceNavigator、紧凑filmstrip新control（如必要） | 所有UI绑定同一workspace/editor；左/右mode稳定ID；只读检查不写stack |
| 批次与元数据 | `ViewModels/ReferenceColorWorkspaceViewModel.cs`及新的filmstrip/session partial | VisibleTargets、稳定selection anchor、统一导出/同步范围、asset metadata原pipeline |
| 编辑参数 | `ViewModels/TetherReferenceModeViewModel.Develop.cs`及新增对应partial；主VM只集成必要通知/事务 | 同一AdjustmentStack；新节点类型/参数key由Core定义；避免多个agent同时改主VM |
| 处理核心 | `Core/Services/Projects/ColorStudioDevelop.cs`、`ColorStudioModels.cs`、`ColorStudioRenderPipeline.cs`与专用node算法 | neutral exact、finite校验、ordered nodes、高精度、取消；不暗中更改旧node含义 |
| 像素适配/输出 | `Services/ColorStudioBitmapRenderer.cs`、`StudioQuickExport.cs`、RawMatchTiff16ProductPipeline | preview/export共享float链、alpha/ICC明确、frozen target engine |
| 分析与3D | workspace `.Analysis.cs`、`ColorSpace3DViewport.cs`、`CloudInspectionControls.cs`、`ImageHighlightOverlay.cs`、Core ColorStudioColorSpace/Projection | 同一当前preview revision；display settings与editing nodes分开；真实Lab及proxy坐标 |
| 分布绘制 | `PixelTart.Modules.AssetLibrary/HistogramDrawing.cs`、`ToneZoneDrawing.cs`、Core VisualAnalysis | 复用并保持素材库兼容；新增bin hover/读数只影响显示 |
| 方案/项目兼容 | `ColorStudioSchemeStore.cs`、`ColorStudioModels.cs`、Tether SchemeInteractions、项目session store | v2兼容、原子写；scheme与project分别定义；单targetundo恢复 |
| 本地化 | `Core/Models/AppSettings.cs`、SettingsService、独立UI资源/语言服务、新控件字典 | zh-CN默认；不翻译状态ID/存储key；未知locale中文fallback |
| 测试 | 既有Core/WPF颜色、batch、metadata、scheme测试+新的几何与像素用例 | 新测试解决风险；旧实机图不作本轮PASS |

## 最近相关修复（源码历史，不作本轮验证）

- `e148caf`：晚间R01–R12、Develop、中央分析、源格式export、tone hover与球形参考线框。
- `b5bd051`：短窗目标图可视区，中央分析/底filmstrip折叠。
- `6a52e83`：decoder pixels detached，避免worker跨线程WPF失败；零尺寸3D。
- `4315bc9`：检查时保留目标照片。
- `6f8e86f`：上下文分析、metadata与工作区纠错。
- `335e36f`：copy/apply调整保护metadata。
- `0969763`：XMP预设集成。

结论：应复用现有真实处理、状态、元数据、输出和scheme路径，但本次新布局、完整工具层级、连续球面、过滤/同步/项目恢复与语言切换均不能按旧“已存在”结案。此文为实施输入，不是修复或验收证据。
