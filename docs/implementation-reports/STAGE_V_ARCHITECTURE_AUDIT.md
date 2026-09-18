# Stage V 策划中心架构审计

基线：`79360e724c882136b8b38603d24e75780462af4b`  
分支：`integration/pixel-tart-developer-preview`

## 结论

Stage V 必须扩展现有项目、拍摄与引用模型，不能建立第二套 Shot、参考或色彩引擎。策划中心应作为项目流程页面进入，不新增一级导航；页面负责结构化策划与现场上下文编排，不嵌入完整素材库、灵感板或自由画布编辑器。

Stage IV 状态继续保持 **PARTIAL**。真实 App 截图、物理 DPI、物理多屏和真实相机仍未完成，不因 Stage V 开始而升级状态。

## 可直接复用

| 能力 | 当前实现 | Stage V 用法 |
|---|---|---|
| 项目 | `PhotoProjectRecord`、`IProjectRepository`、`SqliteProjectRepository` | 项目身份、名称和现有工作流状态；不创建 ProjectV2 |
| 排期 | `ShootBooking`、现有 Booking service/repository | 只按 `ProjectId` 引用日期、地点、客户和状态 |
| Shot | `ProjectShot`、`ProjectShotStore`、`ProjectShotExecution` | 唯一 Shot 模型、原子 JSON 目录、现场状态机 |
| 拍摄参考 | `ProjectShotReference`、`ShotReferenceKind` | 继续使用 Lighting/Pose/Storyboard/Styling/General |
| 现场执行 | `TetherShotExecutionViewModel`、`TetherCaptureViewModel` | 复用当前项目与 Shot 加载、姿势和完成状态 |
| 色彩优先级 | `ReferenceLookResolver` | 唯一 resolver；策划与联机共用 |
| 项目色彩方案 | `ReferenceLookStore` | 显示、默认方案与 Shot override，只存 ID |
| 项目配色/影调 | `ProjectVisualReferenceStore` | 只显示和引用现有 palette/tone 数据 |
| 自由画布 | `CanvasDocument`、`CanvasDocumentStore` | 已有 `ProjectId`；策划只保存 Canvas ID、角色和显示信息 |
| 灵感板 | `InspirationCollection`、`InspirationTrayStore` | 已有 `ProjectId`；策划只保存 Collection ID |
| 素材身份 | `AssetLibraryStableReference`、`CreativeAssetReference` | Shot 参考和项目素材关系不拥有源文件 |
| 项目素材关系 | `ProjectAssetLink`、`SqliteAssetLibraryRepository` | 新拍素材先建立既有 ProjectAssetLink，再增加非拥有型 Shot 关系 |
| 缩略图 | `AssetThumbnailReference` 与现有 preview cache | 列表用缩略图，中央预览按需加载，策划状态不存 Bitmap/blob |
| 后台任务 | 现有 Task Center | 大量导入/预览继续复用，不新建 Planning Task Manager |
| 视觉系统 | `Resources/DesignSystem` | 复用字体、间距、圆角、按钮、菜单、弹出层和滚动条 |

## 需要扩展

1. 在 `ProjectShot` 上增加预计分钟、场景、归档和拍摄时间等可选字段；保留现有身份与引用结构。
2. 在 `ProjectShotStore` 增加一次性批量保存/重排，Drop 时原子写入一次，不在 MouseMove 写盘。
3. 新增项目级 `PlanningProjectState` sidecar，保存策划摘要、当前 Shot、Canvas/灵感集引用、Shot 素材逻辑关系与修订号。
4. 新增 `ShootExecutionContext`，只传项目、排期、Shot、引用、色彩方案 ID、palette/tone 摘要和 revision，不传 Bitmap。
5. Tether 写回 Shot/姿势状态时继续调用同一个 `ProjectShotStore`；捕获素材以摄入时冻结的 context 建立幂等关系。
6. 新增项目流程内的 `PlanningCenterView` / ViewModel；入口来自排期/项目流程，不加入左侧一级导航。
7. 增加策划 UI 偏好 sidecar，保存左右栏宽度、折叠状态、上次项目和筛选，不写入项目域数据。

## 明确禁止重复实现

- 不创建 `ProjectV2`、`ShotV2`、`PlanningShot`、`PlanningReference` 或 `PlanningAsset`。
- 不重写 Asset Library、Free Canvas、灵感板、ColorCore、视觉分析、LUT 或 Tether ingest。
- 不把图片、缩略图或 Bitmap 序列化进 JSON/SQLite blob。
- 不用真实目录复制 RAW/JPEG 来表达 Project/Shot 关系。
- 不创建第二个色彩方案解析函数；统一调用 `ReferenceLookResolver`。
- 不把策划中心做成富文本、看板、无限画布、SaaS 后台或新的一级导航。

## 持久化与迁移计划

- 中央 SQLite 保持现有 Stage IV schema，不为策划元数据增加破坏性表迁移。
- 现有 `{ProjectId}.shots.json` 继续作为 Shot 真相源；新增字段均为可选并提供默认值，旧 Stage IV 文档可直接反序列化。
- 新增 `{ProjectId}.planning.json` 原子 sidecar；不存在等价于空 v1 状态，因此旧项目无损打开。
- 所有写入使用同路径进程内 gate、临时文件、flush 和原子替换；修订号单调增加。
- 测试记录迁移前后项目、排期、素材、Canvas、灵感集与色彩方案数据不变，并验证旧 Shot JSON 可读。

## 导航与数据流

`Calendar / Booking → Planning(projectId, bookingId?) → Tether(ShootExecutionContext)`

`Tether capture(frozen context) → ProjectAssetLink + ShotCaptureRelation → Planning refresh`

`Planning Shot → Asset Library(projectId + shotId)`；素材检查器反向使用同一逻辑关系返回对应 Shot。

## 风险与边界

- 当前素材库 ProjectAssetLink 位于各素材库数据库，而 Shot sidecar 位于应用数据目录；跨存储写入不能伪装成单一数据库事务。Stage V 使用幂等关系键和可重试写入，源文件始终不变。
- 物理相机、厂商 SDK、远程快门、物理混合 DPI 和真实第二屏不属于自动化通过项，继续单独报告 NOT TESTED。
- 项目模板和高价值 Undo/Redo 若会破坏原子持久化则延期到 Stage V.1。
