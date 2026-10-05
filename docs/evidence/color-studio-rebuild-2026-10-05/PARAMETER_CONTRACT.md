# Color Studio 参数契约（2026-10-05）

本表来自本轮 `ColorStudioToolCatalog`、`ColorStudioToolProcessor` 与兼容节点源码。它描述实现合同，不证明 C1 内部算法、Release 实机通过或用户视觉批准。CODE / TEST / RELEASE_RUNTIME 仍分别见 IMPLEMENTATION_MATRIX 与实际日志；USER_VISUAL_REVIEW=NOT_APPROVED。

## 1. 共用保存与处理规则

- 新参数写入 `ColorAdjustmentStack.Nodes[].NumericParameters[键]`，节点类型保存在 `Type`，身份 `Id`，启停 `Enabled`；不建立另一套 UI 私有调整状态。新增工具显式采用 `ProcessingVersion=2`。旧文件缺省为1，旧整数枚举值和旧 Develop 数学保留。未知未来节点类型明确拒绝加载，不静默渲染为恒等或抹掉保存参数；`UnknownFutureNodeIsRejectedInsteadOfSilentlyDiscardingItsEffect`验证此边界。
- `ColorStudioToolCatalog.Value` 拒绝 NaN/无穷并夹到表中范围。ViewModel数值输入提交前验证，错误保留旧栈；组复位删除该组的键，使其回到表中默认值；双击滑杆恢复该参数默认。启停保留数值，重开项目沿用保存值。
- 共用测试：`EveryNewToolDefaultIsExactFloatIdentityAndDisabledNodesPreserveIt`、`EveryEffectControlChangesItsIntendedFixtureAndResetRestoresIdentity`、`InvalidParametersAreRejectedAndCancellationInterruptsPixelWork`；WPF `StudioToolEditingTests` 检查事务撤销、关闭节点后编辑、数值拒绝及分组复位。测试案例名称不等于实机结果。
- 新节点默认插入顺序：输入解释 → 白平衡 → 基础/HDR（旧Develop同阶段）→ 参考匹配 → 色域/肤色/平衡 → 色阶/曲线 → 细节 → Film → 编码。已有节点保留数组顺序；用户移动后按保存顺序执行，不以工具页顺序覆盖。预览、批量与输出冻结同一份栈。
- 持久RGB缓冲是 **display-referred encoded-sRGB float**，并非相机scene-linear数据。WB/影调显式转换到线性RGB，范围/平衡/肤色/细节进入OKLab；然后返回float sRGB。工具内的输出色域压缩及最终编码边界见 PROCESSING_IMPLEMENTATION。`WorkingSpace=OKLabD65`指颜色节点计算空间，不表示缓冲本身是Lab。
- 全局工具受其节点前全部结果影响；局部范围只改变其节点输入的成员。观察用球面、统计、选区叠加不写入调整栈。空间滤波在代理/全图不同尺寸下不可声称逐像素完全相同。

## 2. 每组算法、测试与实际用途

| 节点/组 | 算法作用域与边界 | 用途实例 | 专项自动验证 |
|---|---|---|---|
| WhiteBalance | 解码后linear RGB相对通道指数增益；冷暖与绿/洋红分别控制。不是Kelvin或相机as-shot温度 | 中性灰偏蓝时少量增暖；不宣称RAW白点重新解码 | WhiteBalanceUsesRelativeDecodedColorAndNeutralTintHasOppositeDirections |
| BasicTone/基础曝光 | 曝光2^EV加连续高光shoulder；亮度为保端点bend；对比以中间调为中心反向bend | 曝光+.3EV提亮暗图；亮度+15主要改变中调 | BasicToneRampsRemainOrderedAndControlsHaveDistinctScopes |
| BasicTone/HDR | 原linear Y连续区域权重；高光/阴影与更窄的白/黑端权重分开。有界显示域，不恢复已剪切传感器数据 | 高光-25压亮天空，阴影+20抬暗部；纯黑端点保留 | 同上；真实RAW/JPEG矩阵列参数及差值 |
| BasicTone/全局颜色 | OKLab a/b相对缩放；自然饱和度按原chroma减弱高饱和区作用 | 饱和度-10降低整体鲜艳度；vibrance+15抬低饱和色 | 全控件效果/复位、灰阶、有限输入测试 |
| ColorBalance | 全局及阴影/中调/高光连续权重的a/b向量，纯黑白端淡出 | 阴影方向225°强度8，作冷暖分离 | ColorBalanceUsesIndependentTonalWeightsAndProtectsEndpoints |
| Levels | encoded sRGB输入黑白归一→gamma→输出黑白映射；先RGB后各单通道，输入黑<白、输出黑≤白 | RGB gamma1.1；仅红gamma改变红通道 | LevelsPreserveUneditedChannelsAndRejectCrossedEndpoints；LevelExtremeGammaKeepsAnalyticDarkPrecisionAndCurveTableMatchesAnalyticCurve |
| Curve | 真实控制点，shape-preserving Hermite；先RGB再各通道；允许非单调创意形状但不越段端值；4096段查值近似 | RGB中点从.5移.55；单红通道曲线改变红输出 | CurvesPassThroughPointsStayWithinSegmentsAndSupportAddedDeletedPoints；curve LUT误差；WPF点编辑撤销 |
| Details/清晰度、锐化 | OKLab L不同尺度unsharp；阈值、±.15 L限制；radius以长边1600为参考。负清晰/结构为平滑 | 清晰度+10增强局部对比；锐化20/半径1仅提升边缘 | DetailsReduceNoiseWithoutErasingStepAndSharpenDifferentFromDenoise |
| Details/降噪 | L与a/b独立bilateral混合，5×5参考尺度采样、双线性亚像素、连续影调权重；非AI重建 | 亮度降噪20、颜色降噪30降低合成噪声 | 上项测平场方差与阶跃保持；full/proxy量测见SPATIAL_SCALE_VERIFICATION |
| SkinTone | OKLab hue角范围+羽化，低chroma/暗端/亮端保护；按强度靠近目标L/C/h。没有人物检测，背景近肤色也可能命中 | 橙肤色范围中心45°，色相均匀度20；先检查照片选区 | SkinUniformityProtectsGrayAndBlueAndMovesSampleTowardDeclaredTarget |

## 3. 新工具逐参数目录

目录行均按第1节同键保存/默认复位；单位“相对值”不是物理量或百分比承诺。曲线默认五点只是初始状态，完整可变控制点保存合同见下一节。

| 节点 | 父组 | 参数/保存键 | 合法范围 | 默认 | 单位 | 键盘步进 |
|---|---|---|---|---|---|---|
| WhiteBalance | 白平衡 | 冷暖偏移 / `temperature` | -100…100 | 0 | 相对值 | 1 |
| WhiteBalance | 白平衡 | 绿－洋红 / `tint` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | 基础曝光 | 曝光 / `exposure` | -5…5 | 0 | EV | 0.1 |
| BasicTone | 基础曝光 | 亮度 / `brightness` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | 基础曝光 | 对比度 / `contrast` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | HDR 影调 | 高光 / `highlights` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | HDR 影调 | 阴影 / `shadows` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | HDR 影调 | 白场 / `whites` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | HDR 影调 | 黑场 / `blacks` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | 全局颜色 | 饱和度 / `saturation` | -100…100 | 0 | 相对值 | 1 |
| BasicTone | 全局颜色 | 自然饱和度 / `vibrance` | -100…100 | 0 | 相对值 | 1 |
| ColorBalance | 色彩平衡 · 全局 | 色相 / `master_hue` | 0…360 | 0 | ° | 1 |
| ColorBalance | 色彩平衡 · 全局 | 强度 / `master_amount` | 0…100 | 0 | % | 1 |
| ColorBalance | 色彩平衡 · 阴影 | 色相 / `shadows_hue` | 0…360 | 0 | ° | 1 |
| ColorBalance | 色彩平衡 · 阴影 | 强度 / `shadows_amount` | 0…100 | 0 | % | 1 |
| ColorBalance | 色彩平衡 · 中间调 | 色相 / `midtones_hue` | 0…360 | 0 | ° | 1 |
| ColorBalance | 色彩平衡 · 中间调 | 强度 / `midtones_amount` | 0…100 | 0 | % | 1 |
| ColorBalance | 色彩平衡 · 高光 | 色相 / `highlights_hue` | 0…360 | 0 | ° | 1 |
| ColorBalance | 色彩平衡 · 高光 | 强度 / `highlights_amount` | 0…100 | 0 | % | 1 |
| Levels | 色阶 · RGB | 输入黑点 / `rgb_black` | 0…0.99 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · RGB | 输入白点 / `rgb_white` | 0.01…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · RGB | 中间调 / `rgb_gamma` | 0.1…5 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · RGB | 输出黑点 / `rgb_out_black` | 0…1 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · RGB | 输出白点 / `rgb_out_white` | 0…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 红 | 输入黑点 / `r_black` | 0…0.99 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 红 | 输入白点 / `r_white` | 0.01…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 红 | 中间调 / `r_gamma` | 0.1…5 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 红 | 输出黑点 / `r_out_black` | 0…1 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 红 | 输出白点 / `r_out_white` | 0…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 绿 | 输入黑点 / `g_black` | 0…0.99 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 绿 | 输入白点 / `g_white` | 0.01…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 绿 | 中间调 / `g_gamma` | 0.1…5 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 绿 | 输出黑点 / `g_out_black` | 0…1 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 绿 | 输出白点 / `g_out_white` | 0…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 蓝 | 输入黑点 / `b_black` | 0…0.99 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 蓝 | 输入白点 / `b_white` | 0.01…1 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 蓝 | 中间调 / `b_gamma` | 0.1…5 | 1 | 相对值 | 0.01 |
| Levels | 色阶 · 蓝 | 输出黑点 / `b_out_black` | 0…1 | 0 | 相对值 | 0.01 |
| Levels | 色阶 · 蓝 | 输出白点 / `b_out_white` | 0…1 | 1 | 相对值 | 0.01 |
| Curve | 曲线 · RGB | 0% 控制点 / `rgb_y0` | 0…1 | 0 | 相对值 | 0.01 |
| Curve | 曲线 · RGB | 25% 控制点 / `rgb_y1` | 0…1 | 0.25 | 相对值 | 0.01 |
| Curve | 曲线 · RGB | 50% 控制点 / `rgb_y2` | 0…1 | 0.5 | 相对值 | 0.01 |
| Curve | 曲线 · RGB | 75% 控制点 / `rgb_y3` | 0…1 | 0.75 | 相对值 | 0.01 |
| Curve | 曲线 · RGB | 100% 控制点 / `rgb_y4` | 0…1 | 1 | 相对值 | 0.01 |
| Curve | 曲线 · 红 | 0% 控制点 / `r_y0` | 0…1 | 0 | 相对值 | 0.01 |
| Curve | 曲线 · 红 | 25% 控制点 / `r_y1` | 0…1 | 0.25 | 相对值 | 0.01 |
| Curve | 曲线 · 红 | 50% 控制点 / `r_y2` | 0…1 | 0.5 | 相对值 | 0.01 |
| Curve | 曲线 · 红 | 75% 控制点 / `r_y3` | 0…1 | 0.75 | 相对值 | 0.01 |
| Curve | 曲线 · 红 | 100% 控制点 / `r_y4` | 0…1 | 1 | 相对值 | 0.01 |
| Curve | 曲线 · 绿 | 0% 控制点 / `g_y0` | 0…1 | 0 | 相对值 | 0.01 |
| Curve | 曲线 · 绿 | 25% 控制点 / `g_y1` | 0…1 | 0.25 | 相对值 | 0.01 |
| Curve | 曲线 · 绿 | 50% 控制点 / `g_y2` | 0…1 | 0.5 | 相对值 | 0.01 |
| Curve | 曲线 · 绿 | 75% 控制点 / `g_y3` | 0…1 | 0.75 | 相对值 | 0.01 |
| Curve | 曲线 · 绿 | 100% 控制点 / `g_y4` | 0…1 | 1 | 相对值 | 0.01 |
| Curve | 曲线 · 蓝 | 0% 控制点 / `b_y0` | 0…1 | 0 | 相对值 | 0.01 |
| Curve | 曲线 · 蓝 | 25% 控制点 / `b_y1` | 0…1 | 0.25 | 相对值 | 0.01 |
| Curve | 曲线 · 蓝 | 50% 控制点 / `b_y2` | 0…1 | 0.5 | 相对值 | 0.01 |
| Curve | 曲线 · 蓝 | 75% 控制点 / `b_y3` | 0…1 | 0.75 | 相对值 | 0.01 |
| Curve | 曲线 · 蓝 | 100% 控制点 / `b_y4` | 0…1 | 1 | 相对值 | 0.01 |
| Details | 清晰度 | 清晰度 / `clarity` | -100…100 | 0 | 相对值 | 1 |
| Details | 清晰度 | 结构 / `structure` | -100…100 | 0 | 相对值 | 1 |
| Details | 锐化 | 锐化强度 / `sharpen` | 0…100 | 0 | 相对值 | 1 |
| Details | 锐化 | 参考半径 / `radius` | 0.5…4 | 1 | px / 1600 | 0.1 |
| Details | 锐化 | 边缘阈值 / `threshold` | 0…0.2 | 0.01 | 相对值 | 0.005 |
| Details | 降噪 | 亮度降噪 / `luma_noise` | 0…100 | 0 | 相对值 | 1 |
| Details | 降噪 | 颜色降噪 / `chroma_noise` | 0…100 | 0 | 相对值 | 1 |
| SkinTone | 肤色范围 | 范围中心 / `center_hue` | 0…360 | 45 | ° | 1 |
| SkinTone | 肤色范围 | 色相范围 / `hue_width` | 1…90 | 35 | ° | 1 |
| SkinTone | 肤色范围 | 羽化 / `feather` | 1…60 | 20 | ° | 1 |
| SkinTone | 肤色均匀化 | 色相均匀度 / `hue_uniformity` | 0…100 | 0 | 相对值 | 1 |
| SkinTone | 肤色均匀化 | 色度均匀度 / `chroma_uniformity` | 0…100 | 0 | 相对值 | 1 |
| SkinTone | 肤色均匀化 | 明度均匀度 / `lightness_uniformity` | 0…100 | 0 | 相对值 | 1 |
| SkinTone | 肤色目标 | 目标色相 / `target_hue` | 0…360 | 45 | ° | 1 |
| SkinTone | 肤色目标 | 目标色度 / `target_chroma` | 0…0.3 | 0.1 | 相对值 | 0.005 |
| SkinTone | 肤色目标 | 目标明度 / `target_lightness` | 0…1 | 0.65 | 相对值 | 0.01 |

### 可变曲线控制点

每个通道 `c=rgb/r/g/b` 单独保存 `c_count`（整数2…16，缺省5）、`c_xN` / `c_yN`（0…1）。X严格递增，首X=0末X=1；Y允许非单调。缺省X/Y均为N/(count−1)。UI支持新增、移动、删除内部控制点；端点X不可删除。通道复位必须删除该通道count和全部x/y键。禁止把保存点减少为固定五个UI数值而丢弃旧点。

## 4. 保留节点与参数

这些是Pixel Tart原有能力；同名旧Develop与新BasicTone不偷换数学。旧节点参数仍可查看、复位、启停并保存在原位置。下列旧控件的步进是现有UI契约；未统一定义的项明确写“连续”，不捏造C1步进。

### 参考匹配 ReferenceMatch

| 参数/保存键 | 范围/默认 | 单位/步进 | 真实作用与复位 | 测试/案例 |
|---|---|---|---|---|
| 总强度 `match_strength` | 0…100 / 100 | %/连续 | V3 ReferenceLookMatcher变换权重，0精确恒等；复位回参考方案值 | StageIVColorCore/ReferenceMatch；将参考强度降至50 |
| 影调 `tone_strength` | 0…100 /50 | %/连续 | 参考影调CDF混合 | 同上，降影调保原光比 |
| 颜色 `color_strength` | 0…100 /70 | %/连续 | OKLab色彩统计匹配权重 | 同上，控制整体仿色 |
| 对比 `contrast_strength` | 0…100 /50 | %/连续 | 匹配变换对比权重，不是基础对比 | 同上 |
| 饱和 `saturation_strength` | 0…100 /50 | %/连续 | 匹配饱和权重，不是全局饱和 | 同上 |
| 肤色 `skin_protection` | 0…100 /60 | %/连续 | 肤色权重保护，非肤色均匀化 | 同上 |
| 高光 `highlight_protection` | 0…100 /70 | %/连续 | 亮端保护 | 同上 |
| 中性 `neutral_protection` | 0…100 /65 | %/连续 | 低chroma保护 | 同上 |
| 保原影调 `keep_original_tone` | bool /false | 开关 | 保护原片影调；同时保存Look与节点键 | keep-tone/0% parity测试 |

参考图作为只读统计来源；多参考权重及选参考管理由ReferenceLookStore保存。V4 Beta不是新工具全栈实现，遇不支持组合明确拒绝；不能自动忽略节点或以V3输出冒充V4。

### 颜色范围 ColorRange

| 参数/保存键 | 范围/默认 | 单位/步进 | 算法与用途 |
|---|---|---|---|
| `Samples` / `NegativeSamples` | RGB24数组 /空 | 取色、增删 | OKLab a/b距离，正样本最大权重×(1−负样本最大权重)；在节点输入采样 |
| `strength` | 0…100 /100 | %/连续 | 完整范围调整权重，0恒等 |
| `range` | UI0…50 /12；存0….5 /.12 | OKLab a/b距离×100/连续 | 距离内核心；数学最小.001 |
| `softness` | UI0…50 /8；存0….5 /.08 | 距离×100/连续 | smoothstep羽化，数学最小.001 |
| `hue` | -180…180 /0 | °/连续 | a/b旋转；按成员权重平滑混合 |
| `saturation` | -100…100 /0 | 相对%/连续 | range_version2为相对chroma缩放 |
| `chroma` | -100…100 /0 | 相对值/连续 | version2为最多±.1 OKLab绝对chroma，有中性淡出；旧v1保留乘法含义 |
| `lightness` | -100…100 /0 | L×100/连续 | 选中区OKLab L增加，夹0…1 |
| `keep_original_luminance` | bool /false | 开关 | 实际保OKLab感知明度L，不是linear Y |
| `lightness_min/max` | 0…1 /0、1 | OKLab L/.01 | version2成员明度限制，min≤max |
| `lightness_feather` | .001….5 /.05 | OKLab L/.01 | 亮度边界外smoothstep淡出 |
| `range_version` | 1或2 /旧缺省1，新2 | 数据版本 | 不作为用户效果滑杆；决定兼容数学 |

Core `NewColorRangeSeparatesRelativeSaturationFromAbsoluteChromaAndKeepsFeatherContinuous` 和 `SelectionMaskUsesFloatMembershipExcludesTransparentPixelsAndNeverMutatesInput`；WPF `ColorRangePreviewOverlayTests` 检查真实前缀节点输入、RAW master、直方图/图像/输出不被观察叠加替换、清除及切图取消。选区为最大768代理，不宣称全分辨率逐像素选区；RAW proxy保留float后计算成员。普通图（含高位深TIFF）的选区诊断代理当前降至RGB24；不宣称它保留完整输出位深。

### 旧 Develop

| 保存键 | 范围/默认 | 单位 | 原算法/复位组 |
|---|---|---|---|
| `exposure` | -3…3 /0 | EV | 旧linear Y曝光硬边界；明度组 |
| `brightness` | -100…100 /0 | 相对值 | 旧含义=±1EV附加量；明度组 |
| `highlights/midtones/shadows` | 各-100…100 /0 | 相对值 | 原Y连续权重的±1EV；明度组 |
| `contrast` | -100…100 /0 | 相对值 | Y围绕.18线性缩放；对比与端点组 |
| `whites/blacks` | 各-100…100 /0 | 相对值 | 原亮/暗端Y偏移；对比与端点组 |
| `structure/detail` | 各-100…100 /0 | 相对值 | 原box blur unsharp；结构与细节组 |

原`EveningFeedbackCoreTests`及本轮`OldEnumValuesAndLegacyDevelopMathematicsAreUnchanged`保兼容。组复位移除原键；未把旧影调换算成新工具而更改旧照片。案例：重开旧方案仍得到原Develop输出。

### Film 与 Creative

字段保存于节点 `FilmSettings`，同时兼容旧独立Film快照；执行 `PixelTartFilmPipeline`。所有数值有限且0…100，Seed非负int；无独立步进定义的旧滑杆连续。

| 字段 | 默认 | 用途/数学 | 复位与验证 |
|---|---|---|---|
| Enabled | false | 关时恒等；保存其他值 | 旧Film快照迁移/启停/Undo WPF测试 |
| ProfileId / ProfileAmount | PT-N01 /100 | 自有中性、暖调、冷银灰linear RGB偏移及强度 | PixelTartFilmTests；不称实物胶片模拟 |
| GrainAmount / GrainSize |0 /35 | 确定性坐标噪声，seed与尺寸控制 | 颗粒/seed测试；V2已量测full/800/1600，见SPATIAL_SCALE_VERIFICATION |
| HalationAmount |0 | 高光mask扩散形成暖边 | 局部扩散/非硬红覆盖测试 |
| BloomAmount |0 | 高光柔化扩散 | 相邻像素响应测试 |
| VignetteAmount |0 | 径向亮度衰减 | 角部/中心差异测试 |
| SurfaceAmount |0 | 程序表面强度 | 纹理强度/确定性测试 |
| TextureId / TextureAmount |None /0 | FineFiber/Paper/SoftMist/Scanline程序纹理 | 纹理频谱/确定性测试 |
| Seed |17 | 相同值确定性重现；Re-roll才改变 | seed保存/重开一致 |
| SpatialVersion |旧缺省1；显式编辑2 | V2归一图像坐标连续grain/texture及尺度一致扩散；V1保旧数学 | JSON兼容/对齐中心相位/真实full代理量测 |

节点复位恢复默认FilmSettings并保节点身份；Film Preset浏览应用写同一份Film参数，右Film编辑它，Creative接已有纹理。导出包含Film节点；本轮补`FilmAfterNewToolsCreatesARealNodeAndEnableUndoRemainOneState`行为验证。下游证据用实际测试名/结果，不以此表代替TRX。

### 预设 / 过渡 / 观察与输出

- Preset键 `preset_strength`：0…1默认1（UI0…100）；真实支持`exposure/contrast/saturation`，旧曝光作用OKLab L。Adobe XMP导入不代表所有Adobe字段执行，未支持字段保持明确边界。Hover只预览、Commit入栈、离开还原；`AdobePresetHoverStrengthCommitAndUndoTests`。
- TransitionBlend `amount`：0…1默认.25（UI0…100）；a/b最多减少12%，不宣称空间融合算法。复位.25；`TransitionBlend_DoesNotBlurTextureBetweenAdjacentPixels`。
- Fit、100%、Pan、3D旋转、范围观察、直方图通道、原图/结果切换、评级/色标筛选：观察或元数据操作，不写像素节点。它们的参数/运行证据在3D与布局矩阵，不把“没有像素变化”当缺陷。
- 复制/同步冻结选定类别/节点参数，不能复制评级/色标/EXIF；批事务另有Undo/Redo。导出冻结各目标栈；同图同分辨率PNG/TIFF测试差0，JPEG编码容许量化误差；RAW始终解码后处理为输出图，不伪称重新写可编辑RAW。

## 5. 明确未宣称完成的边界

C1实机行为仍由主任务记录，当前数学是Pixel Tart自主明确实现，不是C1私有算法复刻。未实现画笔/径向/渐变空间蒙版、人脸检测、AI降噪、scene-linear RAW高光恢复、任意输出ICC选择；没有这些能力的同名空控件。真实肤色/逆光等对照、full/proxy细节/Film空间尺度与所有DPI交互必须看同一新Release证据；自动数值通过不能替代它们。用户视觉批准始终留给用户。
