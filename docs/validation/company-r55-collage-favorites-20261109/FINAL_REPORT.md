# R5.5 拼贴素材收藏交付

记录：2026-11-09T14:36:03.029252+08:00（Asia/Shanghai）。**素材收藏闭环已完成，完整全模块诊断包已生成；整体仍为诊断预览，不是全部产品/设备/DPI已验收的发行包。**

## 身份与现场保全

- 原产品基线 `38dd3f92bb2db7238f7f058008887929ec971c93`；fetch后最新产品ref `f27f440909fe19ddd5665b167b912c9db9d897d4` 与基线的 `src` 零差异，仅新增测试/交付记录。
- 独立工作树 `E:/Kitao-Worktrees/r55-collage-favorites-20261109`；分支 `feature/r55-collage-favorites-20261109`。
- 产品源码 `623d0a8ccab6305320fd2d93eda2bcdcc45711f3`；最终测试修订 `221f5c9bcebfb1e1b1d28c6435b057f83d9b645d`（仅补UIA弹层关闭后的有上限就绪等待，`src` 零变，发布包无需重建）。
- 开工清点65棵既有树、11棵有脏文件、1棵有未推提交；完整路径/状态/未推列表只留本地 `WORKTREES_BEFORE.json`，均保留。不改A树，不reset/clean/stash/合并旧树。早期失败与未完成验收收据均保留。

## 最小产品增量

复用现有自由画布、素材导入与 `CanvasReference`、仓库身份解析、缩略图、画布存储和PNG渲染。五个产品文件：`AssetLibraryPage.Canvas.cs` 接线；`AssetLibraryViewModel.Canvas.cs` 当前库身份校验与收藏查询；`FreeCanvasView.cs` 收藏来源与列表刷新；`FreeCanvasView.Favorites.cs` 可见按钮/可访问性；`CanvasFavoriteStore.cs` 原子引用存储。

收藏以现有 `LibraryId + AssetId` 为键，在当前素材库 `database/canvases/canvas-favorites.json` 保存版本1身份集合；不借用照片四星评分，不存像素/源路径，不创建另一套素材ID。重开时从当前仓库生成真实CanvasReference，因此保留素材身份并使用当前hash/路径；这不是固定版本快照。非当前库、无效/非素材库对象明确拒绝。跨进程独占写锁、临时文件+atomic move；损坏状态拒绝覆盖。画布列表只枚举GUID文档，不把收藏JSON当工程。

实际验收发现并修复：来源异步刷新曾保留旧列表，用户选择可能随后被替换清空。现在刷新立即清旧列表并检查代次，拒绝旧来源响应。收藏写入后保留仍可见的选择，取消收藏会从收藏筛选中移除，但不会删除画布实例。

## 最终验证

发布目录29份产品DLL初次绑定并逐项核对hash；测试等待修订后再次绑定WPF宿主。**Core16 + WPF相关回归12 + 完整App原生UIA3 = 31 PASS，0 FAIL，0 NOT_RUN（仅所选范围）**。完整结果见TEST_SUMMARY和ENTRY_RECEIPTS。WPF/Application单例相关测试均按方法或阶段用独立进程。

真实入口链：素材栏原生UIA选择→收藏；新进程打开同一隔离库→原生UIA筛选收藏→加入所选素材→保存；第三进程通过“画布→打开画布…”菜单选中已保存工程→原生UIA导出按钮→系统“另存为”文件名字段与保存按钮→输出PNG→取消收藏。App进程PID `26160 → 5304 → 26416`。初始库创建/合成图导入/工作区打开由完整App的既有公共命令完成；上述验收动作使用真实窗口UIA。**不是物理鼠标输入或完整DPI验收。** 三份OFFSCREEN截图只作布局观察，不冒充桌面截图。

PNG 1920×1080，透明像素2,013,600、有色非透明像素60,000；SHA256 `D139FE44BBD1E91F9A7EF90D41E05C12FF1A96021E100E3FB43A9245F86DE63E`。输入是确定性本地合成PNG，导出后源hash不变。RAW Open/Unpack/Process/MemoryImage全部0。真实照片与任何A线像素均未读取、未上传。

相关回归覆盖收藏持久化/取消/同assetId跨库隔离/非法状态和并发写拒绝、源文件不可覆盖、画布稳定引用和灵感板、画布项目关联、15区投影、完整主导航和教程退役。旧Onboarding设定/数据保持；实际完整App首次启动不激活旧教程，正常扫描/匹配/复制/导报可用。没有把教程重新引入包。

发布EXE真实启动且原生UIA读回10个主导航、工具箱和诊断标题；正常Alt+F4关闭。CMD也实际启动同一EXE并原生关闭，退出码0。默认与测试数据均独立于日常数据目录。

保留失败：dev-entry-01/02暴露旧来源刷新选择窗口；dev-entry-03/04为系统对话框定位错误；dev-entry-05/06为测试错误匹配文件列表项AutomationId=1（修为Button+ID）；final-entry-01为弹层关闭后UIA树就绪问题（只修测试等待）。不覆盖这些批次，不将其混入最终31 PASS。完整失败日志都在交付根。

## 完整诊断包

- 目录：`E:/Kitao-Deliveries/r55-collage-favorites-20261109/Guangjing-R55-CollageFavorites-Diagnostic-win-x64`，入口 `Start-Guangjing-Diagnostic.cmd` / `.bat`。
- ZIP：`E:\Kitao-Deliveries\r55-collage-favorites-20261109\Guangjing-R55-CollageFavorites-Diagnostic-win-x64.zip`；77,199,494 bytes；SHA256 `c591420060c2ea55dc169244a6aa76b32a0a011b27c8b24a8086929328eea6df`。
- 全包296文件（清单295项及清单自身），ZIP每项解压读回hash一致；PE machine x64；self-contained win-x64。
- `WholeAppDiagnosticBuild=true`、`PhotographerTrialBuild=false`。全部原模块/资源/策划15区保留；无安装器。原生生产 `raw_r.dll` SHA256 `F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9`，没有替换成A的观测库。
- 默认启动数据 `%LOCALAPPDATA%/KitaoDiagnostic/R55-CollageFavorites-623d0a8c`；可显式提供另一个绝对隔离根。包内不含本轮runtime数据库、样本、像素或测试宿主。

## 板块状态和下一项

唯一完整状态表见MODULE_STATUS.md，逐行区分未开发/已实现待验收/已验收。首个可开工项为素材“已用”角标，仅依赖已有CanvasEditor事件、稳定素材身份和模板；不等待A。其它模块只做保留/局部门禁，不凭编译宣称整模块验收。

## 提交与远端

产品提交 `623d0a8ccab6305320fd2d93eda2bcdcc45711f3`，测试修订 `221f5c9bcebfb1e1b1d28c6435b057f83d9b645d`。报告/HANDOFF/R55大纲单独提交；普通推送独立分支后在本地 `PUSH_READBACK.json` 记录最终LOCAL_HEAD/REMOTE_HEAD和干净状态，不在报告中制造自引用HEAD。A线仍在独立树，不合入本分支。
