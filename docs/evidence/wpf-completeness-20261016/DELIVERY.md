# 完整 WPF 结果归属修复：42 项缺失已关闭，外部门禁仍开放

基线远端fix/kitao-wpf-failures-20261016/2acf74fbcff559d89bf6cc5f41d9bdf971595ee3已核对，独立工作树E:/Kitao-Worktrees/kitao-wpf-result-completeness-20261016。保留旧工作树、Release和失败原件，无main合并/强推。业务验收时区Asia/Shanghai，原主机时间戳不重写。

## 冻结源码与最小改动
构建/测试冻结SHA3162cb10e0bc4838331a9325a1169baff051f2a9。产品src和像素算法相对基线零变化，变化为测试运行器执行单元、P1测试init/cleanup遥测和BoundedTestProcess子进程采样。原42用例/dataRows/断言/覆盖不删，600秒上限不增加；P1默认按原fullyQualifiedMethod10单元执行，25与9行数据组整组保留，每单元独立TRX，runplan+result按ID/FQN/displayName对账。其他类仍按类，Core完整singleprocess。短ordinalTEMP/结果子目录降低证据路径耦合，explicitresource-root不回退D盘或祖先搜索。私有host有界JobObject和treecleanup沿用。

## 超时归因：有证据与不可恢复部分
历史finalfullP1只有600秒timeout/缺TRX，不能事后编造per-case/wait/资源；历史短类42Passed累计526.959秒，362子进程全部结束。full目录100/temp105字符，短类107/temp112更长；不能归成“短路径即修复”。测试程序集hash也不同，不假称strictsamebuildAB。

本轮instrumented整类保留same600模式诊断42Passed，case累计463.741秒，362子进程开始/结束，无子超时或退出后EOF阻塞；CPU/WS/private/threadwait/per-casecwd/temp原始事件已存。它不覆盖历史timeout。

关键新完整套件中P1方法单元累计654.483秒（超过旧整类600），最长381.672秒（低于同600单元上限），全部42Passed且逐名dataRow一致，无拼接。由此复现结构性根因：把42个重工作/数百子进程绑在单累计预算，负载差异可越600，adapter末尾写TRX使一次kill丢42归属。不能唯一解释旧机器那次慢的内核链，但累计期限耦合/缺结果机制已确认。分片fault-isolation而非简单增时/标Skipped。

当前每项/子进程事件含UTC、cwd/temp长度、elapsed、CPU、WS/private、线程等待、exit/stdout/stderrEOF状态。原42内常规git8次+validator等生成362子进程，方法隔离新增少量fixturegit读取，记录不隐瞒。ThreadWaitReason不是WCT锁owner，历史真实WCT不可恢复。

## 发布与测试
新产品完整E:/KR/C16/release/win-x64 290文件、独立工具E:/KR/C16/tool/win-x64 414文件，版本2.3.0-result-complete+3162cb10...；EXE真实hash见RELEASE_EXE，递归逐文件RELEASE_FILES及树哈希RELEASE_TREE_HASHES。C:/KH/C16A、E:/KH/C16B测试host与实际发布DLL哈希绑定，不改旧目录、不拿旧EXE改名。

完整Core源码1708选中/返回=1701Passed0Failed7NotExecuted；实际发布1708/1708=1702Passed0Failed6NotExecuted，strict仍FAILED。独立第二host专项20Passed含顶栏碰撞/照片高度/按钮真InputHitTest/Premiumoptin及子进程超时清理；不把20加进全量计数。最终实际发布完整WPF：发现1626/选中1626/返回1626，1603Passed/0Failed/23NotExecuted/0missing，221执行单元覆盖原212类。严格FAILED因23条件未执行保留；不存在缺TRX/timeout/extra/missing覆盖差异。P1全部42属于同一新完整运行，未拼接。逐项TEST_CLASSIFICATION/FINAL_WPF_CLASSIFICATION。全部原始TRX/stdout/stderr/settings/进程/用例事件与历史失败归档RAW_TEST_RESULTS，旧42诊断不补fullresults。

## 色彩和外部门禁
新增精确有理ICC矩阵往返identity证明及IEEEfloat32gamma5条件舍入界；固定模型RGB最大界约9.55e-7/4.61e-7/4.15e-7（小于.063/.031/.028个16位码），条件normal/known3mul2add/no nonlinear/exactEVhalf明确。它不是LCMS内部系数构造的全引擎证明，也不是LUT/宽色域/其他profile全局epsilon。未改生产容差或V1拒绝。真实负值/高光>1和广色域仍extendedresearch保留；V2explicitclip和高光更差LumaRay候选未进生产，PV45672golden/P3原400ms保持。

干净WindowsVM/独占预约桌面不可用：native加载完整目录启动/策划Color操作/WPS/physicalDPI/visualapproval均NOT_RUN。私有stationHWND/RenderTargetBitmap/命中模型和当前hostminimalPATH不写成实机批准；本批prepare-only新runtime，没发送桌面输入。V1defaultoff，V2research与普通入口边界不变。Sony候选/Structure生产不改。CalibrationEligible=false，无色卡采购/绝对色准标定宣传。

## 交付判据
最终每项归属已对账，42缺失问题在新完整运行关闭；23条件NotExecuted逐名保留，不冒称全部测试执行通过。证据完整性通过不是native或色准验收。每阶段提交及文件类型清单、Release哈希、LOCAL/REMOTE确认日志随封存；普通push新分支，不强推。
