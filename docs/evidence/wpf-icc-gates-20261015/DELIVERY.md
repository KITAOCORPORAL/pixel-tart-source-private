# WPF / ICC gates — BLOCKED / PARTIAL

业务验收时间采用Asia/Shanghai，原主机时间戳保留。起点4e70d024fc10810df0ad5b76b77fdb4a2cd62ed9；本轮开头网络失败，随后01_REMOTE_REFRESH实际核对远端同SHA。独立工作树E:/Kitao-Worktrees/kitao-wpf-icc-gates-20261015，分支validation/kitao-wpf-icc-gates-20261015。未main合并/强推/替代分支串，不动其他工作树或旧Release。

## 冻结与真实修复
发布产品/CSharp工具/测试源码冻结61ed030098cc198019a45188210ef174cb2dc0d1；独立V2研究源码冻结5b07b0ad9af8b36b9c99c9f15a24eed808b484b1。最终证据SHA为包含本文件的提交，不自引用。本批src产品代码相对起点零差异；不是新产品ICC入口。新构建EXE版本2.3.0-wpf-icc-gates+61ed030...，不是旧EXE改名。

第一阶段增加私有非交互windowstation/desktop进程宿主；不调用SwitchDesktop/物理输入，独立剪贴板，无共享桌面争抢。全部212类/1626测试实际发现并执行，不再只选16项。修复7个FindRepositoryFile丢失相对parts、Studio设计资源子目录和两类测试覆盖tracked历史报告的副作用，断言不删/不放宽。首轮1569Passed34Failed23NotExecuted完整保留。冻结后仍有失败，未继续修代码或重启全量回归。

## 最终实际测试
|执行|发现/选中|Passed|Failed|NotExecuted|strict|
|---|---|---:|---:|---:|---|
|源码Core|1708/1708|1701|0|7|FAILED|
|实际发布Core|1708/1708|1702|0|6|FAILED|
|C根实际发布完整WPF|1626/1626|1600|3|23|FAILED|
|E根实际发布受影响WPF|1626/73|73|0|0|PASSED_SUBSET|
|额外opt-in离屏证据|1626/6|5|1|0|FAILED_SEPARATE|

完整WPF三项失败：P3AutomatedRunSetContractTests的WritesPassedSummary、RejectsWrongCount...两个子进程Start目录无效；TetherSplitCompareTests.SplitUsesGeometryAndSharedParentTransform仍RepoRoot路径依赖。冻结后不修改，全部原文见FINAL_WPF_NONPASSING。额外PremiumPrototype_ThreeRealPages在reference-simple.png发现真实ShellCloseCollision，保持发布视觉阻断，不与完整基线计数混合。其他五个opt-in结果包含离屏关键帧/逻辑缩放，非物理DPI批准。23NotExecuted逐名条件保留，不被研究probe提升为Passed。发现/结果/原TRX/settings/stdout/stderr/超时均归档RAW_TEST_RESULTS.zip。

## ICC数值研究，不改V1
五个冻结照片派生输入中V1仅headroomCanon通过(1/5)；三RT照片先profileadmission拒绝，原Canonlinear回程负值导致OUTPUT_DOMAIN拒绝。逐阶段min/max/负值/>1像素统计在02_PHOTO_DOMAIN_AUDIT。不是五独立相机原件，也不将RGB16逆推sensor。

固定LCMS453baf...与skcms97adc...实际12点×3profilepairs比较扩展linearfloat与整数sink。保留负/>1float，两者integer写出裁切；不是感知gamumapping批准。独立tools-only ExtendedLinearClipSinkV2保留扩展值到命名ExportClip0To1V2，随后明确逐通道硬裁切并记录像素数，before-map floatTIFF保留原值。灰阶/饱和/肤色邻域/高光/全范围Canon实际preview/reopen/TIFF16/PNG16/displayJPEG8及ICC读回验证。Canon1392846像素在明确sink被裁切；不通过暗增容差/预压缩修改旧V1。JPEGMAE/PSNR单列。该V2是有限研究工具，不是感知映射、正常产品功能或色准校正。

## native准入与实机
精确LCMS2.16conda包recipe已追到tarSHA、feedstockcommit、VS2019/MSBuild命令和utilitypatch；普通/延迟PE导入审查、MIT文本保存。最小目录+收窄PATH当前机器加载2160通过，不等于干净VM；源码归档下载失败、可复现重建和干净机器仍未关闭。故不捆绑新DLL、不打开产品入口；不同哈希的SdcbLibRaw附带DLL不替代。

新Release完整290文件、独立CSharp工具414文件递归SHA见RELEASE_FILES，C/E实际测试组件逐SHA绑定PUBLISHED_COMPONENT_BINDINGS。显式入口状态设计03_ENTRY_GATE列profile/算法/映射/拒绝原因，但门禁CLOSED。普通项目/recipe/PV7/合成reader不迁移。

本批不查询/抢占共享桌面；短脚本Prepare-NativeAcceptance已对封存290文件校验并准备新隔离runtime与17项验收模板，默认prepareonly，不启动/不点击。Startup/导航/托盘插图搬迁/Color切图取消/保存导出/物理DPI/WPS真实往返均NOT_RUN；后台/AX/截图/视觉批准分别记账。先前用户输入检测UNKNOWN不重试。脚本待人工确认保留桌面后可执行，idle10分钟本身不等于已预约。

Sony候选不捆绑，recipe/完整像素/发布义务仍OPEN；Structure~6.8s慢于full~4.2s不替换生产。原72组PV456 golden/P3原400ms测试通过且原合同文件不改，历史失败保留。CalibrationEligible=false，不购买色卡、不宣称A7M4绝对色准或宝丽来精确复刻。

## 交付状态
完整发布WPF已执行但不通过，不宣称READY。剩余明确：三项WPF失败、额外Premium几何失败、23外部/人工条件、干净机器/native对应、真正实机/WPS/DPI。冻结后只封存结果，不再改源码。最终普通推送及远端SHA查询另有日志；文件清单和完整性验证仅证据校验，不代表产品验收。
