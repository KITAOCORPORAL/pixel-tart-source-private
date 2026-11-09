# RAW-REOPEN-001 — native build, no-RAW self-test and authorized batch

**结论：首个不同的有效内部阶段是P6（X-Trans返回），5127通道/2984像素不同，max815 U16；P1/P3b/P5/serial-ready及必需状态相同。真实native编译、自检和本轮明确授权的两进程已完成；根因仍OPEN/NOT_FIXED。**

2026-11-09, Asia/Shanghai. This delivery continues c156bb83e741e1fd61142431a203d62e62a9e44b and exact R2 native instrumentation 08bfae4a6e311609c95784e8373bead711d9997c. The latest user message explicitly authorizes the separate 20261109 two-process RAW validation batch; the earlier no-RAW-only request remains the scope of the build/self-test subphase, not a denial of the later explicit batch authorization.

## Fixed and observed identities

- Native source: LibRaw cccb97647fcee56801fa68231fa8a38aa8b52ef7; generated seven CPPs and observer header exactly match final R2. No native instrumentation edit was needed to compile.
- MSVC x64 compiler19.44.35229 / toolset14.44.35207; Windows SDK10.0.26100.0. One nmake build attempt,75 translation-unit commands and one DLL link driver command. The standalone cl /Bv inventory emits D8003 for no source; it is not a failed translation-unit build.
- Flags: original upstream /EHsc /MP /MD /I. /DWIN32 /O2 /W0 /nologo plus /openmp and /DLIBRAW_BUILDLIB; DLL /LD. Native warnings are not claimed exhaustively checked because upstream /W0 remains. No RawSpeed/DNGSDK/LCMS/JPEG opt-in. This newly compiled observer is not a bit-reproducible or optional-component-equivalent production binary.
- Native DLL SHA256: **305EEC498644F01EC707E8AC3E58A4B5670043D35FBC853626FF13AC6FF15484**. Host DLL SHA256: **776944C2D4E46B40A0DB2C2383466150EBBC8175CC5EF05B5E73D773EDFEA068**. Actual loaded paths/dependencies are captured from each process and frozen before RAW. Old production raw_r.dll F500C073... and original complete app package remain unchanged.

## No-RAW self-test

One actual native no-RAW self-test process,PID26240, completed with both native synthetic/layout/hash and native file lifecycle exports returning0. ABI reports pointer8/data381256/image0/sizes8/idata192/params5024/rawparams5328/color5384/rawdata193504/parent381248,slot8,processedData16; all checked against the fixed binding.

Directly observed managed invocations: Initialize1 and Dispose1, Dispose returned, subsequent UnsafeGetHandle rejected. Open/Unpack/Process/MemoryImage all0. Individual native Recycle/Close export entries were **not instrumented**: one call to each is inferred from actual pinned binding Dispose IL/source. Close's destructor can invoke internal recycle again, so this is not a claim that total internal recycle count is1. Old literal recycle=1/close=1 receipt fields have been removed in favor of separate measured/inferred evidence.

Host startup now invokes unchanged binding version initialization before explicit exact-module handle lookup, preserving normal OMP initialization order. Startup environment is null, the original binding defaults it to6; observer/runner never set or clear OMP/KMP. Actual MSVC/OpenMP CRT dependencies are byte-identical to the old recorded system DLLs.

## Toolchain installation and disk measurements

No updated company native output or successful self-test was found after fetch; only the preserved toolchain-missing preflight existed. Explicit user installation authorization, elevated Administrator capability and sufficient space allowed the signed Microsoft Build Tools installation to proceed. One install invocation,exit0,no restart. Requested components: Microsoft.VisualStudio.Component.VC.Tools.x86.x64 and Microsoft.VisualStudio.Component.Windows11SDK.26100. Installer-selected dependencies/features are preserved in logs; this is not a claim of a hand-pruned SDK.

Measured directory logical file bytes: E:/BuildTools/VS2022-RR001=1,752,318,799; E:/Windows Kits/10=1,791,023,476; E:package cache22,485,103; C:Visual Studio Installer148,561,904; C:ProgramData VS packages18,046,410. Drive-used changes over the installation window were C:+896,028,672 and E:+5,222,285,312 bytes, with C:51,758,202,880 and E:577,993,515,008 free afterwards. Drive deltas include concurrent workspace/build activity and are not attributed exclusively to installer files; directory measurements are the attributable logical sizes. E:paths were used for the compiler,SDK/shared and cache. Persistent installed components/registrations were user-authorized; Developer shell PATH/INCLUDE/LIB changes were current-process-only.

## New batch accounting

The original exhausted receipt consumed2/remaining0 is hashed and unchanged. The later explicit user authorization is recorded and hashed for **RAW-REOPEN-001-VALIDATION-20261109**,maximum2,independent ledger. Each slot was durably reserved before spawn; no refunds,retries or third decode.

|slot|PID|Asia/Shanghai start/end|Open/Unpack/Process/MemoryImage|OS peak working set|50ms sampled private peak|
|---|---:|---|---|---:|---:|
|first|26928|14:13:08.542—14:13:20.472|1/1/1/1|683532288|659689472|
|second|20016|14:13:20.556—14:13:42.096|1/1/1/1|683347968|659279872|

Both completed exit0. New RAW total is2,each API total2. Native self-test subphase contributed0 of those RAW calls. Both original and new ledgers now have consumed2/remaining0 separately; no further run is authorized by this batch. No app/native production test was added.

Default parameters stayed cameraWB=true,autoWB=false,matrix=true,sRGB16,HalfSize=false,Interpolation=true,UserFlip0,Gamma[.45,4.5],NoAutoBright=false,UserQual=-1. Two configured/opened output snapshots match exactly; complete typed phase state is checked by the final offline comparator. Actual captured XTrans passes3/T512/ndir8,team6,maxthreads6,dynamic0,nested0;176 tile records per process. Compile truth OpenMP1,NOTHREADS0,NO_LCMS1 is explicit. Team/worker scheduling is diagnostic evidence, not hashed as pixel input equivalence.

The runtime manifest freezes host/DLL/dependencies/source/runner/authorization/input identities. The read-only comparator and its tests are separately versioned so review fixes apply offline to this same capture. They are not invoked by the host or runner. Runtime identities were unchanged after both processes. Local26 U16 files total6,751,309,824 bytes; real RAW/pixel files and per-coordinate CSVs remain local. No persistent product pixel cache was introduced.

## Final first-difference result

**首个不同的有效内部阶段为 P6：实际 X-Trans 插值返回后的 RGB。根因仍 OPEN / NOT_FIXED。**

|同阶段、同布局的两次比较|有效图像|必需状态/身份|差异|
|---|---|---|---|
|P1 raw2image后|相同|相同且完整|0|
|P3b scale后|相同|相同且完整|0|
|P5实际插值入口、完整四槽含边缘|相同|相同且完整|0|
|X-Trans serial-ready四槽|相同|相同且完整；allhex/CIELab一致|0|
|P6插值返回、有效RGB|**首次不同**|布局/身份通过，输出审计状态已差|**5127通道 / 2984像素，max815 U16**|
|P7 convert前|不同|通过完整性检查|同一组5127通道/max815|
|P9 convert后|不同|通过完整性检查|同一组5127通道/max815|

首差点(7451,497,R)=164/150；最大差点(2493,510,R)=4769/5584。变化范围x507..7691、y497..4480，R/G/B分别2129/955/2043通道；全域mean0.000369785429 U16。外圈8px和16px变化通道均0。对应stage的完整SHA、状态hash、坐标/数量/幅度见FIRST_DIFFERENCE.json及DIFFERENCE_COORDINATE_SUMMARY；完整逐坐标CSV与像素文件只留本机。

本次P8实测 `rawColor=1`，记录的是原实现的 `not-consumed-raw-color-bypass` 分支，而不是假称实际消费out_cam。比较器现在同时支持“有效矩阵列确实消费”与“明确旁路且未消费”两种实测情况；未更改该原生行为，也不在本轮展开颜色/gamma研究。P6/P7/P9像素hash各自相同，与这个旁路事实相符；P9已初始化histogram有差异是不同图像的结果，不倒推为更早参数变化。

首差结论只在**这份新编译观测DLL**上动态成立。结合固定源中border_interpolate(8)只写外圈8px和本次全部变化在内部，可排除末尾边缘填充作为这些内部变化点的直接写入来源，优先收窄到serial-ready之后的X-Trans tile计算/回写区间；这是源码与实测坐标的推断，不冒充额外的border前运行时检查点。

实际team6、P5与serial-ready完整输入同、P6不同，使研究H1具备继续调查的前提；**尚未记录实际consumer load值/时序，不能宣布H1竞态、scratch未初始化或某条C++语句已证。** 插入日志/哈希/分配会扰动时序，新库与生产编译/可选组件也不等价；原DLL/原失败全部保留。无锁、无清零、无降质量/改参数、无强制单线程、无decoder替换、无持久像素缓存、无放宽断言。

## Independent review and final offline gates

研究sidecar只读核对R2 native源码，F1成立、未发现阻断首次编译的确定问题；另外用纯合成记录发现三处比较器门禁缺口：state/payload几何未绑定、表记录阶段窗口过宽、实际并行证据非必需。主线程运行前已明确区分runtime冻结与离线消费者版本；同一pair落盘后修正这三项，不追加任何RAW。

完整像素定量比较使用已冻结f2aa53ffdabe50efbdf5a8517d6642c7f4f899a8；最终交付比较器为追加严格JSON门禁的8723a4bd。数值循环未变，最终门禁对同一批实际metadata再次核验，无需重复解码；85项门禁测试通过；复用并行64d4dc6的严格唯一JSON解析后最终90项通过，并以实际两进程metadata校验。完整离线比较又实际验证每个文件长度/hash、RGB与audit4槽0/1/2逐样本一致、完整布局/extent、P5→CIELab/allhex→serial-ready→P6及P7→P8→P9窗口、实际passes/T/ndir/OpenMP/team/tile/scratch，并把调度身份与像素语义等价分开。整个离线比较退出0，新增RAW/native调用0。


收尾重新fetch发现并行外部更新：旧诊断树由c156bb83推进64d4dc6，研究树推进0750a014；这不是本任务写入。64d4dc6的JSON重复成员拒绝逻辑及CIELab紧随绑定已复用到最终比较器8723a4bd，新增反例测试后共90项通过；对本批实际metadata的严格唯一解析/布局/时序/并行门禁再次通过，未重编库、未再跑RAW。

研究0750a014对本批数据独立核验插值prefix，也确认完整P5/serial-ready同而P6差5127/max815。其旧完整版比较器64d4dc6在P8要求rawColor0，因此对本批rawColor1明确拒绝；该失败原样保留于concurrent-review/FULL_COMPARISON_REJECTION.json。当前A比较器区分“真正消费out_cam有效列”与“源码已有raw-color旁路且不消费矩阵”的捕获域，后者不等于生产sRGB转换准入。P7/P9数值只是同一捕获域下的有效样本比较，不能包装成生产颜色等价已验收。原像素精确一致断言仍失败，未放宽为PASS。

## Concrete remaining question and next proposal — not executed

下一步只研究已经收窄的X-Trans tile区间：固定当前源/新库及此输入，先由研究端审查实际共享load值的可观测方法，明确多次LIM加载不能靠额外peek或缓存替代。若需要新增检查点，最小是parallel join后、border_interpolate前的完整有效状态；进一步的worker有界实际消费值记录必须说明代码生成/时序扰动。不得为确认H1加锁、清零、OMP1或改质量。本批次两槽已消费，下一方案未执行，不自动创建第三次或另一个新批次。

本轮没有产品功能修复，因此A线不重建完整应用、不执行“修复后”预览/保存重开/TIFF验收。B线在独立产品工作树交付收藏新功能和完整诊断包，身份另列，不能把B功能包当作RAW问题修复包。A线产品功能源码仍38dd3f92bb2db7238f7f058008887929ec971c93，旧全模块/策划15/教程退役保持。

本地完整结果：E:/Guangjing-RawReopen001-NoRaw-20261109（目录名来自本轮最初无RAW编译任务，后续明确授权的真实pair位于validation-batch/pair01）。native-build01保存唯一真实native构建；runtime01保存实际观测host/DLL；no-raw-self-test01为唯一native无RAW自检；comparison-reviewed01为最终离线比较。最终LOCAL_HEAD/REMOTE_HEAD在普通推送后写本地PUSH_READBACK.json并回传，不构造自引用提交SHA。
