# RAW-REOPEN-001 实际观测补丁审查

2026-11-06，Europe/Helsinki。状态：**REVIEW_COMPLETE / PATCH_REQUIRES_CHANGE / ROOT_CAUSE_UNKNOWN / NOT_FIXED**。

本轮从研究基线 `5a4f233dea5ba11622b18380fea137c8f3944e53` 接续，审查实际产品补丁及其生成源码。没有重做 LibRaw/RawTherapee 综述，没有运行 RAW、native 库或参考解码器，也没有修改产品补丁、产品 src、像素算法、默认参数或封存的 V1 锐化。

## 审查对象和身份

产品树：`E:/Kitao-Worktrees/raw-reopen001-native-20261106`。取证时 HEAD 为 `1cabb0cfcbe2184fb55c066326cbd3c1d1bd23eb`，`tools/RawNativeInternalProbe/` 尚未跟踪；因此 HEAD 本身不包含被审查补丁。完整文件 SHA256、大小和本地只读快照位置见 INPUT_MANIFEST.json，不能以产品 HEAD 代替补丁身份。

本轮最终采用产品的 `E:/Guangjing-RawReopen001-20261106/frozen-source-v3`，对应固定 LibRaw `cccb97647fcee56801fa68231fa8a38aa8b52ef7`，绑定原研究检查点。27 份 LibRaw 锁文件及 7 份生成源码均已按字节核验。观察 header SHA256 为 `41321ac0e0fb40b8117542dfcc1d4622179129127376a6d8fbb8bc4fdf6a1539`；生成收据完整 hash 见 DECISION.json。

产品目录在取证期间有并发更新。首份快照已在 Git 外保留并被明确取代，未将旧 sealed 源码与较新 header 混为同一版本；CAPTURE_HISTORY.json 记录这一边界。结论只约束 INPUT_MANIFEST 绑定的快照，后续产品提交须重新核对相同 blob。

最终读回时，产品补丁已提交到 `60cf0b5ec48418d57fb1e8eb8b89bdb5bc19dad2`。PRODUCT_COMMIT_BINDING.json 逐个绑定该提交的 generator、header、comparator 和 contract verifier Git blob：与审查快照的逻辑内容完全相同，差异仅为行尾/文件尾空行；F1/F2/F3 对该提交仍适用。预算审计器的独立改动不纳入本次色彩合同审查。最终产品脏目录及文件漂移保留于 LIVE_READBACK.json，不靠 HEAD 名称推断补丁内容。

## 逐项裁定

“满足”限于下表注明的静态合同，不表示实际运行已完成。

| 核验项 | 裁定 | 具体证据与边界 |
|---|---|---|
| P2 落点 | **满足，静态** | Prepare-Internal.mjs 的 no_auto_scale 锚点；最终生成 dcraw_process.cpp:142。位于 pre_scalecolors_cb 返回后、scale 分支之前，包含 adjust_black/maximum、可选前处理和该回调后的当前状态。|
| P2 仅小状态 | **满足，最低捕获合同** | 原 CHECKPOINTS.md:36 / CHECKPOINT_SPEC.smallState 本就允许 P2 只记录状态。缺全图不能据此判不满足；也不能用状态相同推出该阶段像素相同。|
| P5 实际调用及全图输入 | **满足，受支持布局的静态覆盖** | 最终 dcraw_process.cpp:206–207 包住实际选中的 xtrans 调用；捕获四槽 image4，含全部边界及非 CFA 槽，不再只是稀疏 CFA hash。|
| P5 完整输入等价判定 | **不满足，自动判定合同** | F2：缺失状态、CFA/分支/有效矩阵记录时仍可能得到 stateEqual=true。四槽像素相同只完成输入合同的一部分。F1 还使整份观测库需要修订。|
| P6 有效 RGB | **满足，colors=3 X-Trans 静态覆盖** | 最终 dcraw_process.cpp:208 在实际返回后、mix_green/post callback/median 之前。原 xtrans 函数返回前已结束并行循环并执行 border_interpolate(8)，因此这里的全域槽0/1/2包含该边界处理。第四槽另标 audit4，不视为 alpha 或成片第四色。|
| stride / CFA / extent | **满足，受支持布局且拒绝不支持情况** | rr001_internal.h:63–70 强制 width==iwidth、height==iheight、shrink=0、fuji=0、分配指针相同且 extent 足够；以 iwidth*8 读取四槽、输出紧排指定通道，记录 CFA6×6/尺寸/margins。不能泛称支持所有 RAW 布局。|
| 指针切换 | **满足拒绝合同；不支持重分配后继续捕获** | pre_interpolate 可能换指针。当前 header 不重绑定 g.allocated，因此新指针不匹配时 payload 返回 invalid，最终 end 失败；不使用旧指针拼成“通过”。如需支持，须显式跟踪新 allocation/generation/stride 后另审；本轮不要求扩大支持范围。|
| 初始化范围 | **满足，源码依据；运行仍未知** | 固定 raw2image.cpp:395–402 对整块四槽初始化；allhex 全表显式初始化，CIELab 仅初始化后的表和有效列被记录。私有 scratch 不作未初始化全域 hash；四槽初始化不等于 RGB 四通道有效。|
| 真实运行及实际 team | **证据不足：UNKNOWN** | worker/team/tile 的设计存在，不能推出实际并行。新库没有原生构建/ABI/无RAW运行证据，更没有 P2/P5/P6 的两进程运行数据。|

## 必须修改：F1，P1 提前读取未初始化 quality

位置：Prepare-Internal.mjs:7、9；最终生成 dcraw_process.cpp:74；原赋值在生成行102（原始源码行74），声明没有初始化。

通用 stage 字符串在 P1 执行 `g.quality=quality`，此时原函数尚未给局部变量赋值。即使 g.quality 自身默认为 -1，右侧读的仍是原函数未初始化局部值。这是观测补丁引入的无效读取，不能以“移除标记可恢复原源码”证明观测语义安全；也没有运行证据证明它已造成原产品差异。

**最小修改**：P1 不读取该局部变量，把 quality 标为未建立/UNAVAILABLE；只在原赋值执行之后记录实际 quality。不要提前移动、改写或给原算法变量补初始化，也不要改插值质量。修订后重封生成源码/header/合同身份，静态验证不再存在提前读取。

## 必须收紧：F2，缺失状态不能与相同状态等价

位置：Compare-Checkpoints.mjs:3–6。最终快照已经新增 context/stateEqual 和 earliestDifferentObservedBoundary；确实能识别已经记录的分支、passes、CFA 差异，早期“只比较像素”的宽泛结论不再适用。

仍存在的缺口是 load 没有检查各阶段必需状态和有效表是否齐全，空 context 可进入比较，两个空 context 会得到 stateEqual=true。已用独立 2×2 合成文件证明：六份合法长度/hash 的 payload 配上 completed 状态、完全没有 typed state，仍被接受；空 P5 状态可报相同。同时，已记录状态不同时 stateEqual=false、第四槽像素改变可被检出、失败状态会拒绝，这些已有能力保留。

**最小修改**：对每个 checkpoint 校验必需记录集合、唯一性、顺序/阶段绑定；输入等价至少要求实际 X-Trans 分支、passes、filters/colors、尺寸/布局、CFA、有效 rgb_cam 及必要控制量、源码/观测库/运行身份明确。缺失或不支持数据必须为 UNKNOWN 或拒绝，不能回退为空数组相等。区分 imageEqual、requiredStateComplete、effectiveInputEqual；只有前两者及身份均已验证才允许最后一项为 true。colors=3、支持布局和指针变化的本进程有效性应先验证，跨进程不比较绝对地址。原有状态差异识别保持。

这项合成验证只证明文件比较门禁缺口，不是 RAW 执行证据或 H1 复现。

## 定位边界：F3，串行准备与并行区仍须区分

最终生成 xtrans_demosaic.cpp:176 写出 `XTRANS-serial-ready` 四槽图及已定义 allhex；CIELab 有单独已初始化表记录。Compare-Checkpoints 的六点 order 没有这个额外 payload，context 又排除了 payload，故主结果并不比较串行准备后的图。

在实际 P5 完整输入相同、P6 不同时，可以定位到整个 interpolator 区间，不能直接把首差锁到并行 tile。进一步谈 H1 时，应比较已有 serial-ready、allhex 和 CIELab 有效表，再判断差异是否已在并行区之前出现。这是对已有落盘内容的合同要求，不授权重跑。如果较早阶段已经不同，按真实首差调整定位。

## H1 和执行合同修订

H1 继续为 **UNKNOWN_CONDITIONAL**。只有实际进入相应 X-Trans 且实际 team>1、完整 P5 输入/状态相同而 P6 不同，才支持进一步检查；仍不是根因确认。源码示例坐标不是本次实测差异点。

补丁目前只有 worker/tile 元数据，没有实际共享 load 的消费值。额外 peek、P5 快照、serial-ready 快照都不能代替 LIM 宏实际多次读取的消费值。缓存 LIM 参数、加锁、改单线程会改变行为；不作为被动观测修复建议。日志、hash、额外分配本身也有观察效应，新库不能冒充原发行 DLL 的原样被动捕获。

本轮既有 BUILD_STATUS 为 NOT_BUILT_REQUIRED_MSVC_TOOLCHAIN_UNAVAILABLE，nativeCompilerInvocations=0。既有任务级预算审计和拒绝收据记录 consumed=2、remaining=0、launchAllowed=false；新库未运行不产生新额度。本次不运行 runner、不预留额度、不自行批准额外两进程，也不安装工具链。缺少新内部运行证据，最早内部首差、P5/P6 实际值及 H1 均明确 UNKNOWN。旧公司 Unpack 相同、Process 后不同的证据仍是原先区间定位，不升级为内部阶段定位。

修订附件 CONTRACT_AMENDMENT.json 保留原三份基线合同，只为本次实际补丁追加语义约束，并明确 staticContractComplete=false / executionReady=false。

## 验证与交付

REVIEW_CHECKS.json 包含 94 项源码/身份/捕获位置/合成断言，其中 27 项为固定 LibRaw 文件核对、7 份生成源码可去标记恢复原始字节。合成比较验证是门禁负例与对照，不是产品接受测试。新增 RAW、native init、原生编译、托管构建、GPU、参考数值运行均为0。

复核入口：`python -X utf8 docs/research/raw-reopen-001-contract-20261104/observation-patch-review-20261106/verify_review.py <新的结果JSON路径>`。脚本只读快照/Git对象，在系统临时目录创建微型合成文件；缺本地快照时直接失败，不下载、不解码、不补算。

INPUT_MANIFEST、DECISION、CONTRACT_AMENDMENT、REVIEW_CHECKS 为本次实质产物；源码快照、vendor 原文、自然图及完整像素 buffer 均不进 Git。LibRaw/RawTherapee 版本和许可证边界沿用固定基线，本轮没有引入依赖或复制算法，不构成新的许可批准。报告完成不等于故障已修复，待产品修订与运行事实。
