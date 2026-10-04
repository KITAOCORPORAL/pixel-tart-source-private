# 四组人工验收返工记录

START_HEAD: `c6a8c405b71f0161e4fdad17f39def4f70b5e0b1`
Branch: `integration/pixel-tart-developer-preview`
Production SourceHead: `dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7`

起点 fetch 后 LOCAL == REMOTE，工作区 CLEAN。旧 `00_START_HERE.md` 明确写着 Release 实机尚未执行；历史 CODE/TEST 不作为 Runtime 通过依据。

**当前状态：NOT_READY_FOR_USER_RETEST。VisualApproved=false；UserVerified=false；USER_ACCEPTANCE=NOT_APPROVED。**

## Release 与证据范围

- EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/runtime-correction-2026-10-04-r9/publish/win-x64/KitaoPhotoSelector.exe`
- Manifest：`artifacts/releases/runtime-correction-2026-10-04-r9/release-manifest.json`，含应用版本和发布目录逐文件 SHA256。
- 实际运行普通 Production EXE，使用既有 isolated-runtime 设置保护用户库。测试库为 `RuntimeCorrection-20261004.ptlibrary`。
- 用户明确授权本轮受限桌面操作与截图；使用 computer-use skill 的 sky API。未启动浏览器、Observer、Recorder，未更改 Windows 缩放。
- `artifacts/runtime-correction/final-after/` 的19组截图属于4315bc9候选，不能转标为206051e。每组 PNG 旁 JSON 记录 EXE、SourceHead、时间、窗口与捕获尺寸；摘要和哈希见 `runtime-evidence-manifest.json`。
- 真实桌面观察保留当前系统设置，主要窗口捕获为1600×920；首次素材库为2560×1440。用户优先环境是150%，但sky未返回实际窗口DPI，旧截图JSON中的DpiScale=1.5属于环境假定而非测量值。其他缩放和全部窗口尺寸尚未覆盖，逻辑尺寸测试不能填补这项。
- `artifacts/runtime-correction/after/` 为前一候选 `674bed8` 的诊断截图，不能冒充最终候选结果。
- 用户照片只保留在本地证据中，不随源码上传。

## 一、Color Studio 左右职责（原截图 1、2）

**旧版复现**：导入目标和参考，切换仿色和输出；参考来源、权重等编辑动作与右侧参考查看混杂；收窄窗口后检查参数及主图。

**根因**：左侧已有分类但实际参考来源管理仍在右侧；标题/控件存在无法证明编辑职责已迁移。低矮窗口中参考预览占据右栏，后续分析操作不易到达。

**修改文件**：`ReferenceColorWorkspaceView.xaml(.cs)`；`ReferenceColorWorkspaceViewModel.cs`。实际来源列表、权重、增删顺序移入左侧仿色，输出说明回到左侧；右侧参考图/分析分组可折叠，继续绑定既有 Editor 和参数管线。

**4315bc9 候选实际结果（历史证据）**：该 EXE 导入鞋子目标 JPEG 与人像参考 PNG；左侧调整、中心目标、右侧只读参考可见。参考100%显示细节，拖动平移后 Fit 恢复整图。右栏开关保留目标区域布局。参考载入完成后预览状态恢复普通状态。

After：`16-studio-edit-left-reference-right`、`17-reference-100-pan`。

- CODE：FIXED_PENDING_RUNTIME
- TEST：PASS_SCOPED（真实控件树、绑定位置及12组逻辑尺寸；现有参数/目标回归）
- RUNTIME：PARTIAL
- 未完成：1180×720、1920×1080 与其他实际 DPI；所有五类参数逐项操作及重启恢复未逐项完成。

## 二、3D 映射与当前影调分析（原截图 2）

**旧版复现**：切换目标及处理结果，打开3D并取色；观察点云反馈、主图遮罩及 Esc；查看 Studio 是否有与当前目标一致的直方图。

**根因**：原分析依赖手动构建，原片/结果来源缺乏统一失效规则；Studio 未共享轻量直方图结果。实机进一步发现主窗口 PreviewKeyDown 先消费 Esc，导致清除高亮前跳回工作台。

**修改文件**：`ReferenceColorWorkspaceViewModel.Analysis.cs`、`ColorSpace3DViewport.cs`、`CloudInspectionControls.cs`、`ImageHighlightOverlay.cs`、`ReferenceColorWorkspaceView.xaml(.cs)`、`MainWindow.xaml.cs`；共享 `VisualHistogram.cs`、`VisualAnalysisEngine.cs`、`HistogramDrawing.cs`、`ToneZoneDrawing.cs`、`ColorStudioColorSpace.cs`。

**实现边界**：从当前 Source/Matched BitmapSource 取最多768px分析代理，120ms合并更新、取消旧任务、revision防止过期结果、4项缓存。OKLab颜色球半径默认0.06，可调0.01–0.15；簇与遮罩共享真实像素成员。点大小/不透明度/密度仅属检视状态。0–X 是11个线性亮度等分区，UI明确不是曝光档位。未声称完整ICC工作空间分析。

**4315bc9 候选实际结果（历史证据）**：原片与仿色预览分别标明来源，RGB/Luma及区域比例随仿色强度更新；100%与0%结果的暗区比例从83.5%回到81.6%。展开点云实际旋转、平移、滚轮缩放、Reset、Fit均响应。选暗部簇，主图地面出现临时遮罩；对白鞋采样，Mini点云亮部圈出且白鞋遮罩可见。Esc清除遮罩并保留Studio路由。

After：`09`–`15`、`18`–`19`。`14-Esc-mask-cleared-route-retained` 对应前一候选 `12-FAIL` 的实际回归。

- CODE：FIXED_PENDING_RUNTIME
- TEST：PASS_SCOPED（OKLab成员、容差、直方图同数学、取消、目标切换、缓存、参数不变、Esc顺序、相机共享参数）
- RUNTIME：PARTIAL
- 未完成：最终EXE多目标/不同内容切换、密度/透明度/点大小逐个实操、Mini全部相机动作、完整窗口尺寸；尚未以同一最终EXE做导出前后字节对照。

## 三、标签、标签组与文件夹重命名（原截图 8–10）

**旧版复现**：对普通文件夹、标签组、标签右键或按F2；修改名称，返回Inspector/筛选并重启。

**根因**：既有repository重命名能力未一致连接组织树入口；F2需优先识别组织节点，避免落到素材文件重命名；文件夹同父级重名校验缺失。

**修改文件**：`AssetLibraryPage.OrganizationRename.cs`、`AssetLibraryViewModel.OrganizationRename.cs`、`AssetLibraryPage.cs/.xaml`、`AssetLibraryOrganizationNodes.cs`、repository `P3.cs`/`V15.cs`。复用稳定ID与原metadata/journal，不生成第二套标签值；保存刷新树、Inspector关系投影和当前Query；旧名称智能规则在改名前解析为ID。

**4315bc9 候选实际结果（历史证据）**：该 EXE打开测试库恢复之前保存的长中文/英文/&文件夹名；右键标签组改为“旅拍组 Studio & 中文”，树立即更新；点击标签再F2，标题正确为“重命名标签”；保存“色彩 Blue & 长中文标签”后树和状态反馈更新。导入真实鞋子照片后尺寸3688×2310正常。

After：`01`–`04`。自动测试另外覆盖两个素材、重启repository、稳定关系、智能规则、空名与重名、Inspector和标签query。

- CODE：FIXED_PENDING_RUNTIME
- TEST：PASS_SCOPED
- RUNTIME：PARTIAL
- 未完成：最终EXE中带素材归属的完整改名→Inspector/Query→重启链；空名/重名对话框实操；Enter/Esc真实键盘。桌面工具在模态窗口输入时激活了主窗口，无法据此判定产品键盘通过或失败，鼠标保存不代替键盘验证。

## 四、自由画布菜单（原截图 13、14）

**旧版复现**：进入画布查看顶部长串动作；选择对象，打开排列二级菜单，缩放/撤销，再保存重开。

**根因**：高频与低频动作同排，排列在浮动工具条重复；未通过统一菜单的条件和层级表达动作。

**修改文件**：`FreeCanvasView.cs`、`CanvasEditor.cs`。保留紧凑选择/平移/撤销/重做/Fit；其余进入工具、编辑、视图、排列、画布、项目，复用共享级联定位，单一Surface.Zoom驱动倍率。

**4315bc9 候选实际结果（历史证据）**：从真实素材右键放到自由画布；六组菜单显示；200%切到100%后图像尺寸随之改变且只显示一个倍率；创建副本实际增加对象，撤销移除副本并切换可用状态；排列→自动整理从父项外侧展开，Esc逐级关闭。

After：`05`–`08`。

- CODE：FIXED_PENDING_RUNTIME
- TEST：PASS_SCOPED（真实弹出菜单、剪贴板行为、Zoom、12组逻辑尺寸边界）
- RUNTIME：PARTIAL
- 未完成：屏幕边缘翻转和完整键盘导航、其他尺寸/实际DPI、所有菜单动作及最终EXE保存/关闭/重开。空画布和无项目时所有动作的反馈仍需最终实操审计。

## 自动门与剩余验收

### 实机退出异常与修复

4315bc9候选在访问画布、切到Studio、Alt+F4退出时，18:21:30日志记录未处理界面异常：`CanvasOwnerClosing`在`Closing`期间重新调用`Close()`。已保存文档的异步检查实际同步完成，触发WPF关闭重入限制。206051e在检查前先让出dispatcher，并合并重复关闭请求。真实WPF窗口回归覆盖已保存文档和连续两次Close；8项 focused通过。

206051e新EXE于18:38启动，真实打开素材画布、切到Studio，再Alt+F4，于18:41:11正常退出；该段日志没有未处理异常，PID50972退出。`r5-after/01-restart-organization`和`02-canvas-opened`属于这个EXE，关闭日志为`r5-shutdown.log`。这关闭了已发现的退出重入问题，尚不代表四组完整Runtime验收。

随后全量测试出现1项失败：新窗口卸载时访问了其他STA拥有的进程Application。b839cc1只修正测试夹具，让真实owner调用实际Closing处理器和已保存画布，避免无关shell卸载路径；focused再次8/0，完整复跑1437通过、0失败、11跳过，正常退出。生产源码和206051e EXE保持一致。

同次审计修正空画布“保存到灵感板”可点但无结果：没有图像或保存处理器时禁用该菜单项。

机器测试的精确结果见 `artifacts/runtime-correction/test-summary.json`。初次WPF遇到源文件BOM影响脚本断言与测试跨线程枚举；修正编码和测试STA隔离后全量正常退出。未删除/skip测试或放宽断言。

DPI项目91项读取的 `current-run-visual` manifest SourceHead仍为 `de4c91a...`。它通过不代表本次4315bc9视觉覆盖；本次真实After另行保留。未伪造新manifest或复用旧截图冒充当前图。

以上四组的核心行为已有前述候选Release实机证据，但都未完成本轮完整验收矩阵，因此保持NOT_READY。后续只补本轮未完成项，不启动任何下一阶段功能。

## r5 后续观察及 r6 定位修复

- 206051e / r5 的 `03` 记录正常退出后重新启动；`04`–`06` 记录文件夹长中文、英文及 `/ # %` 保存和 F2 正确对象。标签新增素材关联后数量为1、Inspector标签显示一致，但带关联改名、Enter/Esc及空名/重名的完整链路尚未实机完成。
- `07`–`09` 为同一r5真实素材画布、视图菜单、200%→100%执行。`11`–`12` 为目标JPEG及当前原片RGB/Luma与0–X占比。`13-expanded-cloud` 只记录点击后的主窗口，未显示展开窗口，名称不代表该项通过。
- 在副显示器（截图原点为负X）展开3D时，检查窗口未出现于主窗口捕获。源码证实使用了主显示器的 `SystemParameters.WorkArea`。20fb695复用ContextMenuMonitor取得owner所在屏幕边界，参考和3D检查窗口都用owner坐标及缩放转换；新回归覆盖负坐标屏幕、100/125/150/200%转换及超大窗口限制。
- 新定位回归首次发现浮点边界相减造成极小偏移；生产计算改用origin + remaining extent，保留精确边界断言，4/0复跑通过。
- r6 是20fb695真实Release，新版全量和实机结果单独记录。旧After不能转标为r6通过。

当前阻碍完整Runtime关闭的项目：实际DPI矩阵仍未执行；受限桌面工具对重命名模态窗口输入/焦点不稳定；最终候选尚需逐组重做完整操作链、多个目标和导出不变检查。全部保留PARTIAL/NOT_RUN，不交付READY或用户批准。
## 文件与提交索引

生产与测试文件逐项列表（含新增/修改、行为/XAML/测试类型）：`artifacts/runtime-correction/changed-files.json`。
提交清单：`artifacts/runtime-correction/commits.txt`。测试TRX哈希及分类子集：`artifacts/runtime-correction/test-summary.json`。
截图只存本地；`artifacts/runtime-correction/runtime-evidence-manifest.json`提供每组SourceHead、EXE、捕获时间、尺寸和PNG哈希。r5部分捕获含其他应用的字幕遮挡，不能据此判断被遮区域布局通过。

| 截图/需求 | CODE | TEST | RUNTIME | USER_ACCEPTANCE |
|---|---|---|---|---|
| 1、2：Studio左右面板 | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED |
| 2：3D、映射、当前图影调 | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED |
| 8–10：普通文件夹/标签/标签组重命名 | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED |
| 13、14：自由画布菜单 | FIXED_PENDING_RUNTIME | PASS_SCOPED | PARTIAL | NOT_APPROVED |

这里的PARTIAL表示已观察部分行为，未完成最终同一EXE的全部验收条件。
## r6 最终构建与自动回归

- SourceHead：20fb6957a3236d46b24dfe5cf29ac38f622098af。Clean Release x64 / self-contained win-x64 publish成功；再次publish启用既有IncludeSourceRevisionInInformationalVersion参数，运行日志和DLL版本均记录同一SourceHead。
- EXE SHA256：DFBAD4F7E43AF146D7BE296BFA0F12A249E4A413F28ADA77F8978B7D25C80858。EXE是apphost；发布目录逐文件哈希同时保护实际应用DLL，不能只靠apphost哈希证明业务代码。
- Core full：1537通过 / 0失败 / 4跳过；Core从4315bc9后未再修改。
- WPF full serial：1438通过 / 0失败 / 11跳过，8分29秒，testhost正常退出。
- DPI：91通过 / 0失败；这是自动contract，不是当前版本实际DPI截图通过。
- 分类统计（重叠子集，不可相加）：见test-summary.json，Asset、Color Studio、Reference Match、3D、Free Canvas、Guardian新增失败均为0。
- 19:26:52 r6日志STARTUP_OK，ProductSourceSha=20fb6957a3236d46b24dfe5cf29ac38f622098af，PID26460。`r6-after/01-startup`为该EXE主窗口。
- 随后桌面工具报告 `user input was detected in this window`，已停止自动输入，避免与用户操作冲突。r6四组完整Runtime尚未完成，不能因前候选或自动测试通过写READY。
## r9 交付记录（最新，优先于上述历史候选）

Production SourceHead：`dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7`。随后只提交文档和证据索引，不重建二进制。
EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/runtime-correction-2026-10-04-r9/publish/win-x64/KitaoPhotoSelector.exe`。
SHA256：`0A89D816E09EE5C4D19D45F0548AA8402481D7347B3C9D1907A4E906F8C15326`。
Clean/publish成功；发布目录287个文件全部重新计算哈希并匹配manifest。业务DLL版本为`2.3.0+dc2ba3d47e9937e6ab41395a589ae1c44a2b1ed7`。

### 实机发现的第二个布局问题

r6 `29-window-resize-attempt`：收窄到1180×720后，右侧分析压在目标图片上，FAIL。根因是`UpdateResponsiveLayout` compact分支把RightRail放到中心列并提高ZIndex。
4687b37改为始终预留独立右列，隐藏/展开不改主图视口；新增真实布局测试比较目标和右栏变换后矩形、边界及开关前后位置。r7实机又发现左栏过窄裁切编辑控件（`r7-after/02-FAIL-compact-left-clipping`），64eec3b/dc2ba3d保留可读的300px紧凑编辑栏、宽窗口320px，目标区域保持更宽。
修改：`ReferenceColorWorkspaceView.xaml.cs`、`RuntimeCorrectionWpfTests.cs`。

同批测试暴露旧WideRatio夹具在短命STA创建Application，污染后续控件测试；改为共用持续STA。画布弹出菜单测试改为寻找属于当前edit控件且IsOpen的菜单，避免选中已关闭缓存。保留原行为断言，未删除测试、增加skip或放宽assertion。

### r6 已观察的历史行为（不转标为r9）

- `03`–`16`：owner显示器上的展开3D，Orbit/Pan/滚轮/Reset/Fit，点大小4.5、透明度32%、高密度；肤色簇→脸部遮罩，衣服采样→亮部点云；Esc清除。
- `17`/`18`：仿色100%→0%使RGB/Luma和0–X比例更新，0区21.8%→25.2%。`28`改用鞋子图，来源标为目标原片711×768，0区82.6%。
- `19`/`21`/`26`：带一个素材关系的标签改为“色彩 Blue & 中文 / # r6”，树/Inspector/query和重启后名称、成员数一致。`20`只是滚动尝试，不能证明新名称可见。
- `22`–`25`：画布重新打开，真实缩放和级联/键盘操作。19:51:32正常退出。

### r9 同一EXE After逐项结果

所有After均在`artifacts/runtime-correction/r9-after/`；每组JSON记录SourceHead、EXE、时间、捕获尺寸。实际DPI未独立测得，DpiScale=null；Windows Scaling未改。

| 需求 | 操作 → 观察 | After | REAL_RUNTIME范围 |
|---|---|---|---|
| 左/中/右与折叠 | 1180×720打开右栏，主图位置/倍率与关闭时一致，右栏不覆盖；折叠参考能看到RGB/Luma | 03、04、05 | 此尺寸/开关PASS；完整参数与DPI矩阵PARTIAL |
| 主图→点云 | 在白鞋取色，亮部簇出现圈，主图同色像素高亮 | 06 | 此图PASS |
| Esc | 高亮清除，Studio保留 | 07 | 此路径PASS |
| 点云→主图 | 点选Mini暗青簇，地面/水的对应颜色像素出现遮罩 | 08 | 此图PASS；最终EXE完整相机/导出不变链未跑完 |
| 已保存名称 | r9加载之前真实改名的测试库；左树与Inspector同名，成员数仍1 | 09 | 跨候选重启读取PASS；不代替r9完整改名链 |
| 重命名入口 | 当前标签右键打开“重命名标签”，名字正确、完整输入框可见 | 10 | 入口PASS；工具返回element 805 unavailable，输入/Enter/Esc/空名重名未验证；取消未提交编辑 |
| 画布保存重开与缩放 | 从该素材打开已保存画布，200%→100%实际图像宽高减半，只有一个倍率 | 11、12 | 此尺寸PASS |
| 菜单鼠标/键盘 | 排列→自动整理从父项外侧展开；Esc关闭子菜单；Right重新打开并聚焦第一项 | 13、14、15 | 此位置PASS；边缘翻转/完整DPI矩阵未完成 |
| 重启与退出 | 20:30:48正常退出；20:32:39同SourceHead STARTUP_OK，退出教程后主窗口正常 | 16、17，r9-runtime-session.log | 启动/退出PASS；16含教程，不算工作区操作通过 |

01的名称是早期命名，实际右栏未打开；02捕获1180×689不代表720高。完整1180×720使用03–05，不能混记。宽屏截图2560×1440也不能冒充1920×1080。

### 最终自动门

| Gate | PASS | FAIL | SKIP | 说明 |
|---|---:|---:|---:|---|
| Core full | 1537 | 0 | 4 | 4315bc9运行，之后Core及其测试无diff |
| WPF full serial | 1439 | 0 | 11 | dc2ba3d；8m23s；testhost正常退出 |
| DPI contract | 91 | 0 | 0 | 旧current-run-visual依赖de4c91a；不是本轮实际DPI证据 |
| Asset Library子集 | 522 | 0 | 2 | 以下子集重叠，不可相加 |
| Color Studio子集 | 71 | 0 | 1 | 真实布局/状态测试在全量中 |
| Reference Match子集 | 61 | 0 | 3 | 未扩展ICC/RAW结论 |
| 3D子集 | 57 | 0 | 0 | 成员/容差/相机/分析 |
| Free Canvas子集 | 26 | 0 | 0 | 菜单/Zoom/关闭 |
| Guardian子集 | 10 | 0 | 0 | 自动规则不等于视觉批准 |

最终新增失败=0。此前失败与根因保留在历史记录及本地日志；没有借旧绿灯掩盖失败。当前构建仍有既有分析器警告，不声称零警告。

### 仍阻塞完整关闭的条件

1. 100/125/150/200%实际Windows缩放与1180×720、1600×920、1920×1080完整同EXE矩阵未执行；未获修改系统缩放授权，现工具也不提供实际DPI测量。逻辑布局测试不能代替。
2. 重命名模态窗口工具焦点/索引不可稳定访问；最终EXE三类对象的Enter/Esc、空名/重名和带关系改名→query/Inspector→重启完整链未通过实机门。
3. 最终EXE五类编辑参数持久化、只读参考Fit/100%/缩放/平移、Mini与展开全部相机动作、多个内容分析切换及导出前后不变证据尚未全部重复完成。旧候选观察只作定位资料。
4. 最终EXE菜单屏幕边缘翻转及三尺寸四DPI实机覆盖未完成。

四组维持CODE=FIXED_PENDING_RUNTIME、TEST=PASS_SCOPED、RUNTIME=PARTIAL、USER_ACCEPTANCE=NOT_APPROVED。**NOT_READY_FOR_USER_RETEST**。用户可获得r9诊断候选，但这不是已完成验收的Release。VisualApproved=false；UserVerified=false。未启动后续roadmap。