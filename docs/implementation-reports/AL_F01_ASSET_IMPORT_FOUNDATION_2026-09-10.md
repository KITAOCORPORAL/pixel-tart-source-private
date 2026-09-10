# AL-F01 Asset Library Import Foundation Hardening

日期：2026-09-10  
状态：实现与自动验收完成

## 1. Branch / HEAD

- Branch：`feature/asset-library-import-foundation-hardening`
- Start HEAD：`73530b9cbe2510e5e1266af8115a4e50eb4bc000`
- Final HEAD：以本报告提交后的分支 HEAD 为准（功能 `4f2df29`，测试 `247a69f`）
- Origin：`source-private`；实施前远端没有本 feature 分支，基线远端 `feature/asset-library-eagle-shell-context-p36` 为 `73530b9`

## 2. 实施前状态

- 工作树已有未提交的 AL-F01 草稿：能力登记、技术元数据模型、schema v8 草案、repository 接线与 9 项 extractor 测试。本轮完整审计并保留这些修改，未 reset、clean、覆盖或删除。
- 普通导入仍由 ViewModel 直接等待 repository；文件选择器与 repository 使用不同扩展名列表；切库/关闭 drain 未覆盖普通导入。
- 当前分支包含 P3/P3.5/P3.6 实现报告及 2026-09-09 Eagle 壳层报告；任务中点名的独立 Eagle reference 六文档目录不在该 Git 分支，原始分析帧仅存在工作区外部目录且未纳入提交。
- HEAD 等于旧基线 `73530b9c...`，并未晚于它；本阶段从该基线继续。

## 3. 实现内容

- 新增 Core 层 `AssetFormatCapabilityRegistry`，统一导入、索引、缩略图、Viewer、视觉分析及 provider/限制说明。
- 新增只读 `IAssetMetadataExtractor` 与 MetadataExtractor 实现，落库真实物理宽高、EXIF Orientation、拍摄本地时间、可知 offset、来源、状态与 warning。
- 普通导入接入现有 `TaskOperationBridge / Task Center`，公开扫描、验证、重复判断、元数据、托管复制、索引、完成阶段及计数。
- 导入改为逐项事务安全边界：取消前已完整提交项保留，当前未提交项回滚；Managed 未提交副本清理。单个文件发生可恢复 I/O/权限故障时仅该项失败并进入详情，后续文件继续导入。
- ViewModel 冻结目标 `LibraryId`、库名和 repository；页面 dispose 先取消 lifetime、drain 任务，再释放 repository。Workspace Host 切库等待旧页面 dispose 完成，因此旧库任务不能向新页面发布刷新/选择/Inspector 状态。
- “打开大图预览”正名为“查看信息”，未实现假 Viewer。

## 4. Format Capability 设计

能力表覆盖 JPG/JPEG、PNG、WEBP、BMP、TIFF/TIF、ARW、CR2/CR3、NEF、RAF、ORF、RW2、DNG、PSD/PSB、SVG、GIF、MP4/MOV/AVI、PDF 及常见字体扩展。

- WPF 稳定栅格能力仅对 JPG/JPEG、PNG、BMP、TIF/TIFF 声明缩略图和视觉分析。
- WEBP 明确保留系统 WIC 编解码器限制，不虚报稳定 provider。
- RAW、设计文件、GIF、视频、PDF、字体当前可导入/索引，但不声明 Viewer；未接 provider 的能力保持 false。
- 文件选择器、repository validation/media classification、AsyncThumbnail、视觉分析入口均读取同一 registry。

## 5. Metadata Pipeline

- 容器 header 的物理宽高优先于 EXIF fallback；Orientation 不交换数据库 Width/Height。
- Orientation 缺失保持 null；只有显式 EXIF 1 才记录 Normal。
- CaptureTime 依次采用 DateTimeOriginal、DateTimeDigitized、IFD0 DateTime；不使用导入时间或本机时区伪造拍摄时间。
- offset 存在时保留分钟数与 instant；缺失时只保存 `DateTimeKind.Unspecified` 的 wall-clock。
- 无 EXIF 的有效图片是合法 Complete；损坏图片为 Failed，但仍允许素材索引。
- `BackfillTechnicalMetadataAsync` 只选缺失/未适用记录；提交前复核 AssetId、ContentHash（存在时）、FileSize、ModifiedAt，文件变化时拒绝旧结果覆盖。

## 6. Import Task Pipeline

内部阶段：Scanning → Validating → DuplicateCheck → ReadingMetadata → Materializing（Managed）→ Indexing → Finalizing。Task Center 汇总 total/succeeded/failed/skipped/cancelled；素材库状态同时显示成功、跳过、不支持、缺失、失败。详细问题保留文件名、扩展名、原因和错误码，并通过现有 Task Center 的结构化失败详情/“查看原因”容器呈现首要问题与脱敏技术清单。

## 7. LibraryId 隔离

- 每个 ViewModel 构造时从 `.ptlibrary` manifest 冻结 LibraryId；旧固定库使用数据库绝对路径确定性 ID。
- Task input snapshot 持久化 `TargetLibraryId`、LibraryName、来源与总数。
- task delegate 捕获不可变 repository；Workspace Host 完成旧页面取消/drain/dispose 后才释放旧 write lease。
- 只有 lifetime 仍有效时才刷新该 ViewModel；不会切回新库或发布旧库选择状态。

## 8. Schema 变化

Asset Library schema：v7 → v8。AssetItems 增加：

- `ExifOrientation`
- `CaptureTimeLocal`
- `CaptureTimeOffsetMinutes`
- `CaptureTimeSource`
- `MetadataStatus`
- `MetadataWarning`

迁移使用 transaction；升级前通过 SQLite backup API 创建版本化外部备份并 quick_check；升级后再次 quick_check。可移动库允许读取旧 manifest，完成数据库升级后以临时文件原子更新 manifest 和 payload hash；未来版本继续 fail closed。

## 9. 文件安全证明

- Reference 路径只以共享只读流进行 hash/metadata 读取；测试逐字节验证源文件未变化。
- Managed 目标限定于显式库根并使用 CreateNew；当前项事务未提交或取消时仅删除本次创建的副本。
- 不按同名文件猜身份；backfill 提交要求 AssetId + fingerprint，ContentHash 可用时必须再次一致。
- 自动测试全部位于系统临时目录；未读取、移动、改名、覆盖或删除用户摄影素材。

## 10. 自动测试

| 命令 | 通过 | 失败 |
|---|---:|---:|
| `dotnet build src/RAWSelectionAssistant/RAWSelectionAssistant.csproj --no-restore` | build PASS（0 warning） | 0 |
| `dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj --no-restore` | 1307 | 0 |
| AL-F01 + 切库/加载/上下文相关 WPF 定向集合 | 21 | 0 |

WPF 全套能够完成编译；本次完整运行结果为 1096 通过、49 失败、2 跳过。其中旧 P1 sealed evidence 测试按设计拒绝未批准的新 feature 分支，另有既存按钮审计/自动验收文本基线与当前 P3.6 XAML 不一致。完整运行还暴露了嵌入式测试用未附着 Task Bridge 的历史 seam；已将绕行严格限制为 `synthetic-directory-recursive` 诊断入口并补回归，生产 `file-picker/session` 仍必须经过 Task Center。与本阶段相关的 WPF 契约及暴露回归修复定向集合全绿。

## 11. 真实运行验收

| Case | 状态 | 说明 |
|---|---|---|
| 1 混合 JPG/PNG/TIFF/RAW/不支持 | PASS（repository 真实文件 I/O） | 合成 PNG、损坏 JPG、不支持、缺失混合计数准确；能力矩阵覆盖 TIFF/RAW |
| 2 EXIF/无 EXIF混合 | PASS | Orientation、CaptureTime、offset、无 EXIF、Unicode 均真实解析并重启一致 |
| 3 批量取消 | PASS | 300 个临时 PNG 在第 120 项请求取消；已提交项重启可见，未提交项不残留 |
| 4 导入中切 Library | PASS | 两个独立数据库并行导入，A/B 结果与宽高无串库；Host 契约先 drain 旧页 |
| 5 导入中关闭测试实例 | PASS（自动契约） | lifetime cancellation → drain → repository dispose 顺序验证 |
| 6 重开两个 Library | PASS | 两库重启查询结果保持隔离 |

未启动人工桌面交互实例，因此窗口点击级 Installed UI 验收不冒充已执行；自动运行覆盖真实 SQLite、真实临时文件流、取消、重启和容器迁移。

## 12. 已知限制

- 本阶段不实现 Viewer、RAW 完整解码、视频/GIF/PDF/Font 播放或预览。
- Import Task 的详细 issue 已进入结果契约及现有 Task Center 详情容器；当前详情以首要问题 + 脱敏技术清单呈现，专用的逐文件筛选/分页详情 UI 留待后续。
- metadata backfill 提供内部 service/task 能力，未增加一级 UI 按钮。
- MetadataExtractor 对特殊 RAW/设计容器可能返回 NotApplicable/Partial；这不阻断索引。
- P1 历史 sealed evidence 只允许旧批准分支运行，未为 AL-F01 篡改该历史门禁。

## 13. 修改文件

- `src/RAWSelectionAssistant.Core/Models/AssetLibraryModels.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetFormatCapabilityRegistry.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetTechnicalMetadata.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContracts.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibrarySchema.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/AssetLibraryContainerService.cs`
- `src/RAWSelectionAssistant.Core/Services/AssetLibrary/SqliteAssetLibraryRepository.cs`
- `src/RAWSelectionAssistant.Core/Services/Tasks/TaskOperationBridge.cs`
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs`
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs`
- `src/PixelTart.Modules.AssetLibrary/AsyncThumbnail.cs`
- `src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml`
- AL-F01 Core/WPF 测试及 schema 版本兼容测试
- `docs/implementation-reports/AL_F01_ASSET_IMPORT_FOUNDATION_2026-09-10.md`

## 14. 下一阶段建议

先把通用 Task Center 的 expandable details 适配到 `AssetImportIssue`，再单独规划真实 Viewer；不要在未建立 provider 的格式上提前声明 CanView。
