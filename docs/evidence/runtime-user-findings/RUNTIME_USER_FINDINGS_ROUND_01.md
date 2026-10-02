# Runtime User Findings — Round 01

StartHead: 11e6eddd5e5974ce73de8ab9cdf1702bd6284c18

USER_APPROVED: false
VisualApproved: false
UserVerified: false

本账本独立于历史 HR 矩阵。用户文字反馈是重新打开问题的依据；尚未收到所述 16 页截图，未观看其实际操作路径。测试通过不代表 Runtime 或用户通过。

执行门：A → B → C → D → E → F → G。每批 Build/Test/真实 Release 操作/After 截图齐备后再进入下一批。当前处于 Batch A；后续批次 OPEN，未承接旧 CLOSED。

状态范围：OPEN / IN_PROGRESS / FIXED_IN_CODE / RUNTIME_PASS / RUNTIME_FAIL / BLOCKED。

## RUX-001 — 一级导航重复 Tooltip

| Field | Value |
|---|---|
| ID | RUX-001 |
| User Finding | 一级导航重复 Tooltip；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 导航按钮已显示文本，但 Tooltip 始终启用。现按 IsSidebarCollapsed 开关 tooltip，保留 Automation Name。 |
| Files Changed | src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml |
| Automated Test | NavigationWorkbenchClosure / Version230Rc2Navigation 回归通过；tooltip 动态显示待 runtime。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-002 — 工作台区域层级不清

| Field | Value |
|---|---|
| ID | RUX-002 |
| User Finding | 工作台区域层级不清；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-003 — 素材库顶部搜索框过长

| Field | Value |
|---|---|
| ID | RUX-003 |
| User Finding | 素材库顶部搜索框过长；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 顶部搜索所在列 Width=* 且 MinWidth=300，无上限。现为 260 DIP，范围 200–320。 |
| Files Changed | src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml |
| Automated Test | SearchWidthAndFolderRowsRemainBoundedAcrossLogicalDpiSizes：12 组合通过（未截图）。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-004 — Search 只负责文本搜索

| Field | Value |
|---|---|
| ID | RUX-004 |
| User Finding | Search 只负责文本搜索；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 普通搜索建议会追加结构化 query rule 并打开 Advanced Filter。现建议仅补全文本，字段建议留给筛选入口。 |
| Files Changed | AssetLibraryViewModel.P3QueryComposer.cs |
| Automated Test | TextSuggestionDoesNotOpenAdvancedFilters + P3 搜索/建议回归通过。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-005 — 素材库中间重复标题

| Field | Value |
|---|---|
| ID | RUX-005 |
| User Finding | 素材库中间重复标题；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 上一轮 cfdd0b3 已删除 Gallery 大标题；本轮核对当前 XAML，保留原改动，尚无本轮 runtime 证据。 |
| Files Changed | 无重复修改；现有 AssetLibraryPage.xaml |
| Automated Test | AssetLibraryEagleLayoutReconstruction / P2LayoutBounds 回归通过。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-006 — 左侧组织区顺序

| Field | Value |
|---|---|
| ID | RUX-006 |
| User Finding | 左侧组织区顺序；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 上一轮 cfdd0b3 已调整智能文件夹→标签分组→分隔线→文件夹；本轮保留，查询命令继续使用原实现。 |
| Files Changed | 无重复修改；现有 AssetLibraryPage.xaml / P2Browser |
| Automated Test | P2Browser / P3Wpf 查询与布局回归通过。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-007 — 文件夹树重新设计

| Field | Value |
|---|---|
| ID | RUX-007 |
| User Finding | 文件夹树重新设计；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | TreeView 使用默认模板，新建输入行尺寸不同。现明确箭头/层级缩进/图标/计数/hover/selected，30 DIP 行与新建行一致。 |
| Files Changed | AssetLibraryPage.xaml / AssetLibraryOrganizationNodes.cs |
| Automated Test | AssetFolderTreeLayout + 布局构造回归通过；实际外观/hover 未验。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-008 — 文件夹搜索

| Field | Value |
|---|---|
| ID | RUX-008 |
| User Finding | 文件夹搜索；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 已有 FolderSearch 只过滤分类器列表，侧栏树没有搜索入口。现新增名称过滤投影，保留祖先，搜索展开不写入持久化状态。 |
| Files Changed | AssetLibraryOrganizationNodes.cs / AssetLibraryViewModel.InspectorRelations.cs / P2Browser |
| Automated Test | FolderNameSearchKeepsAncestorsExpansionAndAssetQuery 通过。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-009 — Tag / Folder 真正绑定 Asset

| Field | Value |
|---|---|
| ID | RUX-009 |
| User Finding | Tag / Folder 真正绑定 Asset；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | Inspector 仅添加关系；关系修改后未统一刷新组织列表、计数与 query。现通过既有 browser command service 写入并刷新各投影，支持移除及 durable undo。 |
| Files Changed | AssetLibraryViewModel.ContextualInspector.cs / InspectorRelations.cs / P2Browser |
| Automated Test | InspectorRelationsPersistRefreshQueryAndSurviveReload / MixedSelectionAndStalePickerNeverWriteWrongAssets 通过；Core Asset 143 PASS。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-010 — 标签允许多个

| Field | Value |
|---|---|
| ID | RUX-010 |
| User Finding | 标签允许多个；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | Inspector 只有标签摘要与固定输入框，无逐条关系删除。现单/多选共用 0..N chips，多选显示成员计数。 |
| Files Changed | AssetLibraryPage.xaml / AssetLibraryViewModel.InspectorRelations.cs |
| Automated Test | 两标签增删/重载/混合选中/Undo 通过。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-011 — 标签选择器

| Field | Value |
|---|---|
| ID | RUX-011 |
| User Finding | 标签选择器；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 缺少选中素材的搜索/已选/可用/勾选/新建标签选择器。现借用现有 single-active ContextMenu manager，stale-selection guard 防止误写。 |
| Files Changed | AssetInspectorTagPicker.xaml(.cs) / AssetLibraryPage.Popups.cs / InspectorRelations.cs |
| Automated Test | 标签创建/搜索/选中切换/空选择禁用/过期操作保护通过；Overlay 鼠标与键盘待 runtime。 |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | FIXED_IN_CODE |
| USER_APPROVED | false |

## RUX-012 — Color Filter Popup

| Field | Value |
|---|---|
| ID | RUX-012 |
| User Finding | Color Filter Popup；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-013 — 可配置顶部筛选

| Field | Value |
|---|---|
| ID | RUX-013 |
| User Finding | 可配置顶部筛选；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-014 — Quick Loupe

| Field | Value |
|---|---|
| ID | RUX-014 |
| User Finding | Quick Loupe；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-015 — Quick Loupe / Viewer / Compare 职责

| Field | Value |
|---|---|
| ID | RUX-015 |
| User Finding | Quick Loupe / Viewer / Compare 职责；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-016 — Inspector Preview 背景

| Field | Value |
|---|---|
| ID | RUX-016 |
| User Finding | Inspector Preview 背景；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-017 — Inspector Sections

| Field | Value |
|---|---|
| ID | RUX-017 |
| User Finding | Inspector Sections；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-018 — Inspector Export

| Field | Value |
|---|---|
| ID | RUX-018 |
| User Finding | Inspector Export；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-019 — 所有二级菜单宽度

| Field | Value |
|---|---|
| ID | RUX-019 |
| User Finding | 所有二级菜单宽度；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-020 — Color Label Menu

| Field | Value |
|---|---|
| ID | RUX-020 |
| User Finding | Color Label Menu；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-021 — Rating Menu

| Field | Value |
|---|---|
| ID | RUX-021 |
| User Finding | Rating Menu；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-022 — Context Menu Visual Contract

| Field | Value |
|---|---|
| ID | RUX-022 |
| User Finding | Context Menu Visual Contract；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-023 — Smart Folder Query Builder

| Field | Value |
|---|---|
| ID | RUX-023 |
| User Finding | Smart Folder Query Builder；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-024 — Smart Folder Fields

| Field | Value |
|---|---|
| ID | RUX-024 |
| User Finding | Smart Folder Fields；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-025 — Operators By Field Type

| Field | Value |
|---|---|
| ID | RUX-025 |
| User Finding | Operators By Field Type；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-026 — Real-time Result Count

| Field | Value |
|---|---|
| ID | RUX-026 |
| User Finding | Real-time Result Count；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-027 — Smart Folder Persistence

| Field | Value |
|---|---|
| ID | RUX-027 |
| User Finding | Smart Folder Persistence；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-028 — 发布导出全面中文化

| Field | Value |
|---|---|
| ID | RUX-028 |
| User Finding | 发布导出全面中文化；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-029 — 发布预览黑边

| Field | Value |
|---|---|
| ID | RUX-029 |
| User Finding | 发布预览黑边；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-030 — Publishing 面板

| Field | Value |
|---|---|
| ID | RUX-030 |
| User Finding | Publishing 面板；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-031 — 左右布局交换

| Field | Value |
|---|---|
| ID | RUX-031 |
| User Finding | 左右布局交换；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-032 — Main Image 仍然最大

| Field | Value |
|---|---|
| ID | RUX-032 |
| User Finding | Main Image 仍然最大；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-033 — Reference Navigator

| Field | Value |
|---|---|
| ID | RUX-033 |
| User Finding | Reference Navigator；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-034 — 去重复标签

| Field | Value |
|---|---|
| ID | RUX-034 |
| User Finding | 去重复标签；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-035 — Stable / MatchV4Beta 中文化

| Field | Value |
|---|---|
| ID | RUX-035 |
| User Finding | Stable / MatchV4Beta 中文化；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-036 — Filmstrip Context Menu

| Field | Value |
|---|---|
| ID | RUX-036 |
| User Finding | Filmstrip Context Menu；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-037 — Filmstrip Selection

| Field | Value |
|---|---|
| ID | RUX-037 |
| User Finding | Filmstrip Selection；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-038 — Filmstrip Sync Buttons

| Field | Value |
|---|---|
| ID | RUX-038 |
| User Finding | Filmstrip Sync Buttons；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-039 — 高质量预览任务

| Field | Value |
|---|---|
| ID | RUX-039 |
| User Finding | 高质量预览任务；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-040 — 3D 点云视觉

| Field | Value |
|---|---|
| ID | RUX-040 |
| User Finding | 3D 点云视觉；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-041 — Image → 3D

| Field | Value |
|---|---|
| ID | RUX-041 |
| User Finding | Image → 3D；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-042 — 3D → Image

| Field | Value |
|---|---|
| ID | RUX-042 |
| User Finding | 3D → Image；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-043 — 3D Expand

| Field | Value |
|---|---|
| ID | RUX-043 |
| User Finding | 3D Expand；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-044 — Toolbar 重做

| Field | Value |
|---|---|
| ID | RUX-044 |
| User Finding | Toolbar 重做；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-045 — 新建 / 打开 / 关闭画布

| Field | Value |
|---|---|
| ID | RUX-045 |
| User Finding | 新建 / 打开 / 关闭画布；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-046 — Toolbar 中文化

| Field | Value |
|---|---|
| ID | RUX-046 |
| User Finding | Toolbar 中文化；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-047 — Zoom

| Field | Value |
|---|---|
| ID | RUX-047 |
| User Finding | Zoom；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |

## RUX-048 — 工作日历顶部冗余

| Field | Value |
|---|---|
| ID | RUX-048 |
| User Finding | 工作日历顶部冗余；详见用户本轮完整指令 |
| Current Runtime State | 用户报告未满足；本轮 NOT_RUN |
| Root Cause | 待当前源码逐项核查 |
| Files Changed | 未完成 |
| Automated Test | NOT_RUN（本轮） |
| Runtime Test | BLOCKED：缺用户截图与本轮实机操作方式确认 |
| Before Evidence | 未提供；不生成替代截图 |
| After Evidence | 未采集 |
| Status | OPEN |
| USER_APPROVED | false |


## Batch A checkpoint

- Code / Release SourceHead: fea716c17d5018e51fb6ede9c0d3317953c2de42。
- Core Asset 143 PASS / 0 FAIL；WPF 73 PASS / 0 FAIL / 0 SKIP（含 5 个新行为测试和 1 个 Guardian contract）。
- 发布 manifest: artifacts/releases/runtime-user-findings-batch-a/release-manifest.json；包含全部发布文件 hash。
- 启动真实 Release；仅检查进程与主窗口句柄，不操作页面。启动成功不代表任一 RUX runtime 通过。
- Batch A runtime BLOCKED：未收到 16 页截图与操作方式确认；before.png / after.png 均未伪造。
- 尚未开始 Batch B–G；不得将 OPEN 解释为已实现或已通过。
- 全部 USER_APPROVED=false；VisualApproved=false；UserVerified=false。
