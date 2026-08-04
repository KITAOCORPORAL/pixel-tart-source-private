# 像素蛋挞 2.1.0 统一任务引擎与 SQLite 正式版报告

## 1. Git 基线

- 正式基线：`v2.0.4`。
- `main` 开发起点：`e699051df57a2ae55721bf294d8c0be20d95400b`，包含已封版 2.0.4 及其发布元数据。
- 2.0.4 安装包仍为 `D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.0.4\installer\像素蛋挞_Setup_2.0.4_x64.exe`。
- 2.0.4 安装包 SHA-256 复核为 `8F2131EBF6E13EDF990639F57FE8BEB189BD4829464382CADEF682CDEFAAFEFD`，未被覆盖或修改。

## 2. 分支和提交

- 开发分支：`release/2.1.0`。
- 2.1.0 源码提交：`1b43960e8f04ffc0de89efd52a0de6c2c4aa516a`。
- 版本：产品版本 `2.1.0`，文件版本 `2.1.0.0`。
- 本报告提交后将把分支快进合并到 `main` 并创建不可移动标签 `v2.1.0`。

## 3. SQLite 路径

正式数据库位于：

`%LocalAppData%\KitaoPhotoSelector\Data\pixel-tart.db`

数据库与安装目录分离，图片、RAW、缩略图和文档本体不写入数据库，只保存路径、元数据、状态与关联关系。

## 4. SchemaVersion

- 当前 SchemaVersion：`1`。
- 启动时检查数据库、应用支持版本和迁移序列。
- 高版本数据库以只读方式处理，禁止低版本应用静默写入。

## 5. 表结构

已创建 13 张正式表：`SchemaInfo`、`Projects`、`ProjectSources`、`SelectionInputs`、`MediaFiles`、`MatchDecisions`、`Tasks`、`TaskSteps`、`OperationItems`、`UndoJournals`、`QuickTools`、`AuditLogs`、`Notifications`。

未创建工作日历、预约、支付、CRM 等未来业务表。

## 6. 数据库迁移

实现 `IDatabaseMigrator`、`IDatabaseBackupService`、`IDatabaseRecoveryService`、`IMigration` 和 `MigrationResult`。迁移固定顺序、不可跳级、事务执行、失败回滚、成功登记，重复启动不会重复应用同一迁移。

## 7. JSON 迁移

已将项目记录、项目来源、选片输入、媒体索引、匹配决定和快捷工具顺序迁移到 SQLite。主题、语言、窗口尺寸和简单偏好继续保留在 JSON。迁移完成后旧 JSON 仍然保留，并写入迁移标记和迁移报告。

实际 2.0.4 → 2.1.0 隔离升级结果：Projects 1、ProjectSources 1、SelectionInputs 2、MediaFiles 1、QuickTools 2，SchemaVersion 1，`PRAGMA integrity_check=ok`。

## 8. 数据库备份和恢复

- 迁移备份目录：`%LocalAppData%\KitaoPhotoSelector\Backups\Migration\<timestamp>\`。
- 数据库备份目录：`%LocalAppData%\KitaoPhotoSelector\Backups\Database\<timestamp>\`。
- 损坏数据库不会被空库覆盖。
- 恢复操作需要用户明确确认。
- 实际升级产生 3 个旧 JSON 备份文件，原 JSON 同时保留。

## 9. 任务状态机

支持 Pending、Preparing、Scanning、Validating、WaitingForConfirmation、Running、Pausing、Paused、NeedsAttention、Retrying、Cancelling、Cancelled、PartiallyCompleted、Failed、Completed、Interrupted。合法转换集中定义，非法转换拒绝并记录审计事件。

## 10. NeedsAttention

`TaskAttentionRequest` 包含类型、标题、说明、影响数量、允许操作、默认操作、是否破坏性和创建时间。冲突、源变化、外接盘断开、空间不足、权限/锁定等不会静默覆盖，而是等待用户处理。

## 11. PartiallyCompleted

结果摘要包含 Total、Succeeded、Failed、Skipped、Cancelled、WaitingForAttention、BytesProcessed、BytesWritten。部分成功与部分失败会显示为 `PartiallyCompleted`，不会简单归类为失败。

## 12. 暂停和继续

暂停先进入 Pausing，只在文件项之间、复制/校验完成后、哈希项之间或事务提交后进入 Paused。继续从已保存检查点开始，已完成项不重复执行。

## 13. 取消

取消进入 Cancelling，在安全边界停止新增输出，只清理本任务创建且尚未完成的输出，保留源文件和已确认完成项，并持久化取消摘要。

## 14. 重试

支持整任务、失败项和 NeedsAttention 项重试；保留冲突决定，限制最大重试次数，成功输出不会重复覆盖，移动源文件不会重复删除。

## 15. 崩溃恢复

启动恢复会把异常存留的 Preparing、Scanning、Validating、Running 等任务标记为 Interrupted，保留进度和检查点。实际验证任务 `24a1a51d-1c8c-4927-bdb9-e509cec09a36` 从 Running 恢复为 Interrupted，进度 37%、检查点 `copy`、错误码 `InterruptedByShutdown`。移动、覆盖、删除、数据库恢复和其他高风险操作不会自动继续。

## 16. 文件操作清单

实现 `FileOperationPlan`、Planner、Executor、Validator、ConflictResolver 和 Verification。执行前检查来源、目标关系、禁止路径、可写性、空间、外接盘、文件锁、冲突、预计数量和字节；默认复制、不覆盖、不删除、冲突自动编号、目标 `CreateNew`。

## 17. UndoJournal

撤销复制、移动、重命名、整理结构和任务输出前会检查任务所有权、路径占用、大小/哈希和用户修改。条件不满足时拒绝该项并进入 NeedsAttention，绝不覆盖未知文件。

## 18. NotificationCenter

实现 Toast、InlineError、Modal、TaskNotification、EmptyState、SystemBanner 类型；同类通知可合并和节流，长任务统一进入任务中心，部分失败显示摘要与清单入口。

## 19. ErrorCodeCatalog

已建立文件系统、图像、任务、数据库和网络预留错误码目录，包括 SourceNotFound、DiskSpaceInsufficient、FileLocked、HashMismatch、InvalidStateTransition、InterruptedByShutdown、MigrationFailed、DatabaseCorrupted 等。

## 20. AuditLog

记录任务创建、计划确认、状态转换、冲突决定、复制/移动、校验失败、撤销、迁移、恢复和高风险确认。日志对完整路径与秘密字段脱敏，支持轮换、保留天数、总容量限制、诊断包导出和用户清理。

## 21. 任务中心 UI

新增 `TaskCenterViewModel`、`TaskDetailsViewModel`、`RecoveryCenterViewModel`、`NotificationCenterViewModel`、`DatabaseRecoveryViewModel`。右侧任务中心仅订阅快照，显示状态、进度、步骤、当前文件、结果和错误，并提供暂停、继续、取消、重试、处理冲突、回滚与放弃操作。

真实启动验收发现并修复了进度条只读属性的默认双向绑定问题，改为显式 `Mode=OneWay`。修复后隔离版和安装版均持续运行，窗口标题为“像素蛋挞”。桌面截图采集组件对该 WPF 窗口返回 `SetIsBorderRequired` 不支持接口，因此未伪造截图；限制记录在 `artifacts\ui-review\2.1.0\运行时验收说明.md`。

## 22. 接入的现有工具

已接入统一任务引擎：本地分片扫描、JPG/RAW 索引、匹配、归片复制、整理图片复制、整理图片移动、整理图片撤销、拼图导出、报告生成。文件复制和整理流程同时接入安全边界与操作清单。

## 23. 未接入工具及原因

批量压缩、水印、删废片、FTP、转档等仍为预览壳或未形成稳定真实执行链的工具，只保留 TaskType/接口准备，不创建虚假完成任务。未删减 2.0.4 已可用功能。

## 24. 工作日历预留接口

新增 `ILocalReminderScheduler`、`IReminderRepository`、`IProjectRelationshipService` 以及 ReminderDefinition、ReminderTrigger、ReminderStatus。提醒调度默认关闭；没有侧栏入口、页面、业务表、系统通知或后台偷偷启动行为。

## 25. 修改文件

修改版本/构建/安装：`CHANGELOG.md`、`README.md`、`build/Version.props`、`build_debug.ps1`、`build_release.ps1`、`installer/RAWSelectionAssistant.iss`。

修改核心与接入：Branding、Core csproj、FileLogService、MediaCopyService、MediaIndexService、OrganizeService、ProjectHistoryService、AppDataPaths、App.xaml.cs、MainWindow XAML/代码、应用 csproj、MainViewModel、ToolPageViewModels、HelpWindow、app.manifest，以及原有版本/UI 回归测试中的版本断言。

## 26. 新增文件

新增任务、文件安全、通知、提醒模型；数据库迁移/仓储/恢复服务；任务引擎、状态机、调度、桥接、恢复协调器；文件计划、验证、执行、校验、撤销服务；日志维护；组合根；任务中心 ViewModel；8 个 2.1.0 专项测试文件。

## 27. 测试总数

- Core/业务测试：582/582。
- WPF 测试：5/5。
- DPI 测试：27/27。
- 合计：614/614，通过 614，失败 0，跳过 0。
- Release 编译：0 警告，0 错误。
- 最终 TRX：`D:\AI AGENT\RAWSelectionAssistant\artifacts\tests\2.1.0\final-20260804-1054\`。

## 28. 压力测试

覆盖 10,000 媒体索引、5,000 OperationItems、1,000 任务历史、500 失败项、100 NeedsAttention、进度节流、并发连接、数据库重启和检查点恢复。集合与委托历史有上限，连接显式释放。

## 29. 从 2.0.4 升级测试

使用正式 2.0.4 安装包安装到隔离目录，启动 2.0.4 隔离实例后用正式 2.1.0 安装包覆盖升级。升级后产品版本 2.1.0、文件版本 2.1.0.0，自动迁移成功、项目历史和快捷工具保留、旧 JSON 与备份保留、数据库完整性为 OK。升级安装/卸载日志位于 `artifacts\install-verification\2.1.0\`。

## 30. 数据迁移失败测试

自动化覆盖迁移失败事务回滚、损坏数据库不覆盖、高版本阻止写入、损坏 JSON 单项不阻断其他数据、旧 JSON 保留和恢复入口。所有测试通过。

## 31. 安装、启动、重启和卸载

- 全新静默安装成功。
- 安装后隔离实例创建数据库、设置和日志，窗口标题正确，进程响应正常。
- PE Subsystem=2，确认为 Windows GUI / WinExe，无控制台。
- Provider=None，Release 未启用 Mock。
- 普通卸载成功，安装目录移除，用户数据库默认保留。
- 2.0.4 → 2.1.0 覆盖升级和升级后卸载成功，迁移数据及旧 JSON 仍保留。

## 32. 最终提交

- 源码发布提交：`1b43960e8f04ffc0de89efd52a0de6c2c4aa516a`。
- 文档提交完成后，`release/2.1.0` 将快进合并到 `main`，随后创建并验证 `v2.1.0`。

## 33. 安装包路径

`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.1.0\installer\像素蛋挞_Setup_2.1.0_x64.exe`

## 34. 安装包大小

`49,683,075` 字节。

## 35. SHA-256

`7BF6FF9636ACE34F026D64DDC3103BDF37E3B142614D79F195B5221CCCF6C6E8`

## 36. release-manifest

`D:\AI AGENT\RAWSelectionAssistant\artifacts\releases\2.1.0\release-manifest.json`

Manifest 记录版本、源码提交、标签、构建时间、运行时、测试统计、安装包大小/哈希、Provider=None、SchemaVersion、迁移与恢复验证、DPI 模式和已知限制。

## 37. 已知限制

1. 授权 Provider 仍为 None，未接生产授权后台。
2. 安装包未进行代码签名。
3. 工作日历只预留接口，不提供页面或后台提醒。
4. Windows 桌面采集组件无法截取当前 WPF 窗口；实际启动、标题、响应、日志、自动化 UI 结构/键盘/无障碍测试均通过，但本轮没有新增前台截图证据。

## 38. 是否建议进入 2.2.0

建议在用户确认 2.1.0 封版结果后进入 2.2.0。当前轮次不会自动开始工作日历开发。
