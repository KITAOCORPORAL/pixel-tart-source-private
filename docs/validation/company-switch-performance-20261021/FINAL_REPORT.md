# 光镜公司续接：同步家用成果与Scenario11性能定位

日期2026-10-21，用户时区Europe/Berlin；原始日志保持UTC/主机时间，不改历史家用Asia/Shanghai记录。**诊断版，不能发行；整体PARTIAL，发行BLOCKED。**

## 第一阶段公司续接

fetch并独立ls-remote：家用home/guangjing-whole-app-sync-20261020仍为d39d11d68eb44f5aa0ec8a2f1afc95c98630820d，公司R3仍为a1e52238616b8d22f09b92595a85e76c7d9c1460。新独立树E:/Kitao-Worktrees/company-home-sync-performance-20261021，新分支company/guangjing-home-sync-performance-20261021。从家用当前HEAD续接，未在旧R3树pull/reset/stash/clean/rebase，不合main/不强推。

已读家用home-planning3所有指定报告、矩阵和原始TRX失败；e9b08863新增25Core/64WPF相关测试、99d7818两文件分类修复核对。当前灯位图/服化道与道具及旧别名的添加分类/过滤均存在；新树src tree等于99d7818的src tree。

以99d7818构建完整Windows x64自包含诊断版：Company-Home99-Diagnostic-win-x64。公司SDK10.0.302、runtime10.0.10；EXE/WPF/Core均与家用字节不同，已分别记录，不宣称同哈希。完整290文件、264声明运行依赖均存在。家用ZIP未进Git，公司自行构建。第一阶段提交3a55029e245accf72ca0d7904e81afad7a677d6c，本地远端独立读回一致。

|公司第一阶段|发现/选择|通过|失败|跳过|漏项|
|---|---:|---:|---:|---:|---:|
|策划Core专项|25|25|0|0|0|
|策划WPF专项|64|64|0|0|0|
|完整Core|1714|1709|0|5|0|
|完整WPF|1715|1695|0|20|0|
|发布组件|14|14|0|0|0|

公司成绩不继承家里。所有TRX/发现/执行计划见RAW_PHASE1_TESTS.zip；第一阶段完整证据亦在docs/validation/company-home-sync-20261021。

## 第二阶段仅Scenario11

家用原始32139.0909ms >30000ms失败保留。原测试源码与基线字节hash相同，10000交替OpenAsync循环和30秒门槛没改；100k元数据去重在Stop后，不计入切库计时。不是此前400ms查询测试。

分段副本诊断10000调用中9998缓存命中，仅两次SQLite校验。缓存物理路径检查约80%已计阶段；14万GetAttributes/7万File.Exists/6万Directory.Exists。热路径没有反复SQLite初始化或writer锁等待。每调用分段/尾延迟/CPU/逻辑IO/分配量保留，但物理磁盘等待、SQLite busy等待未做进程相关ETW归因，明确UNAVAILABLE/null。磁盘总体raw counters不冒充每调用磁盘等待，wall-CPU也不当等待。

最小修复0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f只改AssetLibraryContainerService热缓存路径检查：每次Open重查根及固定7组件，消除单调用共享父目录和Exists+Attributes重复；不跨调用缓存安全状态，缺可选路径仍原语义，其他IO错误不吞，manifest/指纹/冷SQLite/锁/schema不改。

|同条件公司实测ms，均10000次|修前|修后|
|---|---:|---:|
|原Scenario11独立进程|3628.9637|1703.5297|
|发布Core冷进程|3418.1982|1463.5954|
|预热第1次|3270.0605|1497.4150|
|预热第2次|3298.6967|1537.2916|
|预热第3次|3324.9674|1416.1346|
|干净四WPF并发第1次|4075.6049|1836.5044|
|干净四WPF并发第2次|4012.2595|1738.3473|
|干净四WPF并发第3次|4013.8295|1686.1591|

全部测次保留，不挑最快。最终干净并发配对在自有Core/WPF全回归、构建完成后顺序执行，用相同最终WPF负载程序集、相同四类工作者/同一shared-fixture/同盘TEMP。早期混入构建或额外Core的测量单列局限，不用于主对照但原始数据不删。

预热中位3298.70→1497.42ms；p99三次0.4876/0.6046/0.5083→0.2638/0.3733/0.2230ms；分配约65.3→44.9MB，逻辑otherOps640000→280000。修后诊断GetAttributes90010/FileExists14/DirectoryExists12，但该分段样本与基线背景不同，仅表工作量减少，不当匹配门槛。详细PERFORMANCE_REPORT.md、PERFORMANCE_RESULTS.json、STAGE_COMPARISON.json及RAW_PERFORMANCE.zip。

**公司未复现家用32秒失败，不能证明并发是家用唯一原因，也不称家用根因完全关闭。** 本次确认一个真实重复工作瓶颈和公司可测改善。下一步在家里同修复包、同盘TEMP、同负载保留全部测次；若再失败，采集关联ETW/磁盘与锁事件，不放宽阈值。

## 修复正确性与最终发布组件回归

新增测试：多库交替LibraryId/DatabasePath、查询排序/筛选、连接释放、writer lease重新获取；database/assets/managed/previews/backups/locks合成symlink替换拒绝与还原；缺可选路径、破损manifest恢复。29/29专项通过，原P35/容器/包/范围排序均保留。

最终完整包Company-SwitchFix-Diagnostic-win-x64，产品源码0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f。发布DLL原样复制到测试宿主并逐hash确认。完整原始成绩：

|最终回归|发现|通过|失败|跳过|漏项|
|---|---:|---:|---:|---:|---:|
|完整Core|1717|1712|0|5|0|
|完整WPF|1715|1693|1|21|0|
|发布模块|14|14|0|0|0|
|容器正确性专项|29|29|0|0|0|
|授权补验两WPF类|20|20|0|0|0|

最终WPF一失败和额外一跳过由本次会话重置遗失两项授权manifest环境变量引起，原运行保留：R3明确拒绝无授权manifest，不扫描；相片页opt-in跳过。补回此前明确授权两照片manifest、全新输出目录，同最终DLL两类20/20通过。该补验不重复加进完整1715，不抹原失败或改产品。其余20WPF/5Core opt-in/外部条件保持。不是全绿发行结论。

新完整诊断包290应用文件/264依赖缺失0，有统一隔离CMD启动入口，UserData不入ZIP。未声称已完成公司物理桌面、Windows/WPS、相机、安装升级卸载/重装；本任务只CLI及组件验证，不以离屏图冒充。策划外链/普通附件/SQLite跨机便携、15区一体汇总导出、设备目录、复杂WPS仍保留原真实PARTIAL/NOT_RUN/BLOCKED。

## 保留、阶段与推送

旧树状态/HEAD/所有未跟踪记录已审计，6个脏工作树共404文件内容SHA逐个复验不变；旧Release保留，未提交私人照片/RAW/数据库/凭据/发布二进制。诊断镜像DLL不进发布，原始性能归档只收JSON/CSV/TRX/log。

阶段：3a55029e公司同步/构建；cebbeace性能定位；0e5c3ea7最小修复/安全回归；最终证据单独提交。改动类型/构建命令/EXE-WPF-Core hash/完整文件清单/ZIP hash/LOCAL_HEAD=REMOTE_HEAD见同目录记录。正常push本公司续接分支，不合main，不强推。发行门禁继续BLOCKED。

## 推送读回

最终证据提交de7ce22d7036f13d1a0633a800d82387a07ca7f3已正常推送，独立ls-remote本地远端一致。此后只封存读回和SDK派生文本行尾规范，不改变产品源码或发布DLL。工作树最终状态另见封存读回。
