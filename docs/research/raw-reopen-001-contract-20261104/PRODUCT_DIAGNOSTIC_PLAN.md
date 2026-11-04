# 交产品线统一安排的最小诊断计划（本轮未执行）

目标仅 RAW-REOPEN-001。不是新工具平台、decoder候选或线程调参实验。研究线不另解码同一RAW；以下是在产品线已批准的下一次隔离producer/reopen pair中采集什么，不是授权现在运行或额外循环。

## 0. 保持不变量

- 沿用产品已授权company-02输入、完整源SHA/target/version/stack身份和正常打开/保存/独立进程重开/导出路径；不换照片，不增加三图审查或20MP/性能实验。
- 保持生产native DLL/托管绑定、gamma/profile/曲线、WB、插值、线程策略、资源预算和取消语义；诊断代码/测试包若有变化另记源码与DLLhash，不冒充原pass4。
- 原Fuji FAIL、Canon既有0差和原TRX不覆盖。只在全新隔离根写诊断收据；私人图像/缓冲仅本地，Git只放经允许的身份/数值摘要。
- 不以“本次未复现”就声称已修复；不放宽exact失败断言，不保存像素缓存绕过重新解码。

## 1. 先做低侵入外层观测，不更换native DLL

在**同一次正常DecodeCore**里观测，避免为采集元数据额外Open/Unpack/Process一遍。

|时点|最小内容|确切含义/限制|
|---|---|---|
|native首次调用后 / Open后|实际已加载Sdcb/raw_r或fallback的绝对module path、文件hash/version；native依赖已加载模块路径/hash；进程ID、managed及OS thread ID、context诊断ID|从该进程的真实module/handle取路径，不只hash发布目录。记录外部并发任务数/时间区间，不新增锁或改变调度|
|Open后与Unpack后|RawContext.DecoderInfo.DecoderName/Flags；绑定支持的idata filters/colors、6x6 xtrans矩阵、raw/visible尺寸、margins、frame；完整OutputParams及可访问rawparams|分清selected unpacker与demosaic。无安全已验证绑定的字段记NOT_EXPOSED，不猜offset；读时点标清，别把初始化收据当effective配置|
|生产configure callback末尾|将配置DTO在所有现有设置完成后作值快照（复制数组），不再修改|此时Sdcb尚未执行内部Commit；应命名configuredRequested而不是measuredAfterCommit。禁止在callback里多调用一次Commit来伪造观测|
|DcrawProcess返回后|OutputParams.FromLibRaw(UnsafeGetHandle())、progress/warnings、thread ID|这是post-process状态，不是假称process前的effective snapshot。若需要严格after-Commit/before-process观测，须另行记录诊断绑定改动身份，不能暗换0.21.1.7|
|MakeDcrawMemoryImage后、processed仍存活|type/width/height/channels/bits/DataSize，**仅有效RGB样本**的全域SHA（byte order/stride明确）|借用span只能在owner有效期内读；不要读data_size之外或结构padding。无需再解码或调用process|
|native→managed复制后|managed RGB48有效长度/SHA与前一条相等性|验证真正copy boundary，签名必须对应同一次decode；不把ToArray后内容与另一个buffer混比|
|FromRaw后、NormalizePixels前后|float尺寸/方向/空间标签、完整域hash、finite/range；转换边界和方向归一时点分开|保持内存预算，使用已有有效buffer流式hash，不为诊断再复制40MP数组；新增观察不会成为新像素定义|
|preview/save-reopen/export|保留原source/stack/variant精确断言、preview和TIFF身份，与对应decode诊断ID连接|现有pass4只把差异定位到master重建边界，不能反推原生哪一步先差|

如果加入progress callback，仅记录stage/iteration；保持delegate生命周期至native返回、必要的解除/强引用，不在callback里发起重入处理或释放context。**progress事件本身不证明选中哪种demosaic，也不等于捕获该阶段像素。**原默认入口没有注册自定义插值回调，不能为诊断替换它而把改变了的算法当原行为。

## 2. 严格的最小分流

|成对结果|下一结论/动作|
|---|---|
|源/加载模块/完整参数/生命周期身份不一致|先定位哪个不同及原因；依据实际错配修正最小接线/初始化，不先改算法|
|同次native有效RGB与managed拷贝不同|检查长度、stride、位深、owner生命周期或并发写；这是明确的copy boundary证据，先修该边界|
|native/managed复制相同，但float或方向归一后先差|定位托管转换/输入/ownership或归一；不要归因X-Trans|
|两次native输出已差，外层身份一致|说明分歧在native出图之前/其中；**仍未证明是unpack、scale、demosaic或输出编码**。先补最少必要的原生阶段数据，不自动关线程|
|所有阶段都相同/本次未复现|保留本次结果和原FAIL，不宣布fix，不循环重跑到PASS|

通过标准不是新阈值：同冻结输入/配置的重建仍按产品原精确一致门槛；每次copy前后有效整数样本应字节一致。仅诊断观测正确也不等于产品修复通过。未来真有补丁，要回到preview/save-reopen/export原断言，并保留旧版本和PV4/5/6定义。

## 3. 只有必要时才提出原生阶段instrumentation（不是本轮执行）

要把源码选择规则变成实际事实，至少应捕获：

1. identify后真实CFA filters/colors/xtrans、选中unpacker、内部fuji_width；
2. process中有效quality、no_interpolation、pre_interpolate后CFA/状态、是否命中custom callback、实际分支名/次数；
3. 解包有效mosaic → 插值前已预处理buffer → 插值后image → memory RGB16 的尺寸/域/全有效区域hash；需找首差时再保留受限局部/完整阶段，原文件只本地。

**现有接口限制：**RawContext.DecoderInfo与公共idata可观测一部分；`fuji_width`和内部`quality`/callbacks并非Sdcb已暴露属性，libraw0.21.1的public C ABI也没有完整pre/post-demosaic-pixel hook。C++有get_internal_data_pointer等接口，但这不等于能对未知编译的DLL随意按偏移读取。先确认架构/ABI和实际构建来源；如要用固定源码instrumented库，单独记录新源码/补丁/flags/hash及与原DLL的差别，不能把其结果写成原DLL已经实测。

只有当首差在相关阶段并取得证据后，才讨论写前读检查、内存/边界检查或并发因素。原DLL含VCOMP依赖、RT在特定调用加mutex、两实现tile初始化不同，都不是自动执行“关闭线程/加锁/calloc”的理由。任何改变线程/堆布局的对照也会改变观察条件，需要产品线另立最小假设与次数，而非本轮默认动作。

## 4. 产品线应交回什么

- 对应产品源码/诊断代码/实际加载native及binding身份；两次decode的完整配置、时点和生命周期/线程收据。
- 选中unpacker；可获得的CFA/插值分支证据，缺字段明确NOT_EXPOSED/NOT_CAPTURED。
- 相同decode内native→managed→master边界签名，以及producer/reopen之间哪一级首次不同。
- 既有精确preview/save-reopen/export断言结果和新原始失败，不仅提供汇总PASS。

本研究线收到上述材料后只追这一首差边界，再决定是否存在明确违约/最小修复。**当前没有依据指定更换decoder、禁用OpenMP、调gamma/profile/曲线或调整插值参数。**也不要求跨GPU逐位一致、购买色卡或重启锐化/胶片研究。
