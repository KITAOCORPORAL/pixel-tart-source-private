# 回传给 GPT：像素蛋挞 2.3.0 阶段 A 联机拍摄技术规划报告

## 1. 阶段 A 结论

像素蛋挞 2.3.0 阶段 A 技术规划已完成。已从正式 `v2.2.0` 创建 `release/2.3.0`，完成本地架构审计、Watch Folder MVP、现场监看、LUT/ICC、第二显示器、四家厂商 SDK、测试与阶段排期规划。

本轮只新增文档，没有修改产品代码、产品/文件版本、SchemaVersion、迁移、Publish、安装包、main 或 Tag。没有下载或接入真实厂商 SDK。

## 2. 当前正式基线

| 项目 | 核验结果 |
|---|---|
| 仓库 | `D:\AI AGENT\RAWSelectionAssistant` |
| 正式版本 | `2.2.0` |
| 文件版本 | `2.2.0.0` |
| 正式 Tag | 带注释 Tag `v2.2.0`，存在 |
| Tag 目标 | `1c9f8e7a7334b29b0081a1ac83faf55882bf6b46` |
| 正式二进制源 | `f72a0caf264022a52705ebc8a03470d2e52417dd` |
| 二进制源关系 | `f72a0caf` 是 `v2.2.0` 目标提交的祖先，包含在正式封版历史中 |
| 2.2.0 测试基线 | `867/867`（正式封版报告） |
| SchemaVersion | `2` |
| 正式安装包 | `artifacts\releases\2.2.0\installer\像素蛋挞_Setup_2.2.0_x64.exe` |
| 安装包 SHA-256 | `B4638CBCB8467B30EA21F474ED2E15603A1A6621081F5E05DB75C3177B313788`，重新计算一致 |

说明：`v2.2.0` 是带注释 Tag，指向正式合并提交 `1c9f8e7`；用户指定的正式二进制源 `f72a0caf` 位于该 Tag 的祖先链。阶段 A 分支严格从 Tag 创建，没有改写这两个提交。

## 3. Git 与分支

- 开始核验时 `main` 工作树干净。
- 已执行从 `v2.2.0` 创建 `release/2.3.0`。
- 当前分支：`release/2.3.0`。
- 当前 HEAD：`1c9f8e7a7334b29b0081a1ac83faf55882bf6b46`。
- 当前工作树仅包含本阶段明确要求的规划文档和本报告；没有产品源代码修改。
- 未使用 `git reset --hard`，未强制清理、覆盖或删除文件。

## 4. 是否修改代码和版本

- 产品代码：未修改。
- 测试代码：未修改。
- `build/Version.props`：仍为产品版本 `2.2.0`、文件版本 `2.2.0.0`。
- SchemaVersion：仍为 `2`。
- Schema 迁移：未修改。
- 2.2.0 Tag、正式二进制源和安装包：未修改。

产品版本规划为 2.3.0，但只在阶段 B 获得明确授权后才改为 `2.3.0`/`2.3.0.0`。

## 5. 本地架构审计

已确认可直接复用：

- `ApplicationCompositionRoot`
- `TaskEngine`
- `TaskOperationBridge`
- `FileOperationPlan`
- `FileOperationValidator`
- `FileOperationExecutor`
- `FileVerificationService`
- `NeedsAttention`
- `PartiallyCompleted`
- `UndoJournalService`
- `NotificationCenter`
- `ErrorCodeCatalog`
- `AuditLogService`

当前没有相机、LUT、ICC、多显示器或联机监看服务。规划采用新增独立模块和接口，不把设备/文件 I/O 放入 `MainWindow` 或 ViewModel。

## 6. 通用相机架构

已规划以下接口职责和依赖边界：

- `ICameraTetherProvider`
- `ICameraDiscoveryService`
- `ICameraSession`
- `ICameraCapabilityService`
- `ICameraCaptureService`
- `ICameraLiveViewService`
- `ICameraTransferService`
- `ICameraSettingsService`
- `ICameraConnectionMonitor`

能力按设备逐项声明，UI 只依据能力快照启用或禁用功能，并显示不可用原因。厂商类型不进入核心业务，不在 ViewModel 直接调用 SDK。

2.3.0 正式 Provider 只规划 `None` 和 `WatchFolderCameraAdapter`。Sony/Canon/Nikon/Fujifilm Provider 默认推迟到 2.4.0；未来原生 SDK 建议使用独立 x64 Host 进程和本机命名管道隔离，不开放 localhost。

总体架构文档：`docs\roadmap\2.3.0\像素蛋挞_2.3.0联机拍摄总体架构.md`。

## 7. 看守文件夹 MVP

结论：可行，且是 2.3.0 风险最低、最有价值的首个联机能力。

核心方案：

- 用户显式选择一个目录并开始会话。
- 只监视顶层，`IncludeSubdirectories=false`，不递归、不扫描整盘。
- `FileSystemWatcher` 只提供低延迟提示；数据库状态和受限补扫是真相来源。
- 有界事件队列、Created/Changed/Renamed 合并、溢出后顶层对账。
- 连续长度/LastWrite 采样、占用/头部检测和有上限稳定等待。
- JPG/JPEG/PNG/TIFF 代理预览；受支持 RAW 进入资产列表，无解码器时安全占位。
- JPG/RAW 支持两种到达顺序和保守配对。
- 断盘、目录删除、权限变化和应用重启进入可恢复状态。

默认只读，不移动、不删除、不覆盖、不上传、不改 EXIF、不写 RAW。

详细文档：`docs\roadmap\2.3.0\像素蛋挞_看守文件夹联机MVP.md`。

## 8. 文件复制安全

可选“复制到项目拍摄目录”和“同步到备份盘”不建立第二套系统，固定复用现有 Task/FileOperation/Undo 架构：

- Copy
- CreateNew
- AutoNumber
- Flush
- 长度校验
- 重要/备份任务可选 SHA-256
- 复制成功并校验后才写数据库关系
- 不覆盖、不移动、不删除源文件
- 部分成功映射 `PartiallyCompleted`
- 断盘/权限/冲突映射 `NeedsAttention`
- Undo 只删除本任务创建且未被外部修改的副本

项目复制和备份是两个独立任务；备份失败不阻止监看或已成功的项目副本。

## 9. 数据与 Schema 提案

阶段 A 没有修改 SchemaVersion。阶段 B 开始前建议单独审查 Schema 3：

- `TetherSessions`
- `TetherAssets`
- `TetherAnnotations`

数据库只保存路径、元数据、状态、星级、颜色标签、收藏和备注关系，不保存图片、RAW、缩略图、LUT 或 ICC 二进制。代理图放受限缓存。

如果 Schema 3 未获批准，阶段 B 可以先交付无持久标记的最小会话，但不建议另建临时 JSON 业务数据库；应缩小范围而不是制造第二套持久化。

## 10. 现场监看架构

结论：可行。

规划三栏工作区：

- 左：虚拟化拍摄缩略图、筛选、配对和标记状态。
- 中：大图、Fit/100%/自由缩放、拖动、全屏、并排/重叠比较、参考图和辅助线。
- 右：EXIF、RGB 直方图、高光/阴影警告、LUT、星级、颜色标签、备注和客户确认。

自动最新和锁定逻辑分离；用户检查旧图或 100% 时新照片不抢焦点。代理图后台解码，100% 按需读取，旧请求可取消，源文件流立即释放，列表不长期持有所有大图 Bitmap。

星级、颜色标签、摄影师备注、客户收藏/备注和快速拒绝只写本地元数据；快速拒绝不删除照片。

详细文档：`docs\roadmap\2.3.0\像素蛋挞_现场监看工作区规划.md`。

## 11. LUT 实现方案

结论：CPU 代理图 LUT 在 2.3.0 可行；GPU 路线应在阶段 E 基准后决定。

规划：

- 安全解析 `.cube`。
- 支持经验证的 1D 和 3D 子集。
- 1D 线性插值，3D MVP 使用三线性插值。
- 收藏、搜索、项目默认、相机默认、强度、开关、分屏和快速切换。
- 代理图 CPU 后台计算、有界并发、取消和缓存。
- 损坏/超限/混合方言安全拒绝并回退原图。
- `.cube` 不完整声明输入/输出空间，因此未知或 Log LUT 必须提示，不能假装色彩正确。

LUT 默认只影响显示；任何导出套用 LUT 都属于后续独立任务，需用户明确选择。

## 12. ICC 边界

结论：基础每屏 ICC 检测和转换可行，但不能宣称硬件校准或完整软打样。

规划使用：

- Windows `WcsGetDefaultColorProfile` 获取每台显示器默认配置。
- WPF `ColorContext` 表示 ICC/ICM。
- `ColorConvertedBitmap` 或等价受控转换映射到目标显示器。
- 主屏和客户屏分别转换；窗口跨屏或配置变化时失效对应缓存。

MVP 支持 sRGB 工作空间、嵌入 ICC 识别、Display P3/Adobe RGB/无配置提示。RAW 嵌入预览只代表预览，不代表最终显影；未经校色的屏幕不宣称色准。

详细文档：`docs\roadmap\2.3.0\像素蛋挞_LUT与ICC色彩规划.md`。

## 13. 第二显示器方案

结论：可行。

关键设计：

- 独立 `ClientMonitorWindow` 和协调器。
- 以设备名/EDID 摘要形成 StableKey，不持久化“显示器 2”数组下标。
- 用户显式选择显示器后才打开；单屏不自动主屏全屏。
- 客户屏可跟随最新、跟随主选择或独立锁定。
- LUT 同步，客户屏按自身 ICC 独立转换。
- 默认隐藏路径、文件名、技术参数、客户资料和序列号。
- 客户可收藏和备注，但没有删除/移动/复制权限。
- 目标屏断开时默认关闭客户窗口并把控制返回主窗口；联机会话和任务继续。
- 混合 DPI 和窗口离屏恢复由协调器处理。

详细文档：`docs\roadmap\2.3.0\像素蛋挞_第二显示器监看规划.md`。

## 14. SDK 厂商调研矩阵

| 顺序 | 厂商 | 结论 | 风险 |
|---:|---|---|---|
| 1 | Sony | 功能和公开许可相对清晰，适合作为首个真实适配器 | 中 |
| 2 | Canon | EDSDK 技术成熟；许可全文和分发范围需登录后确认 | 高 |
| 3 | Nikon | 统一 Z SDK 已发布，但无官方技术支持，完整分发许可待申请后确认 | 高 |
| 4 | Fujifilm | 公开 SDK/EULA 清晰，但使用 SDK 控制相机会触发厂商保修例外和客户告知义务 | 极高 |

详细矩阵：`docs\roadmap\2.3.0\像素蛋挞_相机厂商SDK调研矩阵.md`。

## 15. 建议适配顺序和首个适配器

建议顺序：Sony → Canon → Nikon → Fujifilm。

首个真实厂商适配器建议 Sony，首台测试机建议 `ILCE-7M4`，并再增加一台不同代际官方支持机型验证能力差异。

即便 Sony 风险相对最低，也建议进入 2.4.0；2.3.0 只交付 Watch Folder，除非另行批准、取得 SDK 和实机，并通过全部许可/安装包门禁。

## 16. 需要的测试相机

- Sony：ILCE-7M4 + 一台不同代际官方支持机型。
- Canon：EOS R5 Mark II + 一台入门 R 系列，但采购前必须在当前登录门户复核支持矩阵。
- Nikon：Z6III + Z6II，覆盖统一 Z SDK 和旧兼容路径。
- Fujifilm：X-T5 或 X-H2S；如实际业务需要再增加一台 GFX。

没有实机时只允许合同/Fake Host 自动测试，Fake 只在测试项目存在，不注册 Release。

## 17. 第三方许可风险

最高风险：Fujifilm Camera Control SDK 的厂商保修例外和对客户的强制告知义务，同时包含开发者自行支持、合规和责任承担要求。

Canon 和 Nikon 的重要风险是公开页面无法确认完整再分发条款；在取得当前许可全文前，必须按“禁止随安装包分发”处理。

Sony 公开 EULA 对二进制不可分离分发较明确，但仍有最终用户告知、支持、更新、出口和用途限制，不能视为零风险。

## 18. 性能风险

- FileSystemWatcher 事件重复和内部缓冲区溢出。
- 大 RAW 慢写导致误判稳定。
- 连拍造成事件、解码和 UI 更新洪峰。
- WPF 大图/100% 解码导致内存峰值和文件锁。
- CPU 3D LUT 在大代理图上的延迟。
- 两屏不同 ICC/DPI 导致缓存翻倍和重渲染。
- RAW 无合法解码器时只能占位/JPG 配对。

缓解：有界队列、受限补扫、稳定探测、最新优先、并发限制、取消、代理图、LRU、每屏只缓存最终输出、GPU 不可用时 CPU 降级。

## 19. 文件安全风险

主要风险：误读未完成文件、重复复制、同名覆盖、断盘部分完成、撤销误删、递归扫描无关目录和日志泄露路径。

固定防护：默认只读、顶层目录、Copy/CreateNew/AutoNumber/Flush/校验、成功后写关系、PartiallyCompleted/NeedsAttention、严格 Undo 前置条件、源文件哈希不变测试和日志脱敏。

## 20. 测试计划

测试计划覆盖用户要求的全部场景：新文件、慢写、重复、临时改名、连拍、JPG/RAW 顺序、断盘、目录删除/权限、中文/空格/长路径、大文件、取消、重启、缓存、LUT 损坏/不兼容/开关/源零修改、显示器断开/编号变化、100–200% DPI、主题、无障碍、Fake/Provider None/WinExe/无 localhost。

阶段 A 因只新增文档，没有重跑 867 项。阶段 B 开始时必须先恢复基线并运行全量；各阶段结束时 Debug/Release 全量均需 0 失败、0 跳过、构建 0 错误。

测试文档：`docs\roadmap\2.3.0\像素蛋挞_2.3.0测试计划.md`。

## 21. 阶段 B 实施清单

阶段 B 清单已经细化到恢复门禁、合同/Schema、Watch Folder、稳定/去重、JPG/RAW、统一任务复制、组合根/UI、隐私和测试退出门禁。

完整路径：`D:\AI AGENT\RAWSelectionAssistant\docs\roadmap\2.3.0\像素蛋挞_2.3.0阶段实施排期.md`。

阶段 B 不会自动开始，必须另行明确授权。

## 22. 预计新增文件

阶段 B 预计新增：

- Camera/Tether 模型和合同。
- Watch Folder Adapter、事件源、稳定探测、去重、配对、协调器和 Repository。
- 代理缓存服务和 RAW Preview Decoder 边界。
- 经批准的 `TetherSchemaMigration`。
- 联机工作区最小 View/ViewModel。
- Core、WPF 专项测试。

阶段 C/D 再增加 Preview、Histogram、Annotation、LUT、Color、Monitor、ClientMonitor 组件和测试。详细建议文件名见阶段排期文档。

## 23. 预计修改文件

阶段 B 获授权后预计修改：

- `build/Version.props`
- `ApplicationCompositionRoot.cs`
- 主导航/MainViewModel 相关文件
- `AppDataPaths.cs`
- `AppSettings.cs`
- `ErrorCodeCatalog.cs`
- 数据库迁移注册（仅在 Schema 3 获批准后）
- WPF 项目文件和测试项目文件（仅在依赖需要时）

不修改 `v2.2.0` Tag、`f72a0caf`、2.2.0 正式安装包或发布目录。

## 24. 阶段实施排期

- A：已完成，只读规划。
- B：Watch Folder MVP，建议 7–10 工作日。
- C：现场监看，建议 10–14 工作日。
- D：LUT/ICC/第二显示器，建议 10–14 工作日。
- E：性能、恢复、DPI、安装版验收，建议 7–10 工作日。

总计 34–48 工作日，不含真实厂商 SDK 和实机/法务工作。

排期文档：`docs\roadmap\2.3.0\像素蛋挞_2.3.0阶段实施排期.md`。

## 25. 发布动作与下一阶段建议

| 动作 | 本轮结果 |
|---|---|
| Publish | 否 |
| 生成 2.3.0 安装包 | 否 |
| 覆盖 2.2.0 安装包 | 否 |
| 合并 main | 否 |
| 创建 `v2.3.0` Tag | 否 |
| 下载/接入厂商 SDK | 否 |
| 修改版本号 | 否 |
| 修改产品代码 | 否 |

建议进入阶段 B：是，但只能在用户另行明确批准后开始。阶段 B 应首先恢复 867/867 基线、确认 Schema 3 是否获批，再实现 Watch Folder MVP；不得自动进入阶段 C。

## 26. 本轮生成文档

1. `docs\roadmap\2.3.0\像素蛋挞_2.3.0联机拍摄总体架构.md`
2. `docs\roadmap\2.3.0\像素蛋挞_相机厂商SDK调研矩阵.md`
3. `docs\roadmap\2.3.0\像素蛋挞_看守文件夹联机MVP.md`
4. `docs\roadmap\2.3.0\像素蛋挞_现场监看工作区规划.md`
5. `docs\roadmap\2.3.0\像素蛋挞_LUT与ICC色彩规划.md`
6. `docs\roadmap\2.3.0\像素蛋挞_第二显示器监看规划.md`
7. `docs\roadmap\2.3.0\像素蛋挞_2.3.0测试计划.md`
8. `docs\roadmap\2.3.0\像素蛋挞_2.3.0阶段实施排期.md`
9. `回传给GPT_像素蛋挞_2.3.0阶段A联机拍摄技术规划报告.md`

