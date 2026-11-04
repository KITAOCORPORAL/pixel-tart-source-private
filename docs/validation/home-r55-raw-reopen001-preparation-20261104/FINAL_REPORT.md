# RAW-REOPEN-001 家庭接续：隔离原生观测静态准备

2026-11-05 00:18:59 +08:00，**Asia/Shanghai**。状态：**PREPARATION_PARTIAL / NATIVE_TOOLCHAIN_UNAVAILABLE / ROOT_CAUSE_OPEN / NOT_FIXED**。

## 已完成与停止位置

- fetch确认原诊断远端基线c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39，新独立分支diagnostic/raw-reopen001-home-20261104。不reset/clean/stash，不覆盖旧树/失败/两轮输出；产品源码仍38dd3f92bb2db7238f7f058008887929ec971c93，src/prototypes相对其零差异。
- 已读AGENTS/POLICY、最新company native FINAL_REPORT、LAYOUT_REVIEW、REMAINING_AND_NEXT_PROPOSAL及研究线锁/报告/依赖/计划。复用研究线0e0723df0c8eaf665d2bccdfe150c45621534d3b，不合入或改写研究分支。
- 公司研究根在家庭不存在，仅检查明确旧路径是否存在，不扫描其他盘/冒称访问公司产物。按研究SOURCE_LOCK建立唯一家庭共用源码根raw-reopen001-shared-research/libraw-0.21.1（产品仓库外）。固定cccb97647fcee56801fa68231fa8a38aa8b52ef7，18个已审Git blob哈希全部通过。生成树只是构建导出，不是第二套研究。
- 家庭原NuGet raw_r/lcms2/jpeg8/zlib1及binding哈希等于公司收据，仅静态读文件/PE、不加载。原raw_r SHA F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9；binding SHA 6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3。runtime nuspec仍无native源码commit，编译flags/补丁未知；上游同版本新构建不冒称等价。
- 已准备原创观测header、可逆插入生成器、隔离构建脚本、仅无RAW的ABI宿主，详见STAGE_REVIEW和工具README。无产品算法或默认改动。
- 生成器唯一锚点/原字节恢复/合成有效域排除检查通过；三版生成源码都保留本地，最终isolated-source-03身份见PREPARATION.json。JS检查不等于C++执行。
- 宿主首次编译因错误枚举名失败，改为现有工具使用的正常LibRawNative.Initialize()后三次成功，最终0警告0错误。失败/中间成功日志全留。**只是宿主编译成功，未加载native，未运行ABI/原生自检。**
- 原生预检一次：cl/nmake/dumpbin未发现；PATH中cmake/ninja/GCC及常见VS/LLVM/MinGW目录未找到可用工具链。已有.NET SDK内MSBuild可编译宿主，不能替代MSVC C++。未安装工具链、未发起原生编译或换编译器掩盖。

## 家庭文件、环境与实际调用数

仅核对既有授权manifest定位的家庭文件，SHA **52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865**完全匹配；没有换照片/扫描其他盘。

Windows10.0.26200/win-x64，28逻辑CPU，.NET SDK10.0.401/host10.0.12；公司前轮10.0.19045/12逻辑CPU/.NET10.0.10。**不是公司同环境复跑。** 启动OMP/KMP清单null，绑定未初始化，后置有效环境NOT_CAPTURED；不猜OMP/worker，不设置或清除环境变量。

|操作|实际次数/结果|
|---|---|
|Open / Unpack / DcrawProcess / MemoryImage|**0 / 0 / 0 / 0**|
|完整默认显影/原生产DLL对照|**0 / 0**|
|native init / ABI / native synthetic self-test|0 / NOT_RUN / NOT_RUN|
|原生构建预检/实际编译|1 / 0，缺工具即停|
|托管宿主构建|1失败、3成功，日志保留|
|生成器合成检查|3个版本输出均保留，最终通过|

RAW身份满足，但新构建身份/原生ABI/无RAW自检未满足，因此未消耗最多两次完整开发额度。无自动重试/旧DLL对照/解码进程。宿主没有decode模式，不能删旧V2.1的ABI/hash限制后换库开跑。

## 首差结论不变

本轮无新运行，不能报告新构建相同/不同或更细首差。沿用已证实边界：**有效Unpack相同，DcrawProcess返回后的有效RGB已不同**（公司5316通道、max146 U16）。不能再笼统写保存丢参数或MemoryImage单独引入。raw2image/黑位、scale/pre_interpolate、实际插值、convert_to_rgb内部首差及竞态/未初始化根因仍未知。

## 剩余具体工作

1. 提供可用x64 MSVC C++/Windows SDK；新输出根实际编译，核对flags/宏/可选组件/PE/依赖/DLLhash。当前C++补丁尚未编译验证，不能预设成功。
2. 新库自报offsetof/sizeof对固定binding、原生合成hash及正常init/recycle/close自检；失败留存停止。研究线后续复核本次新增阶段设计，静态自审不当算法准入。
3. 接入新的两槽bounded RAW host/runner（**尚未实现**），复用原默认配置语句，冻结全部身份/参数及启动、binding后有效环境。旧V2.1不能删检查冒用。
4. 门禁全满足后依当时授权最多串行两个独立进程各一次。相同仅“新构建本次未复现”；不同报告首个有效分歧检查点及布局/branch/quality/passes。差异减少也不是fix。

## 保留与交付

本地仓库外共用根保留固定源码、三份生成树、三次合成检查、宿主全部构建日志/native预检。旧公司私有输出仍在原设备留存，本轮不声称已复制到家庭。Git仅提交原创工具/少量锚点、描述及数值身份收据，不提交vendor源码/二进制/RAW/像素。

无产品修复，不改生产native/默认，不重建应用包，不用缓存/单线程/calloc/换插值/降quality；不扩Sony/gamma/肤色/高光。原FAIL/PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE保持。HANDOFF及R55_IMPLEMENTATION已更新。最终LOCAL_HEAD/REMOTE_HEAD提交推送后另存本地PUSH_READBACK.json，避免自引用SHA。
