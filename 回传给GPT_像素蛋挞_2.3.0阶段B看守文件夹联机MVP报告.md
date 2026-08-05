# 像素蛋挞 2.3.0 阶段B：看守文件夹联机MVP报告

## 结论

阶段B已完成。功能、数据库迁移、文件安全、恢复、最小界面和专项测试均已落地；Debug 与 Release 全量均为 996/996，0失败、0跳过、0警告、0错误。未下载厂商SDK，未Publish，未生成安装包，未合并main，未创建Tag，也未自动进入阶段C。

## 1. 阶段B开始时实际HEAD

`139871ba1bd16e00184d290e2ade5c146f036fea`

提交：`docs(recovery): report stage B recovery gate fix`

开始分支：`release/2.3.0`。工作树干净，且使用了包含恢复修复与其后报告提交的最新干净HEAD，没有checkout回旧检查点。

## 2. 阶段A文档提交

`32ec8b66e1c98b5d3c369f864ce5ab915484f5c8`

已确认位于当前提交历史中，未重写历史。

## 3. 恢复门禁修复提交

`3570db5c693a024f636a541c6bf0d490a11b6655`

提交：`fix(recovery): persist abandoned association terminal state`

已确认仍为阶段B功能提交祖先；`RecoveredAssociation_AbandonPersistsAndNeverDeletesFile` 在最终 Release 二进制下再次单独通过。

## 4. 阶段B功能提交

`beab5ecdf4eace01a5bdf910e4b51accfcccd3ee`

提交：`feat(tether): add watch-folder tethering MVP`

## 5. 当前HEAD

报告生成前的功能验收HEAD：`beab5ecdf4eace01a5bdf910e4b51accfcccd3ee`。

本报告按要求单独提交，因此最终仓库HEAD会是紧随上述功能提交之后的报告提交；最终精确HEAD在任务回传中列出。

## 6. 工作树状态

阶段B功能提交完成后工作树干净。报告文件是功能提交之后唯一新增内容，并将单独提交；报告提交完成后再次核验工作树。

## 7. 产品版本

- ProductVersion：`2.3.0`
- FileVersion / AssemblyVersion：`2.3.0.0`
- 已更新统一版本属性、程序集、manifest、关于页、主界面版本文字、测试断言和2.3.0当前测试基线文档。
- 2.2.0正式安装脚本、发布目录、Tag和安装包哈希记录未修改。

## 8. SchemaVersion

`3`

默认迁移链为 Schema 1 → Schema 2 → Schema 3。Schema 2升级前先备份，迁移使用事务；失败回滚；使用 `PRAGMA integrity_check` 验证；重复执行幂等；高版本保护继续由既有门禁覆盖。

## 9. 三张新增表

Schema 3只新增三张业务表：

1. `TetherSessions`
2. `TetherAssets`
3. `TetherAnnotations`

未创建 `ProjectRelationships` 或其他阶段B业务表。数据库只保存路径、状态和元数据，不保存图片、RAW、缩略图、LUT或ICC二进制。

`TetherAnnotations`仅预留兼容结构：Rating、ColorLabel、PhotographerNote、ClientFavorite、ClientNote、IsRejected；阶段B界面不开放标注编辑器，IsRejected仅为标记，不触发删除，也未增加人脸身份字段。

## 10. Watch Folder架构

- 通用合同：`ICameraDiscoveryService`、`ICameraCapabilityService`、`ICameraTetherProvider`、`ICameraSession`、`ICameraTransferService`、`ICameraConnectionMonitor`。
- 默认相机Provider状态为 `None`；本阶段唯一实际提供者为 `WatchFolderCameraAdapter`。
- 用户必须选择明确目录并点击启动；没有自动启动、服务、托盘、浏览器、localhost或上传路径。
- `IncludeSubdirectories=false`，所有枚举固定 `SearchOption.TopDirectoryOnly`。
- 默认只处理会话开始后产生的新文件；导入已有文件必须显式勾选，并可先预览顶层候选数量。
- 软件退出时会停止并释放活动会话。

## 11. 稳定探测

实现 `IFileStabilityProbe` / `FileStabilityProbe`：

- 多次长度一致；
- 多次 LastWriteTime 一致；
- 只读打开；
- 读取必要头部；
- 检测独占写入；
- 最小稳定窗口；
- 异步、可取消、有最大等待时间；
- 不使用 `Thread.Sleep`；
- 超时或权限问题进入 NeedsAttention；
- Changed或顶层对账可以重新尝试；
- 稳定前不复制、不生成代理图。

慢写、独占占用、超时、权限变化和关闭取消均有专项覆盖。

## 12. 事件去重

- 支持 Created、Changed、Renamed、Error；
- 规范路径去重；
- 相同类型短窗口事件合并，不会阻止不同类型的Changed重试；
- 同一Session内数据库规范路径唯一；
- 临时、隐藏、中间和不支持文件过滤；
- 单个坏文件不会终止会话；
- UI快照使用100毫秒节流，并跟踪待发布任务，停止时有明确完成边界。

## 13. 溢出恢复

- 使用容量256的有界Channel；
- `BoundedChannelFullMode.Wait` 与 `TryWrite` 形成背压；
- 写入失败或 FileSystemWatcher Error 只设置一次受限补偿标记；
- 补偿只枚举当前看守目录顶层；
- 数据库资产状态是恢复真相来源；
- 已覆盖 Error补偿、100张连拍、队列边界和重复去重。

## 14. JPG/RAW配对

- 支持 JPG先RAW后、RAW先JPG后；
- 同目录；
- 规范文件名主体；
- 默认5分钟时间窗口；
- 多候选进入 NeedsAttention；
- 配对两端在同一SQLite事务中对称写入；
- 不只凭数字编号跨项目配对。

## 15. RAW边界

- 定义 `IRawPreviewDecoder`；
- 正式实现为 `NoneRawPreviewDecoder`；
- RAW无法解码时显示安全占位，不判定损坏；
- 不修改RAW，不接入来源不明解码库，不下载厂商SDK。

## 16. 代理缓存

- 支持 JPG、JPEG、PNG、TIF、TIFF；
- 最长边2048；
- WPF解码使用 `BitmapCacheOption.OnLoad`，及时释放源文件流；
- 缓存键为SHA-256不透明键，文件名不包含完整路径；
- 缓存不写SQLite；
- 默认上限512MB，按LastAccessTime进行LRU清理；
- 损坏代理可删除并重建；
- 清理缓存只删除缓存文件，不触碰原图；
- 诊断包只读取日志目录，不包含代理图。

## 17. 重启恢复

- `TetherSessions`持久化Provider、目录、状态、开始/停止、创建、最近对账和错误信息；
- `TetherAssets`持久化稳定、Ready、Copied、代理、配对与两路复制状态；
- 启动时恢复最新活动会话；
- 目录不可访问时保留NeedsAttention记录，重新连接原目录后可“恢复目录并继续”；
- Ready代理不会重复生成；
- 已有项目副本路径的Copied资产不会重复复制；
- 停止会话不删除资产记录或文件。

## 18. 安全复制

项目复制与独立备份是两个独立任务，默认均关闭。实现复用：

- TaskEngine；
- TaskOperationBridge；
- WaitForCompletionAsync；
- AwaitableProgress与DrainAsync；
- FileOperationPlan；
- FileOperationValidator；
- FileOperationExecutor；
- FileVerificationService；
- UndoJournal；
- NotificationCenter；
- AuditLog；
- ErrorCodeCatalog。

策略固定为 Copy + CreateNew + AutoNumber + Flush + 长度验证 + 可选SHA-256。不覆盖、不移动、不删除来源。复制验证成功后才写资产副本关系。数据库关系失败时文件保留并返回 PartiallyCompleted；备份失败不撤销项目副本、不停止看守，并进入 NeedsAttention。

## 19. 是否保留WaitForCompletionAsync修复

是。`TaskOperationBridge.RunAsync`仍在操作完成后调用并等待 `engine.WaitForCompletionAsync`，不会在任务终态持久化前返回。

## 20. 是否保留进度Drain

是。阶段C原有 `AwaitableProgress`被无语义变化地抽为共享正式组件；文档复制和阶段B安全复制都在执行器结束后 `await progress.DrainAsync()`，未恢复fire-and-forget进度保存。

## 21. 最小UI

- 新导航入口“联机拍摄”；
- 明确显示“当前为看守文件夹模式，并非相机USB直连”；
- 目录摘要、选择、已有文件数量预览；
- 显式启动、停止、恢复目录、错误处理/顶层核对；
- 会话、已发现、等待稳定、Ready、失败、需处理、队列和复制状态；
- 简单资产列表、JPG小代理、RAW占位、打开所在位置；
- 项目复制、独立备份、SHA-256开关；
- TetherAnnotations只展示阶段B结构边界，不开放阶段C编辑器；
- 普通归片、日历和文档页面通过可选Tether页面依赖保持独立。

## 22. 隐私

- Tether审计只写操作类型、结果、通用错误码、任务/项目/会话标识；
- 不向日志写客户姓名、电话、完整路径、文件名、显示名、哈希或备注正文；
- 复用 `AuditLogService.Sanitize` 对路径、文件名字段、显示名字段、64位哈希和密钥脱敏；
- 诊断包不包含照片、RAW或代理缓存；
- 测试全部使用独立临时目录和合成字节/合成图像，没有使用桌面、真实LocalAppData、真实照片或客户资料。

## 23. 修改和新增文件

主要新增：

- `TetherModels.cs`
- `TetherSchemaMigration.cs`
- `SqliteTetherRepositories.cs`
- `CameraContracts.cs`
- `WatchFolderServices.cs`
- `WatchFolderCameraAdapter.cs`
- `TetherSafeCopyService.cs`
- `AwaitableProgress.cs`
- `TetherProxyCacheService.cs`
- `TetherCaptureViewModel.cs`
- `TetherCaptureView.xaml/.cs`
- 五个阶段B Core/WPF/DPI专项测试文件。

主要修改：统一版本、数据库迁移链、应用组合根、App/MainViewModel/MainWindow、帮助页、AppDataPaths、既有版本/Schema断言、共享WPF逻辑DPI门禁及2.3.0基线文档。阶段B功能提交共涉及46个文件。

## 24. 新增测试数

相对恢复门禁875项，新增121项：

- Core：776 → 874，新增98；
- WPF：61 → 77，新增16；
- DPI：38 → 45，新增7。

## 25. 最终测试总数

`996/996`

## 26. Debug结果

- Build：通过；0警告，0错误。
- Core：874/874。
- WPF：77/77。
- DPI：45/45。
- 合计：996/996；0失败，0跳过。

Watch Folder会话专项类另连续执行3轮，每轮19/19通过。

## 27. Release结果

- Build：通过；0警告，0错误。
- Core：874/874。
- WPF：77/77。
- DPI：45/45。
- 合计：996/996；0失败，0跳过。

恢复门禁关键测试另在Release下单项1/1通过。

## 28. 原875项是否全部保留

是。测试总数只增加到996，没有删除、禁用、跳过恢复门禁测试，也没有降低文件保留、重启验证或终态持久化断言。

## 29. 是否下载厂商SDK

否。未下载、未引用Sony、Canon、Nikon、Fujifilm或其他厂商SDK。

## 30. 是否Publish

否。

## 31. 是否生成安装包

否。未修改2.2.0正式安装脚本，未生成2.3.0安装包。

## 32. 是否合并main

否。

## 33. 是否创建Tag

否。未创建`v2.3.0` Tag，未修改`v2.2.0` Tag。

## 34. 是否建议进入阶段C

否，不自动进入。建议先对本阶段功能提交和本报告进行人工验收；只有收到新的明确授权后才能开始阶段C。
