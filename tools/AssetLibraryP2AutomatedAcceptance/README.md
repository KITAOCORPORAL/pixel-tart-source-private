# P2 素材库全自动验收包

这是与 P1 v2 并行、互不替代的 P2 全自动验收入口。它只允许在当前
`feature/asset-library-eagle-parity-p2` 分支、干净提交、Debug Modular Harness
构建中启用；不会使用桌面输入、UIA Invoke、强制前台、真实显示设置写入、Eagle
读写或用户素材源写入。

入口：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP2AutomatedAcceptance\Invoke-P2AssetLibraryAutomatedAcceptance.ps1" -Mode DryRun
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP2AutomatedAcceptance\Invoke-P2AssetLibraryAutomatedAcceptance.ps1" -Mode RecoveryTest
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP2AutomatedAcceptance\Invoke-P2AssetLibraryAutomatedAcceptance.ps1" -Mode Run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<repo>\tools\AssetLibraryP2AutomatedAcceptance\Invoke-P2AssetLibraryAutomatedAcceptance.ps1" -Mode ValidateExistingRun -RunRoot "<absolute sealed run root>"
```

`Run` 每次只产生一个全新 run root。外层若要求连续三轮，应独立调用三次，禁止把
三个结果拼接。`ValidateExistingRun` 将包装日志写到 run root 外，并在调用前后校验
输入树指纹不变。

固定场景顺序：

1. `fixture-integrity/v1`
2. `organization-browser/v1`
3. `smart-tag-query/v1`
4. `four-views-query-sort/v1`
5. `selection-large/v1`
6. `metadata-drag-command/v1`
7. `inspector-states/v1`
8. `resilience-states/v1`
9. `restart-persistence/v1`（唯一额外重启进程）
10. `layout-dpi-performance/v1`

每轮由 runner 生成 run-owned SQLite fixture：512 条元数据，其中 500 条活动、12 条
归档，并附文件夹、标签、智能文件夹和确定性成员关系。应用在 repository 初始化前把
fixture 复制到每个隔离场景，随后只通过真实 WPF、ViewModel 命令和
`SqliteAssetLibraryRepository` 路径验收。

证据包含 PNG、元素 bounds、查询、选择、四视图、命令/撤销重做、检查器、性能和
SQLite 只读审计快照；事件、摘要与 runner session 都带 run/head/process/binary 身份和
hash 链。模拟布局矩阵固定为 1366×768@100%、1920×1080@125%、
1920×1080@150%、2560×1440@175%，不修改真实显示设置。

性能门槛：首屏 1500 ms、视图切换 250 ms、排序 350 ms、选择 100 项 250 ms、
拖放 100 项 750 ms、UI 阻塞 100 ms。独立 validator fail closed；契约列出的每一种
负例都有明确 guard，缺字段、未知拼接、路径越界、hash 变化或安全计数非零都会拒绝。
