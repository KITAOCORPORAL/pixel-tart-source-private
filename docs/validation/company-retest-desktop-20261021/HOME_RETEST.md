# 家用端 Scenario11 同条件复测说明（待家用机器执行）

本文件是复测方案，不是家用新成绩。公司优化有效，但家用旧失败 **32139.0909 ms > 30000 ms** 未解释。报告日期2026-10-21，Europe/Berlin；原始主机UTC/本地时间原样保留。

## 1. 先验身份，保留旧树和旧包

- fetch KITAOCORPORAL/pixel-tart-source-private，独立 git ls-remote 读公司续接分支，不以旧本地ref代替。
- 修复产品必须为 **0e5c3ea7e25c0c3d1773480cdc351ca9dc94b80f**。诊断/文档HEAD可以更晚，但先读新增提交，并验证 git diff 0e5c3ea7..HEAD -- src tests；如有产品/测试差异，另行标记，不混作同源码。
- 用家用独立工作树，从冻结点或已确认等价树构建完整诊断版，WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false、win-x64自包含。用该机器SDK/runtime记录，哈希不必与公司相同；逐项记录EXE、主WPF/Core、测试程序集、依赖与发布DLL绑定。
- 不reset/stash/clean/rebase/合main或覆盖任何旧工作树、合成失败目录、ZIP、Release。所有输出用新名字；先验证绝对路径属于指定实验根。

## 2. 固定实验条件，不把变快一次当闭合

- 原测试完全保留：AssetLibraryP35AcceptanceTests.Scenario11_TenKSwitchAndHundredKMetadataPreview。
- **10000次交替OpenAsync，30000ms门槛**。测试内部原两库创建在计时前；100000条元数据去重在stopwatch.Stop之后，期望90000不变。元数据时间另列，不移入或移出计时来刷绿。不是P3查询400ms门。
- TEMP与TMP设为家里原失败使用的同一物理卷（据原TRX/summary中的temporaryDirectory确认，不能仅凭盘符名猜）下的**全新**实验子目录。记录NTFS/容量、卷到物理磁盘映射、是否网络/重解析路径、SSD/HDD与空闲容量。公司E盘路径只作例子，不能直接映射家里N盘条件。
- 两个库使用同一合成布局、空库schema和中文名称，不导入私人照片或数据库。对照probe使用同一个shared-fixture路径/LibraryId/字节布局；跨进程先清本进程validated cache，warmup仅在声明的预热条件执行。
- 记录电源计划、CPU/内存、后台进程总览（脱敏）、防护/同步程序是否活动，但**不要为了测试关闭安全软件、改系统策略或全盘清缓存**。不宣称“cold”代表物理磁盘冷缓存；公司cold仅新进程+空容器验证缓存。

## 3. 执行顺序与日志

1. 所有自有构建和完整回归停止后，独立进程精确单跑原Scenario11，保留TRX/stdout/stderr、PID/起止UTC、TEMP、全部组件hash、原门槛与实际值。
2. 同发布Core使用tools/CompanySwitchProbe，预热A/B后同进程完整循环**三次**，保存三个round JSON与各10000行calls CSV；不得只选最好一次。
3. 固定四进程WPF负载，使用tools/company-switch-performance/run_concurrent.py：RuntimeCorrectionWpfTests、ReferenceWorkspaceWideRatioTests、PlanningBatch3BusinessTests、PhotoGradePageV1Tests。修前/后都用相同负载主机、相同并发数/合成数据/TEMP；其他自有Core/构建必须停止。保留每负载进程PID、类、轮次、起止时间、TRX/退出码；测量与负载真实重叠需校验。此负载方案不是家里旧“完整四片+Core”完全相同的重演，若重演旧方案则另列一组。
4. 用公司测试相同定义记录公司/家用发布组件身份；性能probe真正加载的Core必须与本机发布Core SHA一致，不能无声明混用bin里的源码DLL。普通发布测量和插桩副本测量分开。
5. 保存总时长、p50/p95/p99/p999/max、CPU、逻辑I/O操作/字节、分配与GC、元数据去重耗时。分段/系统调用计数只在诊断镜像另跑，不能把观察器开销当产品门槛成绩。每次原始测量均保留，即使通过或不符合预期。

## 4. 再次超时才开启有关联的深挖（保留先前失败）

- 不反复刷绿。如果任何完整10000次结果>=30000ms，先冻结该原TRX/失败消息/配置/组件hash/fixture身份及负载时间线；该失败不被随后通过抵消。
- 在独立新的诊断复跑中，经用户确认桌面/数据隐私边界后采集WPR。先 wpr -status；如已有录制，**不要stop/cancel或接管别人会话**。用本机 wpr -profiles / -help start 核对支持的GeneralProfile、CPU、DiskIO、FileIO、DotNET配置；可用时记录命令，例如 wpr -start GeneralProfile -start DiskIO -start FileIO -filemode -recordtempto <新跟踪目录>，然后执行一次原测试，wpr -stop <新目录>/scenario11.etl。不要全局持久启用provider或修改安全设置。
- 用目标testhost/probe实际PID、进程生命周期与UTC时间窗在WPA中关联CPU调度/ready time、FileIO路径、DiskIO发出/完成/队列和调用栈。仅磁盘总体计数不能归因每次OpenAsync；逻辑otherOps不等于磁盘读写，wall-CPU不等于磁盘等待。
- 锁证据区分：热缓存OpenAsync不获取writer lease，只有冷打开才有SQLite连接/quick_check。记录调用cacheHit、冷连接/命令起止及SQLITE_BUSY/LOCKED错误；必要时在**独立诊断构建**记录实际busy handler重试与等待/线程栈。CLR contention只证明托管Monitor等待，不能自动当SQLite文件锁。保留锁拥有者/连接生命周期与目标PID，不靠静态方法存在推断等待为0。
- ETL可能含绝对路径/进程信息，**仅留本地，不进Git，不上传原始私人路径或凭据**。提交脱敏JSON/CSV与分析结论、原始文件SHA及保留位置；采样丢失/权限失败要写明，不能写0或PASS。

## 5. 正确性和结论门禁

复测原P35/容器/包/排序范围测试及AssetLibrarySwitchPathRegressionTests：库身份、交替后排序/筛选、连接和writer lease释放、中间路径变symlink时拒绝/还原、缺可选路径与破损manifest恢复。不得禁用边界检查换速度。

报告分三类：测试通过；实体桌面已验证；仍阻断发行。若家里没有实体桌面/WPS/设备条件则NOT_RUN/BLOCKED。诊断版不标正式发行。原32秒成因在完成家用同条件重复及关联证据前仍未关闭。

辅助单跑脚本：tools/company-retest-desktop/Invoke-HomeScenario11.ps1（需要PowerShell 7）。全部路径显式传入；只执行一次原测试，不自动重试；它验证源码与发布Core绑定，但同物理盘的选择仍需人工依原失败日志确认。家用执行尚未发生。

单跑命令示例（PowerShell 7，路径须换成家用实际绝对路径；只运行一次）：

```powershell
& ./tools/company-retest-desktop/Invoke-HomeScenario11.ps1 `
 -SourceRoot <家用独立工作树绝对路径> `
 -Dotnet <本次SDK的dotnet.exe绝对路径> `
 -TestAssembly <已绑定发布Core的Tests.dll绝对路径> `
 -PublishedApp <本机完整诊断包app绝对路径> `
 -SameDiskTempParent <与原失败同物理卷的新TEMP父目录> `
 -OutputParent <新的证据父目录>
```

包装器180秒是进程保全上限，不是放宽测试30000ms断言。若挂起到包装器超时，会保留wrapperTimedOut/日志并结束仅此子树；该结果记BLOCKED/FAIL，不记PASS。
