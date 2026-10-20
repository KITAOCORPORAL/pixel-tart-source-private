# 当前产品逻辑 DPI 取证（沿用 RC12 工具目录名）

此生产器使用当前 `App.xaml`、`MainWindow` 和深色主题，生成 **106** 张 RenderTargetBitmap 状态图。它们是 `automated-logical-simulation`，不是 Windows 桌面截图，也不证明物理显示器缩放或人工点击通过。每张图单独进程、独立数据目录，前一进程退出后才启动下一进程。

## 使用本轮发布组件

先从目标冻结源码发布 `UiReviewBuild=true`、`WholeAppDiagnosticBuild=true`、`PhotographerTrialBuild=false` 的 win-x64 自包含取证构建，再传入 `-ExecutablePath <发布的KitaoPhotoSelector.UiReview.exe绝对路径> -SkipBuild -OutputRoot <尚不存在的新目录>`。不传 ExecutablePath 时保持已有本地构建入口。

`UiReviewBuild` 是既有编译期取证通路，不能声称与普通完整诊断 EXE 逐字节相同。必须分别记录取证构建和实际桌面诊断包的 EXE/WPF/Core 哈希。脚本设置显式隔离环境；已有输出目录会在任何写入前被拒绝，不会删除旧图、复用旧 manifest 或拼接历史结果。每次需要新的 OutputRoot。

## 当前状态集

- 产品状态 12；简化流程 10；素材库交互状态 11。
- 视觉分析 7；重复图 5；发布流程 10；比例状态 1。
- 自由画布 10；上下文检查器 2。
- 当前逻辑 DPI 32：MainWindow、AssetLibraryGrid、AssetFilter、AssetContextMenu、AssetViewer、CalendarBookingAssets、AssetInspirationCollection、AssetRecentLibraries，各100/125/150/200%。
- 素材库分辨率状态6。

四档 DPI 主矩阵固定1920×1080逻辑视口，像素尺寸随scale变化；它不代表同一个1920×1080物理屏在四档Windows缩放下的人工观察。六个分辨率状态另覆盖固定像素视口的部分组合。

输出 `current-run-visual-evidence.json`、原图、元数据，记录源码提交、截图/发布组件哈希、每状态进程退出证明、合成输入前后哈希；所有元数据保持 `physicalDpiManuallyTested=false`。

运行 `Test-RC12ProductVisualEvidence.ps1 -EvidenceRoot <本轮目录>`，按当前106状态合同校验。该校验器是证据合同，不是人工物理DPI验收。

现有 `DpiTests` 固定读取仓库 `artifacts/current-run-visual`。若生产在外部隔离目录完成，只能将同一新run的manifest与dpi-current镜像到此前不存在的门禁目录，保留原始绝对证据路径并记录镜像哈希；不得拿历史截图补齐，也不得修改断言。原门禁目录已有内容时应保留并另行安排新工作树，而不是删除覆盖。

仅使用本轮程序生成的合成JPEG，不读取私人照片/RAW。目录名中的RC12只是历史工具命名，不代表复用旧发行证据。
