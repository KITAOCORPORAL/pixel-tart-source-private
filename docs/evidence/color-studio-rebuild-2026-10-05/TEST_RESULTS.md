# 本轮测试结果（脱敏摘要）

2026-10-05。日志根为仓库相对路径 `artifacts/color-studio-rebuild-2026-10-05/tests/`。这里仅保留测试名称、计数、原因与相对证据位置；不复制原 TRX 的测试机用户名、绝对临时目录或用户照片。原始日志留在本机 artifacts。

## 计数方法与当前门槛

- 逐条统计 TRX `UnitTestResult.outcome`。部分 MSTest TRX 的汇总 `Counters.notExecuted` 为0，但结果中存在 `NotExecuted`；本表按实际结果列保留跳过，未把总数减去失败后全部算通过。
- 历次日志属于不同源码时点，不加总为一次完整测试，不以文件名中的 `final` 认定成功。
- 完整Core：`core-full-final-upright.trx`，**1572通过 / 0失败 / 6跳过**。
- 最后完整WPF：`studio-wpf-full-final.trx`，**1486通过 / 0失败 / 12跳过**，1498项结果已实际落盘。该次覆盖方向/真实100%/格式/语言/布局等冻结版本；末轮随后增加的4项P2回归不冒充已含在这份全套内。
- 末轮4项P2（RAW细节请求停止、Esc取消迟到范围取样、未激活旧Film目标批量输出、应用方案undo）及相关回归：`studio-p2-verified.trx` **50通过 / 0失败 / 1跳过**，51项、34秒。同次 `color-studio-rebuild-2026-10-05-wpf-build-p2-verified.log` 编译0错误、33条MSTEST0037断言风格建议。独立Core有效状态聚焦 `core-final-effective-state.trx` 为 **9/0/0**。这些是后续集中编译与限定回归，不称最后源已重跑全套1486项。
- 最终r1发布已完成并核对身份；Windows真实重放由主任务进行，本文件不评定运行结果。自动WPF、manifest校验和程序化真实照片计算不替代RELEASE_RUNTIME；逐项状态以 RELEASE_RUNTIME_REPLAY.md 为准。
- `USER_VISUAL_REVIEW=NOT_APPROVED`、`VisualApproved=false`、`UserVerified=false`。

## 最终r1构建身份（只读核验）

| 字段 | 核验结果 |
|---|---|
| SourceHead / 核验时HEAD | `31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`，相等 |
| 实现提交 | `11379a66072ba88cce61dc1d16efd097262680cd`；随后31d3ac3仅移除4处文件末尾空行 |
| BuiltAt | `2026-10-05T13:08:11.6769967+08:00` |
| 配置 | Release / x64 / win-x64 / SelfContained=true |
| EXE（仓库相对） | `artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish/win-x64/KitaoPhotoSelector.exe` |
| EXE SHA256 | `C9841FB530ABF0E209B8E9BB9B6870C917D54DED985702B2EF41F36EE6DB28CA` |
| DLL ProductVersion（实际文件读取） | `2.3.0+31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591` |
| DLL SHA256 | `DD5BFB88219EC853DD328AA00743BE4519C0A259E19C2016467239948420BCD3` |
| manifest（仓库相对） | `artifacts/releases/color-studio-rebuild-2026-10-05-r1/release-manifest.json` |
| manifest SHA256 | `DDFE6381BD897F2BF7A997144E71E143A10CD26ED2F3CA38A10D4BAA7A9BAE21` |
| 身份记录 | 本目录 `RELEASE_IDENTITY.json` 与manifest、实际EXE/DLL全部一致 |
| 全发布文件核对 | manifest 287条、实际287文件；逐条长度/SHA256核对：0缺失 / 0不匹配 |
| 发布后production差异 | `git diff SourceHead -- src` 与 `-- tests` 均空 |
| 发布日志 | `artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish.log`，实际包含最终publish输出；本机保留且被忽略，不复制原机器路径 |

核验不改变发布文件、manifest、JSON或运行矩阵。工作树未跟踪内容为阶段文档与允许跟踪的manifest；publish二进制已受现有忽略规则保护。自动测试仍保留前述完整套件/末轮聚焦的不同执行时点，不冒称这些TRX在r1发布后二次执行。r1只为后续同一EXE实机重放提供明确身份，不能单凭hash一致升为运行PASS。

## 已执行日志计数

以下表由实际TRX结果核对，不把先前失败删除。P/F/S分别为Passed/Failed/NotExecuted。

### Core 与处理链

| TRX | P | F | S | 执行范围 / 时点 |
|---|---:|---:|---:|---|
| `color-studio-tools-core.trx` | 9 | 1 | 0 | 初版新工具；查出无用OKLab往返破坏纯白精确恒等 |
| `color-studio-core-regression.trx` | 40 | 0 | 0 | 第一阶段工具/栈/兼容聚焦 |
| `color-studio-core-regression-v2.trx` | 43 | 0 | 0 | 增补范围与兼容回归 |
| `color-studio-core-regression-v3.trx` | 44 | 0 | 0 | 工具、栈、旧Film/RAW回归 |
| `color-studio-core-regression-final.trx` | 46 | 0 | 0 | 后续聚焦，不代表最终全套 |
| `color-studio-tool-optimization.trx` | 17 | 0 | 0 | 数学预计算与诊断内存优化 |
| `color-studio-tools-final-math.trx` | 18 | 0 | 0 | 解析Levels暗部精度、曲线LUT与工具数学 |
| `studio-session-core.trx` | 13 | 0 | 0 | 会话3项及原栈10项 |
| `core-final-compatibility-tools.trx` | 23 | 0 | 0 | 20工具/兼容及3会话，含未知未来节点拒绝 |
| `core-final-effective-state.trx` | 9 | 0 | 0 | 末轮有效Film状态Core聚焦，与WPF P2单独计数 |
| `core-full-color-studio-rebuild.trx` | 1564 | 0 | 4 | 初轮完整Core，早于后续新增测试 |
| `core-full-color-studio-final.trx` | 1567 | 0 | 5 | 中间完整Core |
| `core-full-color-studio-overlay-final.trx` | 1568 | 0 | 5 | 加独立颜色范围观察层后的完整Core |
| `core-full-spatial-final.trx` | 1571 | 0 | 6 | 空间V2和兼容后的完整Core |
| `core-full-final-upright.trx` | 1572 | 0 | 6 | 最后完整Core，包含RAW float归正 |

### 球面、映射与真实照片数值

| TRX | P | F | S | 执行范围 / 限制 |
|---|---:|---:|---:|---|
| `space-surface-core.trx` | 5 | 0 | 0 | 球形正逆变换、相机/拾取、软边/alpha/非法值与切片 |
| `space-core-regression.trx` | 45 | 0 | 0 | ColorSpace与晚间反馈Core回归 |
| `space-wpf-first.trx` | 11 | 0 | 0 | 既有映射/viewport/分析行为 |
| `space-surface-wpf-final.trx` | 8 | 0 | 0 | 新4项实际绘制、viewport3、analysis1 |
| `real-raw-optimized-tools.trx` | 1 | 0 | 0 | 优化后的真实RAW计算中间记录 |
| `real-raw-optimized-interactive.trx` | 1 | 0 | 0 | RAW交互代理中间量测 |
| `real-raw-final-math.trx` | 1 | 0 | 0 | 同图point栈full/proxy及实际TIFF16回读 |
| `real-raw-all-final.trx` | 1 | 0 | 0 | 全部5张授权ARW用同版数学重新执行，原hash未变 |
| `real-jpeg-tools-final.trx` | 1 | 0 | 0 | 全部5张JPEG完整工具栈代理实际PNG/JPEG编码回读 |
| `spatial-before.trx` | 1 | 0 | 0 | 诊断量测通过；**不表示当时Film/Details误差已合格** |
| `spatial-after-first.trx` | 1 | 0 | 0 | 中间空间修复诊断量测 |
| `spatial-synthetic-first.trx` | 31 | 0 | 0 | 工具/Film/新尺度兼容合成验证 |
| `spatial-final-gate.trx` | 34 | 0 | 0 | 实际full/1600/800误差预算+20工具+11旧Film+2尺度/兼容 |

实际照片的计算参数、耗时、同中心采样和回读误差详见 PROCESSING_IMPLEMENTATION.md 与 SPATIAL_SCALE_VERIFICATION.md。RAW源为16位sRGB解码后的float，并非sensor域；TIFF是明确派生测试。JPEG质量95是有损压缩，PNG/TIFF精确回读与JPEG误差分开记录。

### WPF 布局、工具与批次

| TRX | P | F | S | 执行范围 / 解释 |
|---|---:|---:|---:|---|
| `studio-filmstrip-state.trx` | 5 | 0 | 0 | 初版胶片条/同步/保存状态 |
| `studio-state-regression.trx` | 55 | 1 | 0 | 原同步测试缺活动源前置，后续补正确源并保留类别断言 |
| `studio-state-layout-focused.trx` | 31 | 5 | 0 | 低矮主图区、0宽误折栏、隐藏页测试容器缺陷 |
| `studio-wpf-full-first.trx` | 1455 | 7 | 11 | 前轮冻结binary，另含新图标规则和旧XAML方向断言失败 |
| `studio-state-layout-second.trx` | 48 | 3 | 1 | 首次工具空栈DeepClone/事务问题；非全部通过 |
| `studio-state-tool-third.trx` | 14 | 0 | 1 | 工具、缩略图、保存、同步、文件像素 |
| `studio-batch-state-final.trx` | 55 | 1 | 0 | 500图缩略图作业字典并发失败 |
| `studio-overlay-thumbnail-fixed.trx` | 17 | 0 | 0 | 范围叠加2、工具5、状态8、500图/30切图2；并发修复复测 |
| `studio-wpf-full-second.trx` | 1474 | 1 | 12 | 剩旧活动导出测试依赖假后端染红；后改真实冻结栈像素oracle |
| `studio-format-locale-layout-final.trx` | 12 | 1 | 1 | 格式/语言等通过；JPEG旧最大误差阈值6遇实际7 |
| `studio-final-changes-focused.trx` | 29 | 1 | 1 | 独立codec oracle等通过；首曝光位置/紧凑面板仍失败 |
| `studio-compact-panel-fixed.trx` | 4 | 1 | 0 | 第一次紧凑修复尚未满足首曝光可达位置 |
| `studio-compact-layout-measure.trx` | 1 | 1 | 0 | 实际测得WB168DIP、曝光Y270；胶片条主图区面积通过 |
| `studio-compact-panel-verified.trx` | 7 | 0 | 0 | 父子组/启停/复位/折叠/紧凑位置、语言4、busy/Fit与胶片条 |
| `studio-latest-final-focus.trx` | 40 | 0 | 1 | RAW100%、八方向、格式4、语言、映射、Film、批次和布局 |
| `studio-wpf-full-final.trx` | 1486 | 0 | 12 | 第13次集中编译的全套已结束；源时点早于末轮P2 |
| `studio-p2-final-focused.trx` | 48 | 2 | 1 | P2首次执行，两处测试oracle错误；保留失败 |
| `studio-p2-verified.trx` | 50 | 0 | 1 | 4项新缺陷回归、Filmstrip状态8、RAW冻结输出、BatchExport等；不合并计入旧全套 |

P2首次编译 `color-studio-rebuild-2026-10-05-wpf-build-p2-final.log` 有1个错误：测试直接调用internal方法不可见；已改测试调用边界并重新编译。该失败编译没有测试结果，不计为PASS。随后48/2/1中的失败是 `InactiveStandaloneFilmExportsAndThumbnailsMatchActivatedEffectiveStack`（2px fixture被缩略图放大，原尺寸oracle不适用）及 `InactiveTargetsExportTheirFrozenProcessedPixelsNotActiveEditorPixels`（旧假后端染色oracle不再代表真实有效栈）；分别改为同尺寸实际像素比较和真实冻结栈oracle，再执行50/0/1。未删除生产断言或放宽像素合同。

## 跳过原因

末轮P2的唯一跳过是 `AuthorizedRealJpegSamplesRecordActualEncodedFullStackPixels`：此限定聚焦未开启真实授权JPEG opt-in。它已有独立 `real-jpeg-tools-final.trx`，不能把独立执行混入P2通过数。

### 最后完整Core：6项

| 测试 | 实际未执行原因 | 后续独立证据 |
|---|---|---|
| `AuthorizedFullRawSpatialEffectsRecordSameCoordinateScaleError` | 实际授权空间证据需显式opt-in | 已在 `spatial-final-gate.trx` 独立执行；仍保留本全套SKIP |
| `AuthorizedRealRawCorpusRecordsNewToolPixelsAndDerivedTiff16` | 真实授权RAW语料需显式opt-in | 已在 `real-raw-all-final.trx` 独立执行；不冒充本全套执行 |
| `CpuGpuParityAcrossEightSceneClasses` | 本机无已验证GPU compute设备 | 未执行；本轮不宣称GPU管线通过 |
| `CpuGpuParityOnSyntheticScene` | 同上 | 未执行 |
| `FilmCpuPerformance_WritesMeasuredEvidenceWhenRequested` | 未设置专用性能证据开关 | 未执行该门槛；空间记录另有实际计时，不等价替代 |
| `ProductionResolutionBenchmarkWritesThreeRepeatsWhenEnabled` | 未开启受限V4三次production-size benchmark | 未执行；不构造性能PASS |

### 最后完整WPF：12项

| 测试 | 实际未执行原因 |
|---|---|
| `CompanyRealRawProductAcceptanceGate` | 此专用门槛的真实相机fixture目录未配置；不表示本轮其他授权RAW语料不存在 |
| `ReviewSheets_ComposeHonestProductionEvidenceWhenRequested` | 未配置该专用review evidence路径 |
| `ThreeSamplesOfPublicBatchCommandsAgainstFresh10128Fixture` | 专用1万图诊断需新合成fixture和输出目录 |
| `AuthorizedRealJpegSamplesRecordActualEncodedFullStackPixels` | 全套未开启授权JPEG opt-in；已另在 `real-jpeg-tools-final.trx` 实测 |
| `ProductionApp_AllRoutes_RenderReviewInventory` | 全软件路由视觉inventory未在本限定回归启用 |
| `ValidateExistingRunAcceptsLatestCapturedP2RunWithoutChangingIt` | 无此只读集成probe需要的已捕获P2 run root |
| `PremiumPrototype_ThreeRealPages_RenderReviewSet` | opt-in原型视觉证据未启用 |
| `ProductionPeers_KeyStatesAndReferenceFamily_ExposeRealContracts` | opt-in全软件peer证据未启用 |
| `BaselineGuard_RequiresExplicitApproval` | 无此测试要求的已批准baseline；不能自动批准 |
| `CurrentReferenceWorkspaceRendersAtFourScalesAndNarrowResolution` | 未设置独立本次输出目录；最终DPI仍待实际运行 |
| `FilmEffects_WriteTruePerEffectComparisonWhenRequested` | 未设置专用Film视觉证据开关 |
| `ProductionCompositionAndToolCatalog_RealAppLoadedAndNavigated` | opt-in整应用启动证据未启用 |

## 已失败问题如何处理

| 失败/缺陷 | 修复与回归依据 | 仍然不能推出的结论 |
|---|---|---|
| 中性BasicTone纯白浮点漂移 | 无颜色变化时跳过无意义OKLab往返；精确恒等测试和后续Core通过 | 不等于所有真实照片效果用户满意 |
| 源码方向/可见性断言及低窗面积 | 改响应逻辑；实际加载控件测量取代过时方向字符串；紧凑空间分配重测 | 不等于全部Windows DPI实机通过 |
| 空调整栈与历史版本字段 | 修复DeepClone/事务前置与版本恢复，单节点启停/组复位/undo行为回归 | 不等于跨重启保存undo操作日志 |
| 缩略图作业并发 | 使用并发容器、取消/完成身份保护；500图回归通过 | 合成微图速度不等于真实RAW图库速度 |
| 活动/非活动导出不一致风险 | 逐目标冻结完整栈/Film/引擎；有效Film状态、同尺寸缩略图与实际导出oracle；P2为50/0/1、Core为9/0/0 | 不以旧假后端颜色证明实际导出正确；最终EXE实机仍独立 |
| JPEG旧单点阈值失败 | 保留均差预算，加入独立WIC质量95对完整处理结果编码的逐像素oracle；未直接放宽常数掩盖错误 | JPEG仍非无损；真实细节最大误差单独记录 |
| Film纹理相位和Details尺度 | V2参考坐标、连续半径/阈值、亚像素bilateral、旧V1兼容；真实空间预算通过 | proxy与full不是逐像素完全相同 |
| EXIF方向丢失、RAW100%只放大代理 | float主图和WIC loader各自一次归正；真实源几何/按需full/cache；实际八方向和高频buffer测试通过 | 必须最终EXE重放方向、100%、导出与交互 |

详尽文件、算法及过程见各模块记录。最终Release的用户界面结果单独进入 IMPLEMENTATION_MATRIX；本文件到此只报告已执行自动/程序化验证。
