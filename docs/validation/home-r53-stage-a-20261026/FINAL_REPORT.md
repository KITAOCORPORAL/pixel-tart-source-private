# 光镜 R5.3 阶段 A 子集 + 旧教程退役 · 家用交付

**阶段 A 部分实现；R5.3 总目标未完成。完整 Windows x64 诊断包，不能发行，无安装包。**

## 起点、路径与保全
- 实际主仓库 `N:/pixart/pixel-tart-source-private`；历史线索 `N:/pixart/pixart/pixel-tart-source-private`不是Git仓库。
- 独立工作树 `N:/pixart/guangjing-r53-stage-a-20261026`，分支 `home/r53-stage-a-20261026`。
- fetch并独立ls-remote核对公司 `company/whole-app-ui-copy-cleanup-20261026` = `48527fa0b7d3d063d5af5f6b62fa1a18cb36ef66`，无后续提交。旧家用成果已为其祖先，没有旧代码覆盖公司成果。
- 20旧工作树HEAD/分支/status末次读回不变，主树211项未提交保留；无独有未推送提交被覆盖。无reset/clean/stash/rebase、main合并或强推。旧ZIP、数据根、阶段内失败构建/测试均保留。
- 当前可运行源码/测试 SHA `901061433ce56dbb85e49ff5cc8c95ecd82760fe`；教程退役主提交 `d6590c99b7ed5f85953f898f9037b6b6ec31267b`；最后实施提交 `955548a1d4b061b7bb0bbeb0d9dc967c839b385f`只改安装器展示/产物名，与源码/测试零差异。验证提交和最终远端HEAD见PUSH_READBACK.json，不把文档HEAD冒充二进制源码。
- 用户环境2026-10-26 America/Phoenix；实测主机China Standard Time已跨入10月27日。UTC/epoch原值不重写；目录20261026是任务标识。

## 真实设计来源
原ZIP `C:/Users/Admin/Downloads/Compressed/光镜-UI-R5.3-定稿.zip`，SHA256 `16f75c0f40b506e4db57f140f389967264040cd8c5930ad24946b11c171e2ca9`已重验。独立解压 `N:/pixart/Guangjing-R53-Home-20261026/design-reference`，实读R5.3文档、dev-spec、layout-directions、暗室/即时胶片HTML、frames-collab-pack.js、移植检查、画布/策划HTML及简易/专业/布局B图片；完整路径/hash见DESIGN_SOURCE.json / DESIGN_MANIFEST.json。原型未作为WebView嵌入，未把设计包或授权未知联名素材装入产品。

## 阶段 A 实际产品
- 使用同一ReferenceColorWorkspaceViewModel/Editor、ColorAdjustmentStack、历史和导出管线；简单/专业仅视图模式。同图真实参数→切模式→改同一节点→撤销→保存重开→PNG导出receipt自动验证。
- 264资源栏/304参数栏、中央主图，专业串行节点在照片胶片条上；DIP ActualWidth断点，窄窗显式入口。预设按钮生成实际节点，不是CSS/截图效果。
- 稳定Photo/Variant ID，新增原始只读变体；F2从原始/F3复制；名称、评分、色标、显隐、主变体持久。旧项目无MainExplicit时保持历史当前编辑输出，显式设置主后固定主输出。
- 用户锁/系统锁分别建模，在参数/名称/排序/删除边界阻止修改；仍渲染、显隐可撤销；修复旧look入口先改保存状态的旁路，修复准备式重开原始只读丢失和RAW切换残留只读。
- 删除确认后归档并可在本会话撤销，不删原照片，恢复该ID不抹掉后来新增变体；归档ID拒绝直接切换，不替换其他变体。重启后撤销删除历史暂未持久。
- 已选路径900ms防抖自动保存，保存gate串行化，变更修订避免IO期间新编辑误写“已保存”；正常关窗等待最后保存，取消/外部文件变更/失败留窗口和内存。首次仍需手动选项目位置。
- B统一画笔（真实蒙版未接入，明确不可用）；反斜线切换比较，按钮按住看调整前，松开/失焦释放。宽窗专用440 DIP的3D区保留主图≥560；窄窗显式专注检查，用户主动解除停靠才浮动。尺寸冲突以布局调整解决，不硬塞264资源栏；数据来自现有真实图像模型。

**未达阶段 A 全量**：局部画笔/擦除/羽化/流量/密度、红色蒙版/反相清空及其持久撤销、通用图层强度/混合渲染、2–4变体同步缩放平移、资源收藏搜索/示波器折叠等精修、完整3D选择/保护匹配持久化、物理DPI/真实鼠标闭环。没有假并行节点或假笔迹。B/C/D/E本轮未新增实施，既有即时胶片/画布/15区策划保留不等于R5.3全量完成。详见STAGE_MATRIX.json。

## 追加：教程退役与品牌
原因是旧22步服务仍在新装启动/旧进度恢复/升级提示中激活，且帮助、教程欢迎/升级弹窗等多处硬编码旧品牌，没有走统一品牌常量。
- 移除首次/升级自动分支、帮助菜单手动入口/重放/重置/删除操作、遮罩/步骤卡/焦点圈与升级窗口。产品示例生成器删除，兼容Service壳不会生成、写入或清理文件；旧设置/证明盐/目录保留，不伪造完成。
- 窗口、帮助、菜单、监看/参考/资料/提示、素材库反馈、默认水印、翻译值、文件产品元数据和安装器可见名/发布者/产物名统一“光镜”。95个XAML、2067翻译值未见用户可见旧品牌。内部namespace/程序集/资源key/旧翻译key、数据库迁移与历史安装路径保留；已有用户自写水印/项目名不强改。安装器只查源码，未构建或运行。
- 旧教程已退役的行为测试改为“不激活、不生成、不删除、设置保全”合同，不把旧22步通过数移植到新产品。正常分片测试用测试专属合成文件，不把产品教程示例偷偷留回。
- 已在docs/PRODUCT_BACKLOG.md、HANDOFF.MD记录：**全部功能完成且交互稳定后，集中制作覆盖光镜所有功能的产品引导与说明教程。** 届时覆盖实际摄影师全流程、模块操作和跨模块衔接；当前不开发替代教程。

## 本机验证（最终发布DLL绑定，不引用公司成绩）
|批次|通过|失败|未运行|
|---|---:|---:|---:|
|Core受影响回归|211|0|2|
|WPF暗室/教程退役/关闭/品牌等受影响回归|108|0|4|
|完整App启动独立进程：first/upgrade/restart|3|0|0|

3个额外进程分别执行真实App.OnStartup、进入LocalSplit、3合成JPG扫描/匹配/复制/CSV，源字节不变；旧18步设置不激活且保留旧目录文件；新装无Tutorial生成。它们在testhost里调用产品命令，**不是鼠标验收**。29发布DLL绑定/测试宿主hash见COMPONENT_BINDINGS，原始命令/PID/时长/TRX在RAW_EVIDENCE.zip。Core2和WPF4为授权RAW/空间/性能opt-in未提供，见NONPASS.json，不计PASS；不扫私人照片、不改符号链接/脚本策略。

宽泛开发检查含旧逐字文案夹具：retirement-development-r2 Core145通过，WPF179通过/7失败；7条分别为公司已清理的收支2/资料3/联机2旧文案镜像，仍原样保存不算通过。另有旧迁移/layout/锁定真实失败及测试实现构建失败已修，全部保留DEVELOPMENT_RUNS/RAW；最终范围按受影响状态选择，没有重复所有无关门禁，也没用改阈值刷性能。

既有家用Scenario11有效原始结果重新读ZIP/hash/TRX：source4f57ca56、10000次、30000ms门槛、实际1292.9921ms Passed；本批未重跑，不称当前提交性能，不用公司耗时替代。原物理卷指纹限制保留。

## 原生范围与图片
实际完整EXE PID35864启动→OS空工作区关闭→同根新PID42044启动，窗口响应且程序集ProductVersion/hash与发布一致，Core/WPF/AssetLibrary/RawTool/OnlineSelection加载读回。**仅进程身份，不是编辑保存闭环。** 本轮用户AGENTS禁止Computer Use/Browser/WebView/CDP，未重新启用；先前工具发现错误“unsupported custom tool call: exec”仅发现阻点，不冒称点击产品失败。

真实鼠标导入/普通列表/JPEG/镜头/自有副本→编辑保存→UI正常退出→新PID重开→15区汇总/另一根导入/快速A-B、100/125/150%物理DPI均NOT_RUN。6张WPF RenderTargetBitmap图仅离屏布局，**不是桌面截图**。提供MANUAL_RETEST.md，不循环空截图或以VM替代。

## 格式、色彩、授权与交付
- Session v1、Stack v2只添加兼容字段，稳定ID与旧快照保留；不改PV4/PV5/PV6数学、默认关闭研究保持。TIFF/JPEG的Software/ApplicationName/LUT注释品牌变动不算色彩算法改善。没有宣称任何专业软件全量色彩能力，颜色科学没有新增改善；像素门禁POLICY仍生效。
- `.ptplan v1`不含自由画布workspace；不宣称画布跨机便携。原包绝对路径只是历史身份，重绑脚本只作用包内合成照片；PowerShell7.5+的-DateKind String保留日期字符串，原始receipt不重写。
- 全部现有模块与10个主导航入口保留，不是独立暗室宿主。Release win-x64自包含，SDK10.0.401，WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false；290app文件、264声明运行资产缺失0。默认CMD在本机PS5.1禁脚本下可用，不改系统策略、不打开网页/资源管理器。
- 完整目录 `N:/pixart/Guangjing-R53-Home-20261026/Guangjing-R53-StageA-TutorialRetired-Diagnostic-win-x64`，启动 `Start-Guangjing-Diagnostic.cmd`；ZIP同名.zip，hash/逐项读回见ZIP_IDENTITY.json。包含暗室合成工作文件、A/B策划包与替换图片，无私人DB/UserData、无原设计包/授权未知联名资源。第三方许可证/notices随app保留。无Setup，不能发行。
- 产品/验证分别提交，普通push新home分支；最终HEAD及独立ls-remote读回见PUSH_READBACK.json。下一入口HANDOFF.MD与docs/PRODUCT_BACKLOG.md，不得把这批写成阶段A或R5.3全部完成。
