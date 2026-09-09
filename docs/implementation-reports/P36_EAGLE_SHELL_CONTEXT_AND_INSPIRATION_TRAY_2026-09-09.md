# P3.6 Eagle 式素材库操作壳、分层右键菜单与灵感托盘

基线：`feature/asset-library-portable-focus-p35` @ `427c079b71dad83375e69692fc83b829db569666`。  
交付分支：`feature/asset-library-eagle-shell-context-p36` @ `fb38898`（报告提交后新增文档提交）。

## 交付

- 保留 P3.5 三栏专注壳、虚拟化、异步缩略图、共享 query/selection/sort/undo 和容器锁。
- 右键未选卡片自动单选，已在多选集中的卡片保留整个选择集。
- 固定右键菜单域：预览/打开、用于策划、加入与整理、素材编辑、工作流、导出、复制、查看模式、更多、归档/回收站。
- 无真实安全 API 的入口明确禁用并说明原因；“用其他文件替换”只出现在素材编辑域；视觉动作改为命令绑定。
- 灵感托盘 SQLite 引用层：批量加入单事务、按 `LibraryId + AssetId` 去重、移除、清空、排序、解析状态、集合快照和稳定引用选择。
- 顶栏仅增加一个带数量徽标的托盘图标；底部抽屉可收起，中央画布关闭抽屉后恢复全部空间。

## 提交

- `b8b22de` feat(asset-library): add inspiration tray reference foundation
- `d506511` test(asset-library): refine inspiration tray assertions
- `29c2f8c` feat(asset-library): add hierarchical context menu and right-click selection
- `a2633d4` feat(asset-library): integrate inspiration tray and context actions
- `fb38898` test(asset-library): close P3.6 menu and tray contracts
- 报告提交：本次文档提交

## 验收

| 级别 | 结果 |
|---|---|
| CodeComplete | Release solution build 通过，0 警告、0 错误 |
| AutomatedVerified | 灵感托盘 3/3；右键菜单/选择 2/2；P3.5 回归基线保持可复用 |
| InstalledUiVerified | 未执行安装包验证，本轮未发布 RC/安装包 |
| UserVerified | `not_requested` |

定向命令：`dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj --filter FullyQualifiedName~InspirationTrayServiceTests`，3/3 通过；`dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj --filter FullyQualifiedName~AssetLibraryP36ContextMenuContractTests`，2/2 通过。Release 构建实际为 0 警告、0 错误。

截图与性能沿用 P3.5 的四 DPI、四视图基线；本轮新增菜单/托盘结构的 XAML 契约和引用服务测试，未把自动截图写成用户实测。

## 数据安全、限制与回滚

托盘只写应用数据 SQLite，不复制原图；Reference 原文件零写入。批量托盘操作使用事务和取消令牌。内容 hash 保留用于离线/重定位解析。当前托盘 UI 展示引用 ID 列表，缩略图卡片拖拽排序和完整活动库 resolver 将在 P4 前继续增强。

精确回滚点为 `427c079`（移除全部 P3.6 提交）或按上述提交逐点回退；不使用 reset、强推或历史改写。P4 进入条件：托盘 resolver 与 `.ptpack` 合并后的映射恢复、策划只读引用集合和当前策划上下文 API 稳定后，再创建 `feature/planning-center-p4`。
