# Color Studio 处理链与参数现状审计

日期：2026-10-05
核对源码：`5427406c4ef3c805ef56fbb814251df69365032d`
范围：只读源码与已有测试审计；本文件不代表新功能实现、测试执行、C1 实测或本轮 Release 验收。
状态：`CODE=审计完成`；`TEST=本审计未运行`；`RELEASE_RUNTIME=NOT_RUN`；`USER_VISUAL_REVIEW=NOT_APPROVED`。

仓库根目录及祖先目录未找到额外 `AGENTS.md`。本轮仅新增本审计文件；未修改产品代码。

## 1. 已有真实调用链

| 环节 | 当前实现 | 需要保留的契约 | 当前边界 / 风险 |
|---|---|---|---|
| 输入 JPEG / PNG / TIFF | `Services/StudioQuickExport.cs`：`Load`，WIC 解码并保留像素格式；有 ICC 时转换到 sRGB；大于 32 bpp 经 RGB48 转换并恢复独立 alpha | 原图只读，ICC 只解释一次；不能重建另一套加载器 | 无配置的输入按 sRGB；不支持任意工作色域或用户可选输出 ICC；WIC render intent 未显式公开 |
| RAW 输入 | Core `RawMatchTiff16ProductPipeline.DecodeMasterAsync/DecodeFrozenMasterAsync`；LibRaw ProfessionalDecode，要求真实 RGB48、sRGB，保存源 SHA | 保留冻结主图、源 SHA 检查、16 位 TIFF 原子写入 | 主图是已解码 display-referred sRGB，不是 scene-linear camera RAW；不能将现有曝光宣称为传感器域恢复 |
| UI 参数 | `TetherReferenceModeViewModel` 与 `.Develop.cs` → `ChangeStack`；参数写 `ColorAdjustmentStackNode.NumericParameters` 或 `FilmSettings` | 一个栈，节点顺序、启停、选中节点和 Undo transaction 保留 | 现有参数大多只有 Slider + TextBlock，缺统一可验证数值输入规范 |
| 预览 | `RenderAsync` 冻结 look/stack/film；revision + asset 校验；普通图 → `ColorStudioBitmapRenderer.Render`；RAW → `RawMatchTiff16ProductPipeline.Render` | latest-wins、取消、最后有效帧、缓存键包含完整冻结状态 | Develop `ChangeStack` 每次立即 `RenderAsync`，未走 100 ms debounce；快速拖动可能不断启动整图处理 |
| 字节 / 浮点 | `ColorStudioBitmapRenderer` 根据源 bpp 分流；Core `ColorStudioRenderPipeline.Render` 有 `VisualPixelBuffer` 与 `HighBitDepthImageBuffer` 两个重载 | 最终输出必须使用同一节点语义 | 8 位路径每个节点输出 RGB24 再交给下一个节点；RAW/TIFF 路径 float 全栈后量化；多节点存在精度分叉 |
| 方案存储 | `ColorStudioSchemeStore` 原子替换 `color-studio-schemes.json`；`ColorStudioSchemeSerializer`；`ColorAdjustmentStack.Normalize` | 旧 Version=2、WorkingSpace=`OKLabD65`、旧 enum 整数及键名不得静默改义 | Normalize 只允许版本 2；新增算法应有显式节点语义版本，不可把旧参数套新数学改变旧工程 |
| 分析 | `ReferenceColorWorkspaceViewModel.Analysis`，显示原图或 MatchedImage → `AnalyzeHistogram`，120 ms 合并，revision 校验，4 项缓存 | 统计对象与屏幕图像一致，旧任务不得覆盖新图 | 分析降至 RGB24，alpha 不参与；每次统计还构造 source/reference/matched 三份相同数据模型，有可精简成本 |
| 3D | `ColorStudioColorSpace`：规则网格真实采样，OKLab，坐标含 SourceX/Y；`ColorSpaceLinking` 最近点/簇/像素成员 | 不把视图旋转和 selection overlay 写入栈 | 映射为 OKLab 欧氏半径硬边界；颜色范围节点却使用 a/b 二维距离 + 羽化，两个定义不同；需 UI 明确或统一可选定义 |
| 快速导出 | `ReferenceColorWorkspaceViewModel.ExportAsync` 冻结各 target 的 look/film/stack；普通图经 `ProcessForExportAsync`；RAW 经同主图管线 | 非 active target 不借当前 active 参数；取消/逐张失败/临时文件清理；不覆盖已有文件 | PNG/TIFF 保容器和位深，JPEG 95；当前无导出格式/ICC 用户选择；不能把源格式沿用说成完整输出配置 |
| 发布配方 | `PreparePublishingTargetsAsync` 冻结各 target，先处理成暂存图再交 Publishing | 发布继续独立负责配方/尺寸/命名 | 新节点必须自动进入原处理栈；不能在快速导出单独补算法 |

### 当前颜色编码

`HighBitDepthImageBuffer` 明确注释为 **display-referred、非线性 sRGB float RGB，0..1，无 alpha**。`ColorAdjustmentStack.WorkingSpace="OKLabD65"` 表示颜色节点的感知计算空间，不表示持久缓冲区本身是 OKLab，也不表示全管线 scene-linear。Develop/Film 内部解码线性 sRGB，ColorRange/Preset/Transition 内部转 OKLab，然后每节点返回非线性 sRGB。

`ColorPipelineDescriptor` 和 `PrecisionTrace.ProfessionalDefault` 目前是通用标签，后者包含固定的 SOURCE:16-bit、V4、TIFF 阶段，即使实际输入/节点并非如此。新证据应根据本次真实输入和已执行节点记录，不直接把固定标签当观测结果。

## 2. 当前 UI 参数 → 数学 → 数据键

以下描述是源码行为，不是 Capture One 算法。

| UI / 默认 / 合法 UI 范围 | 保存键及真实数学 | 已有测试及不足 | 拟修改范围 |
|---|---|---|---|
| 曝光：0，-3…3 EV | Develop `exposure`；Y 乘 `2^EV`，再按原 RGB/Y 等比恢复颜色，硬裁 0…1 | `EveningFeedbackCoreTests.DevelopNeutralIsExactAndEveryControlChangesPixelsWithSharedPrecisionMath`；只验证 +0.7 和像素变化 | 保留旧节点数学；新语义曝光与输出 shoulder 分离，补双向、近白、近黑、可恢复性测试 |
| 亮度：0，-100…100 | `brightness`；与曝光直接相加 `exposure + brightness/100`，因此本质为 ±1 EV | 现测试只能证明变化，不能证明与曝光有独立摄影意义 | 新版本定义中间调亮度曲线并保护端点；不得悄悄改变已有值 |
| 高光：0，±100 | `highlights`；`smoothstep(.45,.95,Y)` × ±1 EV | 未覆盖正负端、渐变连续、近白肤色偏色 | 增加柔和压缩/扩展定义，与 whites 分清职责 |
| 中调：0，±100 | `midtones`；`4Y(1-Y)` × ±1 EV | 同上 | 保留兼容值；定义图示及参数作用域 |
| 阴影：0，±100 | `shadows`；`1-smoothstep(.02,.45,Y)` × ±1 EV | 暗部噪声放大、黑位保护无专项证据 | 与 blacks 分离，补黑色/近黑彩色及局部对比 |
| 对比：0，±100 | `contrast`；线性 Y 围绕 .18，系数 `1+contrast/100` | -100 将 Y 归一到 .18；+100 黑白硬裁明显；非摄影 S 曲线 | 新版本定义保端点 S 曲线或明确线性行为，不能与 Preset 对比混称一致 |
| 白场：0，±100 | `whites`；加 `value/100*.25*smoothstep(.6,1,Y)` | 未验证高光肩部 | 新语义与 HDR 高光分离；测试近白梯度不平铺 |
| 黑场：0，±100 | `blacks`；加 `value/100*.15*(1-smoothstep(0,.2,Y))` | 正向会把纯黑变灰；符合抬黑但缺说明 | 保留抬黑用途，补纯黑/中性和色相测试 |
| 结构：0，±100 | `structure`；Y 减 box blur(Y)，radius=短边×1%；叠回 Y | 同尺寸像素一致测试；缺真实边缘 halo 与 proxy/full 差异 | 需要全分辨率尺度参数；公开副作用和作用半径 |
| 细节：0，±100 | `detail`；半径固定 1 像素亮度 unsharp mask | 小图/大图同一数值作用尺度不同，负值平滑不是独立降噪 | 拆清晰度、锐化与降噪；不能更名当前负值为智能降噪 |
| 仿色强度 / 影调 / 颜色 / 对比 / 饱和度 | ReferenceMatch `match_strength` / `tone_strength` / `color_strength` / `contrast_strength` / `saturation_strength`；`ReferenceLookMatcher.BuildTransform` | V3 0% identity、保护、单图 parity 已有 | 保留为参考匹配，不拿来替代基础曝光/白平衡 |
| 肤色保护 / 高光保护 / 中性保护 / 保原影调 | `skin_protection` / `highlight_protection` / `neutral_protection` / `keep_original_tone`；参考匹配权重 | `StageIVColorCoreTests`、ReferenceMatch 系列 | 保护不是肤色均匀化；新增肤色工具需真实独立参数 |
| 范围强度：100，0…100 | ColorRange `strength` /100 | 有选中/非选中输出测试 | 补连续调节/0精确恒等与边缘 feather |
| 范围：12，0…50；柔和：8，0…50 | UI/100 保存 `range` / `softness`；两者 math 最小 .001；a/b 平面距离；正取样 max × (1-负取样max)；smoothstep羽化 | `ColorStudioStackTests`、`EveningFeedbackCoreTests`；缺多个范围重叠、skin/neutral保护 | 显式说明忽略 L；考虑独立明度限制和中性保护；UI 0 与 math .001 契约须统一 |
| 色相：0，±180° | `hue`；a/b 按 selection 权重旋转 | 缺角度 wrap、近中性、边缘连续测试 | 复用 OKLab；避免新 HSL 暗换处理空间 |
| 饱和度 / 色度：0，±100 | `saturation`、`chroma`；两者乘成 `max(0,1+s/100)*max(0,1+c/100)` | 两控件数学作用等价，易双倍放大 | 合并产品入口或明确独立新语义；保留旧两键兼容 |
| 明度：0，±100；保留明度开关 | `lightness` /100 加 OKLab L；`keep_original_luminance` 实际保 OKLab L，而非线性 Y | 现测试允许 OKLab L误差 .012 | UI 应称“保留感知明度”；新参数避免术语混用 |
| 过渡融合：25，0…100 | TransitionBlend `amount` 0…1，只减少 a/b 最高12%，没有空间模糊/相邻颜色过渡运算 | `TransitionBlend_DoesNotBlurTextureBetweenAdjacentPixels` | 保留旧节点；产品文案不得宣称复杂过渡修复 |
| XMP预设强度：100，0…100 | Preset `preset_strength`；仅 consumption `exposure/contrast/saturation`；曝光是在 OKLab L 上乘2^EV，不同于 Develop | `AdobePresetHoverStrengthCommitAndUndoTests`；部分字段 import可能存而不执行 | 必须列每个 XMP字段兼容性；禁止所有导入字段都标已支持 |
| 胶片启用 / 方案 / 强度 | Film `FilmSettings.Enabled/ProfileId/ProfileAmount`；PT-N01中性、PT-W01线性RGB偏移、PT-C01偏移 | `PixelTartFilmTests` identity及roundtrip | 保留已有自有方案，不宣称实物胶片模拟或C1复刻 |
| 颗粒 / 尺寸 / 重新生成 | `GrainAmount/GrainSize/Seed`；坐标确定性噪声，受明度调节 | 有seed/连续性/纹理测试 | 当前proxy全图像素坐标不同，需定义尺度与种子采样契约 |
| 暖光晕 / 高光柔化 / 暗角 | `HalationAmount/BloomAmount/VignetteAmount`；高光mask卷积/径向亮度衰减 | 有局部扩散、暖边、取消测试 | 半径有2…8像素夹限；proxy/full物理尺寸不一致须量化 |
| 材质 / 表面强度 / 纹理不透明度 | `TextureId/SurfaceAmount/TextureAmount`；程序纹理×两强度乘积 | 有确定性与无短周期测试 | 第二强度在旧胶片页面部分区域缺Slider；新布局应一处真实控制 |
| 全节点启停 / 复位 / 删除 / 排序 / 命名 / 保存 / 撤销 | `Enabled`、节点数组次序、Normalize、`ChangeStack`、`SchemeStore` | `ColorStudioStateClosureTests` 真实 VM 行为测试较多 | 需新增跨 target undo隔离、多同类型同步与旧方案新版compat测试 |

Develop 所有参数均为零时返回原浮点 clone，具备精确恒等。ReferenceMatch 0%也有明确恒等分支；Preset/Transition/ColorRange 零效果仍可能经过 OKLab 往返，float exact identity 未全部保证。`NumericParameters.Normalize` 会拒绝非有限值，但合法范围只由零散 UI/math clamp控制，VM setter 对 NaN 的 `Math.Clamp` 仍得到NaN后Normalize异常；没有统一“拒绝输入并保留上一有效值”的产品契约。

## 3. 已定位的可靠性缺口

这些为源码推导；本审计未执行复现测试，不写运行 PASS。

1. **按类别同步不能处理多个同类型节点。** `ColorAdjustmentStack.SyncSelectedByTypeFrom` 使用 `selected.ToDictionary(node => node.Type)`。允许复制的 ColorRange/Presets 有两个时即重复 key；目标包含同类型多个节点时会用同一 incoming ID替换多个，Normalize unique ID再失败。所需：按类型分组替换、保留源类别内顺序、未选类别保留；补两个ColorRange + 两个Preset测试。
2. **RAW V4 分支忽略完整栈。** `TetherReferenceModeViewModel.RenderAsync` 在 `_frozenRawMaster` 下先执行 V4 session，未应用stack或film；快速导出仅当 Stack=null 且 Film未启用才走 V4，否则普通 RAW栈走V3。所需：明确不支持组合时禁用与提示，或将V4变为真实ReferenceMatch节点执行器；不能一个preview一个export版本。
3. **跨照片撤销历史可能污染。** `ApplyTargetSnapshot` 不清理/切换 `_undoStacks/_redoStacks`；`UndoAdjustment` 无 target身份。所需：按 target储存历史或切target显式清理并说明边界；测试A编辑→B编辑→Undo后B不获得A栈。
4. **Develop 连续拖动立即整图render。** `SetDevelop → ChangeStack → RenderAsync`；没有debounce。取消保障结果版本，但大量已启动工作仍分配数组。所需：一套统一交互render调度，停止拖动后高质量收敛；量测峰值/停止响应。
5. **中性节点的float恒等不全面。** 新节点应全中性快速退出；多种旧节点应先测旧语义再版本兼容，不能把不易肉眼发现误差称“exact identity”。
6. **RGB24节点间量化。** WPF普通照片bpp≤32走byte重载；高位深走float，8位输入并不意味着中间栈可以每步量化。所需：共同float执行路径、输出适配器最后量化；新节点全同语义，旧节点版固定旧输出需要明确兼容策略。
7. **不必要的analysis成本。** `ColorStudioBitmapRenderer`每次计算完整`VisualAnalysisEngine.Analyze`（含palette/sort），即使没有ReferenceMatch；后续`RefreshPreviewAnalysisAsync`还分析一次。所需：冻结reference transform所需统计、无match时跳过完整分析、只对最新结果做必要诊断。
8. **alpha诊断错误。** Renderer恢复alpha，但分析buffer/3D无alpha，完全透明像素的隐藏RGB被计数和映射。所需：诊断使用显式valid-pixel mask；按alpha权重或排除0明确说明；输出alpha独立保持。
9. **批量同步未接编辑Undo。** `SyncSelectedColorNodes/ApplySelectedAdjustments/CopyCurrentLookTo`直接改target snapshots，不见工作区批操作历史。所需：批事务记录被改目标前后栈，Undo不可写rating/tag/关系。
10. **基础编辑数学不够成熟。** Develop在每节点末端裁Y及RGB；Brightness与曝光重复；结构/细节尺度随分辨率不同；Preset曝光语义不同。即使已有“每个控件改变像素”测试通过，也不能证明效果可控、对照片合理或proxy/full parity。

## 4. 缺失能力与最小兼容扩展建议（待设计确认）

不更改旧 enum 值和旧 Version=2文件含义。可在节点尾部增加可选 `ProcessingVersion`，默认为Legacy1；新增工具节点/显式新版Develop使用2。旧文件未写该值仍走旧数学；新方案保留完整参数和版本。旧节点转新版需明确动作和可撤销，不自动迁移外观。方案Catalog若必须升版，应支持读取v2并原子备份，未知节点须报不支持，不能默默当identity。

| 工具组 | 当前缺失 | 建议真实节点语义 | 必须验证 |
|---|---|---|---|
| 白平衡 | 没有温度/色调工具或节点 | 从已解码sRGB线性RGB进行显式白点适应；温度用相对mired偏移或明示“解码后白平衡”，色调沿相应白点轴；新节点默认identity | 灰卡、肤色、近黑白、温度单调、正负范围、保存/恢复；不能声称RAW as-shot温度真实性 |
| 基础曝光 | 已有legacy Develop | 新版参数定义exposure EV、保端点brightness、可控contrast、global saturation；尽量float累积，在规定输出/色域边界裁切 | 灰阶阶梯、黑白端点、±EV、局部色相、零位、复位、preset差异 |
| HDR | legacy smooth masks | 明确定义高光/阴影/白黑的范围、连续mask和有界rolloff；不使用参考匹配strength替代 | 过渡导数、近饱和高光、暗部噪声、肤色与中性保护 |
| 基础/高级颜色范围 | 已有取样a/b范围，缺色相扇区、L限制 | 复用ColorRange节点与Samples；新版本扩展hue/chroma/L范围、feather、保护；用同一selection weight生成mask和输出 | 色相wrap、边界内外、多个取样、负取样、羽化连续、不选中精确不变 |
| 肤色用途 | 仅保护和普通取样 | 明确用户取样肤色及目标L/C/h；均匀化强度分别控制Hue/Chroma/L，不是人物检测 | 多肤色、混光、灰轴、背景近肤色、0identity、强度连续 |
| 色彩平衡 | 无 | master/shadows/mid/highlight的OKLab chroma向量；明示平滑区域权重；避免无依据复刻C1内部算法 | 中性与区域权重、总强度、极端值、预览/导出同链 |
| 色阶 | 无用户RGB/单通道节点 | 黑/白输入点、中间gamma、输出黑/白点；验证Black<White，端点有界 | 三通道隔离、恒等、black==white非法输入、单调性、16bit精度 |
| 曲线 | 只有参考匹配内部tonecurve | 独立Curve节点；保存实际控制点；单调插值模式或明确允许反转模式；RGB与单通道 | 点增删排序、端点、插值不过冲、自由曲线与合法域、保存/撤销 |
| 细节 | legacy unsharp，无降噪 | 独立锐化强度/radius/threshold、亮度与色度降噪；先明确低成本可控算法和边缘保护，不标AI | 平场噪声方差、边缘对比、halo、色噪/亮噪隔离、proxy/full尺度 |
| 局部调整 | 颜色范围节点，没有画笔层/空间蒙版 | 先保留有真实像素语义的颜色选区；如加空间蒙版，保存尺寸无关坐标和mask处理版本 | 位置、缩放平移、羽化、复制/删除、导出坐标、旧项目兼容 |
| Film / Creative | 已有独立Film与纹理 | Film Preset浏览套用仍写同一FilmSettings；右侧Film改参数；Creative仅呈现实际实现的材质/效果 | preset→参数→Undo一致、种子冻结、同图导出一致 |

### 顺序方案

新栈默认顺序可为：输入解释 → 白平衡 → 基础曝光/HDR → 全局色彩/色彩平衡 → 色阶/曲线 → 参考匹配 → 局部色域 → 细节 → Film/Creative → 输出变换。**实际执行必须依保存的数组顺序**；工具分类不能每次根据UI点击顺序悄悄重排。旧栈保持原顺序。用户显式移动节点后所有预览、缓存、直方图、输出都对同一冻结revision重算。

为降低语义风险，先定义公共参数描述（键名、单位、范围、默认、节点类型、processing version），VM与UI读取描述，Core独立校验；不要让范围仅写在XAML。新节点的 identity、range validation、cancel、存储roundtrip先建立，再绑定滑块。曲线点不可拼在无约束字符串或临时UI状态内。

## 5. 新测试清单（既有文件保留）

| 风险 | 需要的行为测试 | 可复用的测试基础 |
|---|---|---|
| 全中性与非有限参数 | 每节点float exact identity；NaN/±Infinity拒绝且原状态不变；上下限与边界行为 | `EveningFeedbackCoreTests`、`PixelTartFilmTests` |
| 摄影影调可控 | 灰阶渐变单调、端点连续、±参数、局部ROI测量；不能只Assert输出不同 | `StageIVColorCoreTests`、high precision buffers |
| 色域选择 | 同色不同L、hue边界、softness边界、skin/neutral保护、mask与实际受影响像素同一权重 | `ColorStudioStackTests`、`RuntimeCorrectionCoreTests` |
| 色阶/曲线 | 通道隔离、默认恒等、端点和控制点、非法交叉输入、旧JSON缺新字段 | 新Core测试文件，真实数值oracle |
| 预览/导出 | PNG/JPEG/TIFF16相同解码输入、多个节点栈、proxy→full降采样；mean/max/tone/chroma/边缘报告 | `BatchExportProcessedPixelsTests`、`EveningFeedbackWpfTests` |
| 重启/迁移 | 现存v2方案fixture解析、hash/像素不变，新方案全字段roundtrip、unknown拒绝 | `ColorStudioStackTests.SchemeV2PersistenceTests`、scheme store |
| Undo目标隔离 | A调节、B调节、Undo、回A；节点移动/启停/复位/拖动单事务 | `ColorStudioStateClosureTests` |
| 异步 | 新版参数连续拖动、A/B快速切图、原图/结果切换、取消后旧job晚返回 | `CancelledJobCannotOverwriteNewTargetTests`、`FailedJobCannotOverwriteLaterSuccessTests` |
| 同步 | 多个同类型node、类别顺序、目标私有node、批undo，rating/color/tag/关系保护 | `SelectedTypeSync_CopiesOnlyRequestedAdjustmentCategories`、`CopyApplyAdjustmentsUsesSelectedCategoriesAndProtectsAssetFields` |
| 3D/影调 | alpha=0排除、近灰/黑白/超色域、范围feather、camera变换不改变membership或导出 | `ColorSpaceProxyTests`、`ColorSpaceBoundsFitTests`、`ColorStudioSampleMappingTests` |
| 性能 | 每节点处理耗时、取消延迟、分析与渲染内存峰值；拖动→停止收敛 | 现有30/100目标export与rapid activation tests |

已有 `PreviewExportParityMatrixRecordsJpegTiff16HighPrecisionAndIdentity` 名称不能代表全矩阵：当前代码实际只测合成常色JPEG等分辨率服务比较，TIFF16/high precision/ICC/proxy/V4/真实RAW明确写NOT_RUN。本轮需新增真实覆盖，不能引用名称升级状态。

## 6. 源文件与最近关联提交

Core：

- `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioDevelop.cs`
- `ColorStudioModels.cs`、`ColorStudioRenderPipeline.cs`、`ColorStudioSchemeStore.cs`
- `ColorStudioColorSpace.cs`、`ReferenceColorCoreV2.cs`、`RawMatchTiff16ProductPipeline.cs`、`PixelTartFilm.cs`
- `src/RAWSelectionAssistant.Core/Services/Color/HighBitDepthImageBuffer.cs`、`PrecisionTrace.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs`、`VisualHistogram.cs`

UI与服务：

- `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml`
- `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs`、`.Develop.cs`、`.SchemeInteractions.cs`
- `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs`、`.Analysis.cs`
- `src/RAWSelectionAssistant/Services/ColorStudioBitmapRenderer.cs`、`StudioQuickExport.cs`

关联历史：`e148caf`（Develop/晚间反馈）、`7a87d3b`（identity）、`b61702c`（float pipeline）、`0969763`（XMP）、`6ffdd49`（节点与同步）、`783caf5`（目标冻结状态）。历史提交是定位线索，不是本轮修复或运行证据。

## 7. 审计结论

已有可靠的冻结目标、节点、方案存储、RAW16、真实色域取样、共享渲染和取消基础，应该在这些契约上扩展。主要缺口属于**参数语义、处理中间精度、类别同步、目标撤销隔离、V4组合路径，以及新工具真实处理能力**。双直方图位置和页签布局需与这些状态连接；单纯搬UI不能关闭本任务。

本审计未启动 Capture One，未运行本轮 Release，未检查新的真实照片效果。所有新能力仍须由实现、自动行为测试和本轮同一Release实测分别证明。
