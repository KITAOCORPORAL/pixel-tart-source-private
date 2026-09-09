# P3.6 基线审计

基线分支 `feature/asset-library-portable-focus-p35`，基线 SHA `427c079b71dad83375e69692fc83b829db569666`。工作树在创建分支前干净；随后发现并保护了其他任务已有修改，未将其纳入 P3.6 提交。

素材库入口为 `AssetLibraryPage.xaml`、`AssetLibraryPage.cs`、`AssetLibraryViewModel*.cs`；容器/锁/迁移/包服务位于 `RAWSelectionAssistant.Core/Services/AssetLibrary`。现有三栏宽度、折叠状态、缩略图、视图、排序、查询、选择和滚动锚点复用 `AssetLibraryWorkspaceSettings`；四种视图共用 `AssetCards`、查询和选择集合，虚拟化由 `VirtualizingAssetPanel` 与 `AssetLibrarySelectionListBox` 提供。

基线已有 P2 素材菜单和事务化浏览命令，但右键未选中卡片不会先提升选择，视觉菜单仍有 code-behind Click，且缺少固定的分层菜单架构。P3.6 已加入右键选择策略和分层菜单；无真实 API 的默认应用、编辑、工作流、策划和回收站命令采用明确禁用门禁。

P3.5 没有托盘模型或持久化服务。本轮新增 `SqliteInspirationTrayService`，使用独立应用数据 SQLite，按 `LibraryId + AssetId` 去重，持久化 `ContentHash`、排序、来源上下文和解析状态；不复制或修改源文件。素材页提供托盘数量入口和底部抽屉。

稳定引用复用 P3.5 的 `AssetLibraryStableReference`。当前库 ID 从容器 manifest 读取；无法读取 manifest 的旧固定库使用数据库路径稳定派生值，避免暴露临时路径。P3.6 未升级 schema v7、`.ptlibrary` v1 或 `.ptpack` v1。

本轮未实现 P4 策划中心、非破坏像素编辑引擎、外部应用打开、文件资源管理器定位、工作流写入和回收站删除；这些入口均显示为禁用能力门禁，不冒充已完成操作。
