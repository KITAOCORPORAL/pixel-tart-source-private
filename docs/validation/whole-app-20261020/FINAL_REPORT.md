# 光镜完整软件验证诊断包 — PARTIAL / 不能发行

用户报告日期：2026-10-20，America/Chicago。日志保留UTC/主机China Standard Time，不把主机时间当用户时区。

## 一份完整构建与统一入口

- 冻结产品源码：96e84b14a0cbb040b97cc531efeb31b1f4144367。
- 独立分支：validation/guangjing-whole-app-20261020；工作树 E:/Kitao-Worktrees/guangjing-whole-app-validation-20261020。
- 上游起点：a1e52238616b8d22f09b92595a85e76c7d9c1460，fetch后未前进。
- 完整Windows x64自包含目录：E:/Kitao-Deliveries/guangjing-whole-app-validation-20261020/Guangjing-FullScope-Diagnostic-final-win-x64。
- 统一入口：Start-Guangjing-Diagnostic.cmd；EXE位于app/KitaoPhotoSelector.exe。
- **WholeAppDiagnosticBuild=true，PhotographerTrialBuild=false**。实际MainWindow完整保留；不会以暗室控件替换整个软件。
- 启动标题与README写明“完整范围诊断预览 · 未完成验证 / 不能发行”。缺少显式隔离目录会在迁移/日志/数据库初始化前拒绝启动。数据只在独立UserData，测试每fixture另建runtime。
- 本轮实际启动最终包PID 15008并核验加载主DLL/Core/模块DLL，见FINAL_PROCESS_MODULES.json。统一CMD也已执行烟测，源脚本hash见LAUNCHER_SMOKE.json。
- 完整包290文件，deps.json列出264运行资产全部存在；缺失0。哈希/类型/文件清单见FINAL_RELEASE_MANIFEST.json、FINAL_DEPENDENCIES.json、PACKAGE_IDENTITY.json。
- ZIP是便携**诊断包**，不是Setup，没有安装升级卸载重装验收。

## 全产品总账而非暗室总账

从当前MainWindow、PrimaryNavigationPolicy、工具目录、各XAML和动态控件生成：10个一级工作域，另列工具箱/系统/工具页面与自由画布、Color Studio子工作区；24个模块/交付域，121条业务闭环，1121条源码动作候选，策划15分区。参见MODULE_TOTAL.md、MODULES.csv、OPERATIONS.csv、ACTIONS.csv、NAVIGATION.json、PLANNING_15.csv。

**1121候选不等于1121可见按钮**：含共享、动态、隐藏/条件与事件路线，逐项标记BuildIncluded=PARTIAL；没有把所有public方法机械转成入口。模块级相关测试不证明每个操作步骤通过；操作结果、自动、人工列分开，跨模块共享测试不能相加。

已读用户局部交接基线及WHOLE_APP审计、策划R7/工作流/图片搬迁、R3最终结果、RUX历史与PARITY撤回报告。原完整A–BC旧会话附件未准确定位，逐条旧附件复核BLOCKED，详见ORIGINAL_INPUTS.md；不让这份局部基线替代全产品范围。

## 本轮测试：最终发布组件逐字节绑定，不继承旧成绩

|组|发现|通过|失败|跳过NotExecuted|漏项/未取得结果|
|---|---:|---:|---:|---:|---:|
|最终Core完整|1713|1708|0|5|0|
|最终WPF完整|1710|1688|2|20|0|
|最终发布模块|14|14|0|0|0|
|文档原型组件|1|1|0|0|0|
|DPI证据合同|91|74|17|0|0|
|胶片研究组件（研究范围）|25|25|0|0|0|

最终WPF按适配器发现分成237执行单元、四片各自进程隔离；合并以(testId,FQN,displayName)多重集合校验planned=discovered，missing=0、extra=0。全部原始TRX/日志/发现/精确执行计划可查；不是源码regex统计。

首轮完整Core1706通过/7未执行；首轮WPF1682通过/7失败/20未执行，原始记录保留。补足发布Core身份和CPU性能输出条件后，最终Core相应两项通过；剩余为授权真实RAW/空间语料、12–60MP性能opt-in、未验证GPU设备，不当作通过。

最终WPF两失败：
1. 策划ProductionPeers合同在“策划正文”旧ValuePattern查找失败；此前v2导航、日期、创建检查已通过。旧脚本还用早期灯光图/服化道/PlanningShotRow，不与当前15分区/DataGrid一致；保持FAIL，不以页面打开替代合同重做。
2. R3两照片测试复用了首轮固定输出目录，产品正确拒绝覆盖未读取review.ptstudio.json。换全新输出目录独立复验同一最终DLL **14/14通过**；保留完整运行原Failed，不刷掉或关闭安全保护。

DPI17失败来自缺少当前运行visual manifest/dpi-current目录；没有复制历史截图掩盖。其余74源码/合同通过不等于物理DPI通过。

## 先失败再修：已闭合的非像素缺陷

详见FAILURE_REPRODUCTIONS.md。比较调度旧ComboBox独立复现失败后，测试改走当前真实菜单MenuItem.Click，保留效果像素/历史/并发断言；光片页签索引同步实际布局。导航“暗室”名称与可访问名一致。缩放改成矢量图标、按钮归入样式分类。诊断护栏和方案切换改用现有主题消息框，保存/放弃/取消三个动作及Enter/Esc保留；历史效果明确撤销恢复入口。专项与最终相关全量复验已执行。

修复提交：96e84b14a0cbb040b97cc531efeb31b1f4144367；完整壳护栏起点f6adf8674bf9c3b7159caeb086c04a0d1eadb923。没有改Core像素算法、PV4/PV5/PV6数学、阈值或分类来换绿。

## 策划15分区与其他模块真实状态

已有15分区强资源模型、镜头/资源ID引用、模式切换、草稿/保存失败恢复、RTF/DOCX/表格/图片托盘/搬迁等从当前实现验证，不从零重写。Foundation资源roundtrip与R5全区构造、R7连续/页眉/表格/搜索、DOCX原子替换、图片相对搬迁与撤销等本轮专项均在总账中逐方法映射；人工仍BLOCKED。工具箱设备目录真实来源缺失、复杂Word/WPS、任意混排结构/复合历史及既有范围限制保留。

素材库原尺寸/评分/弹层/右键/视觉分析、仿色原图只读/批量/预览导出、自由画布缩放和工具栏等有相关本轮自动测试，不据此填鼠标成绩。PARITY高精度结论撤回仍有效，不声称真实RAW/ICC/代理全链精度完成。

P3性能原673.8ms失败历史保留，本轮结果独立；Fuji重复性、显示器色彩、研究PV7/合成ICC、宝丽来物理校准和V3产品接线分别登记于CLAIMS.json。默认NoneOnlineSelectionProvider不是三客户端真实服务；看守文件夹/模拟适配器不是相机真机。

## 实机/视觉及发行门禁

最终完整EXE真实启动，可访问性树见FINAL_DESKTOP_INITIAL.txt。首次教程使导航暂禁用；点击退出教程和激活目标窗口均被当前桌面拒绝：GetCursorPos failed 0x80070005。没有独占桌面输入条件，因此121业务动作的人工列BLOCKED/NOT_RUN；未用自动RaiseEvent/离屏图冒充真人鼠标。

无最终产品桌面截图/录屏，无1366×768/1920×1080并排和物理DPI闭合。相机/跨屏/WPS/外部服务/安装环境不具备时继续其他模块，不使用mock宣称真机PASS。R3_VISUAL_DIFFERENCES.md继续单列，原型HTML和前轮PNG不算本轮桌面证据。

实际桌面业务动作总计121：通过0、失败0、跳过0、未完成操作121（其中117受桌面条件BLOCKED，安装4项NOT_RUN）；启动身份观察另列PARTIAL。没有将环境阻断写为产品业务FAIL。

因此发行门禁 **BLOCKED**，整体 **PARTIAL**。交付的是完整范围诊断包，不是已验证正式完整软件或Setup。

## 分支、保留、推送

52个相关远端ref作merge-base和branch-only差异核对；还对非祖先改动文件作当前blob等价比对。素材库导入加固6个文件缺失、摄影followup有24个当前缺失文件；另有等价/不同当前文件，不能机械按祖先与否判断功能存在。未擅自merge/cherry-pick，依赖及差异见BRANCH_INTEGRATION.json与BRANCH_CURRENT_EQUIVALENCE.json。

其他工作树只读，旧Release与未提交内容不动。没有reset/stash/clean/合并main/强推。阶段提交、文件类型、Git本地远端一致读回见STAGE_COMMITS.json、CHANGED_FILES.json、PUSH_READBACK.json。最终结果封存后正常推送本验证分支。
