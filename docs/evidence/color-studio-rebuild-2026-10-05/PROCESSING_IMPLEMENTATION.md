# 本轮 Core 处理实现与边界

2026-10-05。状态：Core 已实现并通过下列合成回归；本文件不是 Capture One 算法研究结论，也不是照片效果或 Release 实机通过证据。

## 同一数据状态与兼容

- `ColorAdjustmentStack.ProcessingVersion` 是新增可选字段。旧 JSON 缺字段时为1，JPEG/PNG继续历史逐节点RGB24量化。2代表同一float全栈计算、最终显示量化。原RAW/TIFF高位深路径继续float。
- NodeType仅追加 `WhiteBalance/BasicTone/ColorBalance/Levels/Curve/Details/SkinTone`，旧整数和旧`Develop`数学不变。
- 新工具的默认参数不写入也等同中性；每个新节点中性时直接clone，精确恒等。新的numeric keys、范围、默认、单位和步进以 `ColorStudioToolCatalog.Parameters` 为准。
- 新工具通过既有`RenderPipeline`执行；只有适配层处理显示/编码。保存数组次序为实际执行次序。新增工具默认由UI显式升级ProcessingVersion2；Undo应恢复包含该版本的整栈。
- `ColorRange`新增可选`range_version=2`：饱和度是相对色度比例；色度是最大±0.1 OKLab绝对增量并在中性轴淡出。缺字段的旧节点仍为历史两比例相乘语义。v2还支持`lightness_min/max/feather`，与a/b距离羽化相乘。

## 参数数学及可解释限制

| 组 | 当前真实数学 | 重要边界 |
|---|---|---|
| 白平衡 | 已解码线性sRGB各通道相对指数gain；冷暖±100、绿洋红±100；超出显示范围按最大通道归一压缩 | 不是RAW相机Kelvin或as-shot白平衡；UI必须使用“相对冷暖” |
| 曝光 | 线性Y乘2^EV并用连续rational shoulder压到显示域 | display-referred，不是恢复已剪切传感器高光；正向曝光有肩部，不能声称纯scene-linearEV |
| 亮度/HDR | `Y + amount*Y*(1-Y)`；high/shadow/white/black各有平滑区域权重；保持0和1端点 | 此黑场控制保持纯黑，区别旧Develop抬黑行为；全部强度夹到合法范围 |
| 对比 | 保端点亮度bend，围绕中间位置改变斜率 | 不是Capture One私有算法 |
| 饱和度/自然饱和度 | OKLab C比例；自然饱和度对高C减弱 | 输出采用原公共sRGB色域压缩 |
| 色彩平衡 | master/shadows/mid/highlights平滑权重下的OKLab a/b向量偏移；黑白端点淡出 | 各区域交叠，界面必须清楚显示所选组 |
| 色阶 | encoded-sRGB RGB总曲线再各通道；输入黑白、gamma、输出黑白 | 输入黑点>=白点、输出黑点>白点拒绝；不是线性曝光工具 |
| 曲线 | 默认5个控制点；Core允许每通道2–16点（`channel_count/xN/yN`）；shape-preserving Hermite插值，允许有意非单调曲线且不超相邻点范围 | UI若只有5点滑杆，不能声称已交付拖拽增删完整曲线编辑器；X端点固定0/1；Y可调 |
| 肤色 | 明示色相中心/范围/羽化；低C/极暗极亮淡出；hue/chroma/L分别朝指定目标连续均匀化 | 不做人脸/人物检测；背景近肤色也可能受影响；取样默认不代表任意肤色 |
| 降噪 | OKLab邻域bilateral权重，L与a/b混合强度独立 | CPU，固定5×5参考尺度采样，亚像素双线性Lab；没有AI纹理重建 |
| 锐化/清晰度/结构 | L的不同尺度unsharp；阈值与最大±.15 L限制halo；参考半径按1600像素长边换算 | Proxy/full实际尺度已按SPATIAL_SCALE_VERIFICATION量测；空间过滤代理误差有上限，不称逐像素相等 |

当前内部缓冲区继续使用display-referred encoded-sRGB float；各工具显式进入linear RGB或OKLab，输出回float sRGB。色域压缩在每颜色工具输出发生，不宣称无界HDR工作流。基础色彩工具的Gamut归一保持RGB比率，OKLab工具复用项目的chroma压缩；最终量化由显示/输出适配层处理。

## 本轮已执行 Core 验证

命令：`dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release --filter "FullyQualifiedName~ColorStudioToolProcessorTests|FullyQualifiedName~ColorStudioStackTests|FullyQualifiedName~EveningFeedbackCoreTests|FullyQualifiedName~RawMatchTiff16ProductPipelineTests|FullyQualifiedName~PixelTartFilmTests" --no-restore`。

相关Core阶段记录：`artifacts/color-studio-rebuild-2026-10-05/tests/color-studio-core-regression-v3.trx`，44 PASS、0 FAIL、0 SKIP。后续工具数学最终聚焦 `color-studio-tools-final-math.trx` 为18 PASS、0 FAIL、0 SKIP。完整Core初轮 `core-full-color-studio-rebuild.trx` 为1564 PASS、0 FAIL、4 SKIP；它在后续性能改动之前，最终全套结果应以主交付矩阵为准。

新增测试实际检查：

- 全新工具中性float精确恒等；各有效控件对真实buffer有像素作用；复位恒等。
- 白平衡正负方向及tint通道响应。
- 影调±100灰阶单调、黑白端点、亮度与曝光不重名同算、阴影/高光不同作用域。
- 色阶红通道隔离、输入/输出端点非法组合拒绝。
- 曲线过控制点、单调输入曲线保持单调、自由曲线不过冲、3点保存结构。
- 肤色范围使样本朝目标改变、灰轴和蓝色不受影响；色彩平衡区域权重。
- bilateral降噪降低平场方差且保留阶跃边；锐化与降噪结果不同。
- 全栈display adapter与float结果同源；保存重读不改变结果；反转节点顺序产生对应不同结果。
- 非有限输入拒绝、范围夹制、取消；旧JSON/Develop输出兼容；旧字节quantization保留。
- 新ColorRange饱和/色度不再数学重复、羽化无突跳、明度范围外精确不变。

首次工具测试9 PASS/1 FAIL曾查出未调饱和时BasicTone无用OKLab往返使纯白变为0.99999994。已修为未调颜色时跳过该变换，再运行完整Core相关回归通过。失败TRX保留为历史诊断，不作为完成证据。

## 仍需由集成验证证明

- 真实肤色/蓝天/植被/逆光/JPEG/RAW/TIFF的实际视觉效果、拖动连续性与可控性。
- ViewModel数值校验、事务Undo/Redo、节点启停/保存重启、旧方案精度提示。
- 新工具进入照片预览、双直方图、缩略图、导出冻结栈；导出实际文件解码像素比较。
- 真实proxy/full尺寸的细节、grain、bloom效果尺度、性能和取消延迟。
- 当前参数目录描述Core能力，不代表每个工具的UI入口或交互已完成。

`USER_VISUAL_REVIEW=NOT_APPROVED`；本文件不授权升级为用户批准。

## 真实授权照片与性能修复

用户提供 `DSC04831…DSC04835` 的5对Sony RAW/JPEG，原文件hash另见本阶段input-manifest。程序只读取这些命名文件，派生图写入本阶段artifacts；不更改原件。没有原生TIFF素材，本次TIFF16均明确为RAW处理结果派生。

初轮已完成3张RAW的7028×4688全分辨率检查（第4张前取消以优先修复已发现性能风险，整次取消不能标PASS）：

| 输入 | 1600 proxy完整6节点 | Full resolution | 同中心采样proxy/full最大OKLab差 | TIFF16编码readback差 |
|---|---:|---:|---:|---:|
| DSC04831.ARW | 3546.9ms | 56327.7ms | 0 | 0 |
| DSC04832.ARW | 3136.4ms | 54388.3ms | 0 | 0 |
| DSC04833.ARW | 2889.7ms | 52322.2ms | 0 | 0 |

初轮峰值工作集观察约3.7GB。定位到生产渲染为每节点保存原尺寸RGB24输入/输出，和曲线/色阶每像素反复计算。修复：

- `captureNodeDiagnostics=false` 供生产WPF/RAW路径使用，保留原API默认true给节点诊断。像素数学不变，测试断言新旧模式结果一致。
- 没有ReferenceMatch时不重复完整palette/排序分析；处理占位数据不进入UI分析。
- 曲线每节点预计算4096段表再线性查值，标准平滑曲线误差测试小于2e-6。色阶使用预解析的解析系数，保留gamma=5近黑精度，避免粗LUT在暗部误差。
- 白平衡gain、色彩平衡方向和曝光指数移出逐像素循环。

最终数学版本重跑DSC04831：1600 proxy **1373.6ms**、800交互proxy **267.7ms**、full **18342.9ms**；同中心采样proxy/full OKLab mean/max/tone/chroma仍0；真实TIFF16 readback与待编码RGB48精确一致。运行25s测试通过：`real-raw-final-math.trx`。JSON：`artifacts/color-studio-rebuild-2026-10-05/real-corpus-final/REAL_RAW_TOOL_MATRIX.json`。优化前保留在`real-corpus/REAL_RAW_TOOL_MATRIX_BEFORE_OPTIMIZATION.json`。

这是point工具栈（白平衡、基础HDR、平衡、色阶、曲线、肤色）的程序化同链/性能验证，**不是Release UI runtime或用户视觉批准**。18.3s大图处理及267.7ms交互也不能称每帧实时。细节单工具在1600真实proxy有独立参数差值/耗时，但full-resolution细节与proxy空间滤波差异尚未闭环。

实际编码合成WPF两测试在`studio-state-layout-focused.trx`通过：PNG/TIFF完整新工具栈lossless差0、alpha/ICC/位深保留；JPEG质量95，mean RGB差0.7078、max6（允许mean<1.5,max≤6的平滑合成图）。该聚焦run另有布局失败，因此不能整份TRX称全绿。真实JPEG opt-in和最终build验证以主交付矩阵更新。

## 独立颜色范围观察层

本轮发现旧ShowSelection把蒙版替代MatchedImage，造成直方图/3D分析的是选区渲染而非照片，且RAW分支没有相同显示行为。现改为独立 `ReferenceColorWorkspaceViewModel.RangeSelection`，复用预览HighlightWeights，不触碰MatchedImage/调整栈/导出像素。

- 取色操作按用户点击的图像归一坐标，重放所选ColorRange节点**之前**的栈，在其输入处取样。不能把最终已改色照片的RGB直接回填成该节点输入样本。
- 观察权重使用与真实ColorRange数学相同的SelectionWeight；RAW取样/权重保float主图代理，普通图为最大768像素RGB24诊断代理。空间处理在代理尺寸运行，**不宣称代理mask与全分辨率每像素完全一致**。
- ShowSelection不再触发像素渲染、不进入像素缓存键。开启/关闭仅改变观察层；Esc、切图、开始3D或影调观察时取消旧范围任务，revision/源/节点身份检查拦截过期结果。
- `ColorRangePreviewOverlayTests` 是程序化WPF状态测试；仍需本次Release中的取色、缩放平移和Overlay外观证据。代码/自动测试不得代替该runtime步骤。

## 最后数值回归（独立于实机）

`core-full-color-studio-overlay-final.trx`：**1568通过、0失败、5跳过**；跳过含授权真实语料opt-in和可选GPU/performance门槛。不能将跳过记通过。第五统一WPF聚焦中`ColorRangePreviewOverlayTests`两项通过（workspace统一17项聚焦全通过）。

授权5张真实JPEG的完整新工具栈（含Details）在≤1600代理实际编码并重新解码：`real-jpeg-tools-final.trx` 1项通过，JSON在`artifacts/color-studio-rebuild-2026-10-05/real-jpeg/REAL_JPEG_TOOL_MATRIX.json`。这些是JPEG源的程序化代理证据，不是整尺寸RAW/细节parity或Release桌面证据。

| JPEG输入 | 1600×1201处理耗时 | PNG精确最大差 | JPEG均差 | JPEG P95差 | JPEG最大差 |
|---|---:|---:|---:|---:|---:|
| DSC04831 |3344.8ms|0|1.3866|4|49|
| DSC04832 |2053.5ms|0|1.0313|3|40|
| DSC04833 |1838.8ms|0|1.0685|3|34|
| DSC04834 |1891.9ms|0|1.0565|3|36|
| DSC04835 |1806.7ms|0|.9512|3|59|

JPEG质量95有损压缩真实细节处最大差高于平滑合成图，是实测值；未把真实JPEG描述为lossless。首张包含warmup成本。PNG校核的是完整处理后的实际编码结果，不只是文件存在。

另发现WPF `Rgba128Float`代表linear scRGB。新适配层显式linear scRGB→encoded sRGB Core→linear scRGB，保留alpha；中性或未变通道保原始float值。新增`ScRgbFloatPixelsAndEquivalentEncodedSrgbUseTheSameToolColors`同色非中性测试，需下一次统一WPF build执行（普通JPEG/TIFF16/RAW既有路由不使用该适配分支）。

## 同版数学全部5张RAW复跑

`real-raw-all-final.trx`：1项通过，运行2m10s；同一当前Core处理版本重新跑全部5张授权ARW，**不拼接初轮优化前记录**。每张7028×4688、真实16位LibRaw主图，点工具完整栈与同中心采样1600proxy的OKLab均/最大差均为0；每张实际派生TIFF16读回均与完整处理RGB48精确相等（U16最大差0），源文件hash再次校验。证据JSON：`artifacts/color-studio-rebuild-2026-10-05/real-corpus-all-final/REAL_RAW_TOOL_MATRIX.json`。

| RAW |1600 proxy|800交互proxy|7028×4688 full|
|---|---:|---:|---:|
| DSC04831 |1356.7ms|267.1ms|18917.1ms|
| DSC04832 |953.7ms|243.6ms|18735.0ms|
| DSC04833 |1060.1ms|245.0ms|18855.8ms|
| DSC04834 |945.8ms|243.3ms|19147.0ms|
| DSC04835 |954.3ms|241.9ms|18491.6ms|

本记录仍是point工具程序化parity；1600proxy各单工具的实际色差/耗时包含Details，但不把它升级为全分辨率空间滤波parity或桌面视觉通过。EXE实机身份及截图以主交付记录为准，以上测试源码身份明确是`5427406…+WORKTREE`。

最终兼容小修补充：未知未来节点类型在Normalize明确拒绝，保留原数据对象，不再静默当无效果节点。`core-final-compatibility-tools.trx`：**23通过/0失败/0跳过**（20个工具/兼容测试+3会话测试）。上面的全Core1568结果早于这1项新增测试，不能把新增测试凭空计入旧TRX。

## 空间尺度缺口已进一步关闭

上文“Details/Film full-vs-proxy未量测”描述是前一轮的时间点，现已由`SPATIAL_SCALE_VERIFICATION.md`及`spatial-final-gate.trx`补足。真实同一7028×4688 RAW分别执行full、1600、800：修复Film seed空间相位、深黑纹理突变、Gaussian扩散尺度，以及Details整数半径/阈值/降噪采样尺度。34项Core通过，真实误差的均值/P95/最大值都有上限且全部通过；不称空间代理逐像素完全相同。Film新显式编辑采用SpatialVersion2，旧snapshot保持1。


本轮最终完整Core gate更新：`core-full-spatial-final.trx` = **1571通过/0失败/6跳过**（50s），已包含空间修复与未知节点检查。WPF最新聚焦中scRGB同色、独立JPEG codec oracle、Film编辑迁移和Undo均通过；具体全WPF总表由主交付记录。

最后源码版本完整Core为 `core-full-final-upright.trx`：**1572通过/0失败/6跳过**（49s），已追加RAW显示坐标/真实100%所需归正helper测试；细节见 RAW_PREVIEW_COORDINATE_CONTRACT.md。此前TRX的计数保留其执行时点，不混算。

## 最终指定的四项状态修复

最终只读检查发现并按要求修复以下四项；它们晚于上面的完整 Core/WPF 执行时点，不借用旧测试结果。

| 风险 | 根因与修改 | 定向回归 |
|---|---|---|
| 停止后又启动 RAW 100% 计算 | `StopProcessing` 只取消已开始的渲染，120ms 待发细节请求仍可启动。现在先取消 `DetailPreview` 请求。 | 待发时立即停止，源图与结果保持代理；随后显式100%可重新请求。 |
| Esc 后异步取样仍写入颜色范围 | 取样前缀重放完成后只核对目标和栈，未核对用户取消。`RangeSelection` 增加取样 revision，取消/清除/切源使旧任务失效。 | 同一 dispatcher 回合发起取样后取消，返回false，样本集合不增加。 |
| 未激活照片的旧版独立 Film 丢失 | 激活照片会迁移旧 Film，但导出/缩略图遇到 `Look=null, Stack=null, Film.Enabled=true` 会直接返回原图。`ColorStudioEffectiveState.Resolve` 统一普通导出、缩略图和 RAW 渲染的有效状态。 | Core RAW像素与 Film 管线相等；WPF两张图中未激活目标的真实缩略图任务与导出、激活后的有效栈像素相等；已有Film节点不重复添加。 |
| 应用色彩方案不能撤销 | `ApplyColorScheme` 直接覆盖栈，没有入历史。先提交当前事务，记录有效旧栈和节点选择，清除Redo后应用方案。 | 旧Look强度37/Film→方案强度81→Undo恢复原Look参数和Film→Redo恢复81。 |

`core-final-effective-state.trx`：**9通过/0失败/0跳过**，覆盖有效状态、RAW产品管线、会话状态。WPF集中构建首次暴露测试直接调用internal方法的编译错误；已改为通过真实工作区缩略图队列验证，未扩大产品public API。

集中WPF初跑48通过/2失败/1跳过：新Film测试用2像素输入，产品缩略图解码成320像素后错比不同位置；改为同320像素宽双色输入并增加尺寸断言，保持精确像素比较。旧批量测试依赖fake渲染器，已改为真实合法Film/Reference与冻结栈像素oracle，处理开始后故意改变其他目标状态以验证冻结语义。最终 `studio-p2-verified.trx`：**50通过/0失败/1跳过**，包括四项新增回归；未设置授权JPEG语料环境的opt-in仍为跳过，其独立授权5图结果见前文。此前完整WPF `1486通过/0失败/12跳过` 先于四项修复，最终50项是补充验证，不将两份计数拼成虚构的全量run。

本节仍是 CODE/程序化 TEST 证据，未声明 Release 桌面实测或用户视觉批准。
