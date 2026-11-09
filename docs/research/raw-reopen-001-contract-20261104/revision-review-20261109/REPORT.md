# RAW-REOPEN-001：08bfae4a 修订复审

2026-11-09，Europe/Helsinki。固定产品提交：`08bfae4a6e311609c95784e8373bead711d9997c`。

**F1：通过。F2：仍需修正。F3：通过（限定有效、受支持的捕获合同）。总体仍不具备执行准入，H1 待运行验证，NOT_FIXED。**

只复验上次 `71ba977ac2d2ca1b039a59fb3a4b2e3f28882a4f` 的 F1/F2/F3，不沿用旧补丁裁定覆盖本次修订。使用指定提交的 Git blob，未使用随时移动的产品工作树代码。最终现场产品 HEAD 已为 `c156bb83e741e1fd61142431a203d62e62a9e44b`，其与本次固定提交的关系、干净状态另存 PRODUCT_READBACK.json；未因它较新就扩大审查范围。

本轮未编辑产品、不加载 native、不启动 RAW/GPU、不构建。V1 锐化候选继续封存。脚本读固定源码和文件，在明确本地目录创建 40×40 合成 U16 文件及虚构运行元数据，只用于检验比较器输入合同；其中 completed/zeroRaw=false/counts=1 是为了进入加载校验路径的**合成声明，不是实际执行收据**。

## 绑定最终生成源码与 header

实际生成树：`E:/Guangjing-RawReopen001-20261106/frozen-source-v3-r2`。固定源为 LibRaw `cccb97647fcee56801fa68231fa8a38aa8b52ef7`。从本轮指定提交导出的生成器调用纯 `instrument()`，7 份结果与实际生成树精确逐字节相同；全部去除观察插入标记后恢复原始源字节。未重新 clone、下载或重新生成完整 vendor 树。

本轮直接读取并计算下列 SHA256，未只信任 PREPARATION.json 的声称。每个生成 CPP 目录里的 header 均读取验证；header 同时与指定产品提交的 Git blob 精确相同。路径、大小、快照及完整绑定见 INPUT_BINDING.json。

| 实际文件（相对生成根） | SHA256 |
|---|---|
| `src/postprocessing/dcraw_process.cpp` | `33e7474533ef7290516753aba40397b3f39e3dd8f660825416b38b37c8c46458` |
| `src/postprocessing/postprocessing_utils_dcrdefs.cpp` | `f91703b93b8eb1d8ad09e1a583b499feab6cb7e31030a7937d77984cb9c3330f` |
| `src/preprocessing/raw2image.cpp` | `462da26fb330ececbe50c690799c95c8f472d68ed2dcbcedd44f4662ef589130` |
| `src/demosaic/xtrans_demosaic.cpp` | `cf4616cc0f437d28e52b6131b5e241ee2da96e587c8b86f4d62c76f58e8c84fc` |
| `src/preprocessing/subtract_black.cpp` | `8774920911375ed61235f171ca8aa6bedcf4cc9c2bca9280e80679fe34b28544` |
| `src/utils/utils_libraw.cpp` | `0601a36b97608ffa4a7e2b173bcaf453f851790750cd5b8fe91f953d9ec5cf3a` |
| `src/demosaic/ahd_demosaic.cpp` | `f84703a8523310aabf34c25eaa55ebbf5b3dbae20d1dafa49e0ce6f545523232` |
| 各修改目录内 `rr001_internal.h` | `ce42b2efa23806da4f1edabbad4901c1a836ff319133507bac6e4315b0d1f718` |
| `PREPARATION.json` | `5c032c4b048cdf3c467db20decc9e2e8f2d7ae1e42764240c91ef3e30dfe494a` |

## F1：通过，生成 P1 已不读未初始化 quality

代码位置：指定提交 `tools/RawNativeInternalProbe/Prepare-Internal.mjs:7,9`；实际生成 `src/postprocessing/dcraw_process.cpp:74,102`。

生成行74在 P1 只写 `rr001i::g.quality=-1`；原函数对局部 quality 的首次赋值仍在生成行102。复核原赋值前的生成文本没有 `g.quality=quality`，也没有提前改变原算法赋值、质量参数或初始化行为。因此，上次 F1 的具体提前读取问题已闭环。

最小证据：RESULTS.json 中 `F1.generated.P1.no-early-quality-read`；INPUT_BINDING 对该生成 CPP 的实际 hash 及精确重生成记录。该裁定不宣称整个观察库已完成原生编译/ABI/运行验证。

## F2：仍需修正，缺失/重复记录/身份已有拒绝，但阶段绑定和重复 JSON 字段有漏口

指定提交 `Compare-Checkpoints.mjs:10–11,15–41,45–70`。22 个拒绝反例中，19 个实际拒绝，3 个被接受。不是“全部测试通过”；本轮成功复核了这些行为，仍有3个需求未满足，详见 RESULTS.json.requirementsNotSatisfied。

| 最小反例 | 两侧均这样修改或指定侧修改 | CLI 结果 |
|---|---|---|
| P5 缺 quality；缺 CFA 记录；缺 host 身份；空 typed state | 两侧 | 拒绝，无结果相同报告 |
| 重复 actual-scale_mul 记录、重复 P5 pre_mul 记录 | 两侧 | 拒绝 missing/duplicate |
| out_cam 行移到 P8 state 之前 | 两侧 | 拒绝 out_cam phase binding |
| native/binding/input/preparation/contract/host/manifest/nativeSelfTest/source 身份不匹配 | 单侧 | 全部拒绝 |
| process owner、reservation owner、loaded native 不匹配 | 单侧 | 全部拒绝 |
| CIELab 有效矩阵三行移到 P6 之后、P7 state 之前 | **两侧** | **未拒绝：exit0，七阶段 effectiveInputEqual 全 true** |
| allhex 记录移到 P5 state 之前 | **两侧** | **未拒绝：exit0，七阶段 effectiveInputEqual 全 true** |
| P5 JSON 同一对象出现 `"quality":999,"quality":3` | **两侧** | **未拒绝：exit0，七阶段 effectiveInputEqual 全 true** |

### 必须补：CIELab / allhex 的阶段绑定

位置：Compare-Checkpoints.mjs:35–39。

当前 CIELab 表要求全局唯一，矩阵仅要求全局存在三行，allhex 要求全局唯一；只检查 lab/hash、allhex 在 serial payload 之前，没有要求其发生在实际 P5 entry 之后，也没有检查矩阵行的阶段位置。随后 line39 把全局找到的表记录追加到 serial-ready 状态，因此两侧同样错位的记录仍被当作完整且相同的输入状态。

最小复核证据：最终本地 case-9 和 case-10，对应 RESULTS 的 `F2.wrong-stage-CIELab-matrix-after-P6` 与 `F2.wrong-stage-allhex-before-P5`。两侧没有改像素、表的值、哈希、布局或稳定身份，只移动上述记录；比较器产出 `NEW_BUILD_THIS_PAIR_NOT_REPRODUCED_NOT_FIXED`，`earliestDifferentObservedBoundary=null`，全部 effectiveInputEqual=true。

**最小修改要求**：按实际生成器的观测顺序限定记录窗口——实际 P5 捕获完成之后、serial-ready 捕获之前；CIELab lookup 与有效矩阵应属于同一次初始化，allhex 属于本次串行准备。校验 phase-local 唯一性与顺序/归属，错位或缺失时拒绝/UNKNOWN。Prepare-Internal.mjs:67,73 给出了这两个实际插入位置。可以通过明确阶段标识或严格区间验证实现，不需要增加解码、改算法或添加运行日志。

### 必须区分：重复记录已拒绝，重复对象成员尚未拒绝

位置：Compare-Checkpoints.mjs:46 的 JSON.parse；后续 line20、69–70 只看到解析后对象。

重复 typed record 的两个反例已拒绝，不能继续沿用上轮“缺失状态等价”的结论。另一个词法层面的反例是单个 P5 JSON 对象同时具有两个 quality 成员。JSON.parse 采用后值，丢弃前面的999；因此比较器无法再识别成员重复。最后的3符合阶段合同，于是两侧会被接受并报完全相同。最小证据为最终 case-11、`F2.duplicate-JSON-member-quality`。

**最小修改要求**：在 JSON 对象成员被覆盖之前拒绝重复键，至少覆盖 typed 状态/运行身份输入。应使用可验证唯一对象成员的解析/读取方式；解析后的 schema 校验仍保留。不要用简单字符串/正则扫描代替嵌套 JSON 的成员唯一性校验。若产品只承诺拒绝重复“记录”，应明确这个更窄合同；它仍不能解决前述阶段错绑，因此本轮 F2 总裁定不变。

## F3：通过，P5 相同/P6 不同时会保留更早的串行边界

代码位置：Compare-Checkpoints.mjs:7 将 serial-ready 放在 P5 与 P6 之间；35–39 将已初始化 allhex/CIELab 表纳入 serial-ready；69–73 区分 imageEqual/stateEqual，按两者保留最早观测边界并保持 H1=UNKNOWN。

四组独立 CLI 合成对照均先验证 P5.effectiveInputEqual=true，再让 P6 RGB 不同，另外各只令下列一项先不同：

| 较早改变量 | earliestDifferentPayload | earliestDifferentObservedBoundary |
|---|---|---|
| serial-ready 四槽中的一个 U16 值 | XTRANS-serial-ready | **XTRANS-serial-ready** |
| allhex 的已定义 span hash | P6 | **XTRANS-serial-ready** |
| CIELab lookup 表 hash | P6 | **XTRANS-serial-ready** |
| CIELab 有效矩阵一项 | P6 | **XTRANS-serial-ready** |

三种表差异的“首个像素 payload”仍是 P6，不能据此称首差在并行区；比较器的 observedBoundary 正确保留 serial-ready。四组均输出 `H1=UNKNOWN_NO_ACTUAL_CONSUMER_LOAD_EVIDENCE`，没有归因于 parallel tile loads。

证据：RESULTS 的四个 `F3.earlier-serial-boundary:*`，最终 case-24 至 case-27 的 FIRST_DIFFERENCE.json。此通过限于本轮构造的有效、受支持阶段记录；F2 的错绑拒绝仍需修正，F3 不能据此升级为所有输入已安全验证。

## 复核与剩余事实

本轮完成50项源身份、精确重生成、F1、基线及反例行为观察，其中22项为F2拒绝反例、4项为F3边界对照；3项拒绝要求未满足。实际源码/header 哈希绑定为通过，合成比较并非产品像素或 native 运行验收。

- 完整裁定：VERDICT.json；逐项最小结果：RESULTS.json。
- 合成输入与输出在 Git 外；SYNTHETIC_EVIDENCE_HASHES.json 绑定基线、三个漏口及四组 F3 的本地文件 SHA。
- 复现：`node docs/research/raw-reopen-001-contract-20261104/revision-review-20261109/review.mjs <新的结果目录>`。需要 INPUT_BINDING 指定的固定快照及本地源 Git 对象；缺失直接失败，不下载或执行 native。脚本只调用文件/Git/JavaScript 比较路径。
- H1 仍为待验证：实际并行 team 和共享槽消费值均 UNKNOWN；没有额外 peek/缓存 LIM/加锁/改单线程方案，没有新增实验额度。
- 新增 RAW=0、GPU=0、native init=0、native build=0、managed build=0。研究没有修改产品、默认参数或 V1 锐化候选。

此报告替代上次裁定中的 F1/F3 未闭环部分；F2 按本次具体漏口继续开放。它不表示原始 RAW 故障已修复，也不授权运行。
