# RAW-REOPEN-001：公司内部阶段工具接续交付

2026-11-06，**Asia/Shanghai**。状态：**PARTIAL_TOOLING / REQUIRED_MSVC_UNAVAILABLE / NO_REMAINING_VERIFIED_ALLOWANCE / ROOT_CAUSE_OPEN / NOT_FIXED**。

本轮提交了实际工具实现，完成托管host编译、静态/合成校验与额度拒绝路径验证；**没有取得新的原生内部首差证据**。新观测DLL尚未生成，原生ABI/布局/hash/生命周期自检NOT_RUN，不能把静态合同或托管构建说成工具已具备RAW执行条件。

## 1. fetch、现场和额度

- origin/source-private fetch均成功。家庭准备分支固定 **1cabb0cfcbe2184fb55c066326cbd3c1d1bd23eb**；研究规范固定 **5a4f233dea5ba11622b18380fea137c8f3944e53**。公司旧诊断HEAD为 **c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39**，也是家庭分支merge-base。只读取研究Git对象和既有LibRaw共用源码，不合并研究历史、不搭第二套成熟实现。
- 开始清点62个公司工作树：10树有脏状态，另asset-library-v1有8个相对全部remotes未推提交，均未改/推/重置。公司诊断树干净，内部阶段旧根 E:/Guangjing-RawReopen001-NativeStages-20261104 仅有REFS_BEFORE/WORKTREES_BEFORE，没有DLL/运行收据/consumed slot。
- 管理worktree工具拒绝E盘sourceRoot（不在聊天根列表）；使用Git创建独立树/分支 **diagnostic/raw-reopen001-native-20261106**，从指定家庭HEAD开始。原树/脏文件/失败输出保留；不reset/stash/clean。
- 实际公司收据中，V1.3有两次完成解码；V2.1也有两次，PID **15284/4908**，Open/Unpack/Process/MemoryImage均各1次。本轮不把家庭0次当公司0次。
- **当前指令没有新增两次授权。** 家庭文档把“新库计划本地0次”与旧V1/V2分开记录，但这本身不是另发额度的依据。按本轮“原任务最多两个独立进程、换机不重置”的约束，runner保守绑定已有V2.1两条原收据：**已消费2，可确认剩余0**。新内部库本地执行0次另列，不冒称它已运行2次，也不从旧V1+V2合计4次中虚构新槽位。
- 最终额度审计对原结果/slot身份做SHA及PID核对、核验指定公司根，保留29条已发现历史收据/marker清单。未知执行、缺失根、漂移收据均拒绝；历史允许清单也绑定SHA。合成测试目录显式标记zeroRaw，不能混成真实执行。

详见receipts/FINAL_TASK_BUDGET_AUDIT、BUDGET_CONFIG_FINAL及COMPANY_EXECUTION_RECEIPTS。runner实测在读native manifest/初始化binding/创建解码进程之前返回 **NO_REMAINING_VERIFIED_TASK_ALLOWANCE**。失败不会退款或自动补跑。

## 2. 实际工具增量

工具目录：`tools/RawNativeInternalProbe`，15个文件，保留旧RawNativeBoundaryProbe V2.1和家庭RawNativeStagePreparation全部字节/检查。

- 原创多文件可逆生成器：固定LibRaw **cccb97647fcee56801fa68231fa8a38aa8b52ef7**，验证研究锁27份LibRaw文件，在7个CPP插入观测；去除插入段必须逐字节恢复原文件。生成代码、上游源码及DLL只留本地，不复制到Git。
- P2移到所有黑位/maximum/green及pre_scalecolors回调之后、no_auto_scale之前。adjust_bl/adjust_maximum/subtract_black实际入口/返回、WB用户/auto/fallback/white8×8/camera/as-shot分支、真实scale_mul和pre_mul浮点bits、阈值/wavelet/aber均有观测落点。
- P1采active CFA及完整初始化四槽；P3b采完整四槽；**P5采全尺寸、含边缘的全部已初始化四槽及CFA/黑位/矩阵/实际分支参数**，不再用稀疏CFA相等代表全部输入状态相等。
- P6采完整有效RGB（包含算法自身border_interpolate）及独立第四槽状态审计；第四槽不标成RGB。P7在后处理/ICC/pre-convert callback后，P8记录实际out_cam有效列（旁路明确不读取未消费列），P9采RGB/第四槽及已初始化histogram，P10只记录最终callback/stretch/restore小状态。
- 实际分支在原dispatcher每个选中调用处记录；当前安全观测合同只接纳实际XTrans分支。记录quality/passes、compile truth、原始current/saved状态、allocation extent/指针代数/CFA/尺寸/原始pitch。未知pointer replacement或shrink/Fuji变换布局立即INVALID，不猜偏移。
- 增加XTrans serial green-bound结束的完整四槽、已定义allhex、cielab已初始化lookup/matrix摘要；实际team/worker/top-left/mrow-mcol/scratch extent使用预分配、worker独占有界记录，join后输出，不在并行region内做文件/hash/共享日志锁。
- 新RAW host复用原默认赋值，独立验证新库自报sizeof/offsetof、实际加载路径/hash、固定binding及前后环境；不删除旧V2.1硬编码生产DLL/ABI限制来换库。
- task-wide ledger的排他事务先持久化consumed再spawn；失败/启动失败均消费，不自动重试。runner串行、每host每API最多1次、2GiB/600秒/50ms采样。新manifest只能在匹配native自检与原额度都成立后封存。
- 离线比较器按P1/P3b/P5/P6/P7/P9逐同阶段校验布局/长度/hash和变化坐标/幅度；另比typed state，排除作为诊断身份的地址/sequence。六个payload相同不能掩盖不同的scale/matrix状态。新构建相同只写“本次未复现/NOT_FIXED”。

**限制仍明确：** source-preserving insertion不是MSVC编译/运行证明；P5全四槽仅覆盖已定义输入及控制状态，不读取未初始化scratch、不证明每个实际scratch读取安全；没有instrument具体LIM load时序，不声称H1已证实。hash/I/O、额外分配及worker记录会扰动堆与调度。没有改原算法公式/初始化/线程数/质量/default，也没有锁tile、清零scratch、缓存输入或替换生产DLL。

## 研究线并发审查及修正

收尾盘点发现新研究工作树，已提交审查 **71ba977ac2d2ca1b039a59fb3a4b2e3f28882a4f**；只读获取其固定DECISION/CONTRACT_AMENDMENT/PRODUCT_COMMIT_BINDING，不改变研究树、不发消息或代研究批准。它对60cf0b5e补丁提出F1/F2/F3，早期“静态合同完整”收据因此不能当最终版。

- **F1已修正**：P1不再把尚未原赋值的局部quality复制到观测状态，而是记录-1/UNAVAILABLE；原声明和初始化位置/公式完全保留。此前确有观测器写前读错误，未执行该原生代码；这不是产品根因修复。
- **F2已修正**：比较器校验各检查点必需typed state、唯一性、顺序、CFA/分支/passes、完整布局、真实WB/scale_mul、有效矩阵/表、实际已加载库及source/input/selftest/host/run身份；缺失或两边同缺都拒绝。分别输出imageEqual/requiredStateComplete/stateEqual/effectiveInputEqual；已有有效状态差异检测保持。
- **F3已修正**：首差顺序在P5/P6之间包含XTRANS-serial-ready四槽及已初始化allhex/CIELab表；不能忽略更早的串行差异而归因并行tile。仍无实际consumer load证据，H1保持UNKNOWN。

修正版本新增缺失/重复/错误分支、WB/CFA/scale/matrix/表、运行身份、P1提前读和serial顺序测试，最终38项静态/合成通过；没有RAW/native执行。修正版仅本工具自核，**研究线对修正提交的再次审查NOT_RUN**，不得声称外部复审通过。原冻结目录、审查前收据和编译失败全部保留。收尾共有64树，开始时62棵旧树的HEAD/porcelain/未推列表均一致；新增的另一棵是独立研究审查树，不归本任务修改。当前权威源码见SOURCE_IDENTITY及OBSERVER_FREEZE_DELIVERY。
## 3. 工具链与验证

已检查PATH、vswhere位置、VS SxS及Windows Kits两个注册表视图、常见C/D/E BuildTools/VS/SDK位置、D:/Tools和已存在MSBuild目录。**未发现所需 cl.exe / nmake.exe / dumpbin.exe、Windows SDK include/lib、x64 Developer shell**。不是“全盘证明不存在”，不以.NET MSBuild替代C++/SDK。未安装工具或改机器设置。

|检查|本轮实际结果|
|---|---|
|托管host构建|4次成功，最终Release 0 warning/0 error|
|托管默认配置、无native自检|4次；nativeInit/Open/Unpack/Process/MemoryImage全部0|
|最终静态/合成检查|**38项通过**：原字节恢复、锚点拒绝、P2顺序、P5非CFA/第四槽/边缘变化、out_cam、额度/未知收据/缺根/失败保留、比较布局等|
|生成器早期失败|2次因不唯一锚点拒绝，发生在生成输出前；缩小作用域后成功，失败日志保留|
|JS/PowerShell语法|文件解析通过；不等于生成C++被MSVC编译|
|原生build预检|1次，退出3；在调用cl/nmake前报告具体缺项，**MSVC compiler invocation=0**|
|补充语法探针|既有Zig0.15.2一次-fsyntax-only尝试，FileNotFound失败，无DLL、无重复；不能代替MSVC/native验证|
|新native DLL / PE/依赖|**NOT_BUILT / NOT_MEASURED**；Makefile/flags与依赖计划已冻结，不能编造新DLL hash|
|原生ABI/布局/hash/生命周期自检|**NOT_RUN**；只有实现代码，没有运行收据|
|bounded runner拒绝路径|确认消费2/剩余0，解码进程启动0|

最终host DLL SHA256：**68E5E49D1217715D9A8A706A9218B03EA09F210640AC1969A2685FDFDDB2D389**。它不是产品DLL，也不是新的LibRaw DLL。

冻结生成输入/补丁身份以PREPARATION_R2、GENERATED_INPUT_FILES_R2、STATIC_CONTRACT_R2、OBSERVER_FREEZE_DELIVERY为准。此前同名无R2/DELIVERY后缀收据保留为审查前快照，不能继续用于执行。计划编译使用固定上游Makefile的DLL target，原COPT保持、附COPT_OPT=/openmp；没有偷偷启用RawSpeed/DNGSDK/LCMS/JPEG，BCrypt是观测新增依赖，**不称与生产可选组件/编译flags等价**。真正编译后仍必须保存cl版本/flags/宏、imports/exports、全部实际加载依赖及DLL hash，并完成native自检；当前没有这些实测值。

## 4. 内部首差报告

|本轮内部检查点|新实测|
|---|---|
|P1 raw2image后|NOT_OBSERVED_NO_NEW_RAW|
|P3b scale后|NOT_OBSERVED_NO_NEW_RAW|
|P5实际插值入口完整输入|NOT_OBSERVED_NO_NEW_RAW|
|P6实际插值返回|NOT_OBSERVED_NO_NEW_RAW|
|P7 convert前|NOT_OBSERVED_NO_NEW_RAW|
|P9 convert后|NOT_OBSERVED_NO_NEW_RAW|

不能根据新代码中存在检查点、38项静态/合成通过或host编译，选择一个内部阶段作为首差。沿用既有已证事实：V2.1有效Unpack相同，Process已差5316通道/max146，MemoryImage差5311/max4628；**具体内部根因仍OPEN/NOT_FIXED**。原FAIL、原两进程、启动失败、所有旧目录及参数都保持。

## 5. 调用数、产品和交接

**本轮新完整RAW解码0；OpenFile=0 / Unpack=0 / DcrawProcess=0 / MakeDcrawMemoryImage=0 / nativeInit=0。** 没有新私照像素缓冲、第三次、自动补跑或原DLL额外对照。

产品功能源码仍 **38dd3f92bb2db7238f7f058008887929ec971c93**，src/prototypes对该提交零差；不修改默认RAW/算法/质量/线程，不扩Sony、gamma、肤色、高光，也不变更PV4/5/6。未修复，不运行修复后App预览/保存独立重开/TIFF，不重建应用、不发布新包。原完整诊断应用及全模块/策划15项/教程退役保持。

实现提交 **973856b017555d96764d99db60ffb463be8eebd5**；额度拒绝未知公司证据加固提交 **60cf0b5ec48418d57fb1e8eb8b89bdb5bc19dad2**；研究问题修正bc9fe4843d922e08a5576d6dc0188f151143a9fb，最终观测工具源码 **08bfae4a6e311609c95784e8373bead711d9997c**。HANDOFF/大纲按Asia/Shanghai更新；最终文档提交/远端HEAD在推送后读回，不用工具源码提交冒充最终交付HEAD。

本地结果：**E:/Guangjing-RawReopen001-20261106**。最终生成源frozen-source-v3-r2；托管产物host-reviewfix；额度FINAL_TASK_BUDGET_AUDIT；拒绝执行bounded-runner-denied。上游/生成源、原生/托管产物、synthetic缓冲与完整日志留本地；Git只有原创工具和元数据/摘要。

尚缺两道独立条件：真实x64 MSVC/Windows SDK及原生自检；可确认未消费的原任务额度。现在两者都不满足，不能自动转入RAW。未来有工具链只可继续无RAW编译验证；没有额外明确授权不能因构建成功补发进程额度。
