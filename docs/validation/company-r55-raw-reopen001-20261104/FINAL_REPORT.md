# RAW-REOPEN-001：默认富士 RAW 跨进程差异的最小隔离定位

2026-11-04，**Asia/Shanghai**。本轮范围只含 RAW-REOPEN-001。状态：**REPRODUCED_NATIVE_BOUNDARY / ROOT_CAUSE_OPEN / NOT_FIXED**；产品仍 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。

## 结论先行

**首个已观测不同阶段是 MakeDcrawMemoryImage 返回的 native RGB16，发生在托管复制之前。** 两个不同进程、每进程一次默认解码、串行运行，无 App/预览/保存/重开/缩略图参与，仍有 14,275 个通道样本不一致，最大差 5,600/65,535。

因此，本次差异不由托管复制、float转换或方向归一引入；原App异步/会话处理也不是触发差异的必要条件。**尚未证明 native 内部具体根因**：Unpack、DcrawProcess内部以及MakeDcrawMemoryImage内部未进一步拆阶段，不能直接称为已确定的X-Trans线程竞态/未初始化内存。未修改产品、未宣称修复。

## 1. 接续与身份

- fetch成功；审查分支 docs/raw-default-chain-closure-20261104 仍为 e2437d2300b37872d5b66482a721e3b6463f1ce4；产品基线 f27f440909fe19ddd5665b167b912c9db9d897d4，产品源码 **38dd3f92bb2db7238f7f058008887929ec971c93**。审查测试/文档HEAD不是产品源码变化。
- 新独立分支 **diagnostic/raw-reopen001-20261104**，工作树 E:/Kitao-Worktrees/raw-reopen001-diagnosis-20261104，从审查HEAD开始；不改旧树、不合研究分支、不reset/stash/clean。61个既有工作树现场单独留存。
- 已读 AGENTS/POLICY、原FINAL_REPORT/NONPASS、RawColorChainReviewTests、pass4原FAIL及指定输出。原富士三个TIFF输出及原件SHA复核一致；原测试未编辑。
- 唯一输入 company-02 / DSCF0347.RAF / Fujifilm X-T5；固定 SHA256 **52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865**。Canon 0差仅作历史背景，不重新解码、未扩展素材集合；Sony/gamma/肤色/高光均不处理。

## 2. 先审调用链，再补最小观测

详见 CALLCHAIN_REVIEW.md。默认源中每次新建 RawContext，包装层每次 Initialize(None)/OpenFileW；Unpack后从当前native读取并提交原有OutputParams；RGB16在processed释放前按有效样本数ToArray，processed先清理、context后Recycle/Close。默认RAW导入已有“先缩略图、再激活”的串行复用保护；session staging移交同一组目标，不二次重建master。没有发现可直接批准的初始化、复制或生命周期最小修复。

旧夹具读取正确default identity TIFF、当前master、冻结栈/变体；await真实入口、Apply与导出命令。它没有全后台task barrier，不能写全部异步活动已静止；但新无App隔离解码仍native不同，排除了“只是测试未等缩略图”作为完整解释。

原pass4缺nativeRGB16阶段hash和逐次有效参数，所以新增 **RAW-REOPEN-001-StageProbeV1.3**：从冻结LibRawDecoder.cs精确生成，改变namespace/类名用于隔离编译，仅插入读取、哈希、保存与计数；全部原生产语句/参数赋值保持。Verify-Generated.mjs对源SHA、唯一锚点、生成文本和生成SHA核验通过。工具只引用已发布Core/依赖DLL，不重建产品。

## 3. 两个真正完成默认解码的进程

| 项 | first | second |
|---|---|---|
| PID | 35784 | 32688 |
| 时间（Asia/Shanghai） | 13:54:15.936—13:54:30.754 | 13:54:30.769—13:54:45.628 |
| OpenFile/Unpack/DcrawProcess/MakeDcrawMemoryImage | 各1次 | 各1次 |
| 调用退出 | 0；观测完成 | 0；观测完成 |
| OS峰值工作集 | 949,080,064 bytes | 948,678,656 bytes |
| 私有内存峰值（50ms采样） | 921,321,472 bytes | 921,042,944 bytes |

“观测完成”不是像素一致性PASS。**两进程一致性FAIL已复现。** 没有自动循环或第三次完整RAW解码。master 482,236,416 bytes，小于既有536,870,912预算；诊断器外部2GiB上限未触发，native MaxRawMemoryMb保持2048。完整四阶段二进制各自保留本地，总计2,893,418,496 bytes，非持久产品缓存，也不进Git。

实际环境：Windows10.0.19045/x64、.NET10.0.10、12逻辑处理器。**继承 OMP_NUM_THREADS=6**，两次相同，本轮没有设/清/改它，也没有强行单线程；实际native worker数量和调度未观测。进程串行不等于native单线程。

## 4. 首个分歧点与传递关系

| 阶段 | 两进程关系 | 每个进程内部核验 |
|---|---|---|
| native返回RGB16 | **14,275通道不同**；max5600，mean0.0512105083 U16；均内部像素，无16px边界差异 | 复制前/后native hash一致 |
| 托管复制RGB16 | 与native完全相同的14,275通道差异 | 两次各自 native==managed，native释放后返回数组hash仍一致 |
| float master | 同样14,275通道不同；max0.0854505263 | 每通道都严格等于各自ushort/65535f；uint16 roundtrip全0误差 |
| 方向归一master | 与各自float master hash相同 | orientation=1，归一为no-op/同对象 |

最大差异仍在 **(7581,3980,B)**：first=6193、second=593，与原pass4最大差异点和值吻合但方向相反；原pass4有16,344通道不同，本轮14,275，不伪称整幅输出与旧run完全相同。

Native SHA256：
- first: 72879A9A8DBB2F914AC3CBA8B7B379CB31AA39554DCE12298462791EFC34C4BD
- second: 9729A70A4CB29D9CB16A7AEABD630D37A2BEAED63F914C8FF6B13B6DA9F422E4

布局均7752×5184×3×16-bit，stride46512，DataSize与有效字节均241118208；完整stage哈希/数值比较见 STAGE_COMPARISON 收据。本次不再运行原三图或App整链。

## 5. 实测参数，不作哈希推断

- 每个实际context均保存 open/unpack/process后全部已展开output/nativeOutput/unpack/color字段；configure回调的全部OutputParams在wrapper Commit前保存；两个进程逐字段比较输出参数、unpack参数与颜色字段均0差。
- 默认cameraWB=true、autoWB=false、cameraMatrix=true、sRGB/16bit、HalfSize=false、Interpolation=true、UserFlip=0；继承Gamma=[.45,4.5]、NoAutoBright=false、UserQual=-1等原值，不调整它们。
- unpack实际UseRawSpeed=1、UseDngSdk=39、Options=2、ShotSelect=0、MaxRawMemoryMb=2048等一致；不是专业RAW适配器的另一套初始化。
- 所记录三阶段快照唯一差异是image.XmpDataPtr地址；记录指针/长度但不复制XMP span内容。它不是曝光/WB/unpack参数差异；也不等于全部native内部隐藏状态已观测相同。
- 原pass4未采集相同级别参数，本轮不能事后补证它与新run所有参数相同。

## 6. 没有隐藏观测器失败或多算解码

完整保留3个早期诊断启动失败：PID18196重复DLL resolver、PID30252诊断目录缺原native依赖、PID23144反射XmpData Span getter不支持。前两次Open/Unpack/Process均0；第三次只OpenFile1次，Unpack/Process0。

因此本轮总计 **OpenFile=3、Unpack=2、DcrawProcess=2、MakeDcrawMemoryImage=2**；真正完整解码进程为上表两个。另有一次零RAW序列化自检暴露IntPtr顺序问题，修正后零RAW自检通过。早期构建错误/无产物调用/原失败目录都保留，未改写为产品失败或删除证据。V1.3自检/编译通过后先冻结软件与依赖manifest，再执行两次完整解码。

## 7. 具体根因状态与最小下一步（未执行）

**已证明**：同一授权输入、相同已记录配置、固定二进制、串行独立进程，在nativeRGB16边界产生稀疏差异；托管复制、float和orientation不制造差异；App状态/后台任务不是必要条件。

**尚缺**：Unpack输出原始有效区域/布局的hash、DcrawProcess返回后native image有效区域/hash、MakeDcrawMemoryImage前后的更细粒度映射边界，以及实际native worker/隐藏状态证据。当前观测无法在这些内部环节之间区分，更不能证明具体C++语句或线程竞态。

最小后续研究提案：仍只该SHA，固定同一binary/环境，在独立版本观测中增加上述两个native内部检查点及布局/有效长度；不改变算法、线程策略和参数，不自动扩样/循环。任何native算法、decoder替换、线程策略或参数修复，先取得POLICY要求的 **LibRaw + RawTherapee固定commit/入口/许可/可复现数值比较**。本轮仅取得包装层固定commit静态参考，成熟算法对照 **NOT_RUN**，不等于批准实施。

没有证据支持本轮修改产品调用链；没有使用持久缓存、放宽断言、降质量、单线程或换decoder。**未修复，因此修复后预览/保存重开/导出验证NOT_RUN**，原pass4 FAIL保持。未来确有获批修复后，必须沿原失败路径精确验证，不能用肉眼相似替代。

## 8. 源码、测试/观测与包

- 产品源码仍38dd3f92bb2db7238f7f058008887929ec971c93，src/prototypes对基线零差异。新增仅诊断工具、证据和HANDOFF/大纲。
- 观测工具源码提交 **aa7cc94b8559ea7d2fa6e9c782b31b0ab16b7966**；不是产品源码变更。
- V1.3观测DLL SHA256 **D0DE344357BCCA06006287B430C8926A2D40319BB2BF9E8334DFA32F65A0B822**；native raw_r.dll SHA256 **F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9**。执行前冻结host/Core/runtime/native依赖，运行时核验实际加载路径/hash。
- 7个托管依赖与发布件字节一致；native raw_r/lcms2/jpeg8/zlib1及系统MSVC/OpenMP依赖固定身份。没有加入候选decoder。
- 原完整包 E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64.zip；SHA256 **357D0E9D30BA30CC9C157FE40D66371F6DE7A978E81E8730497286CBA3AFA1A2**。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全部模块、策划15项和教程退役保持。无产品改动，**不重建完整诊断包**。
- 公司产品入口 E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd；本轮未启动产品来重跑其他任务。新probe是诊断工具，不是用户产品启动入口。
- 执行根 E:/Guangjing-RawReopen001-20261104/pair-v1_3；原失败和全部stage.bin在本地保留。Git只收源码和参数/哈希/数值收据，不上传RAW/TIFF/PNG、像素二进制、数据库、vendor源或发布二进制。
- 最终LOCAL_HEAD/REMOTE_HEAD及工作树状态见推送后独立收据 E:/Guangjing-RawReopen001-20261104/PUSH_READBACK.json；审查分支与产品分支不被本诊断分支覆盖。
