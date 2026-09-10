# Eagle → Pixel Tart 功能映射

日期：2026-09-09。本文是产品映射与提案，不是代码变更或运行验收。`A` 直接借鉴，`B` 借鉴交互后摄影化，`C` 暂缓，`D` 不做。状态依据当前 HEAD `73530b9`、未提交工作树的只读审计及 [05 缺口](./05_PixelTart素材库缺口.md)。工作树原有 17 个源码文件改动未触碰，历史验收不自动覆盖这些改动。V01–V06 依据外部辅助报告 `D:\AI AGENT\eagle-analysis\2026-09-09\prior\prior-videos.md`，V07 为本轮主录屏事件摘要；正式视频证据统一查 [01 索引](./01_Eagle录屏索引.md)。P0～P3 在本文表示优先级，不等同历史开发阶段编号。当前未发现应在本表强造的 P0 阻断项。

## 证据索引

| ID | 来源与时间 | 本次可引用事实 |
|---|---|---|
| V01 | `bandicam 2026-09-09 10-43-33-156.mp4`，00:20、00:30、02:25、06:20 | Library 二级菜单、库选择器、eaglepack 文件、导入窗口 |
| V02 | `10-53-44-561.mp4`，00:00–03:40、07:02 | 大图预览、瀑布流、多选 Inspector、右键菜单 |
| V03 | `11-01-20-736.mp4`，00:20–00:55 | ARW/JPG/PNG 混排、左栏计数、选择变化 |
| V04 | `11-03-45-674.mp4`，00:32、00:36 | 搜索浮层、智能文件夹加载占位；浏览器/Photoshop 是协同环境 |
| V05 | `11-08-00-384.mp4`，00:12、00:48、01:42 | 浏览器收藏弹窗、重复素材提示、链接 Inspector |
| V06 | `11-11-30-481.mp4`，00:19、00:23、00:33、00:38 | 后台导入进度、重复提示、切库风险警告和加载遮罩；强制切库可选，不是绝对阻止 |
| V07 | `bandicam 2026-09-09 11-32-33-636.mp4` | 00:02 删除失败任务确认；00:12 更多筛选/图钉；02:13 宽高范围；02:19 语义搜索输入框未执行；02:23 标签包含/排除/Esc；03:37 字母组/双布局；03:51 组内联编辑；03:58–04:02 标签浮层与新标签数量；04:20–04:43 切库、渐现、5/2/1 列密度；04:48 多格式缩略图；05:13 回收站、05:21 永久删除确认；05:49 触边菜单；05:55 导出子菜单。00:39–01:57 AI 动作只是说明页截图，不证明安装或执行。 |

快捷键或按键行为仅在视频明确显示时采信；V01–V06 的历史说明中已明确未可靠捕获 Ctrl/Shift/Alt/Delete/Enter 实际按下。V07 的“左键/右键/方向键/Esc”作为画面可见交互记录，仍需运行验收复核。

## 功能映射

| Eagle 功能（旧 F） | Eagle 行为/证据 | 今日 Pixel Tart 状态 | 是否需要 | 分类 | Pixel Tart 设计（提案或复用） | 优先级 |
|---|---|---|---|---|---|---|
| 启动与单库（F-001） | 启动后恢复图库 | 单一同窗 Shell，模块 route 已有 | 需要 | A | 复用宿主导航与安全恢复 | P2 |
| 资源库创建/打开（F-004/005） | V01 Library 菜单和系统选择器 | P3.5 已有 `.ptlibrary` 创建、打开、最近库 | 需要 | B | 入口保持库名菜单，显示库 ID/离线状态 | P1 |
| 库切换风险（F-005） | V06 任务中切换给取消/强制切换；V07 04:20 切库渐显 | 会话、写锁、新页初始化和任务 drain 已有 | 需要 | A | 风险摘要后允许安全取消/分段，再提供强制切换；不绝对阻止 | P1 |
| 合并/迁移库（F-006） | V01 菜单可见合并/eaglepack | P3.5 `.ptpack`、预览、幂等合并已实现 | 需要 | B | 只合并 Pixel Tart 引用和元数据，先预览映射 | P2 |
| 文件导入（F-013/014） | V01 文件选择与导入空状态 | Reference/Managed、hash 去重已实现；任务中心未覆盖普通导入 | 需要 | B | 导入任务绑定 LibraryId，提取 EXIF/尺寸；不写源 | P1 |
| 剪贴板/网页采集（F-016/021） | V05 浏览器扩展收藏弹窗、目标文件夹/标签 | 无网页扩展；灵感托盘只存引用 | 暂缓 | C | 仅把外部来源 URL 作为可选引用，待安全评审 | P3 |
| 重复检测（F-046） | V05 “使用已有/保留两者”单选 | 导入 hash 去重有；独立扫描处置无 | 需要 | B | 按 hash+项目分组，关联已有或建立托管副本；先预览 | P2 |
| 文件夹树（F-010/022/025） | V01/V02 树、目标选择、多选归类 | 无限层级树、成员关系、移动排序、Undo 已有 | 需要 | A | 复用多对多文件夹；项目目录只是关系视图，不强迫单一父级 | P1 |
| 智能文件夹（F-011/023/044） | V04 搜索浮层与智能文件夹加载占位 | P3 AST、嵌套 AND/OR、锁定、保存已完成 | 需要 | A | 复用 query 文档；项目状态作为受控字段，不解析标签名 | P1 |
| 标签组/标签（F-028/029/030） | V07 字母分组、近期/推荐/组、内联编辑、数量更新 | P3 生命周期、合并预览、批量编辑已实现 | 需要 | B | 自由标签与业务状态分离；保留组、搜索、包含/排除和 Esc | P1 |
| 标签浮层键盘（F-029） | V07 03:58 方向键、Enter、Esc；02:23 左键包含/右键排除 | 部分 UI 已有，快速分类器焦点未闭环 | 需要 | B | 统一选择/排除语义，IME 时快捷键让位输入法 | P2 |
| 评分/批注（F-028/041） | V02 多选 Inspector 批量注释/评分 | metadata command、混合值、journal 已有 | 需要 | A | 评分是价值/完成度；客户意见走 SelectionComment | P1 |
| 未分类/未标签集合（F-008/009） | V03 左栏显示即时计数 | 系统集合与查询已实现 | 需要 | A | 保留即时计数；项目收件箱可作为独立关系投影 | P2 |
| 四种布局（F-047） | V02/V03 瀑布流、横竖原比例 | Grid/Masonry/Justified/List 已实现 | 需要 | A | 复用共享 query/selection；图片保持主导面积 | P1 |
| 密度/列数（F-045） | V07 04:22–04:43 5/2/1 列、原比例渐显 | 列数/布局状态持久化；精确锚点仍缺运行证据 | 需要 | B | 改变密度保留选中 AssetId 与首个可见项锚点 | P2 |
| 格式角标与缩略图（F-040/051） | V03 ARW/JPG/PNG；V07 ARW/TIF/MP4/GIF/PNG/SVG 可见缩略图 | 分类与栅格缩略图部分实现，导入白名单漏 PSD/视频 | 需要 | B | 格式感知卡片；特殊格式显示能力/代理状态 | P1 |
| 大图预览（F-048/061） | V02 深色画布、右 Inspector | 当前“打开大图”仅展开 Inspector，独立 Viewer 禁用 | 需要 | B | 四命令分开：检查器、只读 Viewer、外部应用、定位 | P1 |
| 摄影格式 Viewer（F-051/052/058） | V02 证明普通图片大图预览；V07 特殊格式只证明缩略图，MP4 右键未证明播放 | PSD/视频/完整 RAW Viewer 缺失 | 需要 | B | Viewer Registry：栅格/代理/视频首帧/RAW 代理，失败可解释 | P1 |
| 通用文档深预览（F-054～057） | 历史源码列出 PDF/字体/3D 等 Viewer；本轮未验证这些深层操作 | 素材库没有通用深预览闭环 | 暂缓 | C | 核心摄影预览完成后再评估 PDF；复杂字体/3D 不进入当前路线 | P3 |
| 搜索建议与范围（F-034/035） | V04 浮层含文件夹/标签/智能文件夹/文档；V07 搜索框/图钉 | P3 建议、历史、当前/全库、锁定已实现 | 需要 | A | 复用 P3 composer；图钉表示锁定条件，不制造第二查询 | P1 |
| 更多筛选（F-038～040） | V07 00:12 更多搜索、图钉；02:13 宽高范围 | 评分/日期/尺寸/视觉字段已支持；普通导入尺寸常为空 | 需要 | B | EXIF/尺寸回填后提供范围条件；无可靠语义字段不伪造 | P1 |
| 语义/以图搜索（F-042） | V07 02:19 语义输入框未执行；AI 00:39–01:57 仅说明页截图 | 未接 AI/MCP，Provider 默认 None | 暂缓 | C | 仅保留能力接缝；待本地模型、安全与可解释性评审 | P3 |
| 多选与 Inspector（F-059） | V02 “已选 N 个文件”、批量字段；V07 05:49 六选菜单 | P2/P3 三态检查器、共享选择已实现 | 需要 | A | 客户已选/初选来自事实关系，不能覆盖临时 UI selection | P1 |
| 右键分层菜单（F-032/061） | V02/V07 右键；V07 05:49 菜单向左避屏边 | P3.6 分层菜单、未选先单选、多选保留已实现 | 需要 | A | 菜单按查看/整理/工作流/导出/危险分组；禁用项说明原因 | P1 |
| 拖放/排序（F-025/047） | V01/V02 目标文件夹、拖放重排线索 | metadata-only 拖放与虚拟布局已实现 | 需要 | A | Ghost、数量、目标高亮和冲突预览；不拖动源文件 | P2 |
| 删除到回收站（F-033） | V07 05:13 三选入回收站；恢复未拍 | Pixel Tart 回收站仍禁用，仅归档/恢复 | 需要 | B | Reference 只改 metadata；Managed 保留原位置；恢复失败可见 | P1 |
| 默认永久删除（F-033） | V07 05:21 出现永久删除确认，05:25 为空；恢复未拍 | Pixel Tart 回收站/永久删除尚未启用 | 不需要 | D | 普通 Delete 不映射永久删除；保留现有门禁，本轮不启用不可逆能力 | P3 |
| 导出包/文件/格式/CSV（F-063/064） | V07 05:55 子菜单仅可见；V01 eaglepack 文件 | `.ptpack` 与 CSV/项目归片输出各自存在，未统一交付预设 | 需要 | B | 按项目生成交付批次、代理 JPG/水印/CSV；不做 eaglepack | P2 |
| 后台任务/进度（F-069） | V01/V05/V06 进度、失败详情、切库警告；V07 00:02 删除失败任务确认 | Task Center、分析任务、取消/drain 已有；普通导入仍缺统一任务 | 需要 | A | 阶段/数量/失败/重试；任务绑定 LibraryId；强制切库先安全收束 | P1 |
| 撤销/重做（F-069） | V02/V05 低频编辑与重复处理需可回退 | durable v2 journal 已有 | 需要 | A | 所有元数据批量先 Preview→Apply，保留 before/after | P1 |
| 错误/空/加载状态（F-083） | V01 空库；V04 灰占位渐显；V06 加载遮罩；V07 空回收站 | 组织栏/浏览器/缩略图状态壳已实现 | 需要 | A | 原因化空状态、可取消加载、重试与不支持格式说明 | P1 |
| 设置/快捷键（F-071/075） | V04 设置模态；旧源码默认键表；录屏未证实际按键 | 宿主设置、浏览快捷键部分已有 | 需要 | B | 只保留高频键；Esc/IME/焦点规则统一；不照搬 13 页设置 | P2 |
| 插件中心/导出插件（F-065～067） | 旧 Eagle 文档源码确认；V07 AI 说明页非执行 | Pixel Tart 不建第二插件市场/API | 暂缓 | C | 未来以受控扩展点接入导出，不暴露本地写 API | P3 |
| 备份/恢复与多库（F-004～007） | V01 库菜单；V06 切换确认 | `.ptpack`、锁、事务回滚、离线重定位已实现 | 需要 | B | 项目交付快照与库包分开；不把备份当回收站 | P2 |
| 复杂备份与修复中心（F-007） | 历史菜单有清缓存/修复线索；本轮没有版本化备份还原闭环 | 有包与事务保护，无完整定期备份/版本恢复中心 | 暂缓 | C | 保留可校验包与恢复契约，复杂版本管理后置 | P3 |
| 文件夹密码/设备/托盘（F-026/070/077/081） | 旧 Eagle 源码/菜单有入口，历史运行受权限限制 | 明确不复制 Eagle 独立产品能力 | 不需要 | D | 使用宿主许可与安全边界，不新增账号锁/设备中心 | P3 |
| RAW→JPG 派生（PixelTart 扩展） | Eagle 无摄影项目语义；V01/V02 有 RAW/图片浏览 | `IRawToJpegSafeConversionService`、TaskId、可选 ProjectId 已有 | 需要 | B | 记录原片→代理亲缘和任务来源；不覆盖 RAW，不等于精修完成 | P1 |
| 项目/拍摄日期关联（PixelTart 扩展） | Eagle 只有文件日期/添加日期 | `PhotoProjectRecord`、`ShootBooking.ProjectId`、`ShotCompletedAtUtc` 已有 | 需要 | B | 计划拍摄日、实际完成时间、EXIF CaptureTime 分开；显式关联 | P1 |
| 客户在线选片（PixelTart 扩展） | Eagle 无客户确认模型；V05 仅网页采集 | SelectionProject/Asset/Choice/Comment/FinalResult 与同步服务已有 | 需要 | B | SelectionProjectId/AssetId 显式映射到库稳定引用；确认版本可追溯 | P1 |
| 精修与交付状态（PixelTart 扩展） | Eagle 标签/评分不能表达事件流程 | BookingWorkflowService 有拍摄完成、后期阶段、交付/重开 | 需要 | B | 素材级状态事件化：初选→客户已选→待精修→精修中→待交付→已交付；项目状态由事件汇总 | P1 |
| 客户/模特/摄影师实体（PixelTart 扩展） | Eagle 仅通用标签/批注 | BookingContact/BookingStaffMember 归属 BookingId | 需要 | B | 复用预约人员；不建立新 CRM，不用同名标签当实体 ID | P2 |
| 财务关联（PixelTart 扩展） | Eagle 无摄影收支关系 | FinanceTransaction 支持 BookingId/ProjectId | 需要 | B | 项目上下文跳转收支与交付待办；金额不压到每张卡片 | P3 |
| 灵感托盘/策划引用（PixelTart 扩展） | Eagle 快速访问/收藏可作交互参考 | P3.6 托盘以 LibraryId+AssetId+ContentHash 引用，策划中心未实现 | 需要 | B | 托盘快照作为项目策划只读引用；resolver 区分离线/缺失/hash mismatch | P2 |

## 摄影关系模型提案（未实现）

当前 `AssetItem` 有 `AssetId`、源路径、内容 hash、CaptureTime、评分和 Comment，但没有 `ProjectId`；不要把项目关系塞进 Comment 或自由标签。建议建立独立关系层：

- `ProjectAssetLink(ProjectId, BookingId?, StableReference, Role, AddedAt, Source)`：一张素材可被多个项目引用；`StableReference` 为 LibraryId+AssetId+ContentHash；`Role` 区分原片、参考、代理、精修输出与交付版本，选择/进度另走事件。
- `AssetDerivativeLink(SourceReference, DerivedReference, DerivativeKind, TaskId, CreatedAt, ContentHash)`：连接 RAW、代理 JPG、精修版和交付版，保留转换任务来源。
- `SelectionAssetLink(SelectionProjectId, SelectionAssetId, LibraryId, AssetId, MappingVersion)`：在线选片身份与本地库稳定引用显式映射；名称匹配只能生成候选，冲突必须确认。
- `AssetWorkflowEvent(ProjectId, StableReference, EventType, Actor, OccurredAt, EvidenceId, IdempotencyKey)`：以事件记录“初选、客户确认、开始精修、完成精修、生成交付、客户确认收件”；当前状态由事件投影，重复同步不重复推进。撤销通过补偿事件保留历史，禁止批量打标签直接伪造客户确认或交付。
- `DeliveryBatch(BatchId, ProjectId, SelectionVersion, AssetReferences, Preset, CreatedAt, DeliveredAt, Confirmation)`：交付是批次事实，可重发、撤回或生成新版本；不把“已交付”直接传播为所有项目素材状态。

真实连接点：`ShootBooking.ProjectId/ClientDisplayName/StartAtUtc/ShotCompletedAtUtc`（拍摄计划与完成时间）；`SelectionProject`、`SelectionChoice`、`SelectionFinalResult`（客户选择与确认）；`SelectionResultSyncService`（RAW 匹配与冲突）；`TetherSessionRecord/TetherAssetRecord.ProjectId`（联机导入）；`PhotoProjectRecord`/`ProjectHistoryService`（归片作业）；`RawToJpegBatchRequest.ProjectId` 与任务协调器（代理生成）；`FinanceTransaction.BookingId/ProjectId`（收支）；`BookingDocumentRecord.BookingId/ProjectId`（策划/协议资料）。这些接口可复用，但尚未形成统一的项目素材关系 API。

这些模型中的 `ProjectId` 不可直接视为同一外键：`SelectionAsset.ProjectId` 指选片项目，`PhotoProjectRecord.Id` 是归片作业/项目记录；连接前必须有显式来源和映射版本。工作台只读取现有任务、Booking 和项目聚合来显示“待处理的下一步”，项目历史保留作业记录，不能把归片 Completed 当作摄影交付完成。日历继续保留既有四个主视觉状态，精修细阶段只在项目上下文展示。

| 可复用接口/模型 | 仓库相对路径与已核对行号 | 接入边界 |
|---|---|---|
| AssetItem / StableReference | `src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs:8`；`src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs:9` | AssetItem 没有项目外键；稳定引用要求完整 SHA-256 |
| 托盘集合 | `src/RAWSelectionAssistant.Core/Services/AssetLibrary/InspirationTrayService.cs:33` | 复用集合快照、解析状态；完整 resolver 仍待补 |
| 素材选片适配器 | `src/PixelTart.Modules.AssetLibrary/AssetLibrarySelectionSourceAdapter.cs:5` | QueryAssets/GetAssetSnapshot/GetProxySource 已有；完善适配，不复制查询 |
| 归片项目/历史 | `src/RAWSelectionAssistant.Core/Models/ProjectModels.cs:12`；`src/RAWSelectionAssistant.Core/Services/ProjectHistoryService.cs:29` | 保存源目录、选片输入、匹配/复制结果；只作作业事实 |
| 拍摄/后期/交付 | `src/RAWSelectionAssistant.Core/Models/ShootBookingModels.cs:78`；`src/RAWSelectionAssistant.Core/Services/Bookings/BookingContracts.cs:40` | 复用 MarkShootCompleted/SetPostProductionStage/MarkDelivered/ReopenDelivery；元数据标签不直接调用项目交付 |
| 在线选片 | `src/RAWSelectionAssistant.Core/Models/OnlineSelectionModels.cs:61`、`:124`、`:167`；`src/RAWSelectionAssistant.Core/Services/OnlineSelection/SelectionResultSyncService.cs:10` | 客户最终结果按版本映射；RAW 名称/编号匹配有冲突状态；生产云默认 Provider=None，见 `docs/architecture/OnlineSelection_API_Contract_v1.md:5` |
| 联机与现场注释 | `src/RAWSelectionAssistant.Core/Models/TetherModels.cs:11`、`:31`、`:57` | 文件稳定后登记，保留 SessionId/配对/复制 TaskId；客户现场喜欢不等于线上最终确认 |
| RAW→JPG | `src/RAWSelectionAssistant.Core/Models/RawToJpegModels.cs:81`；`src/RAWSelectionAssistant.Core/Services/RawToJpeg/RawToJpegContracts.cs:17` | 从逐项完成回调建立派生引用，失败/取消不推进精修状态 |
| 人员与客户 | `src/RAWSelectionAssistant.Core/Models/BusinessRecordsModels.cs:16`、`:31`；`src/RAWSelectionAssistant.Core/Services/Business/BusinessContracts.cs:12` | 复用 Booking 联系人/人员角色，不新建 CRM；客户显示名不作唯一 ID |
| 财务与资料 | `src/RAWSelectionAssistant.Core/Models/BusinessRecordsModels.cs:60`、`:81`；`src/RAWSelectionAssistant.Core/Models/ShootBookingModels.cs:171` | 项目/Booking 跳转收支、策划、授权资料；“商用”标签不代替授权文件事实 |

事件投影的验证场景：客户重发同版结果只更新一次；精修输出导入失败保持原阶段；仅部分照片交付时项目保留待交付汇总；重新打开交付不修改原始照片；离线库返回 `LibraryOffline` 并保留引用；包合并后按稳定 ID 映射恢复，hash 不符时阻止错误替换。这些是未来验收输入，本轮未运行。

自由标签、临时选择和业务事实严格分开：标签描述风格/题材/地点；图库 `selectedAssetIds` 只是当前操作选择；客户已选来自 `SelectionChoice`/最终确认；初选/拒片来自摄影师事件；精修与交付来自项目事件。任何状态变更均应显示来源、时间和影响范围。

## 采用顺序

1. P1：补齐 EXIF/尺寸回填、导入任务保护、回收站安全语义和只读 Viewer 能力边界。
2. P1：建立 `ProjectAssetLink`、选片映射和派生链契约，接入既有 Booking/Selection/RAW→JPG 接口。
3. P2：实现事件化精修/交付投影、交付批次预览与项目上下文筛选，保持三栏图片优先。
4. P2/P3：灵感托盘 resolver、策划只读引用、财务/文档跳转；不创建新 CRM 或第二任务中心。
5. C 项（插件/AI/复杂文档深预览/更重备份）在安全和真实需求明确后再评估；D 项（机械 Eagle 克隆、普通 Delete 默认永久删、破坏性强制切库）保持排除。

## 来源与阅读顺序

- 历史 Eagle 原文：`D:\SOFTWARE\eagle\docs\software-analysis\README.md`、`00_overview.md`–`08_learning_notes.md`；其中 `03_ui_layout.md:5-31,97-127,175-196` 是页面/菜单/快捷键索引，`04_runtime_test.md:38-109` 是隔离库运行证据，`08_learning_notes.md:1-35` 是可复用交互原则。
- Pixel Tart 当前实现：[`P3_CURRENT_STATUS.md`](../../implementation-reports/P3_CURRENT_STATUS.md)、[`P35`](../../implementation-reports/P35_PORTABLE_LIBRARY_AND_FOCUS_WORKSPACE_2026-09-08.md)、[`P36`](../../implementation-reports/P36_EAGLE_SHELL_CONTEXT_AND_INSPIRATION_TRAY_2026-09-09.md)、[`AssetItem`](../../../src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs#L8)、[`StableReference`](../../../src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs#L9)、[`AssetLibraryModule`](../../../src/PixelTart.Modules.AssetLibrary/AssetLibraryModule.cs#L6)。
- 产品约束：[`PixelTart_UX_Container_Rules_v1.md`](../../design/PixelTart_UX_Container_Rules_v1.md)、[`PixelTart_Accessibility_Rules_v1.md`](../../design/PixelTart_Accessibility_Rules_v1.md)、[`像素蛋挞_文件安全规范.md`](../../roadmap/像素蛋挞_文件安全规范.md)。
