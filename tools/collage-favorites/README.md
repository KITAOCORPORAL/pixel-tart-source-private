# R5.5 拼贴素材收藏

产品基线 `38dd3f92bb2db7238f7f058008887929ec971c93`。现有 `FreeCanvasView` / `AssetLibraryViewModel.Canvas` / `CanvasDocumentStore` 架构不变。

收藏使用素材库 `LibraryId + AssetId`，不借用照片评分，不缓存像素，不改变导入或渲染。引用保存在当前素材库 `database/canvases/canvas-favorites.json`，版本1；重开时通过现有仓库生成含当前路径/hash的 `CanvasReference`。当前素材库外的引用拒绝收藏；收藏不等于复制或固定内容版本。文件锁+唯一临时文件+atomic move，损坏文件不覆盖。画布列表只识别GUID文件名，不把收藏文件当画布。

入口：素材库 → 自由画布 → 素材栏选素材 → 收藏所选素材/取消收藏；来源选“收藏” → 加入所选素材；画布 → 打开画布… → 既有工程；保存；导出 → 系统另存为 → 透明PNG。来源刷新先清除旧列表，代次校验拒绝过期响应。

## 可重放验证

`build.py <新收据名> <dotnet参数...>` 使用仓库既有脚本记录的本地 .NET 10 SDK，只写新的外部构建收据；同名日志存在时拒绝覆盖。WPF构建始终传 `-p:WholeAppDiagnosticBuild=true -p:PhotographerTrialBuild=false -p:Platform=x64`。

`run_acceptance.py <新批次目录名>` 依次在三个独立VSTest进程启动实际完整App，使用同一隔离素材库和应用数据目录：收藏；独立重启筛选/加入/保存；第三进程从真实画布菜单打开工程、导出并取消收藏。每阶段检查实际TRX结果，不把命令退出0当测试通过。UI入口使用原生UIA Selection/Invoke，系统文件对话框按控件类型和AutomationId定位。它是完整App/TestHost原生UIA验收，不是物理鼠标或全部DPI验收。输入是本地生成的确定性PNG，不使用RAW或私人照片。

最终测试必须将发布的产品DLL复制到测试host并逐项核对SHA256，绑定证据另存 `PUBLISHED_BINDINGS.json`；不得将早期UNFROZEN_WORKTREE结果冒充最终发布验收。Core验证覆盖收藏持久化/跨库身份/取消/非法状态与并发写拒绝；另运行受影响的画布引用、来源保全、文档保存、透明输出及全壳/教程退役检查。禁止重新激活教程；不生成安装器。
