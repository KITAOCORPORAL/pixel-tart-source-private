# ICC reliability increment — PARTIAL / OPEN GATES

业务验收时区仅Asia/Shanghai；原始主机+08时间戳不改，不引入未经验证的其他客户端时区。基线远端feature/kitao-controlled-icc-20261015已核对b37d5e2d07df129605772516032653700df62bf6。独立工作树E:/Kitao-Worktrees/kitao-icc-reliability-20261015；分支fix/kitao-icc-reliability-20261015。保留所有旧分支、旧Release和用户资料，无main合并/强推。

## 实际修复
1. 测试宿主：Core/WPF根资源入口从assembly祖先扫描改为KITAO_TEST_RESOURCE_ROOT或随程序集生成test-resource-root.txt。旧摄影evidence子目录合同保持。C:/Kitao-IsolatedHosts/reliability-A及E:/Kitao-IsolatedHosts/reliability-B均不在仓库祖先路径，完整Core1701Passed/0Failed/7NotExecuted，WPF安全16项均Passed。首次遗漏导致11Failed记录保留，修正后不是跳过断言。runner新增--resource-root，可显式指向资源快照。其他专门DPI/native项目未机械改成普通单元测试。
2. ICC容器：新增单段/多段APP2真实JPEG成功路径、缺失/重复/损坏chunk和长度测试；TIFF大小端、重复ICC及nextIFD策略。首IFD单图支持，非首IFD/多页明确ICC_CONTAINER拒绝。EXIF2..8实际工作流拒绝ICC_ORIENTATION；opaqueRGB24/48范围不扩张。
3. WIC输入：WindowsCodecs.dll10.0.19041.5129；已知RGB48码逐点exact，RGB24JPEG解码误差独立记录，WIC升位code*257精确且通道顺序一致，LCMS输入/输出值逐点封存。不是只以同DLL相等证明输入。
4. 线性/显示：原linearTIFF16/PNG16不冒充普通screenpreview。新tools-onlybounded路径另写displayPNG8/JPEG8，显式IEC sRGB OETF和stockencodedICC；实际物理显示/monitorprofile未校准。JPEGMAE.00165745、PSNR50.91dB，阈值MAE<.02/PSNR>30，有损max.17255记录而非改容差。LCMS实际Windowsstockprofile与公式最多.000172差；skcms对该stocktarget拒绝，内置标准sRGB另比，不伪装等价。显示gate仍研究限定，不开普通入口。
5. 内存：同Canon全尺寸已准备headroom派生照片，旧wholearray工具20ms分阶段采样peakWS4.248GB；32行隔离候选initialpeak1.208GB，最终发布版见FINAL_MEMORY_TOTALS。预览/重开按同frozenrecipe分band，TIFF16逐码与旧版exact，PNG16文件字节exact；源hash/recipeidentity重验。2GiB保守codec+buffer预算，不以50MP代替预算。取消/失败不发布目标；保留partial诊断。修复WPFColorContext持有暂存profile文件句柄导致小JPEG发布失败，改从固定profile外部路径加载，不强制GC。

## 最终源码、发布与测试
冻结生产/工具源码SHA：6cea361464e27c4fc224bc0cc019b55cf600a884。最终证据SHA为包含本报告的HEAD（不自引用）。本批有产品Core容器可靠性改动和工具代码，不是报告-only；旧PV456路径不迁移。新增LCD/native不捆绑，普通用户ICC入口仍关闭。

产品新完整Release：artifacts/icc-reliability/release/win-x64，290文件。独立工具：tool-release/win-x64，414文件。两个真实新构建版本均2.3.0-icc-reliability+6cea361464e27c4fc224bc0cc019b55cf600a884；EXE SHA在RELEASE_IDENTITY，递归文件/类型/hash在RELEASE_FILES。没有旧EXE改名，没有发布包、照片、DB或第三方源码/二进制提交Git。

|最终执行|发现/选中|Passed|Failed|NotExecuted|strict|
|---|---|---:|---:|---:|---|
|源码Core|1708/1708|1701|0|7|FAILED|
|实际发布Core|1708/1708|1702|0|6|FAILED|
|实际发布安全WPF|1626/16|16|0|0|仅子集PASSED|
|完整WPF|1626/未全选|—|—|1610未运行|NOT_RUN|

72组PV4/PV5/PV6 golden和P3原400ms测试在源码/发布CorePassed，原文件hash不变。历史673.8/2499.7ms、上批369Core/6WPF失败、本批11失败及所有NotExecuted原样归档。研究probe不把六项改Passed。完整原始发现/选中/结果/TRX/stdout/失败log在RAW_TEST_RESULTS.zip，逐名WPF分类FINAL_WPF_CLASSIFICATION。

## 可审查进度与真正实机
Sony0.22.2callback取消返回-100010，RAIIclose实际执行；正常完整导出RGB16 4624x3080，color4不当Bayer。原生研究probeWS约266.75MB→close后5.67MB并自然10s保持，基线4.15MB；不是产品UI内存验收。旧0.21.1拒绝、RT共享/独立差异及旧recipe不迁移保留。候选recipe产品保存重开、矩阵前阶段像素、源码提供/许可证发布义务仍OPEN，候选不捆绑。

Structure本轮三轮candidate6831.91/6856.00ms中位/最差，full4221.89/4226.34，旧tile7176.99/7188.04；全部冻结floatSHA一致，逐tile/halo37/319tiles/GC/load采样。较前轮稳定不等于解释9.05s全部原因。RT实际scalarboxblur与DT实际horizontalhelper转置组合跑出~5.6e-7以内局部窗口差；不是完整DT优化vertical/Structure对照。仍不快于full，不换生产。

原生实机：最终真实Release在隔离native-runtime启动；AX观察到工作台准备就绪、没有观察到阻塞对话框。截图通道FrameArrivedtimeout，无视觉批准。首次原生策划导航click被工具报告用户输入检测；立即停止，click结果未知不计Passed。后续策划图片交互/Color切图/保存重开/导出/WPS实际往返/物理DPI100/125/150/200均NOT_RUN，详见05_NATIVE_OBSERVATION。后台合同、原生点击、WPS、视觉批准四类分开；不称完整产品验收。

## 开放项
完整WPF和产品原生路径、WPS/DPI/视觉；nativeLCMS精确构建对应/依赖/随包审查及普通入口；显示profile剩余差异和physicaldisplay；WICSave不能中途取消、预算非OS硬上限；candidate更广图像范围/RAWrecipe和Sony阶段像素；完整空间节点双实现及速度门槛。原全范围Canon仍ICC_OUTPUT_DOMAIN拒绝，只有限定/准备图像可试用，未压缩裁切修改其结果来掩盖。

CalibrationEligible=false；现在不要求购买色卡，不宣称A7M4绝对色准或宝丽来精确复刻。最终普通推送并核对LOCAL_HEAD/REMOTE_HEAD。证据完整性通过不是全量用户验收通过。

阶段源码/变更文件类型与测试对应见STAGE_COMMITS、FILES_AND_TYPES。JPEG成功样本是已知RGB24码值，EXIF2..8拒绝已执行；任意CMYK/alpha/JPEG/BigTIFF不批准。真实照片仍限定原headroom准备图，原范围超域拒绝未掩盖。32行工具仍持有全输出ushort和WICcodec缓存，不叫全流式或常量内存。
