# 家里照片引擎核验：交付已封存，产品验收仍 BLOCKED

日期：2026-10-16（Asia/Shanghai；原构建日志保留其记录时区）。本次续接原会话在上下文耗尽时留下的交付收尾，不声称所有产品、发行或桌面验收都已完成。

## 1. 基准与实际完成

- 独立工作树：`N:/pixart/kitao-photo-engine-home-20261016`。
- 分支：`feature/kitao-photo-engine-home-20261016`。候选基准及本次续接远端核验均为`2ca931e0e1550942ed2c37ff58c51a0382a3d18d`，无新增候选差异。
- 已有9个分阶段提交保留；生产改动只有显示器ICC关联只读诊断，不改变像素、算法、版本配方或PV4/PV5/PV6 golden。
- 修正WPF连续创建标签测试的命令完成等待；未改变产品AsyncCommand行为，也未松动断言或性能门槛。
- 本次新增证据封存器及4项回归测试。与既有门禁自检合计13项全通过；全部历史产品测试**未重跑挑绿**。
- 逐一核验290个发布文件；发布测试和后端实际Main/Core/Kernel/模块/原生依赖SHA与发布身份一致。7份保留测试结果按原TRX回读，精确FQN/参数行/计数无缺失。
- 原严格发布门禁与家用验收门禁均重算，JSON与原结论一致，仍为`BLOCKED`。

## 2. 完整测试结果（全部保留原始失败）

|结果批次|发现|选择/结果|执行（排除NotExecuted）|通过|失败|条件未执行|缺失|
|---|---:|---:|---:|---:|---:|---:|---:|
|首次源码Core|1708|1708|1701|1701|0|7|0|
|首次源码WPF|1626|1626|1603|1602|1|23|0|
|最终源码Core|1708|1708|1701|1700|1|7|0|
|最终源码WPF|1628|1628|1605|1605|0|23|0|
|最终发布组件Core|1708|1708|1702|1701|1|6|0|
|最终发布组件WPF|1628|1628|1605|1605|0|23|0|
|实际发布Core身份专项|1708|1|1|1|0|0|0|

WPF发现数+2来自新增2个必需显示器诊断测试。首次失败为`AssetLibraryP3WpfTests.TagManagerRefreshPreservesTheSelectedGroupForConsecutiveTagCreates`，原始TRX/发现清单/日志封存，等待命令真正完成后的复跑单列。

最终Core必需失败原名：`RAWSelectionAssistant.Tests.AssetLibraryP3PerformanceTests.TenThousandSyntheticItemsMeetP3QuerySuggestionSmartFolderAndBatchThresholds`。当前/全库切换最终源码**2807.1ms**、发布组件**835.8ms**，均超过未更改的**400ms**。首次通过不能覆盖最终失败。

23项WPF、6项发布Core条件未执行的**完整原名、原因及原源码行**见`CONDITIONAL_TESTS.md`和`02_tests/INHERITED_CONDITIONAL_NAMES.json`。唯一未运行条件29项，严格门禁58条是源码/发布重复记录，不是58个独立缺口。原始runner有NotExecuted时仍报FAILED，不改成全绿。

## 3. 产品后端与研究边界

|分类|实际结果|没有证明的内容|
|---|---|---|
|产品已实现/本机后端证实|Canon旧链完整TIFF16/PNG16，PV4 TIFF/PNG与数值合同；Fuji旧链完整TIFF16/PNG16、预取消；发布组件身份匹配|不是产品EXE真实启动、前台切图、处理中取消或保存重开实机验收|
|产品已实现|显示器ICC关联读取、profile头/hash状态、缺失/损坏诊断、卸载解绑；预览tooltip明确未做补偿|不是显示器像素转换、校准或跨屏前台验收|
|产品实际阻断|Fuji同源旧默认多线程输出38,375通道样本不同，最大11,713/65535；PV4另因估计工作集超过1GiB拒绝|不把平均差小、审计单线程相同或拒绝当全链通过；不降尺寸/位深绕预算|
|研究工具证实|官方LibRaw0.22.2包身份、Canon/Fuji预编译全尺寸线性PPM；两构建LCMS有限profile参考，exact-profile最大6码，8bit跨构建最大0码|不是源码重建、干净VM、许可发行批准、产品Sony支持或任意ICC支持|
|推测/待定位|Fuji原生X-Trans并行路径可疑；显示器型号与profile文件名不同需人工核对|未证实完整竞态根因；不能单凭文件名宣布profile错误|
|未运行|产品前台启动/导航/切图取消/保存重开/导出对话框、真实关闭内存、物理100/150/200%DPI；Sony精确授权素材；候选干净重建/VM及产品全链；新实拍盲评|不以后台API、控件测试、只读OS清点或构建成功替代|

细表见`PRODUCT_RESEARCH_INFERENCE_NOTRUN.csv`。ICC路径审计见`04_color_audit/DECODE_TO_DISPLAY.md`；WIC RGB48实际拒绝0x88982F80保留，没有偷偷降为8bit。第一次理想OETF参考失败和修正后的exact-profile参考均保留，没有放宽阈值。固定sRGB研究工具仍不是通用ICC。

## 4. 完整诊断Release身份（未批准普通交付）

- 冻结构建源码：`ff1df9b27db4b14530d1d9a85107e9351a6a623b`。后续提交只增加交付审计工具和证据，不重写这个身份。
- 目录：`N:/pixart/kitao-photo-engine-home-20261016/artifacts/home-photo/07-blocked-diagnostic/win-x64`。
- 模式：Release、win-x64、自包含、非单文件，共290文件。
- EXE：`KitaoPhotoSelector.exe`。
- EXE SHA256：`FBB2A57651481B91CE2CC51F6E71B62426F1FA7FF1F729A049382A728C11B6BB`。
- Core SHA256：`420058EE3C9E768D2AA13684890EE9554D020EBAD95D68FB9A7D7BA7BABBCEEB`。
- 状态：`BLOCKED_DIAGNOSTIC_BUILD_NOT_APPROVED`；未启动产品EXE，未混入候选0.22.2 DLL。
- 全文件/依赖/测试与审计来源见`07_release/FILES.json`、`07_release/IDENTITY.json`。

## 5. 封存与证据限制

`RAW_TEST_RESULTS.zip`：2,741个精确选定成员，5,925,438字节，SHA256：

`95BDD25F9BD14553D58CE7A2877E99F8A7A5E86A2448F996082CC07219D15FFF`

包括完整原TRX、runsettings、stdout/stderr、发现清单、结果和直接构建/工具日志。每个成员已按SHA回读。只纳入runner精确文件，不递归收入测试runtime；无照片、RAW、SQLite、ICC、凭据文件、候选DLL或发布二进制。保留清单和限定模式敏感内容筛查，不声称是全盘秘密扫描。

**限制**：首次WPF测试DLL已被后续编译覆盖；原SHA/发现清单/TRX/日志仍在，原DLL未能额外封存。最终源码和发布测试DLL的现有SHA均验证一致。封存器初版路径分隔符/审计子目录/参数化执行单元适配的开发失败已本地保留，不更改任何原测试结果。详情见`EVIDENCE_INTEGRITY.json`。

复现命令见`REPRODUCE.md`。实际桌面未运行记录见`03_NATIVE_NOT_RUN.md`。显示器和DPI均未修改，无恢复操作可声称已完成。

## 6. 交付完成与仍开放的条件

本轮能独立收尾的证据、可复现命令、分类表、发布身份和正常推送完成情况单独记录。**不以交付收尾完成宣称产品验收完成**。

下一步必须处理：必需P3性能根因；Fuji默认旧链重复性及兼容版本合同；实际显示补偿设计/参考；候选Windows源码重建和发行义务；Sony精确授权原件及全链；桌面独占/物理DPI；真实照片盲评。具体门禁见`NEXT_BLOCKERS.md`。不得强推、合并main、覆盖旧Release、清理其他树或搜索私人资料。

正常推送与最终LOCAL_HEAD/REMOTE_HEAD由`PUSH_STATUS.md`和最终回复核对，推送成功不改变BLOCKED。
