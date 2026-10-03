# 人工验收缺陷关闭轮 — 16 张截图

START_HEAD: `d8e26e1adedc5b547139e1fd647dcc0e72511a4b`

Branch: `integration/pixel-tart-developer-preview`

起点 fetch 后 LOCAL == REMOTE，工作区 CLEAN。已逐张读取 `G:/UI问题/1/` 的 1–14、24、25，共16张。

VisualApproved=false · UserVerified=false · USER_ACCEPTANCE=NOT_APPROVED

Production SourceHead: `8457e9022ac8278df5401c6c1842f70a39c98129`

Release EXE: `N:/pixart/pixel-tart-source-private/artifacts/releases/manual-acceptance-2026-10-03/publish/win-x64/KitaoPhotoSelector.exe`

## 验收规则

- 本文件独立于历史 CLOSED 矩阵。用户截图是缺陷依据；没有 FINAL PASS。
- 本轮按用户此前明确答复，先修复/Build/Test，用户随后统一实机验收；Browser/Computer Use/SendInput/自动截图/更改DPI未使用。
- RUNTIME全部NOT_RUN，After全部缺失，因此所有截图总状态BLOCKED，最终NOT_READY_FOR_USER_RETEST。
- 自动STA/WPF控件测试不是Production EXE实机证据。历史DPI artifact不证明本轮。
- 截图1内交换面板标注与本轮A1文字不一致，按直接指令：左调整、中目标、右参考/分析/3D。
- 截图24/25实际为Eagle窗口，作为交互/主题参考；不冒称Pixel Tart实机截图。

## 逐张对照

原图已按字节复制到 `artifacts/manual-acceptance/latest/user-before/`；manifest记录SHA256。未创建假的After图。

### 截图 1 · A1–A2

- 复现：进入参考仿色；导入目标和参考；开关右栏、缩放、收窄窗口。
- 原问题／根因与新结果：左右职责保留；右栏隐藏仍保留宽度，避免目标区域反复重排；参考导航增高到360 DIP，独立展开继续只读。
- 修改/审计文件：ReferenceColorWorkspaceView.xaml(.cs)。
- CODE: PARTIAL
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/1.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：窄窗口主图比例与面板开关仍须实机。

### 截图 2 · A3

- 复现：生成当前模型；展开；旋转/平移/缩放/重置/Fit；双向取色、Esc。
- 原问题／根因与新结果：原视图只有单字坐标轴，缺图例与刻度；新增明度/色轴解释及投影刻度，保留当前原片样本与preview-only联动。
- 修改/审计文件：ColorSpace3DViewport.cs; ReferenceColorWorkspaceView.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/2.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：实际选点可识别性待用户验证。

### 截图 3 · B

- 复现：胶片条单选/多选右键；评分色标；两种导出。
- 原问题／根因与新结果：直接JPEG/RAW TIFF与发布配方调用链不同，名称说明结果；共享菜单负偏移和默认SeparatorStyleKey导致边界/亮线问题。
- 修改/审计文件：ReferenceColorWorkspaceView.xaml(.cs); Controls.Menu.xaml; Components.Foundation.xaml; ContextMenuPlacement.cs。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/3.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：两级菜单屏幕边缘及导出全流程待实机。

### 截图 4 · C1

- 复现：滑杆从Min逐步到Max；拖动、轨道点击、方向键。
- 原问题／根因与新结果：按列数撑满宽度导致长区间尺寸不变；等高模式210 DIP上限；改为连续目标尺寸，布局复用滑杆同一最大值，处理Gallery外宽与滚动条后内容宽度差，保留比例。
- 修改/审计文件：AssetLayoutEngine.cs; VirtualizingAssetPanel.cs; AssetLibraryPage.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/4.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：数学逐步测试不能替代MAX实机截图。

### 截图 5 · C2–C3

- 复现：使用评分/颜色/标签/文件夹/方向/格式筛选；收窄窗口。
- 原问题／根因与新结果：固定筛选可定制，默认扩展现有字段；分离搜索操作行与可换行筛选行。
- 修改/审计文件：AssetLibraryPage.xaml; AssetLibraryWorkspaceSettings.cs。
- CODE: PARTIAL
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/5.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：12个真实DPI/窗口组合未运行。

### 截图 6 · D1/D3

- 复现：选中素材；拖动分组标题/右键上下移；折叠；退出重启。
- 原问题／根因与新结果：原分组顺序固定；现重排原控件，顺序/折叠写入已有工作区设置；图片身份保持在分组外。
- 修改/审计文件：InspectorSectionPanel.cs; AssetLibraryWorkspaceSettings.cs; AssetLibraryPage.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/6.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：用户重启后体验待实机。

### 截图 7 · D2

- 复现：打开快速工具；切换JPEG/RAW/缺失文件/多选。
- 原问题／根因与新结果：快速工具缺折叠；Export复制原件与Publishing渲染不同，命名区分并分组；按选择禁用并解释原因。
- 修改/审计文件：AssetLibraryViewModel.QuickTools.cs; AssetLibraryPage.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/7.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：查看/处理/输出/创作点击待实机。

### 截图 8 · E1

- 复现：创建根/子文件夹；立即查看、重命名、排序/移动。
- 原问题／根因与新结果：创建只刷新未选中；创建后选中Query；清理空输入行，提供明确新建入口。
- 修改/审计文件：AssetLibraryViewModel.cs; AssetLibraryViewModel.P2Browser.cs; AssetLibraryPage.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/8.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：深层树命名/移动待实机。

### 截图 9 · E1

- 复现：空/非空文件夹右键删除；取消及确认。
- 原问题／根因与新结果：原来只有归档；新增仅删除文件夹定义，说明直接成员/子文件夹范围，子文件夹上移，照片保留。
- 修改/审计文件：AssetLibraryOrganizationNodes.cs; SqliteAssetLibraryRepository.Deletion.cs; AssetLibraryViewModel.P2Browser.cs。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/9.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：确认交互待实机。

### 截图 10 · E2

- 复现：点击智能文件夹标题；空状态/新建/查看/编辑/保存/重开。
- 原问题／根因与新结果：原标题TextBlock无动作；接现有智能文件夹查询及规则编辑器。
- 修改/审计文件：AssetLibraryPage.xaml; AssetLibraryPage.Popups.cs。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/10.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：完整新建与规则查询待实机。

### 截图 11 · F

- 复现：混合文件夹素材入回收站；重启恢复；删除原文件夹后恢复；彻底删除记录。
- 原问题／根因与新结果：原关系/时间字段不变；新增原文件夹快照与缺失归属提示；回收站提供顶层恢复/彻底删除库内记录，避免隐藏父菜单也藏住恢复；删除两次确认，源文件不删。
- 修改/审计文件：AssetLibrarySchema.cs; SqliteAssetLibraryRepository.V15.cs; SqliteAssetLibraryRepository.Deletion.cs; AssetLibraryPage.cs。
- CODE: PARTIAL
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/11.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：照片手动排序尚无产品模型，无法验证手动位置；现有7种字段排序重启恢复覆盖。

### 截图 12 · G

- 复现：打开日历并切换月/周/日及列表；收窄窗口。
- 原问题／根因与新结果：前轮误删整组图例；恢复4色点及文字，WrapPanel，无冗余标题。
- 修改/审计文件：WorkCalendarView.xaml。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/12.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：日历可读性待实机。

### 截图 13 · H1

- 复现：进入画布；选择/平移/文本/素材/撤销/重做/Fit/Zoom；More打开关闭。
- 原问题／根因与新结果：重排工具/编辑/视图/画布/项目分组；生命周期低频动作移More，More正确锚定自身；原Zoom唯一状态。
- 修改/审计文件：FreeCanvasView.cs。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/13.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：工具栏视觉层级待实机。

### 截图 14 · H2–H3

- 复现：逐个打开素材/胶片条/画布级联菜单；边缘、键盘、滚动、关闭。
- 原问题／根因与新结果：画布缺共享定位；共享模板负偏移且级联事件冒泡干扰父级；自动附加已有定位、限制可滚动尺寸、过滤子级事件。
- 修改/审计文件：ContextMenuPlacement.cs; AssetLibraryPage.cs; Controls.Menu.xaml; Components.Foundation.xaml; FreeCanvasView.cs。
- CODE: FIXED_PENDING_RUNTIME
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/14.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：所有级联菜单实机验证未执行。

### 截图 24 · E3–E4

- 复现：打开Pixel Tart规则编辑器字段菜单；嵌套、150/200%。
- 原问题／根因与新结果：截图24是Eagle参考；Pixel Tart已用独立深色ComboBox模板；收紧列表高度240 DIP，字段/运算符合同回归。
- 修改/审计文件：AssetLibraryP3Styles.xaml; P3QueryNodeView.cs(existing)。
- CODE: PARTIAL
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/24.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：深色菜单与深嵌套屏幕边界待实机。

### 截图 25 · E3–E4

- 复现：逐字段选择运算符，保存重开并检查结果。
- 原问题／根因与新结果：截图25是Eagle参考；沿用实际支持运算符，不添加示例中不存在能力。
- 修改/审计文件：P3QueryNodeView.cs(existing); ManualAcceptanceInteractionTests.cs。
- CODE: PARTIAL
- TEST: PASS_SCOPED
- RUNTIME: NOT_RUN
- Before: `artifacts/manual-acceptance/latest/user-before/25.png`
- After: 未采集；EXE/Git HEAD/Windows缩放/窗口尺寸将在用户实际运行证据中补记。
- 状态: BLOCKED
- 剩余：150/200%真实下拉及规则结果未运行。

## 真实剩余问题

1. 16张截图的Release实际操作、After截图/短录屏和Windows缩放/窗口大小均待用户提供，不能据自动化测试升级PASS。
2. 照片手动位置排序在当前产品模型中不存在；字段排序和文件夹排序不能冒充手动照片排序。
3. 150%/200%窄窗口、深层规则、级联菜单斜移与Flip、主图不突跳及缩放Max仍未实际验收。

## Tests / Release

最终测试结果、提交、Release路径见本轮 `artifacts/manual-acceptance/latest/` 汇总。

## 本轮发现并修正的回归

- 初次完整WPF运行：332通过、9失败、2跳过后testhost异常退出。7个资源加载失败及testhost崩溃同源：提取后的Inspector QuickTools模板引用了后声明的StaticResource转换器；已将资源声明移到模板前。另2项为新增智能文件夹标题按钮的结构契约变化，保留样式断言并更新真实控件数量/结构。
- 回收站菜单审查：恢复原来嵌套在被隐藏的管理组；现创建顶层恢复项，回归实际多选恢复命令执行。
- Canvas初次focused运行3失败：重复挂入相同Button导致logical parent异常；修正后13项通过。
- Core初次完整运行1失败：菜单分隔线改动未复用既有低对比度Brush；已恢复共享MenuSeparatorBrush，最终Core 1533通过、0失败、4既有跳过。
- 这些中间失败保存在本轮tests目录，不能作为最终通过记录覆盖。

## DPI证据边界

DPI项目91项通过，但其current-run-visual manifest仍引用`de4c91a67c9146b5272bf20e10360172c4bf6189`。
这是历史证据contract检查，不能证明本轮Release在100/125/150/200%的视觉结果。
本轮不运行桌面截图/Observer/Recorder，不修改Windows缩放；当前HEAD的用户实机After证据仍缺失。

## 提交拆分

- `63115dc` fix(asset): address gallery inspector folder and trash acceptance defects。包括共享菜单与Canvas修复；新增恢复/删除/缩放/Inspector行为测试。
- `8457e90` fix(studio): improve inspection space and preserve target viewport。参考导航、3D标注、导出语义、日历图例与主图视口回归。

后续文档提交只更新证据，不循环重建Release。

## 逐项自动验证范围（不等于Runtime）

| 截图 | 有意义的行为回归 / 现有覆盖 | 尚未覆盖 |
|---|---|---|
| 1 | RuntimeGeometry_UsesWideRatioAndResponsiveRails：宽/窄布局；新断言开关右栏时目标viewport宽度不变 | 实际缩放/窗口收窄视觉突跳 |
| 2 | BoundsFitReframesOnResizeAndResetRemainsDistinct；PreviewMaskIncludesMatchesBeyondTwelveThousandAndClears；ColorInspectionOnlyChangesPreviewAndClearsOnTargetSwitch | 新刻度可读性、实机取色高亮 |
| 3 | FilmstripClickModifiersKeepAnchorSelectionAndActiveIndependent；PublishingReceivesProcessedFrozenTargetsInsteadOfOriginalFiles；CopyApplyAdjustmentsUsesSelectedCategoriesAndProtectsAssetFields | 实际菜单几何与导出交互 |
| 4 | EverySliderStepChangesEveryPhotoSizeWithoutEarlySaturation；ScrollbarWidthCannotSaturateTheLastPartOfTheSlider；GalleryPanelUsesTheBoundSliderMaximumWithItsOwnViewport | 拖动/轨道/键盘及Max截图 |
| 5 | SearchWidthAndFolderRowsRemainBoundedAcrossLogicalDpiSizes；RecoverableErrorRetryIsReachableByForwardKeyboardTraversalWithoutStartingAttemptTwo | 真实DPI工具栏可读性 |
| 6 | InspectorReorderAndCollapseSurviveSerializationKeepingSameControls；InspectorRelationsPersistRefreshQueryAndSurviveReload | 用户拖动与整机重启 |
| 7 | NewFolderSelectsItsQueryAndUnsupportedConversionIsDisabled；共享QuickTools单选/多选模板实际加载 | 每个工具的用户点击结果 |
| 8 | NewFolderSelectsItsQueryAndUnsupportedConversionIsDisabled（根/子文件夹查询切换）；FolderNameSearchKeepsAncestorsExpansionAndAssetQuery | 树形拖动/命名视觉 |
| 9 | RemovedFolderPromotesChildrenAndTrashRestoreExplainsFallback | 空/非空删除确认窗口 |
| 10 | SmartFolderEditorNoOpSavePreservesCompleteDocumentJsonHashAndResultsAcrossRestart；NestedRuleViewRoundTripsAndClearUnlockedPreservesOnlyLockedRules | 标题菜单与完整新建流程 |
| 11 | TrashRestoreAfterRestartPreservesMixedMembershipsAndAllSupportedSorts；PermanentRemovalRejectsLiveAssetsAndLeavesOriginalFileBytes；TrashMenuExposesTopLevelRestoreForTheSelectedSet | 双确认和手动位置模型 |
| 12 | 既有Calendar测试回归；新增WrapPanel图例经编译 | 新图例/视图切换无独立用户验证 |
| 13 | CanvasWorkflow/CanvasTransform/CanvasSelection回归；此前逻辑父级异常修复后13项通过 | 新工具栏实际可达性 |
| 14 | SharedMenuStyleAndScrollBoundsApplyToNewCascadeItems及已有ContextMenuPlacement几何用例 | 屏幕边缘、鼠标斜移、键盘滚动 |
| 24/25 | EveryRuleFieldRejectsOperatorsOutsideItsContract；QueryNodeRoundTripsCaseSensitivityAndUsesTheCoreOperatorMatrix；SmartFolderConditionHeadersUseStackedLayoutAtNarrowWidths | 下拉菜单150/200%实际主题与边界 |

所有TEST通过声明仅限上述断言；不代表截图问题已经完整验收。

## 最终机器结果

- Clean Release x64：0 warnings / 0 errors；自包含发布287文件。
- Core full：1533 PASS / 0 FAIL / 4 SKIP。
- WPF full serial：1429 PASS / 0 FAIL / 11 SKIP；9.84分钟，正常退出，未再发生testhost crash。
- DPI：91 PASS / 0 FAIL；旧artifact边界如上。
- Asset 543/0/2；Color Studio 88/0/1；Reference Match 203/0/8；3D 39/0/0；Canvas 24/0/0；Guardian 10/0/0。均为full TRX按命名提取、可重叠的子集；不是额外运行。
- 新增失败0。最初引入的失败及中间结果仍保留，最终全部修正。
- Release SourceHead: `8457e9022ac8278df5401c6c1842f70a39c98129`。
- EXE SHA256: `6476D024DA74E2754A88C39304214BD8E8F428BEFD841247B06AC8E31E2C373F`。
- 应用DLL SHA256: `3F3C74028C68E6554E1329251CD3B005104830A1C5966A4F93F626A54ACEB743`。
- manifest记录全部发布文件；没有启动EXE，按当前要求等用户自行打开。

最终状态：**NOT_READY_FOR_USER_RETEST**。16项Runtime及After证据缺失、真实DPI/窄窗口未验证、照片手动位置模型未实现。
