# Pixel Tart：2026-10-04 晚间截图仓库只读回读

## 1. 审计边界与仓库现场

本次仅回读仓库、12 张用户截图及已有证据，新增本文档。没有修改功能、测试、旧验收矩阵或用户数据；没有构建、运行测试、启动 EXE、操作桌面或截图。下文“所需修改范围”是后续修复建议，本次未执行。

| 项目 | 核对结果 |
|---|---|
| 实际 Git 工作树 | `N:/pixart/pixel-tart-source-private`，由 `git rev-parse --show-toplevel` 确认 |
| 用户所给逻辑路径 | 当前执行环境未能将该逻辑路径解析为 Git 工作树；后续全部命令明确指定上述实际路径。本文不复写含个人账户名的逻辑路径 |
| 分支 | `integration/pixel-tart-developer-preview` |
| START_HEAD / 审计源码 HEAD | `756541d49bac9498bbee174ef18abe12e5b4e5c0` |
| fetch 后 LOCAL_HEAD | `756541d49bac9498bbee174ef18abe12e5b4e5c0` |
| fetch 后 REMOTE_HEAD | `756541d49bac9498bbee174ef18abe12e5b4e5c0` |
| 起点工作区 | CLEAN；`git status --porcelain=v1` 无输出，无未提交文件需要归属辨认 |
| 同步命令 | `git fetch origin integration/pixel-tart-developer-preview`，复核时再次执行，远端未前进 |
| 写入前复核 | 分支未变，LOCAL == REMOTE，工作区仍干净，本文档原先不存在 |

本文固定记录提交前的源码现场。只含本文档的审计提交 SHA 和提交后 LOCAL/REMOTE 在交付消息中回传，避免为了把文档自己的 SHA 写进文档而不断追加提交。

已读取：

- `artifacts/user-retest/latest/00_START_HERE.md`
- `docs/evidence/human-review/POST_HUMAN_REVIEW_GAP_MATRIX.md`
- `docs/evidence/human-review/POST_VIDEO_RUNTIME_REVIEW_MATRIX.md`
- `docs/evidence/human-review/RUNTIME_CORRECTION_2026-10-04.md`
- r9 Release manifest、已有测试摘要/TRX、r9 运行会话记录及相关源码。

旧 HR 矩阵已经标注 HISTORICAL / SUPERSEDED。最新记录保留四组 RUNTIME=PARTIAL、NOT_READY_FOR_USER_RETEST。00_START_HERE 的准确含义是“已进行部分受限 Release 操作，但完整实机矩阵未通过”，不能解读为“完全未操作”，也不能解读为“已完成验收”。

**VisualApproved=false；UserVerified=false；USER_ACCEPTANCE=NOT_APPROVED。本文不升级任何人工验收状态。**

## 2. 截图来源及 Release provenance

### 2.1 十二张截图的范围

逐张查看了 `G:/UI问题/2/1.png` 至 `12.png`。该目录只有这 12 个 PNG，没有同名构建清单或会话 sidecar。文件时间集中于 2026-10-04 晚间；文件时间只能辅助识别这一批，不能证明拍摄时刻或运行的二进制。

其中 1–7、11–12 是 Pixel Tart 界面反馈；8、9、10 是用户用于说明直方图、影调映射和参数层级的外部界面参考，不能记作 Pixel Tart 已实现功能的运行证据。截图中的批注用于识别本次审计对象，不触发代码修改。

PNG 元数据检查发现 pHYs，没有 tEXt/iTXt/zTXt/eXIf 构建标识。图像尺寸是 PNG 像素大小，部分为裁剪图；不能从尺寸或 pHYs 推断应用窗口尺寸、Windows 缩放或 EXE HEAD。

| 图 | PNG 像素大小 | SHA256 |
|---|---|---|
| 1 | 3840×2160 | `0A461D6682BE70466C4A65253C4D2DACB56DF46386CE246DFDEAD026DF3FB36C` |
| 2 | 3840×2160 | `F611018884D65FEC758F41C8F88BEBD3441415742EAF43CD2B6F89956BBC8824` |
| 3 | 2523×1488 | `5D3BB0035E2953C321C7481430D363D6A786263254D19D0F562F6A4350ED505B` |
| 4 | 3840×2160 | `F3804E10DF391531FFA9D52ACE2CFCD89B7736E2A2E2F4853D628E1F013F5E02` |
| 5 | 1476×1344 | `C9559F40DE84D26E27F50E06B0CBF330268D9FDA42F3F038EF32CF3352C0F6A7` |
| 6 | 2727×2154 | `611E4D315C129EC724404ECB8BFE6BFAA22AE3BA9E24D37796E853E98363E062` |
| 7 | 3840×2088 | `225E5D7A3EE9C234DFF1870C07F9BCC8E19BD55492A84F3FA647AD130596987B` |
| 8 | 387×927 | `42315B0C23CAEF72BF1B2C5AA221FBE17E2D1FEEC8B53D86FC38BAFA0CAAFB97` |
| 9 | 2979×1805 | `79C91371F9B1BE3AEF6A01F49DC388DD12B5615EBD8397E64FB92EE4D49BC436` |
| 10 | 1097×1845 | `2F3C413099B1730F6892A397597557A7F7D9A76BC0694F324B23B716B9B214AF` |
| 11 | 3828×2055 | `EF87A151E483ADAE93CF3A6943E35F1103620A6541936C88CA41F47A582A8C63` |
| 12 | 1122×1962 | `3CA7C0C530B6A6CB76290885470E5A9A0C4804CB25FBBC357D7B745CD830819B` |

截图及素材内容未加入 Git；本文只记录编号、界面事实和哈希。

### 2.2 已核对的候选 Release

| 项目 | 结果 |
|---|---|
| Manifest | `artifacts/releases/runtime-correction-2026-10-04-r9/release-manifest.json` |
| EXE（仓库相对路径） | `artifacts/releases/runtime-correction-2026-10-04-r9/publish/win-x64/KitaoPhotoSelector.exe` |
| Configuration / Platform / Runtime | Release / x64 / win-x64；SelfContained=true |
| Manifest SourceHead | `dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7` |
| 实际 EXE SHA256 | `0A89D816E09EE5C4D19D45F0548AA8402481D7347B3C9D1907A4E906F8C15326`，与 manifest 一致 |
| 实际应用 DLL ProductVersion | `2.3.0+dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7` |
| 发布目录逐文件核对 | manifest 列出的 287 个文件全部存在，逐文件 SHA256 一致，缺失/不匹配=0 |
| SourceHead 等于当前仓库 HEAD | NO：dc2ba3d 与 756541d 不同 |
| 两者间 production source/test 变化 | NO：`git diff dc2ba3d HEAD -- src tests` 为空；完整差异仅文档、证据、证据汇总脚本及 manifest 等 21 个路径 |
| 晚间截图所用 EXE SourceHead | **UNKNOWN** |
| 晚间截图 EXE HEAD 是否等于当前源码 HEAD | **UNKNOWN，不能认定相等或不等** |

EXE 是 .NET apphost，单个 EXE 哈希不能证明业务 DLL 的版本，因此同时读取了应用 DLL 版本并核对整个 manifest 文件表。已知 r9 与当前审计 HEAD 的生产代码相同，不构成重建理由；这仍不能证明用户晚间截图来自 r9。

已有 `artifacts/runtime-correction/r9-runtime-session.log` 记录 20:11 和 20:32 的 STARTUP_OK/ProductSourceSha=dc2ba3d。这是已记录 r9 会话证据，未与这 12 张晚间 PNG 建立可核对关联。不能仅因界面相似、文件时间接近或有旧启动日志而归因于同一 EXE。

## 3. 逐项状态总览

CODE 描述源码事实，不表示 UX 完成。TEST 的“有限覆盖”表示已读到相关测试，缺少针对截图缺陷的验证；本次所有测试执行状态均为 NOT_RUN。RELEASE_RUNTIME 指本次十二图对应问题在可归属的当前 Release 中的操作验证；本次全部 NOT_RUN。用户截图反馈独立保留，不能被这个 NOT_RUN 覆盖或降级。

| 项 | 截图 | CODE | TEST（现有覆盖） | 用户截图事实 / RELEASE_RUNTIME |
|---|---|---|---|---|
| R01 智能文件夹编辑器 | 1 | PARTIAL；尺寸/关闭入口缺口确认 | 有限覆盖，缺可达性验证 | 布局、关闭入口未达要求；NOT_RUN |
| R02 扩展名筛选滚动 | 2 | PARTIAL；滚动故障根因待实测 | 有限覆盖，缺滚动链验证 | 用户报告不能滚动；NOT_RUN |
| R03 多选遮罩 | 3 | CONFIRMED_DEFECT；透明资源被不透明画刷覆盖 | 有限覆盖，缺主题后 alpha 验证 | 大块遮挡图片；NOT_RUN |
| R04 自由画布 Fit | 4 | NEEDS_INVESTIGATION；命令存在，不等于点击有效 | 仅弱 Fit 断言及其他画布行为 | 用户报告点击无响应；NOT_RUN |
| R05 智能文件夹侧栏 | 5 | CONFIRMED_DEFECT；标题样式不统一 | 缺实际样式一致性验证 | 标题字重/层级不同；NOT_RUN |
| R06 文件夹搜索文字 | 6 | CONFIRMED_LAYOUT_CONFLICT | 模型与主搜索宽度覆盖，缺字形裁切验证 | 中文文字上下裁切；NOT_RUN |
| R07 Studio 左右栏 | 7 | REQUIREMENT_CONFLICT；实际编辑左、分析右 | 按旧方向验证，不能批准新方向 | 最新批注要求反向调整；NOT_RUN |
| R08 RGB/影调图 | 7、8、12 | PARTIAL；计算已有，通道视图/图表交互不足 | 有计算、缓存测试，无参考 UI 完整行为 | 8 为参考；12 为现有条图；NOT_RUN |
| R09 影调区间→像素 | 9 | NOT_IMPLEMENTED | 无该端到端行为测试 | 外部参考需求；NOT_RUN |
| R10 调整父子层级 | 10 | PARTIAL；分类/平面节点已有，参考层级未齐 | 有栈/持久化测试，无完整分层操作验证 | 外部参考需求；NOT_RUN |
| R11 快速导出/格式 | 11 | PARTIAL；选目录已有，原格式/格式选择缺失 | 有冻结目标/实际像素测试，无格式选择契约 | 底部入口与新要求不符；NOT_RUN |
| R12 3D 球形及双向映射 | 12 | PARTIAL；笛卡尔 OKLab 与映射已有，球形未实现 | 有数学/相机/预览隔离测试 | 静态高亮不能证明双向交互；NOT_RUN |

下面的复现步骤是根据截图重建的后续重放步骤，本次没有执行。历史提交按相关行 blame/文件 log 区分“引入/调整”和“真正修复”；未找到针对本次缺陷的修复时明确注明。

## 4. 逐项代码回读

### R01 / 图 1：智能文件夹编辑器尺寸、关闭与滚动

**复现步骤**：素材库→智能文件夹→新建或编辑→展开规则/更多选项→在低矮窗口找关闭、保存与取消。

**调用链与文件**：

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.Popups.cs`：`SmartFolderCollection_Click` 生成已有文件夹的选择/编辑与新建入口。
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3SmartFolder.cs`：`NewP3SmartFolderCommand → OpenP3SmartFolderEditor`；`SaveP3SmartFolderCommand → SaveP3SmartFolderAsync`；关闭经 `RequestSmartFolderClose`、未保存保护和 `CloseP3SmartFolderEditorCore`。
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml:1122`：宿主 Border 只有 Row/ZIndex/Margin/Padding，没有针对编辑器的宽高上限和居中尺寸约束；默认拉伸占据 Gallery。该页还在 P3SmartFolderOpen 时折叠三栏工作区。
- `src/PixelTart.Modules.AssetLibrary/AssetSmartFolderEditorView.xaml:14`：外层 ScrollViewer MaxHeight=430，内部 StackPanel 包含标题、规则、更多项和页脚；规则另有 MaxHeight=260 的滚动区。标题无关闭按钮，取消/保存在滚动内容内部，不是固定页脚。

**为何仍能出现**：430 高度只约束内层滚动内容，未约束外部大背景；滚动到底才能到达的取消按钮无法替代持续可见的关闭入口。规则内部/外部滚动的路由还需真实输入复核，不能把“写有 ScrollViewer”当作滚动有效。

**上次相关修复**：`074264c` 简化编辑器层级；`82b7ea6` 增加草稿关闭保护。现存宿主布局行源自 `da8d3444`，430 上限源自 `dc7fedc2`；这些提交没有关闭当前整体几何/顶部关闭缺口。

**测试**：`AssetLibraryP3WpfTests.SmartFolderEditorNoOpSavePreservesCompleteDocumentJsonHashAndResultsAcrossRestart` 覆盖保存/重启文档；`EmbeddedAssetLibraryWpfTests.SmartFolderDraftGuardCancelsDiscardsAndSavesBeforePopupTransition` 覆盖草稿切换；不是标题关闭按钮、固定页脚及 150% 下可达性的证明。

**后续修改范围**：编辑器宿主尺寸、固定标题/关闭/页脚、单一主要滚动区和嵌套滚动路由；复用现有未保存保护和 Query 模型。补内容超高、嵌套规则、关闭取消保存及多个 DPI 的控件行为/Release 验证。CODE=PARTIAL；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R02 / 图 2：扩展名筛选弹层滚动

**复现步骤**：顶部固定“扩展名”筛选→展开条件/历史→在条件树和空白处分别滚轮、拖动滚动条、键盘滚动到末尾。

**调用链与文件**：

- `AssetLibraryPage.xaml:427` 固定筛选按钮→`AssetLibraryPage.Popups.cs:140 PinnedFilter_Click → OpenQuickFilter`。
- `OpenQuickFilter` 只对评分、图片颜色、标签、日期走专用入口；Extension 走通用分支：`EditQuickFilter → OpenFilterPanel`。
- `AssetLibraryViewModel.P3QueryComposer.cs:40` 为共享 P3QueryRoot 查找或添加 Extension 规则，保留同一 Query。
- `AssetLibraryPage.xaml:471` 的 AssetFilterPopover 是 Width=680、MaxHeight=440 的右上覆盖层，内含 ScrollViewer/AssetQueryComposerView。
- `src/PixelTart.Modules.AssetLibrary/AssetQueryComposerView.xaml` 的 StackPanel 内还有建议滚动区和规则 TreeView；`AssetLibraryP3Styles.xaml:413` 的树样式没有解决外层滚动路由。
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Tables.xaml:129` 提供全局 ScrollViewer 模板及命名滚动条。

**为何仍能出现**：扩展名打开的是完整规则编辑器，因此内容容易超过弹层；存在嵌套滚动控件。尚未确定是测量约束、TreeView 吞滚轮还是模板/命中区域的问题。模板 VerticalOffset 的 OneWay 绑定本身不是失效证明，WPF 命名滚动条还有框架事件处理；不能据此直接判共享模板坏了。

**上次相关修复**：`a8e801f` 调整弹层边界；`191bdb0` 建立目前 OpenQuickFilter 分流；`82b7ea6` 修 Query/草稿切换。未找到关闭图 2 滚动故障的专项修复。

**测试**：`RuntimeUserFindingsBatchBTests.QuickFilterPinsSurviveReloadWithoutChangingTheQuery` 检查固定筛选/Query；现有 P3 行为测试不检查 Extension 弹层在指针位于树节点时能否滚到底。

**后续修改范围**：先测滚动 extent/viewport、命中及滚轮路由，再决定局部弹层布局或共享模板修复；保留 Query 单一状态。补树内/外滚动、滚动条、键盘、底部保存入口和低矮窗口。CODE=PARTIAL/根因待核；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R03 / 图 3：素材库多选遮罩挡住照片

**复现步骤**：正常启动并应用主题后，在素材 Gallery 空白区域拖框，保持鼠标按下观察照片。

**调用链与文件**：

- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.cs:364`：`AssetGrid_PreviewMouseLeftButtonDown → UpdateMarqueeSelection → CompleteMarqueeSelection → ApplyMarqueeSelection`；LostMouseCapture 会取消并隐藏。
- `AssetLibraryPage.xaml:821–827`：框选层 ZIndex=6、不可命中，Border 背景为动态资源 Brush.Accent.Subtle。
- `src/RAWSelectionAssistant/Resources/DesignSystem/Colors.Dark.xaml` 原定义带 Opacity=.12。
- `src/RAWSelectionAssistant/Services/AppearanceService.cs:89` 的 `ApplyAccent` 运行时覆盖该资源；`AccentColorService.BlendBrush:168` 用 `Color.FromRgb` 预混背景色，输出 alpha=255、Opacity 默认 1 的画刷。

**确认根因**：主题应用后透明叠加色变成了不透明的深色混合色；矩形虽不可命中，仍会遮住图片。这条代码路径直接解释截图，不是选择集合算法失效。

**上次相关提交**：框选功能 `02050cf4`；资源接入 `ea075b23`；运行时覆盖行 `9fab3447`。未发现之后针对框选 alpha 的修复。

**测试**：`AssetLibraryP2KeyboardSelectionWpfTests.MarqueeGeometryNormalizesReverseDragAndCtrlToggleIsDeterministic` 验证几何/Ctrl；同类测试还覆盖选择同步和键盘。没有验证 AppearanceService 应用后实际资源透明度及照片可见性。

**后续修改范围**：为框选建立明确透明叠加语义，避免直接改共享 Brush.Accent.Subtle 导致其他面板变化；保留边线和选择算法。补真实主题加载后的画刷 alpha、拖动可见性、不同主题回归。CODE=CONFIRMED_DEFECT；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R04 / 图 4：自由画布 Fit 点击无响应

**复现步骤**：三张不同位置图片→明显放大并平移到偏离中心→点击 Fit→比较倍率、中心及全部对象可见范围；另测已经 Fit 的幂等情形。

**调用链与文件**：

- `src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs:53` 的真实 Button 回调是 `Surface.Fit()`；Loaded 和部分排列操作也调用 Fit。
- `FreeCanvasSurface.cs:206`：`Fit(bool selection=false)` 调用 `Editor.Bounds(selection)`，依据 ActualWidth/Height 减 140 的空间求倍率，限制 .03–2，重算 pan，InvalidateVisual、ViewChanged、LoadPreviewsAsync。
- `src/RAWSelectionAssistant.Core/Services/FreeCanvas/CanvasEditor.cs:70`：Bounds 用对象 X/Y/Width/Height；没有将 Rotation 纳入外接边界。
- Surface 放在剩余 DockPanel 的 stage Grid；SizeChanged 刷新绘制，没有持续保持 Fit 状态的机制。

**当前判断**：不是空按钮。截图单帧的 193% 不能证明回调未执行，也不能证明 Fit 成功；Fit 到同一几何状态时可能没有可见变化。旋转外接边界、事件时刻视口尺寸、空/隐含对象、硬编码留白与上限是应检查点；尚不能把其中一个断言为这次点击失败的根因。

**上次相关修复**：Fit 数学实现仍来自 `d9e038c6`；`6f8e86f` 重整工具栏，`206051e` 修画布关闭重入，均不是这次 Fit 失效的专项修复。

**测试**：`CanvasSurfaceInteractionTests.RealSurfaceRoutesDeleteAndRestoresSavedObjects` 调用 Fit 后仅断言 Zoom 为有限数；`RuntimeCorrectionWpfTests.CanvasMenusExecuteClipboardAndShareZoomStateAtAllViewportSizes` 验证菜单/剪贴板/倍率，不证明 Fit 后所有旋转图片位于可用区域。

**后续修改范围**：先在已归属的 Release 捕获点击前后的 zoom/pan/viewport/object bounds，补真实按钮调用和屏幕边界断言；若几何不正确，局部修 Bounds/Fit 与时序，不另建 Zoom 状态。CODE=NEEDS_INVESTIGATION；TEST=覆盖不足/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R05 / 图 5：智能文件夹侧栏样式不一致

**复现步骤**：展开组织侧栏，对比智能文件夹、标签分组、文件夹标题及其 hover/选中状态。

**源码与原因**：`AssetLibraryPage.xaml:572` 的智能文件夹是 Ghost Button，内部两个普通 TextBlock；标签分组和文件夹标题使用 `PixelTart.Type.SectionTitle`。标题字重/视觉层级没有共享契约，截图所见差异仍存在于源码。点击链仍是 `SmartFolderCollection_Click` 的真实菜单，不应为修外观拆掉行为。

**上次相关修复**：`63115dc` 将标题改成可用的集合入口，但没有统一其文字样式。

**测试**：已有组织/智能文件夹数据与 Query 测试可保护选择行为；未找到运行主题下三类标题字重、行高和状态的一致性测试。字符串存在测试不作完成依据。

**后续修改范围**：标题文字/行高/缩进及各状态共享样式，保留集合菜单、编辑、新建和 Query；验证长中文及150%。CODE=CONFIRMED_DEFECT；TEST=缺专项覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R06 / 图 6：文件夹搜索框中文字显示不全

**复现步骤**：文件夹名称搜索框输入中文、英文混合长名，分别观察正常/聚焦态和150%缩放。

**调用链与文件**：

- `AssetLibraryPage.xaml:618`：TextBox 固定 Height=30、MinHeight=30，没有局部 Padding，绑定 OrganizationFolderSearch 并即时更新。
- `src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml:2–5`：隐式 TextBox Padding=10,7，正常边框1、聚焦边框2。
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.InspectorRelations.cs`：OrganizationFolderSearch 更新组织节点的 ApplyNameFilter，保留原组织数据。

**确认的布局冲突**：30 高度扣去上下14内边距和2/4边框，文字可用高度只有14/12 DIP。对该字体/字号的完整中文字形不足，聚焦时更小。截图中的裁切与此一致；本次未实测每个 DPI 的 glyph bounds。

**上次相关提交**：`fea716c1` 新增固定30高搜索框；全局10,7内边距早已存在。后续 `63115dc` 改周边文件夹布局，并未修这两行。

**测试**：`RuntimeUserFindingsBatchATests.FolderNameSearchKeepsAncestorsExpansionAndAssetQuery` 验证筛树；`SearchWidthAndFolderRowsRemainBoundedAcrossLogicalDpiSizes` 实际主要断言 AssetLibrarySearchBox 主搜索框宽度，不检查此文件夹搜索输入框的中文字形。

**后续修改范围**：局部合理高度/内边距与聚焦边框空间，补输入框文字区域测量及真实DPI观察；无需改搜索数据模型。CODE=CONFIRMED_LAYOUT_CONFLICT；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R07 / 图 7：Color Studio 左右栏方向与布局

**复现步骤**：进入参考仿色，分别有/无目标图，载入参考，展开编辑项及参考/分析栏，收窄并折叠。

**当前实现**：

- `src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml:33` LeftRail 是调色、仿色、预设、胶片、输出及真实参数；RightRail（约148行）是 ReferenceNavigator、分析和3D。
- `ReferenceColorWorkspaceView.xaml.cs:403 UpdateResponsiveLayout` 控制左300/320 DIP、右预留空间、中心 star，<900 时左编辑栏变抽屉；右栏折叠仍保留列空间以减少主图跳动。
- 参考 Navigator 为 Height=360、MinHeight=320；已有 Fit/100%/缩放/平移与只读展开查看链；应继续使用既有 `ColorStudioImageViewport`。

**需求方向差异**：截图 7 明确批注将蓝框的右侧内容移到左侧，将红框的左侧编辑移到右侧，同时要求上方直方图。上一轮文字要求恰为“编辑左、参考分析右”，当前代码与该旧要求一致。不能把旧方向测试绿了作为拒绝最新截图的理由，也不能本轮只读审计中擅自交换。后续修复需以确认后的最新方向为准。

截图此时尚无目标图像，分析区显示“尚无目标图像”；参考图已载入不代表目标直方图应有数据。不能将此空态认定为参考缓存污染。

**上次相关修复**：`6f8e86f` 搬真实编辑职责与分析，`4315bc9` 保护主图，`4687b37` 避免紧凑检查覆盖，`64eec3b`/`dc2ba3d` 调整紧凑编辑宽度。最新是宽度修复，不是按截图 7 换方向。

**测试**：`RuntimeCorrectionWpfTests.LeftEditingControlsAndRightAnalysisRemainBoundedAcrossViewportSizes`、`CompactInspectionNeverCoversTargetAndToggleKeepsViewportStable` 检查旧方向控件树和逻辑尺寸。不能代替最新方向决策或Windows实际DPI。

**后续修改范围**：若采用最新截图方向，调整真正控件归属、响应式列/抽屉、导航绑定、折叠行为及对应行为测试；保留同一个 Editor/参数持久化，不复制第二套控件状态。CODE=REQUIREMENT_CONFLICT；TEST=旧方向有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R08 / 图 7、8、12：RGB 直方图和影调比例图

**复现步骤**：有目标和参考→切原图/结果→改强度→切目标→观察分析来源及图表；对照图8的 RGB/R/G/B/L 切换需求。

**调用链**：

- `src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs` 订阅 Editor.SourceImage/MatchedImage/EffectiveViewMode 变化（分析时读取 ShowOriginal）→`SchedulePreviewAnalysis`。
- `ReferenceColorWorkspaceViewModel.Analysis.cs`：取消旧任务、清空旧分析/高亮、120ms合并、revision防过期；选择显示的目标原图或当前结果；按 BitmapSource 身份和 SamplingTier 缓存最多4份。
- `ToVisualBuffer` 将最长边限制768并转 RGB24；`VisualAnalysisEngine.AnalyzeHistogram` 复用计算，`HistogramDrawing`、`ToneZoneDrawing` 复用绘制；XAML右栏绑定 PreviewHistogram 和 Zones。
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs:11`：RGB为编码sRGB的256bin，Luma为线性sRGB Y取整至0–255；11区使用 `min(10,luma*11/256)`。这是量化线性亮度区间，不是11档曝光Zone System。
- 分析标签明确“目标原片/当前仿色预览 · sRGB · 取样尺寸”，不是参考图分析。ToVisualBuffer 的像素格式转换也不等于完整 ICC 色彩变换。

**当前缺口**：`src/PixelTart.Modules.AssetLibrary/HistogramDrawing.cs` 始终画RGB，只有ShowLuma开关，没有图8的单独R/G/B/L模式、参考网格与完整坐标；每个通道按自己的最大bin归一，不能由高度比较不同通道绝对频数。ToneZoneDrawing已有11个青色比例条与百分比，但无灰阶区间hover交互。

**上次相关修复**：`6f8e86f` 实现实时共享分析/绘制；`4315bc9` 对齐所检查图像；HistogramDrawing最近的 `674bed8` 文件变更不能作为通道交互完成证据。

**测试**：`RuntimeCorrectionCoreTests.LiveHistogramUsesSameColorMathAsFullAnalysisAndCancellation`；WPF `AnalysisInvalidatesOnTargetChangeAndMappingDoesNotEditParameters` 和 `InspectionCanAnalyzeResultWithoutChangingViewOrAdjustmentState` 覆盖当前图/缓存/结果/空态。未覆盖图8的通道选择，因为该能力未实现；120ms合并与768代理是实现约束，不是最终Release性能达标数据。

**后续修改范围**：复用现有计算，加清楚的通道/刻度展示契约和一致的归一说明；布局位置随R07确定；补不同内容、缓存失效、空态、快速切图及实际性能。CODE=PARTIAL；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R09 / 图 9：影调区间到图片像素的映射

**复现步骤**：参考图9，在0–X某区hover/选择，期待主图仅临时标出对应亮度区间；切区、移出/Esc清除。

**源码事实**：`ToneZoneDrawing.cs` 只有 Distribution依赖属性和OnRender，没有区间命中、MouseMove/Leave、选择事件。共享 `VisualHistogram` 保存bin和比例，不保存区间成员；当前 `ImageHighlightOverlay` 只接色彩空间的 HighlightedPixels。没有 ToneZone→亮度像素成员→Overlay 调用链。

**为何不能视为已做**：OKLab颜色簇容差不是亮度区间选择；柱状图能显示百分比也不意味着能定位像素。图9是需求参考，不能伪称当前Release已演示过。

**上次相关提交**：`6f8e86f` 增加 ToneZoneDrawing/11区比例；没有该映射功能的修复提交。

**测试**：现有直方图计数/取消、OKLab映射测试不覆盖亮度区间的边界和成员集合。该行为专项覆盖缺失。

**后续修改范围**：复用同一量化线性Y区间定义，增加hit/hover状态、成员计算及preview overlay；处理0/255和区间边界、缩放/平移/分屏/目标切换/取消，保证不写编辑栈或影响导出。是否改为摄影曝光分区是另一个需明确的数学契约，不能静默替换。CODE=NOT_IMPLEMENTED；TEST=缺专项覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R10 / 图 10：调整面板的父子层级

**复现步骤**：按参考图10逐层展开明度、对比、结构、保护/局部子参数，对照当前面板分类和选中节点。

**调用链与文件**：

- `ReferenceColorWorkspaceView.xaml` 顶层五类分段；调色含对比/饱和度等，仿色有强度及保护Expander，另有 AdjustmentNodeList 平面列表和 SelectedAdjustmentNode 对应参数。
- `src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs`：SelectedAdjustmentNode/SetSelectedNumeric/ChangeStack/SyncSimpleFromStack 等操作同一 AdjustmentStack，方案保存及每目标快照继续复用。
- `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs`：实际节点类型为 ReferenceMatch、ColorRange、Film、TransitionBlend、Preset；NumericParameters 是节点参数，不是图10全部分组的产品实现。

**当前缺口**：已有分类不是图10要求的完整可折叠父子参数体系；仅添加父标题也无法创造不存在的亮度/结构等处理能力。应逐个映射现有数学能力与缺少能力，避免把语义不同的仿色强度重命名成曝光/亮度。

**上次相关修复**：`6f8e86f` 调整左右职责；`82b7ea6` 等处理方案交互；`335e36f` 修改栈的复制应用保护。未发现图10完整层级的专项完成提交。

**测试**：`ColorStudioStackTests.SchemeV2PersistenceTests`、`SchemeV2_RoundTripsOrderedNodesAndHash`、`RenderPipeline_PreservesLinearOrderAndDoesNotMutateSource`、`SelectedNodeSync_PreservesTargetSpecificNodes` 覆盖数据顺序/持久化，不能证明父子面板交互。

**后续修改范围**：先确定现有参数到父子组的对应表；界面分组复用原属性/节点，真正缺少的处理项需另立明确范围与持久化/预览导出契约。补折叠、滚动、切节点、重启及单一参数源验证。CODE=PARTIAL；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R11 / 图 11：快速导出入口、格式及目录

**复现步骤**：从顶部寻找快速导出；选JPEG/PNG/TIFF及RAW目标，查看是否可选格式和目录，再核对输出编码与所选目标状态。

**当前调用链**：

- `ReferenceColorWorkspaceView.xaml:170–171` 是底部“快速导出所选/全部”；胶片条ContextMenu也连接同一命令。没有图11要求的顶部入口。
- `ReferenceColorWorkspaceViewModel.cs:194–195`：ExportSelectedCommand/ExportAllCommand→`ExportAsync:608`→冻结各目标Look/Film/Stack→处理→编码。
- 正常路径 `ChooseFolder("选择批量导出目录",null)` 已存在；测试环境覆盖变量及失败重试可复用目录。不能写成“从未支持选目录”。
- 非RAW统一命名为 _仿色.jpg 并 `EncodeJpeg`（QualityLevel=95）；RAW走高精度管线写TIFF。没有输入格式默认保留或用户格式下拉。
- `PreparePublishingCommand → PreparePublishingAsync:99` 是另一路：先对冻结目标生成处理中间PNG/TIFF，再交 OpenPublishing。它与直接JPEG/TIFF快速导出的结果及配置职责不同，不能仅看入口相似就合并掉。

**为何仍能出现**：入口位置和硬编码格式就是当前实现；发布配方存在不等于快速导出具备相同格式选择。

**上次相关修复**：非RAW/RAW分流行 `5d747cc2`；`6f8e86f` 区分快速导出/发布文案与工作流。没有实现“快速导出原格式”的后续修复。

**测试**：`BatchExportProcessedPixelsTests.PublishingReceivesProcessedFrozenTargetsInsteadOfOriginalFiles`、`ColorStudioInactiveTargetUsesFrozenStackAndSamePreviewRenderer`、`ActiveTargetCurrentEditsFreezeWhenExportStarts`、`CancelBatchKeepsCompletedFileAndCleansUnfinishedFile` 保护像素/目标/取消；不覆盖缺失的格式选择、顶部入口或每种格式的编码契约。

**后续修改范围**：明确顶部入口与选中集，给快速导出复用合适编码/Publishing能力的轻量格式选择；保留冻结目标、取消、失败重试。JPEG/PNG/TIFF输入的“原格式”还需位深/alpha/ICC定义；RAW不能承诺把调整后的图写回原RAW格式，应明确TIFF等输出。CODE=PARTIAL；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

### R12 / 图 12：3D点云、球形视图及图片双向映射

**复现步骤**：载入有颜色差异的当前目标→展开3D→旋转/平移/缩放/Reset/Fit→图像取色→点云选簇→Esc；检查是否能切球形视图，以及图像/点云对应性。

**渲染及数据链**：

- `ReferenceColorWorkspaceViewModel.Analysis.cs` 从当前显示目标的768边长代理取真实像素，与直方图共用失效/cancel/revision。
- `src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioColorSpace.cs` 的 ColorSpaceVisualizationBuilder、ColorSpacePoint、ColorSpaceProjection、ColorSpaceLinking：点带OKLab与SourceX/Y，采样档位1024/4096/16384，实际数量受图像大小约束。
- Studio实时inspection用同一当前图buffer作为builder两输入及identity transform，并固定展示Source cloud。这里的“Source”是当前被检查的图，可能为仿色预览；不是同时展示真正参考图或迁移比较，不应混称。
- `src/RAWSelectionAssistant/Views/ColorSpace3DViewport.cs` 用WPF DrawingContext/CPU投影并画圆点；显示 L=0–1、a绿↔红、b蓝↔黄、中性轴距离表示色度。尺寸够时绘制刻度；Mini与展开共用点大小/透明度/容差设置。
- 鼠标拖动Orbit、Shift或PanMode平移、滚轮Zoom、ResetCamera、FitCamera→renderer bounds-aware Fit；已有相机继续复用。

**双向映射链**：

- 图片取色：`ReferenceColorWorkspaceView.xaml.cs` 从显示图像采样→`HighlightDisplayedImageSampleAsync`，必要时先对齐当前分析图→`HighlightImageSample → FindNearest(OKLab) → ToPixelMembership`。
- 点云点击：viewport投影HitTest→SelectionChanged→`HighlightCloudSelection`→同一ToPixelMembership→HighlightedPixels→`ImageHighlightOverlay`。
- 默认OKLab半径0.06，限定0.01–0.15；SelectCluster使用同一颜色距离。它是颜色邻域映射，不是空间连通物体或语义对象选择。
- 预览状态与AdjustmentStack分离，Esc调用ClearColorSpaceHighlight。现有单点/簇链不等于任意图像框选→多个簇的完整能力。

**当前缺口**：viewport/model没有球形/Spherical模式、球壳网格或坐标切换。图12仍是笛卡尔OKLab点云，符合代码现状。截图的高亮圈只证明某个可见状态，无法证明两方向触发、清除及导出隔离在截图EXE中都有效。球形框架和真实样本分布需区分，不能为了“像球”强行把样本移到球面而破坏颜色距离。

**上次相关修复**：`6f8e86f` 当前图分析、真实成员及控制；`674bed8` 可达性/短窗口；`4315bc9` 显示图对齐和选点反馈；`20fb695` 展开窗口owner显示器定位；`dc2ba3d` 周边布局。没有球形视图完成提交。

**测试**：

- `ColorSpaceDataModelTests.EyedropperMapsPixelToCloudAndBackToMembership`
- `RuntimeCorrectionCoreTests.ImageAndCloudMembershipUsesBoundedOklabTolerance`
- `ColorSpaceBoundsFitTests` 的横竖/旋转模型及padding/NaN测试
- `ColorSpace3DViewportTests.PreviewMaskIncludesMatchesBeyondTwelveThousandAndClears`、`BoundsFitReframesOnResizeAndResetRemainsDistinct`
- `RuntimeCorrectionWpfTests.CloudInspectionControlsShareTransientSettings`、`AnalysisInvalidatesOnTargetChangeAndMappingDoesNotEditParameters`

这些验证数学、相机和暂态状态，不是球形功能证明，也不是最新截图EXE的完整鼠标交互与导出文件不变证据。

**后续修改范围**：先明确球形是坐标参考外壳还是可逆投影；沿用真实采样、OKLab距离、同一成员集合及相机。补球形/笛卡尔切换、两向拾取、原图/结果/分屏同步、不同内容、Esc、导出不变和实际密度性能验证。CODE=PARTIAL（球形NOT_IMPLEMENTED）；TEST=有限覆盖/本次 NOT_RUN；RELEASE_RUNTIME=NOT_RUN。

## 5. 已有自动证据与限制

以下是读取已有结果，不是本次重跑。三个TRX文件实际存在，SHA256与 `artifacts/runtime-correction/test-summary.json` 一致；TRX的Total/Passed/Failed与摘要一致。Skip数沿用摘要的归类，不能用TRX的notExecuted=0推断没有跳过。

| 历史门 | 记录源码 | 通过 / 失败 / 跳过 | 范围 |
|---|---|---|---|
| Core full | 4315bc9；此后Core无变化 | 1537 / 0 / 4 | core-final.trx，历史记录 |
| WPF full serial | dc2ba3d | 1439 / 0 / 11 | wpf-r9.trx，记录正常testhost退出 |
| DPI | 4315bc9 | 91 / 0 / 0 | dpi-final.trx；读取旧current-run-visual de4c91a，不是最终实际DPI证据 |

分类摘要为重叠子集，不可相加：Asset 522/0/2、Studio 71/0/1、Reference Match 61/0/3、3D 57/0/0、Canvas 26/0/0、Guardian 10/0/0。

历史 `runtime-evidence-manifest.json` 分别记录多个候选，不同EXE的After不得合并成最终同一Release通过；部分DPI曾是环境假定，已有文档主动注明。当前代码中的逻辑DPI Measure/Arrange测试也不能替代Windows实际DPI、滚轮命中、字体显示与桌面交互。

**本次新增测试结果：无；本次构建结果：无；本次Release操作证据：无。** 本次不据绿色历史门修改任何用户截图缺陷的状态。

## 6. 最近提交与定位依据

以下为审计HEAD上的最近16个提交，省略作者邮箱等个人信息：

```text
756541d docs(review): record runtime corrections and remaining acceptance gaps
dc2ba3d fix(studio): balance compact editing rail and image space
64eec3b fix(studio): retain readable edit controls in compact layout
4687b37 fix(studio): keep compact inspection clear of target viewport
20fb695 fix(studio): keep inspection on the owner display
b839cc1 test(canvas): isolate shutdown regression from foreign application STA
206051e fix(canvas): defer owner close after document save
4315bc9 fix(studio): keep image visible during cloud inspection
8b3cb6d fix(studio): clear inspection before shell escape navigation
674bed8 fix(studio): make short inspection panels accessible and preserve source encoding
6f8e86f fix(ux): correct studio inspection, organization rename and canvas menus
c6a8c40 docs(review): publish manual acceptance release and unverified runtime matrix
8457e90 fix(studio): improve inspection space and preserve target viewport
63115dc fix(asset): address gallery inspector folder and trash acceptance defects
d8e26e1 docs(runtime): record screenshot fixes and final user retest release
d195fb1 test(asset): assert loupe trigger and dismissal boundaries
```

相关文件log和关键行blame用于区分最近文件修改与本缺陷修复。这里的“fix”是历史提交标题，不自动代表本次截图问题已关闭。

## 7. 相关文件索引与本次变更范围

### 产品代码：只读

- 素材库宿主/输入：`src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml`、`AssetLibraryPage.cs`、`AssetLibraryPage.Popups.cs`。
- 智能文件夹/Query：同目录 `AssetSmartFolderEditorView.xaml`、`AssetLibraryViewModel.P3SmartFolder.cs`、`AssetLibraryViewModel.P3QueryComposer.cs`、`AssetQueryComposerView.xaml`、`AssetQueryComposerView.xaml.cs`、`AssetLibraryP3Styles.xaml`。
- 文件夹搜索：同目录 `AssetLibraryViewModel.InspectorRelations.cs`、`AssetLibraryOrganizationNodes.cs`。
- 共享图表：同目录 `HistogramDrawing.cs`、`ToneZoneDrawing.cs`。
- 画布：`src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs`、`FreeCanvasSurface.cs`；`src/RAWSelectionAssistant.Core/Services/FreeCanvas/CanvasEditor.cs`。
- 主题：`src/RAWSelectionAssistant/Services/AppearanceService.cs`；`src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml`、`Controls.Tables.xaml`、`Colors.Dark.xaml`。
- Studio View：`src/RAWSelectionAssistant/Views/ReferenceColorWorkspaceView.xaml`、`ReferenceColorWorkspaceView.xaml.cs`、`ColorSpace3DViewport.cs`、`CloudInspectionControls.cs`、`ImageHighlightOverlay.cs`。
- Studio ViewModel：`src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.cs`、`ReferenceColorWorkspaceViewModel.Analysis.cs`、`TetherReferenceModeViewModel.cs`、`TetherReferenceModeViewModel.SchemeInteractions.cs`。
- 颜色/分析模型：`src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioModels.cs`、`ColorStudioColorSpace.cs`；`src/RAWSelectionAssistant.Core/Services/AssetLibrary/VisualAnalysis/VisualAnalysisEngine.cs`、`VisualHistogram.cs`。

### 测试：只读

- `tests/RAWSelectionAssistant.WpfTests/`：AssetLibraryP3WpfTests.cs、AssetLibrarySmartFolderEditorWpfTests.cs、EmbeddedAssetLibraryWpfTests.cs、AssetLibraryP2KeyboardSelectionWpfTests.cs、RuntimeUserFindingsBatchATests.cs、RuntimeUserFindingsBatchBTests.cs、CanvasSurfaceInteractionTests.cs、RuntimeCorrectionWpfTests.cs、BatchExportProcessedPixelsTests.cs、ColorSpace3DViewportTests.cs。
- `tests/RAWSelectionAssistant.Tests/`：RuntimeCorrectionCoreTests.cs、ColorStudioStackTests.cs、ColorSpaceDataModelTests.cs、ColorSpaceProxyTests.cs、ColorSpaceBoundsFitTests.cs。

### 文档及产物：只读

- 前述三份human-review历史矩阵/返工记录及00_START_HERE。
- r9 release-manifest.json及其287个本地发布文件。
- `artifacts/runtime-correction/test-summary.json`、`tests/core-final.trx`、`tests/wpf-r9.trx`、`tests/dpi-final.trx`、`runtime-evidence-manifest.json`、`r9-runtime-session.log`。

### 本次唯一新增

`docs/evidence/human-review/2026-10-04_REPO_READBACK.md`：审计文档。未复制截图/照片，未修改其他文件。只允许提交此路径；若提交前发现分支、远端或其他工作区变动，保留现场并停止推送。

## 8. 结论与交接边界

1. 当前仓库与远端同步，r9发布文件可核对，生产代码与审计HEAD相同；**截图EXE的身份仍未知**，不能将所有问题归为“用户用了旧版”。
2. 可由源码解释的缺口包括：智能文件夹外层布局/关闭入口、框选画刷不透明、侧栏标题样式、搜索文字空间不足、快速导出固定格式。扩展名滚动和Fit失败的具体运行根因仍需后续定位。
3. 最新截图提出的通道视图、影调区间映射、参数父子组织及球形3D，与已有计算/控件不是同一个完成标准。左右栏方向存在明确的新旧要求差异。
4. **当前产品验收仍为 NOT_READY_FOR_USER_RETEST，USER_ACCEPTANCE=NOT_APPROVED。** 这份审计没有修复或实机通过任何项目；不进入下一开发阶段。提交本文后停止，等待用户下一份修复指令。
