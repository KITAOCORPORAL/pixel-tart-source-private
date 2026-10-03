# RUX-001～048 实机验收索引

Release SourceHead: `8f9a28f3c21bcf41e24e700024d14a8648fde5b5`

统一口径：STATUS=FIXED_IN_CODE；TEST=自动回归通过（Core1530/WPF1421，均0FAIL；DPI91有历史证据边界）；RUNTIME=NOT_RUN；USER_ACCEPTANCE=NOT_APPROVED。

| ID | 问题 / 根因处理 | 代码 | 测试 | 实机 | 证据 |
|---|---|---|---|---|---|
| RUX-001 | 一级导航重复 Tooltip：导航按钮已显示文本，但 Tooltip 始终启用。现按 IsSidebarCollapsed 开关 tooltip，保留 Automation Name。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-001/result.json) |
| RUX-002 | 工作台区域层级不清：工作台SectionSurface透明导致区域混在背景；项目/任务/近期安排采用轻微明度差并统一内距，未加粗描边。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-002/result.json) |
| RUX-003 | 素材库顶部搜索框过长：顶部搜索所在列 Width=* 且 MinWidth=300，无上限。现为 260 DIP，范围 200–320。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-003/result.json) |
| RUX-004 | Search 只负责文本搜索：普通搜索建议会追加结构化 query rule 并打开 Advanced Filter。现建议仅补全文本，字段建议留给筛选入口。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-004/result.json) |
| RUX-005 | 素材库中间重复标题：上一轮 cfdd0b3 已删除 Gallery 大标题；本轮核对当前 XAML，保留原改动，尚无本轮 runtime 证据。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-005/result.json) |
| RUX-006 | 左侧组织区顺序：上一轮 cfdd0b3 已调整智能文件夹→标签分组→分隔线→文件夹；本轮保留，查询命令继续使用原实现。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-006/result.json) |
| RUX-007 | 文件夹树重新设计：TreeView 使用默认模板，新建输入行尺寸不同。现明确箭头/层级缩进/图标/计数/hover/selected，30 DIP 行与新建行一致。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-007/result.json) |
| RUX-008 | 文件夹搜索：已有 FolderSearch 只过滤分类器列表，侧栏树没有搜索入口。现新增名称过滤投影，保留祖先，搜索展开不写入持久化状态。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-008/result.json) |
| RUX-009 | Tag / Folder 真正绑定 Asset：Inspector 仅添加关系；关系修改后未统一刷新组织列表、计数与 query。现通过既有 browser command service 写入并刷新各投影，支持移除及 durable undo。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-009/result.json) |
| RUX-010 | 标签允许多个：Inspector 只有标签摘要与固定输入框，无逐条关系删除。现单/多选共用 0..N chips，多选显示成员计数。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-010/result.json) |
| RUX-011 | 标签选择器：缺少选中素材的搜索/已选/可用/勾选/新建标签选择器。现借用现有 single-active ContextMenu manager，stale-selection guard 防止误写。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-011/result.json) |
| RUX-012 | Color Filter Popup：二维色板藏在高级面板，顶部颜色入口混入 metadata 编辑。提取原色板及 HSV 查询绑定到独立浮层。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-012/result.json) |
| RUX-013 | 可配置顶部筛选：顶部四个筛选永久固定；新增真实字段的编辑/固定菜单，固定列表保存到原 WorkspaceSettings。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-013/result.json) |
| RUX-014 | Quick Loupe：原先鼠标离开入口后预览仍保留；修复中又暴露popup自身尺寸变化误关。现悬停入口管理生命周期：入口离开即取消；菜单预览由Esc/外部点击关闭，不由非交互popup的MouseLeave关闭。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-014/result.json) |
| RUX-015 | Quick Loupe / Viewer / Compare 职责：保留 magnifier→loupe / double click→viewer / compare→workspace 既有命令，不更换职责。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-015/result.json) |
| RUX-016 | Inspector Preview 背景：固定 240 高带背景的图片容器造成灰块；改透明且按图片自适应，上限240。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-016/result.json) |
| RUX-017 | Inspector Sections：评分与颜色未分区、来源标题重复。增加统一分隔与折叠分组，第二来源改为 EXIF。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-017/result.json) |
| RUX-018 | Inspector Export：来源信息内重复 ghost 导出按钮；移到快速工具，用现有 Publishing 命令。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-018/result.json) |
| RUX-019 | 所有二级菜单宽度：submenu 固定 MinWidth220，空图标和快捷键列仍占宽；移除固定下限并折叠空列。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-019/result.json) |
| RUX-020 | Color Label Menu：复用现有色块菜单，内容宽度跟随 swatch；Inspector 也改为同一元数据的直接色块操作。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-020/result.json) |
| RUX-021 | Rating Menu：菜单同时使用数字星级文本与数字快捷键；改清除评分/★，快捷键不变。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-021/result.json) |
| RUX-022 | Context Menu Visual Contract：修共享 canonical/implicit 菜单模板空列，保持 ContextMenuPlacement/Monitor。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-022/result.json) |
| RUX-023 | Smart Folder Query Builder：主流程与说明/排序/复制/归档/预览列表平铺；名称、条件树和结果/保存取消置主层，其余收进更多选项。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-023/result.json) |
| RUX-024 | Smart Folder Fields：字段来自现有 AssetQueryField/codec：文件名、格式、标签、文件夹、评分、颜色、备注、导入/拍摄日期、尺寸/比例等。修改日期/创建日期/时长/链接没有正式 query 字段，不伪造入口。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-024/result.json) |
| RUX-025 | Operators By Field Type：复用 codec 支持的按类型 operators；日期文案改早于/晚于/当天/日期区间；String/Number/Reference 保留真实 operators。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-025/result.json) |
| RUX-026 | Real-time Result Count：既有 debounce+cancellation+repository query 实时计数继续使用；移到主流程底部，不添加固定数字。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-026/result.json) |
| RUX-027 | Smart Folder Persistence：保留创建/查询/重载/条件回填；新增确认后只删除查询定义，FK只级联规则文档，不触碰Asset/source。仓库重启回归通过。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-027/result.json) |
| RUX-028 | 发布导出全面中文化：发布面板和内置配方显示英文；加入中文显示名，保持内部 ID/文件命名 contract。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-028/result.json) |
| RUX-029 | 发布预览黑边：WPF DrawImage 用像素坐标但 RenderTargetBitmap 用输出 DPI，72 DPI 时只覆盖75%宽高。按96/DPI缩放绘制坐标，水印同链。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-029/result.json) |
| RUX-030 | Publishing 面板：Studio 输出只暴露 Look/LUT；接入既有Publishing，冻结各target调整，保留文件名/取消/失败摘要。发布页明确当前设置与配方模式，位深随格式，ICC只显示可用配置。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-030/result.json) |
| RUX-031 | 左右布局交换：当前已 LEFT 工具/CENTER 图片/RIGHT 参考/BOTTOM filmstrip；遵照文字目标保留分工，不因箭头反向改动。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-031/result.json) |
| RUX-032 | Main Image 仍然最大：既有右栏折叠/窄窗 overlay 与展开参考视图保留；选中 filmstrip 背景明确，边框厚度固定避免移动。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-032/result.json) |
| RUX-033 | Reference Navigator：复用300 DIP navigator、Fit/100%/zoom/pan及独立展开窗，无第二套 viewport。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-033/result.json) |
| RUX-034 | 去重复标签：参考列表重复显示48px缩略图；移除重复图像，保留一份名称/来源/权重。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-034/result.json) |
| RUX-035 | Stable / MatchV4Beta 中文化：ComboBox 直接绑定 enum；使用中文显示 converter，内部 enum不变。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-035/result.json) |
| RUX-036 | Filmstrip Context Menu：Filmstrip 只响应左键，没有右键菜单。接入真实评分/颜色/复制应用/同步/导出/移出，已选右键保留多选。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-036/result.json) |
| RUX-037 | Filmstrip Selection：复用单/Ctrl/Shift选择与active独立模型；强调正在编辑标签及背景，固定边框尺寸。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-037/result.json) |
| RUX-038 | Filmstrip Sync Buttons：新增明确复制当前调整和应用已复制调整；同步沿用原engine，metadata不复制；空调整也更新快照，避免旧Look残留。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-038/result.json) |
| RUX-039 | 高质量预览任务：保留已有source/frame cache、latest activation/cancel/idle所有权；界面补可见停止预览按钮。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-039/result.json) |
| RUX-040 | 3D 点云视觉：固定屏幕十字轴不随3D camera，文字混入内部mode；改为与真实采样同camera投影的OKLab三轴。灰图仍保持真实灰轴分布。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-040/result.json) |
| RUX-041 | Image → 3D：保留真实OKLab nearest sample和置顶双圈高亮；图片取色打开3D折叠区，避免反馈藏在折叠栏。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-041/result.json) |
| RUX-042 | 3D → Image：保留完整像素membership mask、同一zoom/pan映射及Esc/click-away清除；只读overlay不进入导出。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-042/result.json) |
| RUX-043 | 3D Expand：复用大inspection窗、orbit/pan/zoom/reset/fit；展开后立即 bounds-aware fit。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-043/result.json) |
| RUX-044 | Toolbar 重做：原toolbar英文标签、项目控件散落末尾；中文画布/工具/编辑/排列/视图/项目分组，project picker归入项目组。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-044/result.json) |
| RUX-045 | 新建 / 打开 / 关闭画布：打开/关闭隐藏More，new外挂尾部；生命周期置首组，未保存切换执行保存/放弃/取消，已有自动保存保留。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-045/result.json) |
| RUX-046 | Toolbar 中文化：TOOLS/EDIT/VIEW/CANVAS/PROJECT改中文显示，内部类和enum不变。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-046/result.json) |
| RUX-047 | Zoom：继续使用Surface.Zoom和唯一_zoomButton，撤销/重做根据editor状态禁用。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-047/result.json) |
| RUX-048 | 工作日历顶部冗余：四态工作流图例独占标题和一行图例；删除该冗余区，日期/筛选/创建与engine保持。 | FIXED_IN_CODE | 自动回归通过，见逐项记录 | NOT_RUN | [record](../../../../artifacts/runtime-user-findings/latest/RUX-048/result.json) |
