# 真实失败关闭与色彩路径：PARTIAL / OPEN GATES

业务验收时区Asia/Shanghai，原始主机时间戳保留。基线validation/kitao-wpf-icc-gates-20261015/444551295dcc6d92d612dccfaf5e520a811c3e75与fix/kitao-icc-reliability-20261015/4e70d024已在线核对；merge-base为4e70d024，两者产品src相同。旧研究63ca45只是可追溯祖先资料，不整分支merge覆盖。独立工作树E:/Kitao-Worktrees/kitao-wpf-failures-20261016；分支fix/kitao-wpf-failures-20261016。

## 冻结身份
最终产品/工具/测试/宿主源码SHA：c6142c14c7fb4d1c9af500b42673ddb7a4a910ae。后续仅封存日志/证据；证据SHA为包含本报告的最终提交（不自引用）。初始3213805完整回归轮次保留，不能冒充最终源码测试。色彩数值研究脚本94309e0单列，不改V1/V2生产行为。本批src实际改变仅Reference顶栏布局，无像素算法改动。

最终新发布完整目录E:/KR/F16/release/win-x64，290文件；独立工具E:/KR/F16/tool/win-x64，414文件。真实新构建版本/EXE哈希见RELEASE_EXE_FINAL，递归逐文件清单RELEASE_FILES_FINAL，目录树摘要RELEASE_TREE_HASHES_FINAL；不是旧EXE改名。C:/KH/F16A、E:/KH/F16B实际测试host与发布产品DLL逐SHA绑定。首轮发布目录/结果也保留，不覆盖旧Release。

## 原失败修复
- P3两项嵌套PowerShell失败：过深run-ownedvalidator目录作CreateProcess工作目录不合法；改verifiedSystem短目录+绝对powershell.exe，-File/-RunRoot绝对身份不变。原3项专项Passed，断言不变。
- TetherRepoRoot：移除publishedhost祖先src搜索，使用显式TestResources.Root。原2项Passed。
- PrivateWpfHost：INFINITE改默认10800秒、配置1..21600秒，有界原始UTC事件；child suspended→assign KILL_ON_JOB_CLOSE JobObject→resume，timeout kill整个ownedtree并等10秒。实际2秒smokeexit124、parentchild都消失，不切换输入桌面。
- Premium collision：本轮先复现close矩形[1872,64,40,40]与导入/导出重叠。Auto列无限测量且responsivecode重置XAML；初独立行修复碰撞但使1180x720照片高度386.7<原396要求，原失败保留。最终wide/medium显式MaxWidth(width-220)有限Auto操作区、narrow独立行，全部按钮不藏不裁；原照片高度断言、Premium7模式/逻辑scale零碰撞和每enabledbuttoncenter InputHitTestownancestor通过。实际发布B专项18Passed含Premium1optin，分开不加入完整1626计数。真实RenderTargetBitmap截图保留及人工视读，不叫physicalDPI/nativeclick。

## 完整回归与失败历史
最终完整Core/实际发布WPF的发现、选中、Passed、Failed、NotExecuted、超时缺TRX均以TEST_CLASSIFICATION/原TRX为准。Core1708项源码1701Passed7NotExecuted，实际发布1702Passed6NotExecuted，strictFAILED。PV4/PV5/PV672golden及P3原400msPassed且原文件内容不改。

完整第一轮（3213805）产生新高度1Failed及P1/BatchExport两类70结果缺失超时，保留FULL_RELEASE_ROUND1_SUMMARY。P1独立短根诊断42Passed不是完整套件替代。最终c6142c1完整重跑不改90/600秒class限额，也不再修源码；任何超时/缺TRX/Failed严格阻断，无结果不能记Passed或NotExecuted。历史3Failed、Premiumcollision、初XAML修复未生效、height回归和新timeout全部原样归档，不无限重跑挑绿。具体最终计数：1626发现/选中，1584返回，1561Passed/0Failed/23NotExecuted，42缺TRX超时，strictFAILED；逐名见FINAL_WPF_COUNTS.md及classification。

## 色彩科学：不靠全局clamp让测试绿
03_COLOR_CHAIN_CONTRACT分清legacyLibRaw最终RGB16非sensorlinear、受控ICC线性矩阵研究路径、工作调整/output/display编码。ICCprofileSHA/PCS D50/adaptation1/relative1/BPCoff、浮点域、recipe/source/output身份/量化/映射阶段独立。V1defaultoff严格0..1拒绝不变，V2仅命名explicitclip研究版；PV456无漂移。

Canon固定矩阵回程60,510,960样本，1,557,374微负全部来自输入恰零通道，正输入通道负值0，最小-2.8223e-10；对exacthalf的float最大7.45058e-9，directidentity0；原ICCfixed16矩阵double逆/回程误差5.55e-17，fixedskcms样本复验同类roundoff。此具体负值属于数值回程误差证据，不泛化LUT/不同输出空间/非线性节点。真实ProPhoto原色转sRGBdouble有~-.7、>2等有意义超域，不能丢弃。

未来零边界策略只能在精确profile/运算/source证明和独立区间舍入界内研究，不以本次测得最大误差制定全局epsilon；本批未加生产epsilon、quietclamp/headroom或曝光压缩。保留extended float、命名hardclipV2、未批准LumaRayStudy候选比较灰阶/高光levels和肤色/saturation损失。高光512levels→hardclip102、candidate50，candidate不是可批准感知映射。LCMS矩阵profileperceptual与relative输出实际相同，intent开关本身不是gamutmapping。双成熟完整感知映射准入未关闭，生产映射OPEN。

## Native/RAW/Structure与实机
精确LCMS2.16来源/recipe/MIT/PE与hash复核不等于可复现重建；干净WindowsVM及完整发布加载NOT_RUN_NO_ISOLATED_VM，不以当前机器minimalPATH替代。不同hash的SdcbDLL不冒充，新DLL不捆绑，普通入口不打开。Sony0.22.2继续research，recipe/完整pixel/发布义务OPEN；Fuji5178/5184原点/版本合同保留，不后裁图。Structurefull/tile质量/取消/自然WS证据可复用，但~6.8s慢于full~4.2s，生产不换。

本批未预约独占共享桌面，不发送输入、不自动凭idle启动。已按最终Release290哈希准备新native-runtime和短脚本。启动、策划/Color导航、托盘simple/advanced插图与搬迁重开、undo/redo、DOCX、切图取消/保存导出、真WPS往返、物理DPI、视觉碰撞和10s系统内存均nativeNOT_RUN。后台HWND/hittest/AX/RenderTargetBitmap/logicalDPI与真实点击/WPS/visualapproval分账；旧输入检测UNKNOWN不改。

CalibrationEligible=false，当前不要求用户买色卡，不宣传A7M4绝对色准或宝丽来精确复刻。普通push新分支并核对LOCAL/REMOTE HEAD，不main合并、不强推。完整性校验只证明证据/文件身份，不等于产品READY。
