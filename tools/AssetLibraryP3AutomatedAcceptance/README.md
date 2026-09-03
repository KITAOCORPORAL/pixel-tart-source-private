# P3 素材库全自动验收包

本包是独立于 P1/P2 的 P3 自动验收入口，只接受
`feature/asset-library-eagle-parity-p3-query-metadata` 分支上的干净提交。它使用隔离的
Debug Modular Harness、run-owned SQLite fixture 和产品内自动化 seam，不注入桌面输入，
不调用 UIA，不强制前台，不修改真实显示设置，不读写 Eagle，也不写入用户素材源。

## 入口

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP3AutomatedAcceptance\Invoke-P3AssetLibraryAutomatedAcceptance.ps1" -Mode DryRun
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP3AutomatedAcceptance\Invoke-P3AssetLibraryAutomatedAcceptance.ps1" -Mode RecoveryTest
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP3AutomatedAcceptance\Invoke-P3AssetLibraryAutomatedAcceptance.ps1" -Mode Run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP3AutomatedAcceptance\Invoke-P3AssetLibraryAutomatedAcceptance.ps1" -Mode ValidateExistingRun -RunRoot "<absolute sealed run root>"
```

`Run` 只创建一个全新的 sealed run root。`ValidateExistingRun` 的 wrapper 日志写在 run
root 的外部 sibling 目录，并用校验前后树指纹保证证据树没有被补写或改写。

每轮在生成 fixture 或启动应用前，会先把 runner、validator、三轮聚合器、fixture
generator、真实负例证明器、contract 和本说明复制到 `runner/acceptance-inputs`。DryRun
会对其中全部 PowerShell 与 Python 输入分别做解析以及 Python AST/compile 预检。manifest 固定记录每个
输入的长度、SHA-256 与整体树 hash；后续 fixture 和验证只读取这些 run-owned 副本。
成功轮次在最后一次 manifest 写入后生成 `runner/run-seal.json`：清单精确覆盖当时全部
文件并刻意只排除 seal 文件自身，随后把 run root 内全部现有文件设为 ReadOnly。

三轮均通过后，可在 PowerShell 中把三个绝对 run root 交给独立聚合器。输出目录必须
位于三个 run root 之外：

```powershell
& "<repo>\tools\AssetLibraryP3AutomatedAcceptance\Test-P3AssetLibraryAutomatedRunSet.ps1" `
  -RunRoots @("<run-1>", "<run-2>", "<run-3>") `
  -OutputDirectory "<absolute external summary directory>"
```

聚合器会用每轮自己的 sealed validator 重验，并要求 source HEAD 相同、run id、进程
身份与进程会话均不复用，且三轮所有声明证据的绝对路径集合互不相交。

## 固定验收拓扑

主流程严格按以下 14 个场景执行：

1. `scope-switch/v1`
2. `ime-cancellation/v1`
3. `search-suggestions-history/v1`
4. `folder-any-all-not/v1`
5. `tag-any-all-not/v1`
6. `scalar-null-composition/v1`
7. `visual-composition/v1`
8. `nested-canonical-query/v1`
9. `invalid-query-fail-closed/v1`
10. `smart-folder-lifecycle-preview/v1`
11. `smart-folder-invalid-migration/v1`
12. `tag-manager-lifecycle/v1`
13. `bulk-metadata-journal/v1`
14. `four-view-resilience-layout/v1`

随后分别重启并复验搜索历史、智能文件夹、批量元数据日志，共 17 个相互独立的进程
会话。每个会话都绑定 run id、source HEAD、进程会话 id、PID/HWND 和 run-owned 二进制
hash，禁止跨 run、跨进程或跨二进制拼接。

## Fixture 与证据

当前 fixture 是 schema v7，共 10,128 条：10,000 条活动、128 条归档、512 条缺失；
其中视觉分析成功 3,072 条、失败 1,024 条、未分析 6,032 条。另有只用于迁移场景的
schema v6 fixture，共 64 条（60 活动、4 归档）。生成器、两份数据库和 fixture 期望文件
都复制到 run root 并记录 SHA-256；validator 通过 immutable SQLite 连接独立只读复核。

必需证据覆盖截图、边界与可访问性身份、规范查询文档、参数化查询计划、结果 hash、
搜索历史、智能文件夹、标签、成员关系、journal、命令、选择、四视图、性能和数据库。
布局矩阵固定为 100%、125%、150%、200%，只做产品内模拟，不写真实显示设置。

`tag-manager-lifecycle/v1` 写入版本化的 `tag-manager-lifecycle/v2` 证据。它只经公开产品命令
完成标签组和标签的创建、重命名、相邻排序、标签跨组移动、标签归档与恢复，以及对已有
重叠成员关系的合并去重。排序前后以规范 GUID 顺序 hash 绑定；合并前后计数满足集合并集
恒等式。标签组循环防护依据公开的平面组排序及标签组外键合同验证，不使用对象反射、
桌面输入或 UIA 推断。

性能上限（毫秒）：10k 首屏 1500、搜索建议 200、单筛选 300、八规则嵌套查询 600、
智能文件夹预览 750、范围切换 400、批量标签 100 项 750、500 项 2000、UI 阻塞 100。
validator 会按产品写入端的精确 UTF-8 字节契约，逐行重算 `events.ndjson` 和
`summary.ndjson` 的 record hash，并同时核验 previous hash 链。契约列出的 50 类负例
不是名称清单：每次验证都会为每类负例创建独立的内存副本、施加对应变异，再由独立
validator 子进程逐项证明 fail closed；sealed run 本身始终只读。

安全计数也不是无来源常量。runner 会封存三个产品自动验收 seam 的源码快照，在运行
前后用固定规则扫描桌面输入、UIA 调用、强制前台、显示写入、Eagle、网络、直接设置和
直接 SQL 调用；同时记录进程、环境变量值 hash、显示参数、run-owned 路径约束和输入树
指纹。validator 会从封存快照独立重扫并反推 manifest 中的每一个安全计数；任一来源
缺失、不一致或非零都会拒绝。
