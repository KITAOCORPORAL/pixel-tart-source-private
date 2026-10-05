# 工作区、批次和保存状态实施记录

本文件记录本轮实际修改，不替代 Release 实机证据。源码起点为 `5427406c4ef3c805ef56fbb814251df69365032d`。完整布局与处理算法由同阶段其他记录说明。

## 实际行为和调用链

| 能力 | 修改与根因 | 行为验证 |
|---|---|---|
| 胶片条筛选与多选 | 新 `ReferenceColorWorkspaceViewModel.Filmstrip.cs` 用 `VisibleTargets` 统一筛选、键盘、鼠标与操作范围；Shift 锚点用目标 GUID，避免过滤后索引指错。保留隐藏选中项，但其不参与可见所选的评分、色标、同步、导出；显示隐藏数量 | `ColorStudioFilmstripStateTests.FilteredSelectionRangeAndHiddenTargetsUseOneVisibleOrder` |
| 批次同步 | 活动照片作为源，不算目标。完整、节点、类别同步均深拷贝冻结状态。Core `ColorStudioModels.cs` 按有序类别组替换，修复多个同类型节点重复字典键/重复节点 ID；无关目标节点保持 | Core `ColorStudioSessionStateTests`；WPF `SyncExcludesSourceAndHiddenTargetsAndBatchUndoRestoresTargets`、`SyncRefreshesActiveEditorAndNewProcessingVersionSurvivesSnapshot` |
| 批次撤销/重做 | 新 `.BatchHistory.cs` 只记录 look、调整栈、胶片和引擎；评分、色标、素材关系不进入调整同步/撤销。对当前目标同步后刷新 Editor | 同上；旧 `BatchExportProcessedPixelsTests` 的类别隔离断言保留，补实际活动源图前置条件 |
| 单照片撤销隔离 | 新 `TetherReferenceModeViewModel.History.cs` 保存各目标 undo/redo、选中节点与方案上下文；切图复原对应历史。原实现切图后共享上一张历史 | `PerTargetUndoDoesNotImportPreviousPhotographsAdjustments` |
| 工作文件保存/打开 | 新 Core `ColorStudioSessionStore.cs` 与 VM `.Session.cs`。`.ptstudio.json` 保存多图路径/身份/调整/引擎/选中/筛选/活动目标，原子写入。完整校验后再替换批次。素材库照片评分色标仍从数据库恢复；普通文件标记保存于工作文件。源文件缺失时保留调整和记录 | Core roundtrip、版本/重复/null 身份拒绝；WPF 多图保存重开、原片字节不变、无效文件不替换现场、缺失源保留调整 |
| 冻结导出引擎 | 目标保存 `EngineSnapshot`、`ExecutionModeSnapshot`，快速导出和发布配方调用 `.RawExport.cs`。原代码用当前活动 Editor 引擎决定其他目标的 RAW 导出 | `RawMatchTiff16ProductWpfTests.FrozenRawEngineDoesNotFollowActiveEditorOrSilentlyIgnoreExtraNodes`，其输入为明确合成 RAW 解码器；真实相机输入另见语料证据 |
| 缩略图反映处理状态 | 新 `.Thumbnails.cs`：保留源缩略图、冻结目标调整后重算；250ms 去抖、目标级取消和作业身份校验、最多两项并发。同步/撤销/工作文件重载均通知；不取临时选区或预设 hover 图。提示区分调整结果与失败回退原图 | `AdjustedThumbnailsFollowFrozenLatestStackForActiveAndSyncedTargets` 检查像素、快速覆盖、活动/非活动隔离和源文件未改 |
| 双直方图与响应式测试 | 更新旧版方向测试，测量右侧上下双图与中央照片位置、左四模式/右七页的实际内容、已加载窗口的自动折叠与手动打开。缩放/撤销按钮改为实际 Path 图标 | `ReferenceWorkspaceWideRatioTests`、`RuntimeCorrectionWpfTests`、`EveningFeedbackWpfTests` |

## 明确限制

- 工作文件保存最终状态；进程内撤销日志不跨重启持久化。重开后调整、滤镜和选择恢复。
- 左侧筛选、评分与色标更新不写入任何图像像素。工作文件中资产关联保留 `AssetId`，加载时使用原素材库关系，不能以会话 JSON 覆盖共享评分/色标。
- V4 Beta 当前只支持 RAW 的 ReferenceMatch 调整，不支持本轮完整工具栈/胶片；该组合明确报错，不能静默省略工具或改用 V3。完整工具栈采用稳定 V3。
- 缩略图采用缩小输入经同一处理链计算；局部空间滤镜的采样尺度与全尺寸导出不同，缩略图不是导出像素等同证明。
- 本记录的自动 WPF 是程序行为检查，不等于 Windows 用户实机验收。`RELEASE_RUNTIME=NOT_RUN`、`USER_VISUAL_REVIEW=NOT_APPROVED`，直到本阶段独立记录提供同一新 Release 的实际证据。

## 测试过程（保留失败，不用旧绿色替代最终结果）

日志根为 `artifacts/color-studio-rebuild-2026-10-05/`。

| 本轮运行 | 结果 | 说明 |
|---|---|---|
| `tests/studio-filmstrip-state.trx` | 5 通过 / 0 失败 | 初始状态测试，尚未包含后续3项 |
| `tests/studio-session-core.trx` | 13 通过 / 0 失败 | 新 session3 + 原 stack10 |
| `tests/studio-state-regression.trx` | 55 通过 / 1 失败 | 原同步测试缺活动源；已补真实源前置，保留同步断言 |
| `tests/studio-state-layout-focused.trx` | 31 通过 / 5 失败 | 发现低矮窗口主图过小、初始0宽误折叠、隐藏页面测试取错容器；生产布局与测试前置分别修复 |
| `tests/studio-wpf-full-first.trx` | 1455 通过 / 7 失败 / 11 跳过 | 1473项，9m01s；使用前一轮冻结 binaries，因此包含上述5失败，另有新Unicode图标门禁和旧源码字符串断言。非最终门槛 |
| `tests/studio-state-layout-second.trx` | 48 通过 / 3 失败 / 1 跳过 | 布局、语言、状态通过；新工具首次使用发现空栈被 `DeepClone` 拒绝，修复快照载入及事务开始，保留版本字段 |
| `tests/studio-state-tool-third.trx` | 14 通过 / 0 失败 / 1 跳过 | 新工具事务、缩略图、工作文件、同步与像素一致性聚焦通过；跳过真实私有语料 opt-in 用例 |
| `tests/studio-batch-state-final.trx` | 55 通过 / 1 失败 | 文件名虽含 final，该次未通过，不是最终门槛；500图批量导入发现新缩略图任务字典并发缺陷，已改并发容器/作业取消与完成保护，待复测 |
| `tests/studio-overlay-thumbnail-fixed.trx` | 17 通过 / 0 失败 / 0 跳过 | 第五次集中 Release 构建成功后，新范围叠加2、工具5、状态8、500图导入/30图切换2全部通过；包含前轮缩略图并发失败的复测 |
| `tests/studio-wpf-full-second.trx` | 1474 通过 / 1 失败 / 12 跳过 | 1487项，8m27s。唯一剩余是旧 active export 测试仍期望被假后端染红；现在 Film 升级为真实节点后已经进入共享完整栈。测试改为实际冻结栈像素对比，并保留73强度、Film与节点身份断言；待最后构建复测 |
| `tests/studio-format-locale-layout-final.trx` | 12 通过 / 1 失败 / 1 跳过 | 第七次集中构建。格式3、语言4、处理忙闲布局、活动冻结导出、scRGB边界均通过；一个旧JPEG损失上限为6而新白底合成输出为7，已交处理模块核查，不能称整体通过 |
| `tests/studio-final-changes-focused.trx` | 29 通过 / 1 失败 / 1 跳过 | 第八次集中构建；编码oracle、Film空间版本、数据状态均通过。失败为新紧凑面板首曝光位置；共享Section模板 padding与输入行间距需进一步缩减 |
| `tests/studio-compact-panel-fixed.trx` | 4 通过 / 1 失败 | 已改25DIP局部父/子header，但首曝光仍低于目标位置；保留失败 |
| `tests/studio-compact-layout-measure.trx` | 1 通过 / 1 失败 | 明确测得WB168DIP、曝光滑杆Y270DIP；胶片栏高度与主图面积通过。输入框固定25DIP并减少行/slider间距后再测 |
| `tests/studio-compact-panel-verified.trx` | 7 通过 / 0 失败 / 0 跳过 | 第11次构建0警告0错误；工具真实父子层级/启停/复位/折叠/首曝光可达、语言4、忙闲照片Fit不抖动、胶片栏三窗口高度与折叠返空间均通过 |
| `tests/studio-latest-final-focus.trx` | 40 通过 / 0 失败 / 1 跳过 | 第12次集中构建后包含RAW真实100%请求、方向处理、格式4、参数语言、范围映射、Film、批次/保存、紧凑布局；最后一个RAW缓存预算与uint方向读取小改仍需最后构建纳入 |
| `tests/studio-wpf-full-final.trx` | 1486 通过 / 0 失败 / 12 跳过 | 1498项，8m15s；12:47:29–12:55:45 同一集中构建全套串行执行。包含RAW缓存预算、方向处理、工具密度、格式与语言末轮修改；不包含随后4项定点缺陷修复，后者须另列最终集中构建及回归 |
| `tests/studio-p2-final-focused.trx` | 48 通过 / 2 失败 / 1 跳过 | 后续4项定点修复与受影响路径共51项。新Film测试2像素图被320宽缩略解码放大，无法逐像素比较不同尺寸；旧批次测试假染色后端不再对应真实完整栈。这两处改为同尺寸、真实处理像素oracle，并保留冻结后修改目标的检验 |
| `tests/studio-p2-verified.trx` | 50 通过 / 0 失败 / 1 跳过 | 51项、34s；涵盖停止细节任务、Esc 后迟到取样、未激活旧胶片快照、方案应用撤销，以及胶片条状态、RAW冻结导出、批次导出与同链像素。跳过为私有真实JPEG opt-in；非最终EXE实机证据 |

上表保留本轮失败过程。最后4项定点缺陷修复由最后一行覆盖相关路径，未重跑全套WPF；不能将前一行全量结果与后续聚焦结果相加，或声称最后源码完成了新一轮全套。

2026-10-05 集中构建 `color-studio-rebuild-2026-10-05-wpf-build-final.log` 成功，0错误，32条 MSTEST0037 断言风格建议警告。对应全量 WPF 为1486/0/12；Core `core-full-final-upright.trx` 为1572/0/6（49s）。这些自动结果仍不能升级 `RELEASE_RUNTIME` 或用户批准状态。

末轮首次 `color-studio-rebuild-2026-10-05-wpf-build-p2-final.log` 因新增测试直接调用internal方法而编译失败（1错误），随后改为真实workspace缩略图路径。最后集中构建 `color-studio-rebuild-2026-10-05-wpf-build-p2-verified.log` 为0错误、33条MSTEST0037风格警告；对应 `studio-p2-verified.trx`。源码及测试由集成阶段冻结，后续最终发布身份及实际运行另记。

## 文件清单与类型

- 生产 Core：`ColorStudioModels.cs`（同类/节点同步）、`ColorStudioSessionStore.cs`（工作文件存储）。
- 生产 ViewModel：`ReferenceColorWorkspaceViewModel.cs`、`.Filmstrip.cs`、`.BatchHistory.cs`、`.Session.cs`、`.RawExport.cs`、`.Thumbnails.cs`；`TetherReferenceModeViewModel.cs`、`.History.cs`。共用主文件中另有其他模块集成，最终文件差异以集成提交为准。
- UI 集成小改：`ReferenceColorWorkspaceView.xaml` 缩放/撤销图标与缩略图状态 Tooltip。整体布局由主实施模块负责。
- UI 本地化：`StudioToolPanel.cs`、`StudioCurveEditor.cs`、三份 `Resources/Studio/*.json` 的参数/组标题/曲线操作；`StudioLocalizationTests.cs` 新增真实控件语言切换且栈/自定义名不变验证。
- UI 密度修复：`StudioToolPanel.cs` 每节点一个25DIP父标题（带独立启用开关），单组无重复标题，多组为子Expander，组复位放同一标题行；输入框25DIP，折叠状态保留于控件实例。`StudioToolPanelLayoutTests.cs` 实际点击绑定/展开测量；`EveningFeedbackWpfTests` 新增处理忙闲和胶片条面积测量。
- Core 测试：`ColorStudioSessionStateTests.cs`。
- WPF 新测试：`ColorStudioFilmstripStateTests.cs`。
- WPF 既有测试更新：`BatchExportProcessedPixelsTests.cs`、`RawMatchTiff16ProductWpfTests.cs`、`ReferenceWorkspaceWideRatioTests.cs`、`RuntimeCorrectionWpfTests.cs`、`EveningFeedbackWpfTests.cs`。
- 文档：`WORKSPACE_AUDIT.md`（实施前审计）、本文件（实施过程与状态）。

第五次集中构建后的微型合成图片回归记录：100图导入 81.4ms，补齐至500图 267.0ms，测试进程峰值 300.41MiB；30图快速切换 18.7ms。该输入只用于并发/状态验证，不代表真实 RAW 图库性能。
