# Color Studio 重构实施与验收矩阵

日期：2026-10-05。本表记录已发布的 r1 及正在进行的同版重放；完整实机验收尚未完成。

- START_HEAD：`5427406c4ef3c805ef56fbb814251df69365032d`；r1 SourceHead：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`，以 `RELEASE_IDENTITY.json` 和发布 manifest 核对。
- 本表不使用起点审计、C1 截图、旧 r4 EXE 或自动 WPF 截图证明新 Release 运行通过。
- CODE 的 `IMPLEMENTED` 只表示表中限定行为已有生产实现；`PARTIAL` 表示需求仍有明确缺口。两者都不是产品验收完成。
- TEST 的 `PASS（范围）` 仅对应已执行的具名回归；完整 WPF 为1486通过/0失败/12跳过，后续4项P2及相关聚焦为50通过/0失败/1跳过，独立Core聚焦9/0/0。不能把旧全套计数冒充含有后加改动，也不把两个时点相加为一次全套。
- `RELEASE_RUNTIME` 仅引用 [RELEASE_RUNTIME_REPLAY.md](RELEASE_RUNTIME_REPLAY.md) 的同一 r1 EXE 实际操作。复合行未全测保留 PARTIAL；单张图不代表所有模式、参数、输入和DPI已通过。
- `USER_VISUAL_REVIEW=NOT_APPROVED`；`VisualApproved=false`；`UserVerified=false`。
- **当前状态：NOT_READY_FOR_USER_RETEST。** 剩余项见 [OPEN_ISSUES.md](OPEN_ISSUES.md)。

## 1. 真实来源

| 代号 | 本轮记录 | 使用边界 |
|---|---|---|
| S | [START_STATE.md](START_STATE.md) | fetch 起点、参考原图 hash、授权语料及旧 EXE 不同源 |
| C1 | [C1_FEATURE_BEHAVIOR_MATRIX.md](C1_FEATURE_BEHAVIOR_MATRIX.md) | C1 16.6.1.2962 实际观察及未知项；不是 Pixel Tart 修复证据 |
| D | [COLOR_STUDIO_DESIGN_SPEC.md](COLOR_STUDIO_DESIGN_SPEC.md) | 最新布局及兼容设计；设计不等于运行通过 |
| P | [PROCESSING_IMPLEMENTATION.md](PROCESSING_IMPLEMENTATION.md)、[PARAMETER_CONTRACT.md](PARAMETER_CONTRACT.md) | 生产数学、参数范围/保存键、真实 RAW/JPEG 数值结果及限制 |
| SP | [SPATIAL_SCALE_VERIFICATION.md](SPATIAL_SCALE_VERIFICATION.md) | Details/Film 全图、1600、800 同图尺度量测和版本迁移 |
| W | [WORKSPACE_STATE_IMPLEMENTATION.md](WORKSPACE_STATE_IMPLEMENTATION.md) | 胶片条、同步、历史、工作文件、缩略图、布局测试及失败过程 |
| G | [SPACE_IMPLEMENTATION.md](SPACE_IMPLEMENTATION.md) | 实际球面与 OKLab 映射、alpha、分析、WPF 绘制及性能边界 |
| E | [QUICK_EXPORT_FORMAT_CONTRACT.md](QUICK_EXPORT_FORMAT_CONTRACT.md) | 格式、位深、alpha、ICC、EXIF 八方向、冻结导出 |
| R | [RAW_PREVIEW_COORDINATE_CONTRACT.md](RAW_PREVIEW_COORDINATE_CONTRACT.md) | RAW 像素归正、真实尺寸、按需 full 预览、缓存 |
| L | [LOCALIZATION_SCOPE.md](LOCALIZATION_SCOPE.md) | 中文优先及三语言覆盖边界 |
| M | [MODULE_RUNTIME_OBSERVATIONS.md](MODULE_RUNTIME_OBSERVATIONS.md) | 第5/11候选构建的真实操作与局部图证；不是最终同版Release验收 |
| RT | [RELEASE_RUNTIME_REPLAY.md](RELEASE_RUNTIME_REPLAY.md)、[RELEASE_IDENTITY.json](RELEASE_IDENTITY.json) | r1唯一身份与同版实际重放；1600×920已观察，其他窗口/DPI未覆盖 |

自动日志、真实授权照片及衍生图片保留在本地 `artifacts/color-studio-rebuild-2026-10-05/`。原始 TRX 可能含测试机路径，照片含用户内容；本表只引用相对路径及脱敏结果，不把它们直接粘贴进源码仓库。

## 2. 逐需求矩阵

| ID / 需求 | 生产文件与实际行为 | TEST：已执行范围与来源 | CODE | RELEASE_RUNTIME | USER_VISUAL_REVIEW |
|---|---|---|---|---|---|
| CS01 最新参考图布局 | `Views/ReferenceColorWorkspaceView.xaml(.cs)`：左参考/3D/预设/节点、中央照片、右双直方图与七工具页；真正移动容器与响应逻辑 | PASS（自动几何/绑定范围）；`ReferenceWorkspaceWideRatioTests`、`EveningFeedbackWpfTests`、`RuntimeCorrectionWpfTests`；D/W | IMPLEMENTED | PARTIAL（RT02：1600×920） | NOT_APPROVED |
| CS02 紧凑顶部与有效入口 | 同 view；导入、保存/打开工作文件、语言/格式、快速导出及面板操作均接现有流程。顶栏并非 C1 菜单的算法或功能声明 | PASS（入口/状态相关自动测试）；W/E/L；参考图完整顶栏对照仍待同版运行 | IMPLEMENTED | PARTIAL（RT01/02：重开及布局） | NOT_APPROVED |
| CS03 左侧四个模式与参考只读导航 | 同 view、`ReferenceNavigator`：四模式真实内容；Reference 原图 Fit/100%/Zoom/Pan 沿用独立视口，Film Preset 与右 Film 参数分责 | PASS（模式内容/导航绑定范围）；`ReferenceWorkspaceWideRatioTests`、既有 viewport 回归；W/G | IMPLEMENTED | PARTIAL（RT03：仅3D） | NOT_APPROVED |
| CS04 右侧双直方图及七页 | 同 view、`StudioHistogramView`、共享 `HistogramDrawing`：RGB/亮度上下排列，右栏独立滚动；收起不移到照片上方 | PASS（位置、像素绘制、通道和读数）；`ColorSpaceSurfaceWpfTests`、布局回归；G/W | IMPLEMENTED | PARTIAL（RT02/03：双图及部分页） | NOT_APPROVED |
| CS05 窄窗口与工具密度 | `UpdateResponsiveLayout`、`StudioToolPanel`：父节点标题/启停、子组折叠与复位；自动折栏及手动打开，胶片条收起返还空间 | PASS（已加载窗口测量/控件行为）；`studio-compact-panel-verified.trx` 7/0；实际 Windows 100/125/150/200% 未构成最终同 EXE 证据；W | IMPLEMENTED | PARTIAL（RT02：仅1600×920） | NOT_APPROVED |
| CS06 中文优先与语言设置 | `StudioLocalizationService`、`StudioTextExtension`、`Resources/Studio/*.json`：简中/繁中/英文主要界面和新工具动态切换，稳定数据键不变 | PASS（4 项语言行为及真实深色选择器）；旧动态中文文本和其他模块未全翻译；L/E | PARTIAL | NOT_RUN | NOT_APPROVED |
| CS07 球形实际绘制及颜色模型 | `ColorSpaceSurface`、`ColorSpace3DViewport`：逐像素逆投影球面、sRGB 边界、可逆显示坐标；真实 OKLab 样本保持原值，不压到球壳 | PASS（Core5、新WPF4及映射回归）；正逆变换/真实绘制/颜色连续性；G | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS08 球面观察控制 | 上述 viewport、VM `.Analysis` 与 `ColorSpaceViewSettings`：Orbit/Pan/Zoom/Reset/bounds Fit、模式、点大小/透明度、背景/网格/轴/色域、色度和 L 切片、XYZ | PASS（相机、过滤、拾取和共享设置）；真实手势/长时间拖动仍待测；G | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS09 照片⇄3D 临时映射 | `.Analysis`、`ColorStudioColorSpace`、`ImageHighlightOverlay`：精确色中心、ΔOKLab 容差、smoothstep 软边；共享图片变换；Esc/切图清除，不进导出 | PASS（稀有色、alpha、坐标/容差及观察隔离）；G/P | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS10 3D 边界与性能 | 768 分析代理、缓存像素 OKLab/样本坐标、320 静态/160 拖动球面；alpha0 排除、近黑白及超色域数学测试 | PASS（数学/缓存限定）；同步 768² 选区遍历的真实持续输入性能待测；G | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS11 当前结果统计与旧任务隔离 | VM `.Analysis`、`VisualAnalysisEngine`：同一已显示版本统计，标来源/尺寸；取消和 revision；直方图及影调排除 alpha0 | PASS（版本、统计端点/透明度/通道）；素材库完整 palette 分析仍为旧 RGB24 合同；G/P | IMPLEMENTED | PARTIAL（RT04：冷暖后更新） | NOT_APPROVED |
| CS12 影调区间⇄像素 | `ToneZoneDrawing`、`ToneZoneMembers`、VM/Overlay：线性 Y 等分的 0–X 分区及临时高亮，共用统计边界；非摄影 Zone System | PASS（端点/成员及映射既有回归）；hover/离开/切图/缩放需同 EXE 重放；G | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS13 非破坏处理链与中性 | `ColorStudioToolProcessor`、`ColorStudioRenderPipeline`、调整栈：ProcessingVersion2 float 整栈，保存数组顺序；零位恒等，有限输入校验，取消 | PASS（新工具/旧兼容/顺序、保存、有效参数效果）；最终Core1572/0/6；P | IMPLEMENTED | PARTIAL（RT04/05：部分工具） | NOT_APPROVED |
| CS14 白平衡、曝光与 HDR | catalog/processor、`StudioToolPanel`：相对冷暖/绿洋红、EV/亮度/对比、HDR 分区和饱和度；各有处理数学 | PASS（灰阶单调、端点、不同作用域、复位）；真实5 RAW/JPEG数值结果；不是传感器域 Kelvin/HDR 恢复；P | IMPLEMENTED | PARTIAL（RT04：冷暖/恢复曝光） | NOT_APPROVED |
| CS15 基础/高级范围与肤色 | `ColorRange` v2、`SkinTone`、VM `.RangeSelection`：真实范围与羽化，输入节点取样，肤色均匀化、灰轴/端点保护 | PASS（范围边界、羽化、肤色/蓝色保护、叠加与过期任务）；背景相近颜色也会命中，无人脸检测；P | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS16 色彩平衡 | `ColorBalance` 节点：全局/暗部/中调/高光独立 OKLab 向量和连续权重，启停/复位/保存 | PASS（区域权重、端点和完整栈）；C1 同名工具尚未实测，不称其算法复刻；P/C1 | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS17 色阶和曲线 | `Levels/Curve`、`StudioCurveEditor`：RGB及单通道、合法端点、真实控制点增删/拖动、2–16点保存、形状保持插值 | PASS（通道隔离/解析精度/曲线端点/增删/保存与事务）；P/W | IMPLEMENTED | PARTIAL（RT05：RGB，未含单通道） | NOT_APPROVED |
| CS18 细节处理 | `Details`：分尺度清晰/结构/锐化、阈值渐入、独立亮度/颜色 bilateral；1600参考半径与连续亚像素 | PASS（噪声方差/阶跃保持/锐化区别；真实 full/1600/800 数值预算）；SP/P | IMPLEMENTED | PARTIAL（RT07：清晰度37.275及图05） | NOT_APPROVED |
| CS19 Film、Creative 与预设兼容 | `PixelTartFilm`、节点/预设 view：左预设应用，右参数；Film SpatialVersion2 明示编辑升级，旧 snapshot V1 保留，grain/texture相位与扩散尺度修复 | PASS（旧11+新尺度/迁移及 WPF undo；真实空间量测）；本轮未扩建新 Film Lab/Polaroid；SP/P | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS20 节点历史/存储/旧工程 | `ColorStudioModels`、store、`TetherReferenceModeViewModel.History`、工具VM：单照片 undo/redo、启停/组复位、顺序/版本深拷贝；未知未来节点拒绝而非吞效果 | PASS（Core兼容、WPF工具事务/单照片隔离/保存）；跨重启恢复最终状态，撤销日志仅进程内；P/W | IMPLEMENTED | PARTIAL（RT01/04：恢复及一次undo） | NOT_APPROVED |
| CS21 空间局部蒙版/图层 | 当前只有颜色范围节点及观察 overlay；尚无画笔/空间 mask 图层、编辑/复制/删除/羽化完整工具链 | NOT_RUN（该能力未实现）；已有范围测试不能代表空间蒙版；C1/P | PARTIAL | NOT_RUN | NOT_APPROVED |
| CS22 预览、分析、缩略图与导出同状态 | 冻结 look/stack/film/engine；`.Thumbnails` 目标级取消/并发及身份校验；像素缓存不包含观察遮罩 | PASS（同链文件像素、最新栈/缩略图、切图/快速覆盖/500图并发）；空间proxy有明确误差预算；W/P/SP | IMPLEMENTED | PARTIAL（RT04：预览/分析，未含导出） | NOT_APPROVED |
| CS23 胶片条显示/选择/过滤/元数据 | `.Filmstrip`、view：紧凑缩略图、当前与多选区分、可见序列 Ctrl/Shift/键盘、评分及色标筛选。隐藏选择保留并报告，执行集限可见所选 | PASS（过滤序列/状态/数量/隐藏集合、三窗口面积）；W | IMPLEMENTED | PARTIAL（RT01：3图显示） | NOT_APPROVED |
| CS24 所选同步与撤销 | `.BatchHistory`、Core sync：当前源排除、冻结类别/节点顺序、多同类节点不丢失；不复制评分/色标/EXIF/关系 | PASS（同步类别边界、活动编辑刷新、undo/redo、snapshot版本）；W | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS25 工作文件重开与缺失源 | `ColorStudioSessionStore`、VM `.Session`：原子 .ptstudio.json、验证后替换；保留身份/编辑/筛选/选择/活动图，缺失源保记录 | PASS（重开/无效不替现场/缺失源/源字节不变；素材元数据用DB）；W | IMPLEMENTED | PARTIAL（RT01：既有批次重开） | NOT_APPROVED |
| CS26 格式、ICC、alpha 与冻结批量输出 | `StudioQuickExport`、VM Export/RawExport：源格式或 JPEG/PNG/TIFF；RAW 完整栈到 TIFF/PNG/JPEG；逐张失败/取消/原子文件；发布配方独立 | PASS（真实 WIC 编解码、格式位深/alpha/ICC、RAW单解码、冻结栈 codec oracle）；E/P | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS27 方向与真实100% | `RawImageOrientation`、`StudioQuickExport`、`SourcePixelSize`、`ColorStudioImageViewport`：RAW/master和raster各归正一次；按需full预览与同源几何；JPEG/TIFF输出orientation1 | PASS（float八方向、实际WIC八方向、全尺寸高频样本/缓存/切图）；100%定义为原像素/DIP；R/E | IMPLEMENTED | NOT_RUN | NOT_APPROVED |
| CS28 V4 兼容边界 | `.RawExport`：冻结每目标 engine；V4仅支持限定RAW ReferenceMatch，完整新工具栈/Film不支持时明确阻止 | PASS（不随活动引擎改变、不默默忽略节点）；完整V4工具链未实现；W | PARTIAL | NOT_RUN | NOT_APPROVED |
| CS29 C1 完整行为调查 | 已观察实际C1曝光/阴影/曲线/色阶与若干入口；保留可复核局部证据 | PARTIAL；范围/步进/边界、平衡、肤色完整行为、局部、同步、ICC/导出等未覆盖；C1 | PARTIAL | NOT_RUN | NOT_APPROVED |
| CS30 同一最终 Release 身份与完整验收 | r1已发布，287文件manifest校验0差异；SourceHead/DLL版本/hash及启动会话已记录；1600×920与部分工具已有After，其余窗口/DPI及完整工具链未完成 | PARTIAL（发布身份可核对，完整实机门槛未满足）；RT | PARTIAL | PARTIAL（身份及局部重放） | NOT_APPROVED |

文件名未加目录时：Core 位于 `src/RAWSelectionAssistant.Core/Services/Projects/`，VM/View/Service 位于 `src/RAWSelectionAssistant/` 对应目录。最终完整修改文件清单须从集成提交差异生成，不以本表省略清单替代 Git 差异。

## 3. 最新自动验证快照

| 日志（相对本阶段 artifacts 的 tests） | 实际结果 | 范围与限制 |
|---|---|---|
| `core-full-final-upright.trx` | 1572 PASS / 0 FAIL / 6 SKIP | 最终Core，含归正、空间版本、兼容和数学；跳过不算通过 |
| `studio-latest-final-focus.trx` | 40 PASS / 0 FAIL / 1 SKIP | 第12次WPF聚焦；包含格式4/方向、RAW100%、语言、工具与批次；后续末轮小改需最终full覆盖 |
| `studio-wpf-full-final.trx` | 1486 PASS / 0 FAIL / 12 SKIP | 1498项实际结果；第13次集中编译，末轮4项P2后续改动不冒充已含在该全套 |
| `studio-p2-final-focused.trx` | 48 PASS / 2 FAIL / 1 SKIP | 首次P2执行，两处oracle错误，保留失败；首次测试编译也有1个internal可见性错误已修 |
| `studio-p2-verified.trx` | 50 PASS / 0 FAIL / 1 SKIP | RAW停止、Esc迟到取样、未激活Film有效栈、方案undo及批次/冻结导出相关回归；新集中编译0错误 |
| `core-final-effective-state.trx` | 9 PASS / 0 FAIL / 0 SKIP | 独立Core有效状态聚焦，不冒充后改源全套Core重跑 |
| `spatial-final-gate.trx` | 34 PASS / 0 FAIL / 0 SKIP | 真实授权RAW的Details/Film full与两档proxy预算及兼容 |
| `real-raw-all-final.trx` | 1 PASS | 5张授权RAW，同版点工具、全尺寸TIFF16回读及源hash；程序化证据 |
| `real-jpeg-tools-final.trx` | 1 PASS | 5张JPEG代理完整栈编码/回读；PNG精确，JPEG有损差已量测 |

历史失败和后续修复链见 W/P/SP/E，保留原结果，不把失败日志重命名为通过。完整日志计数、P2首次编译/48-2-1失败的修复及跳过原因见 TEST_RESULTS.md；不使用旧全套 PASS 冒充含P2最新源，也不使用任何自动结果或第5/11候选观察升级本表的最终 RELEASE_RUNTIME 或用户批准。

## 4. 最终 Release 与正在进行的重放

- EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish/win-x64/KitaoPhotoSelector.exe`
- SourceHead：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`；Release / x64 / win-x64。
- DLL ProductVersion：`2.3.0+31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`。
- EXE SHA256：`C9841FB530ABF0E209B8E9BB9B6870C917D54DED985702B2EF41F36EE6DB28CA`；DLL SHA256：`DD5BFB88219EC853DD328AA00743BE4519C0A259E19C2016467239948420BCD3`。
- 发布文件manifest：`artifacts/releases/color-studio-rebuild-2026-10-05-r1/release-manifest.json`（287文件）；hash见 `RELEASE_IDENTITY.json`。
- 会话：PID36892，`2026-10-05T13:08:50.5696117+08:00`启动；窗口1600×920，真实系统DPI尚未取得读数。
- 已覆盖：2 JPEG+1 RAW工作文件重开及EV 1.127恢复；右侧双图展开/折叠；冷暖25.199并Ctrl+Z恢复0；RGB曲线点0.496→0.654及复位；RGB色阶gamma1.448及复位。对应 `final-runtime-evidence/01`–`04`，精确文件名见RT表。
- 清晰度37.275已观察完成和细节改变，`05-clarity-37275.jpg`已保存；不代表其余细节/Film完成。其他复合操作继续保留 PARTIAL / NOT_RUN。后续桌面重放等待用户对最新Computer Use禁止规则给予本轮例外答复，不将等待标为产品FAIL。

每个 CS 项实际重放后追加：输入文件hash、窗口/DPI、操作前置/参数、可观察结果、After相对路径、PASS/FAIL/PARTIAL/BLOCKED及剩余原因。参考图对照使用本次1672×941原图；C1研究与Pixel Tart运行证据分开。缺失项目见RT表，不以本段局部观察概括为完整通过。
