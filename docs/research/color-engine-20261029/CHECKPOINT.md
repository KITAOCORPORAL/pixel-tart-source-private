# 当前检查点：RAW-REOPEN-001 实际观测补丁审查（2026-11-06，Europe/Helsinki）

从5a4f233d接续。实际产品未跟踪补丁与frozen-source-v3已按独立快照绑定，未合入产品分支。P2落点和小状态满足基线；P5全四槽/P6含边界RGB在支持布局上静态覆盖满足，运行均UNKNOWN。发现P1读取未初始化quality；新版比较器能检测已记录状态不同，但缺必需状态仍可相等；serial-ready额外payload未进入主比较，H1维持UNKNOWN_CONDITIONAL。94项源码/文件/合成核验；27份源锁/7份字节恢复通过。新RAW/native init/构建/GPU/参考数值运行均0；已有预算consumed2/remaining0，未新增额度。交付observation-patch-review-20261106/REPORT.md、DECISION.json及CONTRACT_AMENDMENT.json。状态REVIEW_COMPLETE / PATCH_REQUIRES_CHANGE / NOT_FIXED；不重跑，不改产品像素，V1锐化继续封存。

---

# RAW-REOPEN-001 内部交接已推送（2026-11-05，Asia/Shanghai）

复用已完成内部审查，未重复生命周期报告、解码或运行实验。研究内容提交3d06c14004c8047a88ab5362b14039fff0bbf7ac及历史失败收据ef95df88e5ca7b44a6adbcd0f7278941c46db58f已正常推送，远端实际读回ef95df88e5ca7b44a6adbcd0f7278941c46db58f一致。原PUSH_RECEIPT.json仍保留当时网络失败；当前成功记录为process-internal-review/PUSH_SUCCESS_20261105.json，后续文档提交HEAD另读回。

产品线交接入口：docs/research/raw-reopen-001-contract-20261104/process-internal-review/CHECKPOINTS.md、CHECKPOINT_SPEC.json、HYPOTHESIS_H1.json、INITIALIZATION_AND_PARALLEL_AUDIT.md；文件/函数行范围锁在INTERNAL_SOURCE_LOCK.json，产品输入绑定c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39。材料覆盖内部函数落点、有效域/布局/初始化、实际quality/passes/分支与worker字段、唯一H1的读写关系和证伪条件。

ROOT_CAUSE_OPEN/NOT_FIXED不变：最早缺raw2image_ex返回有效image/state是否已差；H1还缺实际XTrans分支/team与插值前同/后差及消费值证据。产品统一安排隔离观测，研究等待内部事实。此次新增RAW/native加载/build/GPU/参考运行均0，src/tools/tests零变更；旧锐化V1保持封存default-off/NOT_ADMITTED。

---

## 以下保留原检查点与历史网络失败，不代表当前交付状态

# RAW-REOPEN-001：DcrawProcess内部交接（2026-11-04，Asia/Shanghai）

本轮源码交接本地提交3d06c14004c8047a88ab5362b14039fff0bbf7ac。两次正常push连接重置，ls-remote端口443超时；远端接收未确认，最后成功fetch研究0e0723df/产品c7df75d4。PUSH_RECEIPT记录实际失败，网络恢复后只补推送读回，不重做审查或解码。

研究基线0e0723df0c8eaf665d2bccdfe150c45621534d3b，产品新观测diagnostic/raw-reopen001-20261104@c7df75d4ec8dde31cb1bd6d33ce3726eb2db7c39已fetch并读取实际report/source/receipt。有效Unpack同、Process首次已不同（5316通道/3042像素/max146U16）；实际unpacker=unpacked_load_raw()/flags4096、filters9/colors3。OMP启动前null→原binding初始化后6，nativeactualworker仍未知，停止称必然外部继承。

只审固定LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7和RT5.11@db575c6690d30ce6428c0e1383bec8b8f1fb8d21；复用本地RT归档，补固定LibRaw源，35file/35anchors。raw2image/黑位、scale/pre_interpolate、实际选择、插值/convert路径精确位置/域/布局/初始化状态在docs/research/raw-reopen-001-contract-20261104/process-internal-review/CHECKPOINTS.md和CHECKPOINT_SPEC.json。

找到唯一条件性H1：XTranstop并行tile共享image读取（含非绿slot1=minGreen）与邻tileRGB回写交叠；有明确源码读写/静态索引实例，不是单凭malloc/OMP或RT清0。实际算法/passes/team、P5输入同且P6输出先差以及真实消费值尚未捕获，ROOT_CAUSE_OPEN/NOT_FIXED；不加锁/清零/换decoder。最早缺的内部事实仍是raw2image_ex返回有效域是否同；产品统一安排隔离观测，研究不再跑旧外层计划。

新增RAW/nativeinit/unpack/process/memory/build/GPU/参考运行/像素候选全0。产品src/UI/default/旧结果不变；RT为共同Markesteijn/dcraw血缘+LibRaw输入复用，不是生产同域独立数值真值。旧锐化V1封存/defaultoff/NOT_ADMITTED，不开V2、不扩跨GPU/BT/20MP/胶片/Sony/新相机。

权威process-internal-review/REPORT.md、INITIALIZATION_AND_PARALLEL_AUDIT.md、HYPOTHESIS_H1.json和PRODUCT_EVIDENCE_BINDINGS/INTERNAL_SOURCE_LOCK。旧REPORT/PRODUCT_DIAGNOSTIC_PLAN仅保留历史，不能将已完成外层观察再列下一步。完成本次源码交接后等待产品内部阶段事实，不启动研究线运行实验。最终Git身份以实际push/readback为准。

---

## 以下是历史检查点，未回写当时未知项

# RAW-REOPEN-001 单一源码/契约对照（2026-11-04，Asia/Shanghai）

研究从封存6f1ae27aff72a7439c661e605a38c1702feea4ce接续，产品审查e2437d2300b37872d5b66482a721e3b6463f1ce4仅只读，未合入产品分支。新研究分支research/raw-reopen-001-contract-20261104，权威docs/research/raw-reopen-001-contract-20261104/REPORT.md、SOURCE_LOCK.json、PRODUCTION_DEPENDENCIES.json和PRODUCT_DIAGNOSTIC_PLAN.md。

确认发布/缓存/既有pass4 testhost原生raw_r.dll及Sdcb.LibRaw net6.0资产字节匹配；native版本复用0.21.1-Release收据，绑定0.21.1.7/43e9c177。固定LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7与RawTherapee5.11@db575c6690d30ce6428c0e1383bec8b8f1fb8d21相关函数/许可；native包源码/编译溯源仍不完整，不能声称上游tag与DLL可复现同源。

现有默认成功路径未发现明确生命周期/输出buffer契约违约；实际unpacker/CFA/fuji_width/插值分支旧收据未捕获，不凭型号/UserQual猜算法。RT默认输入端共用LibRaw、后续为自有float demosaic，非完全独立同域数值oracle。mutex/scratch差异不足以证明竞态或未初始化读取，不建议直接关线程、换decoder或calloc。仅给产品线最小被动诊断计划，研究线本轮RAW/GPU/build/参考运行/像素比较均0，产品src/默认参数无改动。

本轮CODE_CONTRACT_REVIEW_COMPLETE、ROOT_CAUSE_OPEN；独立同域数值对照NOT_RUN，非像素变更准入。V1锐化继续封存/default-off/NOT_ADMITTED；无V2或胶片候选。下方原检查点字节保留，不重复评价或封存V1。

---

# 第十三批 V1 研究已结束并封存（2026-11-03，Asia/Shanghai）

接续 research/color-engine-round13-human-review-20261103 @ 8b9dcfa0bf8bd07e983a63a0a79bfb84a7110ca9，fetch/本地/远端核对一致。复用本会话已核验真实人工评价；原 JSON SHA256 48bd60ff065eaadbb49576700fbf3c5f89ce85ea121d6b50747a9cb8a5dfd397，本次哈希确认未变，不重复评价、不补答案/文字原因。

当前人工状态：**已收到并核验，未报告明确版本偏好**。30/30 项已填写；没有场景明确偏好 X 或 Y；总体4场景无明显偏好、2场景无法评估；无文字原因，观看条件记录不足。此前“尚未收到/NOT_RUN_AWAITING_HUMAN”仅为下方历史准备状态，不再是当前结论。

SharedIncrementEnvelopeUnsharpV1：技术结果及取舍保留；人工评价已完成但未建立值得推荐的主观收益；默认关闭、NOT_ADMITTED，**结束V1研究、封存不接入**。独立验证保持 NOT_RUN_DEVELOPMENT_ONLY。无明确新问题，不自动调参/V2/新增样本，不继续追逐零值数量或跨GPU逐位一致。

此次只汇总文档和 Git 交付：新增build/GPU/RAW/20MP/六图渲染/BT/算法调整/产品打包全部0。原JSON、评价页面、私照、匿名key/映射只本地保留，不揭盲、不上传。Git仅提交原文件hash、汇总/核验范围/局限。权威ROUND13_V1_CLOSURE_20261103.md及round13-v1-closure-20261103/SUMMARY.json；当前能力表已更正。最终Git提交/远端身份以实际push后的读回消息为准。完成后停止该候选任务。

---

## 以下为历史检查点（保留原字节；不代表当前人工评价状态）

# 第十三批人工摄影评价准备已收口（2026-11-03，Asia/Shanghai）

接续已审查 research/color-engine-company-scale-closure-20261103 @ 9b8c52715b45540ffaf1d1fc99e0a7bd832678fc，fetch 无增量，复用干净独立公司研究 checkout，新分支 research/color-engine-round13-human-review-20261103。CPU 数值定位停止；保留 double 身份差异与 tc_b / 相同输入子集 upsample 差异，无实现错误证据，不改像素数学。旧 validator/hash/localcopy 不重跑，缺家庭四完整数组不阻断此准备。

限定 E:/Kitao-Research 顶层和明确 round13/原 N: 路径未发现冻结 A/B；不扫描其他盘、不换公司照片。家庭只读入口 tools/color-round13-human-review/README.md 已交付，61 个明确文件缺项清单入 human-review-prep-20261103。输入/输出 SHA、长度、finite、原 PNG 显示像素及旧匿名映射/裁片绑定，缺项拒绝，成功仅生成私有本地 HTML；X/Y 同尺度切换、100% 既有 ROI，不重渲染、调参或新增候选。

8 项新入口离线合成文件测试和合成 UI smoke 通过，仅此入口测试，不是真实六图读回或摄影通过。公司真实六图入口生成/观看 NOT_RUN_MISSING_PRIVATE_FILES，人工偏好 NOT_RUN_AWAITING_HUMAN，独立验证 NOT_RUN_DEVELOPMENT_ONLY；代理未代填。新增研究 GPU/RAW/20MP/自然图/BT/build 全 0。照片/评价图/答案/引擎资源不入 Git。

权威 ROUND13_HUMAN_REVIEW_PREPARATION_20261103.md、CAPABILITIES_ROUND13_HUMAN_REVIEW_20261103.md 与 human-review-prep-20261103。产品 UI/default/PV456、旧工具/配方/证据不变；SharedIncrementEnvelopeUnsharpV1 仍 DEFAULT_OFF / NOT_ADMITTED。交付后等待实际摄影评价，不自动第二候选、独立样本实验或产品接入。下方全部旧历史原字节保留；本轮新收据 Asia/Shanghai。

---

# 公司既有capture CPU收口并停止（2026-11-03，Asia/Shanghai）

fetch家庭`research/color-engine-home-scale-20261102@0fa710e606b962efac66727215fb6c8f0c76a5ed`准确，无增量。公司旧树099b2fb8clean/unpushed0，另建`research/color-engine-company-scale-closure-20261103`，旧公司/家庭/失败与历史时间保留。

仅CPU实际读公司完整E:/Kitao-Research/color-engine-scale-localization-20261102-v2/gray-once，先核身份/17观察hash后已有completevalidator通过。公司实际sensitivity/LUTdoubleSHA与home封存不同、floatLUTupload/deviceSHA与home封存相同；home完整4数组未迁移，差量/首坐标保持null，不调用fullpairedcomparator冒充闭环。公开小数组直接复读确认tc_b输出tc487/brightness82不同；158同tc/b像素中156仍upsample变429分量。保留已定位层级，不回compound、不只归LUT，不拆shader指令。

21文件公司完整localhandoffcopy+shape/format/hash清单已核验，restrictedLUT/sensitivity仍只本地不进Git。检查只授权研究根顶层/相关读回目录和home已知N路径，无其它盘/私人目录扫描，无home重生成。

新增engine/GPU/readback/RAW/20MP/六图/stream/BT/候选/build/sourcegeneration全部0。当前无实现错误证据：**保留跨环境数值差异及未决原因，暂不修改像素数学；本阶段交付后停止数值定位扩张。** 不把max1code外推所有照片，不把跨GPUbitexact当其它研究前提。SharedIncrementEnvelopeUnsharpV1继续defaultoff/NOT_ADMITTED，产品src/UI/default/PV456/任务桥不改，许可单列。

后续摄影评价的匿名人工偏好、独立样本与细节/肤色/噪声目标单列，不自动重渲六图或扩精度实验。权威COMPANY_FILM_SCALE_CPU_CLOSURE_20261103、CAPABILITIES_FILM_SCALE_CPU_CLOSURE_20261103和company-scale-cpu-closure-20261103。本地材料E:/Kitao-Research/color-engine-scale-cpu-closure-20261103/readback，最终LOCAL_HEAD/REMOTE_HEAD实际push后读回。

---

# 家庭一次gray子阶段观测（2026-11-02，Asia/Shanghai）

接公司research/color-engine-company-diagnostic-20261102@099b2fb8dbd43fa872703cef84e04b3776c96aab，新家庭独立research/color-engine-home-scale-20261102，旧家庭93b553d1clean/unpushed0保留。同版tap/Release/原runtime已冻结；只runner身份采集在NVML UnknownError时保留失败并读取真实WMI家庭GPU/driver，未改数字路径。

唯一17×33gray：engine1/open1/whole1，stream/BT0，7readback；final对家庭封存RGBA16全通道0差，fullhomecapture通过。CPUmatrix/LUTdoublecast→staging→device实际逐bit0差。底层初始化probe及totaldispatch未计数，不编造。

跨端：sensitivity/LUTdouble封存SHA已不同（公司完整数组不在家，误差坐标/幅度未知），floatLUTupload/deviceSHA相同；input/matrix可直接字节0差。首个实际成对GPU输出差异是spk_tc_b：tc487/1122、brightness82/561同dispatch，不能分内部指令。upsample1520/1683不同；同tc/b输入的158像素中156仍upsample不同，不能全归前级传播。EV0各端upsample→exposure→旧compoundbit0差。硬件/驱动/JIT未分离，不改数学/FMA/精度/驱动。

公司完整sensitivity/LUTdouble和floatupload/device四文件仍缺直接内容比较，仅hash分层；public17文件明确非fullcapture。实现/证据提交 e9c2c166907927d357de75a11bcb3d88cc348aff 已正常推送并远端读回匹配；收据home-scale-localization-20261102/remote-receipt.json，最终HEAD另读回。

权威HOME_FILM_SCALE_LOCALIZATION_20261102、CAPABILITIES_HOME_FILM_SCALE_20261102与home-scale-localization-20261102/。完整home输出N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once，restricted只本地。11项CPU控制PASS，旧1760历史/product+旧home20MPhash保全。RAW/20MP/natural6/stream/BT/candidate13均0；候选默认关闭NOT_ADMITTED。到此停止，不自动扩大实验。Git最终状态另由remote-receipt和实际读回确认。

---

# 公司定位续接只读核验（2026-11-02，Asia/Shanghai）

本次fetch确认已审查7309911e之后已有4c4bf788公司子tap和6c47fb5b收据，研究树clean/远端一致。没有重复相同gray观测：新增engine/API/GPUreadback/dispatch/build全部0，RAW/20MP/六图/新候选/BT继续0。

重新CPU验证完整localcapture，矩阵/LUT的CPU窄化→实际staging→device逐bit0差，finalRGBA16对公司封存结果逐通道0差；59payload/709历史文件、10source/DLL和35shader/9数值CPU旧入口身份只读核验一致，离线指标与子阶段分析SHA完全重现。旧报告/失败/时间和NOT_RUN不改。

结论不升级：公司差异承载已在Hanatos upsample完成输出、曝光之前；家庭没有同版实际常数/tc/b/upsample/exposure tap，因此跨端首个operator或CPU准备仍NOT_CONFIRMED、hardware/driver未分离、算法修复需求NOT_ESTABLISHED。下一步仅家庭一次同版graytap并先验证家庭final0，先常数字节后子输出比较；公司到此停止，不增加泛化验证。

SharedIncrementEnvelopeUnsharpV1 defaultoff/NOT_ADMITTED，产品src/UI/任务桥/default/PV456不改。当前读回COMPANY_FILM_SCALE_READBACK_20261102.md及CAPABILITIES_FILM_SCALE_READBACK_20261102.md；原家庭最小接续REPRODUCE_SCALE_LOCALIZATION_20261102.md继续有效。研究不等于产品或分发许可批准。新本地输出E:/Kitao-Research/color-engine-scale-localization-readback-20261102，仅元数据/公开描述结果入Git，fullruntimeLUT/sensitivity仍本地。

---

# 公司gray子阶段定位（2026-11-02，Asia/Shanghai）

接续`research/color-engine-company-diagnostic-20261102@7309911e1ad501b517877a073a86e468a9507941`，fetch无增量、开工clean。先离线GPU0核对gray首差(2,0,R)1ULP、max代表(32,0,G)4ULP，nearzero/negative/normal分账，无新跨设备阈值。固定真实链：CPUfilmSensitivity/runtimeLUT/CAT16矩阵→实际f32upload→tc_b→MitchellLUT→EV0affine；Pillow未入数值路径，不当同等嫌疑。

旧包缺实际上传常数/子输出，故独立FilmScaleSubstageTapV1只公司graywhole执行一次：engineCreate1/open1/render1、7被动GPUreads、0新增数学dispatch、stream/BT/RAW/20MP/六图/候选0。source/shader/flags/模式不改，最终RGBA16对封存公司gray0差。矩阵/LUT CPUcast=实际staging=device逐bit0差；scale1offset0，upsample→exposure及exposure→旧stage逐bit0差。

公司差异承载收窄到Hanatosupsample输出、曝光之前，但home无同tap，**未确认跨端首因在CPU准备、tc_b或LUT插值哪个环节**，也没分离hardware/driver。到此停止GPU，下一步最小任务仅家庭一次同版graytap，先常数字节后tc/b/upsample比较；不靠改数学归零。本轮算法修复需求NOT_ESTABLISHED。

SharedIncrementEnvelopeUnsharpV1仍defaultoff/NOT_ADMITTED，产品src/UI/任务桥/default/PV456不改。私人偏好NOT_RUN，旧公司诊断/家庭十至十三批/失败时间收据全部保留。完整runtimeLUT/sensitivity仅本地，Git是明确publicsubset+hash。权威COMPANY_FILM_SCALE_LOCALIZATION_20261102、CAPABILITIES_FILM_SCALE_LOCALIZATION_20261102、REPRODUCE_SCALE_LOCALIZATION_20261102和company-scale-localization-20261102。新收据Asia/Shanghai，最终LOCAL_HEAD/REMOTE_HEAD实际push读回。

---

# 公司公开合成诊断已执行（2026-11-02，Asia/Shanghai）

接续已审查家庭`research/color-engine-home-20261030@93b553d1c22937a2870743a40adc43d6df8b526d`，fetch无增量。公司原研究树HEAD0f3204c0、clean、unpushed0；新独立分支`research/color-engine-company-diagnostic-20261102`承接，旧树/失败/第十至十三批历史均保留，不合入产品。

按修正后的`tools/color-round11/diagnostic/run_company.ps1`显式Checkout/Library/TraceLibrary/Resources/Output/Python执行一次。公司GTX1650/572.70、Windows10build19045、Python3.13.13，运行前真实build身份已冻结。3公开合成输入，whole/rows1/7/17/64与trace-final全通道0差；144stage文件/域/尺寸/顺序/finite/hash和来源独立验证通过。

家庭/公司各最终whole变化通道数：gray17、saturated31、edge31，max均1U16；各rows模式相同。BT独立既有13值探针1值变化max5.58794e-9。whole与stream最早记录分歧都`film_scale_and_expose`，不直接归因GPU；固定commit/工具字节/flags/73resources34SPIRV/resolved一致，授权根字面量、DLL/构建路径、OS/Python/Pillow、GPU/驱动混杂分列。跨设备无事后PASS阈值。

本轮实际GPU输出API22（21Portra+1既有BTprobe），3自然入口拒绝发生GPU前；底层dispatch及初始化math-probe未独立计数，不把22当vkdispatch。RAW0、20MP0、六图0、第十三批候选0；只运行验证器负控制22+比较器3tests，无GPU重跑。公司包与完整比较在company-diagnostic-20261102，入口run attempts1/retries0。

SharedIncrementEnvelopeUnsharpV1保持默认关闭、NOT_ADMITTED，未调包络/曲线/旧unsharp、未开第二候选；家庭私人A/B目录未迁移，人工偏好NOT_RUN。下一步若授权定位具体差异，仅拆`film_scale_and_expose`内部setup/tc_b矩阵/LUT插值/曝光，不从已不同的下游锐化倒推根因。本次只报告定位需求，未执行新调查。

权威COMPANY_PUBLIC_SYNTHETIC_DIAGNOSTIC_20261102.md、CAPABILITIES_COMPANY_DIAGNOSTIC_20261102.md与company-diagnostic-20261102收据/包。当前公司执行取代“公司尚未执行”这一当前状态，但下方旧NOT_RUN历史与收据原样保留；不把公司synthetic执行写成公司候选/自然照片/20MP验收。最终LOCAL_HEAD/REMOTE_HEAD实际push读回提供。

---

# 当前检查点：第十三批唯一受约束锐化候选（2026-11-01，Europe/Helsinki）

基线3f7fe01beafc4ed83d113013ab00b300292a7461，fetch/本地/远端一致。固定dt/RT原函数及依赖已读、实际9夹具5配置共45次CPU运行完成；非完整应用/线性RGB等价oracle。

唯一SharedIncrementEnvelopeUnsharpV1：默认关闭、显式研究启用，线性RGB原unsharp增量共享gain约束在原5×5局部包络，未移植Lab公式。原sigma/amount、PortraA及资源不动。合成9例whole/r7、自然6既有1200 stage whole/r17/r37均float/encoded/RGBA16全通道0差，旧分支与冻结结果0差。RAW/上游film/20MP0，GPU stage36进程4088dispatch；15项只读专项测试通过。

结论KEEP_FROZEN_RESEARCH_ONLY / NOT_ADMITTED：NEX5R下冲/零值消除，但阶跃/脉冲锐化完全退回原输入，正弦细节增益削弱、噪声仍1.248×，自然梯度增量仅保留44.7%–67.0%，部分色相/肤色响应变化。不拿零值少判胜，不在本轮调参/另立第二候选。仅ratio1，真实preview尺度/独立验证/人工偏好/产品链/许可仍OPEN。R6仅整理有效曝光域与hidden defaults，不改像素。公司NOT_RUN（home5060Ti）。

实现/证据提交 ec53effe66c64590b26b3d99ee3469a28af0cffe 已正常推送并远端读回匹配；收据home-round13/remote-receipt.json，最终HEAD另读回。

权威HOME_ROUND13_UNSHARP_REPORT、CAPABILITIES_HOME_ROUND13_V1、UNSHARP_NODE_COMPARISON_ROUND13与home-round13。自然A/B/匿名图、外部源码/候选shader/runner及binary留N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1。下一步先人工匿名偏好和独立验证目标，不重复本批/20MP；若改数学另显式版本并预冻结门槛。Git交接见本批remote-receipt（生成后）及最终读回。

---

# 当前检查点：第十二批两项原因定位（2026-11-01，Europe/Helsinki）

基线603e8a49c7e35546053ad51339eaed2ad7989793，fetch和远端身份核对成功。旧FAILED_NETWORK收据原样保留，仅代表当时失败，不代表当前状态。

- A：NEX5R固定11,562像素的22,326零分量，22,322负值裁切+4正小值量化+0精确零；默认unsharp(.7px,.7)、5×5反射邻域产生下冲，最终量化与保存图全通道0差。画面暗侧边缘对应，非自动判bug。
- B：R6固定vehicle_shadow [0,500,300,250]中19,567零密度分量全部因film profile log域≤−3常数端点；242原不同曝光合并。纸张/扫描保留非零偏蓝底色，黑白校正关闭。原master端点污染与范围内曲线负斜率仍是边界，不把所有损失归于单节点。
- 只读复核6代理/72阶段；实际定位只2图。RAW/GPU/full20MP调用均0，新观察DLL0；固定PortraA CPU setup-only辅助1次。15项分析专用测试通过。
- 本批无像素修复；两个成熟实现本轮固定代码/数值对照未完成，候选门禁NOT_RUN。公司NOT_RUN（本机家庭5060Ti）。不调参、不扩功能、不改产品或旧20MP链。

实现/证据提交 4ff110e1ba83a740aef54b363b0622cad520a537 已正常推送，远端读回相同；收据home-round12/remote-receipt.json。

权威HOME_ROUND12_CAUSE_REPORT、CAPABILITIES_HOME_ROUND12_V1和home-round12；私有坐标/邻域/图像/CPU外部helper留N:/pixart/ColorResearchHome-20261030/round12-cause-v2。下一步先补成熟实现固定域对照，再决定另立像素候选；不重做本批或20MP。

---

# 续接检查点：第十一批收尾（2026-11-01，Europe/Helsinki）

已从原会话未提交现场接续。保留第十批冻结链、原第十一批运行和失败证据；本次仅补验证和交接，GPU/RAW/full20MP 调用均 0。43 项唯一测试通过；6 图既有像素 whole/r17/r37/trace 共18次比较0差，72阶段读回与统计通过。补齐只读审计器及阶段报告门禁，最终源码身份另存；首次全局stage编号定位错误及修复结果均保留。

详细结果：home-round11/resume-20261101/CLOSURE.md。公司实跑仍 NOT_RUN，暗部压缩/新增零值未解决，不能批准一般摄影或产品接入；下一步真实公司公开小包诊断，然后按 POLICY 另立暗部问题候选。本地代码/证据提交 a95071d0ad773f3e885f36b70081b5d88c8917ec 已完成，31 个源码 blob hash 与最终清单一致。两次普通推送/远端读回均网络失败，状态 FAILED_NETWORK，未确认远端收到；详见同目录 git-receipt.json。网络恢复后仅补正常推送读回，不重复20MP。

---

# 当前检查点：家庭第十一批（2026-10-30，Europe/Berlin）

基线07df3c56c551c38893abc1e4b7eaefb8e4a83be4，fetch无新增。第十批固定PortraA流式20MP已通过并冻结；本轮20MP engine调用0次，旧caller链/freeze/receipt/output/failed/AUDITED_STATUS原样保留。只新tools/color-round11和home-round11证据，产品src/UI/default/PV4/5/6不改。

- 诊断V2显式home/company角色，实际machine/GPU/driver/Python/tool/build身份。verify_run/compare_packages共享同一函数且左右各自实际读数据重验whole/4rows/trace0差，固定输入/shape/resolved/resource+SPIRV/stage顺序/域/finite/长度。22测试PASS，CLI坏左/坏右两个subcase均拒绝；改stream且同步重封hash不能绕过。
- 当前仍家庭5060Ti，公司新诊断NOT_RUN；新run_company.ps1先采本机identity再role=company执行/验证。旧第十批home record不改，精确sealed V1可显式验证。
- 6张SHA匹配授权开发照片，PortraA、最长1200、每图r17/r37全行whole/stream0差，输入finite，live0。新独立NaturalCoverageAdmission/ProxyAdmission/NaturalPhotographyStream合同及symbol精确source+proxyPixel+shape，原Canon守卫byte-prefix及实际拒绝新源都保留。
- 已看全部前后与12同坐标局部；发现CanonR6阴影蓝灰底/细节方差压缩（Y标准差比0.132）及NEX5R11,562个无旧端点足迹的新output0像素。未改算法、没以胶片风格批准。有限肤色仅shade-lit手臂，高光旧前端满值干扰，色准/偏好/独立holdout/物理拟合NOT_RUN。

本批权威HOME_ROUND11_REPORT、CAPABILITIES_HOME_ROUND11_V1、REPRODUCE_HOME_ROUND11、LICENSE_HOME_ROUND11、home-round11。私人照片/RAW/大buffer只N盘round11。下一最有价值任务：公司实际跑公开包定位跨设备首stage；另列POLICY对照后定位固定PortraA暗部/新增端点问题，不直接调曲线。技术与分发许可独立，不注册产品。

---

# 当前检查点：家庭第十批（2026-10-30，Europe/Berlin）

接续575b0b1614010c20bcbd9bf38ac2356d6ac7467f，远端无新增。只完成最终caller一次20MP收尾＋公开合成跨设备包。**固定PortraA流式研究阶段最终caller合同已通过并冻结**，通用胶片/产品/许可仍OPEN。

1. 唯一full新目录round10-full-final-caller-once；调用前freezeSHA968f46...a09e6、child核验；最终成功组提交OS/sample峰值380588032<512MiB，device43848656<256MiB、staging25523424<64MiB，live0/negative删除。receipt固定终点SUCCESS_GROUP_COMMIT，报告发布后一次OS峰值检查，之后不追加新范围。旧第九批AUDITED_STATUS/输出保持不动。
2. saved audit新进程全行hash exact、renderCalls0。重复Ctrl-Break和受控INT/INT/BREAK小图native−7/join/live0；caller链成功后未改。
3. 公司/家庭99resolved、输入、全部resource/SPIRV和记录工具exe hash一致，native算法仅授权路径根差异；GPU/driver/运行时及完整编译命令仍混杂，不从自然hash猜首差。
4. tools/cross-device-diagnostic公开fixture/whole/stream/BT/12+36stage样本及小RGBA16已家庭执行并入Git。natural入口拒绝synthetic；跨设备比较器给first/count/max/mean/分位数，无事后阈值。公司执行NOT_RUN，下一步公司设备合法运行包后逐阶段定位即可；不是等私人照片才有工作。

D80014 V2/BT家庭补丁/WB/曲线/桥本轮冻结，不重复大RAW/全部回归；UI/项目/任务桥/default/PV4/5/6没改。实际执行与许可单列。权威HOME_ROUND10_REPORT.md、STREAMING_HOME_CAPABILITIES_V1.md顶部、REPRODUCE_HOME_ROUND10.md、LICENSE_HOME_ROUND10.md、home-round10/，旧记录以下保留。

---
# 当前家庭接续：公司第八批→家庭第九批（2026-10-30，Europe/Berlin）

独立家庭树/分支research/color-engine-home-20261030，基点0f3204c09b15e041c8e70cf95ddbee789fcf7cec。旧research/color-engine-20261029及像素/外部source/build/resources不覆盖。

已完成same-source B7D358...33401生产/桥输入恢复（全hash相同）、家庭GPU small whole/stream0差、native/live0/取消/失败/顺序/height门禁、一次20MP输出与完整raw/TIFF字节audit。379.4MB最后报告前host峰值通过，但publication后OS峰值未取样；原输出已AUDITED_STATUS明确降级，最终hardened helper小图/故障通过，未第二次full，所以最终full host合同仍PARTIAL。

跨设备hash不同，缺公司private像素，firstDifference/changedCount保留null。家庭whole20MP host下界拒绝不强上。BT原失败复现与兼容补丁必要2回归通过；D80014惰性WB生命周期崩溃已保留并修成值快照，V2完整链/fresh reopen/取消和14合同测试通过。不注册产品或作色准/审美结论。

本批权威：HOME_ROUND9_REPORT.md、STREAMING_HOME_CAPABILITIES_V1.md、REPRODUCE_HOME_ROUND9.md、home-round9/全统计与失败。技术与许可分开。下一步若明确新批准跑一次full，使用最终helper；跨设备逐像素需合法拿公司输出。不要重复闭合调查、扩stock/B/seed/曲线/WB或更改Grok布局。

---
# 当前接续检查点：第三批已落地（2026-10-29）

当前研究状态仍 OPEN，完整结果见 `ROUND3_REPORT.md`，最小后端契约 `BACKEND_CONTRACT_V1.md`，复跑 `REPRODUCE_ROUND3.md`。以下第0号保留为历史，不再代表“尚未运行”的当前状态。

- 矩阵float：共享校验18测试，实际产品Display四路全图门禁通过；独立默认关闭解码工具可在progress中直接生成float，衔接现有面积预览、RGB16导出、研究变体新进程重开，保留负/>1。仅固定D800开发范围，不注册产品默认；无C1/色准/主观偏好结论。
- WB余量：LibRaw highlight=1实跑，归一/参数读回、35480 scale样本预测0差；满值4405→0，但AHD与暗部改变，本批拒绝产品应用，不与矩阵收益合并。
- Spektra：完整固定源码/资源/工具已构建，9组合实际渲染；ABI取消−7、stride/alpha/所有权/释放/重复ledger、空间零值/条带均实际测试。CTest10PASS/1FAIL（BT传递边界）保持失败；商业分发许可未批准，暂缓接入。
- 产品src、UI/Grok布局、旧PV4/5/6和旧recipe未修改；不合main、不强推、不上传RAW/大缓冲/第三方engine资源。详细hash与原始日志见round3。
- 大像素本机根 `N:/pixart/color-engine-local-20261029`，上批stage在Temp保留；均非独立验证/保留组。需要独立场景、人工偏好、资源授权和失败修复时明确阻点。

---
# 色彩引擎第 0 号检查点（2026-10-29）

状态：OPEN / 本批无候选通过。仅只读探针，不改变产品像素或默认流程；不移交算法候选。C1 同源配对 NOT_RUN，色准、审美、跨机一致性 NOT_RUN。

## 基线和隔离
- 2026-10-29 fetch 后，公司 `origin/company/r54-closure-20261029` = `f8beaa37977e2b76b2096fa51f3a716a45f33274`；家用 `origin/home/r54-darkroom-20261028` = `ec71ed406c96f1b547091ca2fd66b92f95f062f5`，两侧独有提交分别 8/9，非线性关系。当前从公司 HEAD 建树，未合并家用；家用涉及渲染图和 RAW 输出等源码，不能当成相同版本。
- 调查来源 `origin/codex/raw-c1-feasibility-20261027` = `dbd19d66cebd043437c23433d2b1deb30dd78306`，仅历史证据；本轮已重新读当前源码和测试。未修改其他工作树。
- 已读 AGENTS、HANDOFF、open-source-first POLICY；原有 D800 为明确列名的本地兼容测试素材，文件哈希核验。未扫描其他私人目录。

## 本批限定的三个假设与结果
1. **现有整数前端可能压掉传感器未饱和的信息**：源码确定有截断操作；当前样本输出饱和位置不能定位原因，真实同条件前后对比 NOT_RUN，假设未验证。一次实际基线见 `baseline-d800.json`。
2. **不改变解包，后处理 float 矩阵可以保留矩阵越界信息**：预期只对矩阵之前尚存的值有效，无法挽救 WB 阶段已截断数据；尚无正确提取同一去马赛克输出的已验证接口与数值对照，NOT_RUN，未实现。
3. **保持传感器解包后在 float 进行黑位/WB/去马赛克可保留更多数据**：需确认各格式 CFA、边缘、相机白电平、去马赛克和 WB 参数同条件及许可证；NOT_RUN，未实现。不得将这些假设表述为高光可恢复。

## 当前产品链数值域（源码核验，不是每阶段实测）
`ProfessionalRawDecoder` 在 Unpack 前设置 `CONVERTFLOAT_TO_INT`，限制三色 Bayer，1 GiB 估算及 master 512 MiB预算；以 camera multipliers 设 user_mul、关闭自动/相机WB覆盖，AHD、ProPhoto、gamma 1/1、16bit、关闭自动亮度、highlight 0。LibRaw 0.21.1 `dcraw_process.cpp` 约 82/116/235 行依次黑位、缩放、后续插值及矩阵；`postprocessing_utils.cpp` 的 `scale_colors_loop` 对含黑位/增益值写回 uint16 时 CLIP，`convert_to_rgb_loop` 对 float 矩阵结果转整数再 CLIP。去马赛克工作缓存为 uint16，具体每一步及不同格式仍需单独证明。`MakeDcrawMemoryImage` 得 RGB16，随后 `/65535f`，float 容器不能撤销量化和截断。最终 RGB16 满值只是输出满值，不证明传感器饱和或可恢复；负/大于1可来自工作空间转换，不能推断传感器值。

LibRaw 类型 `libraw_types.h` 955-965 存在 float 原始图指针，但取决于输入/解码器，不代表常规整数 RAW 的完整 float 去马赛克输出；当前配置还要求 float-to-int。`libraw_processed_image_t` 暴露 bits/data 而非通用 float 管线。尚未验证通过现有 Sdcb 接口可得到所需端到端 float 数据，不能以字段存在宣称已解决。

当前 `RawWideOutputV1` 是默认关闭的既有 RGB16 后 LCMS 转换，无法恢复前端裁切；并行 `StudioGraphRenderer` 要求显示 sRGB，不可把新线性宽域直接冒充节点输入；宝丽来依赖独立定义，不用于判定基础 RAW 效果。LCMS 接口与 RAW wide 测试已查，现有默认 PV4/5/6 未改。

## 两个固定源码参考及许可证
- LibRaw 0.21.1 `cccb97647fcee56801fa68231fa8a38aa8b52ef7`，`src/postprocessing/dcraw_process.cpp`, `src/postprocessing/postprocessing_utils.cpp`, `libraw/libraw_types.h`；源码 SHA256 分别 `81EFB3BAB8631285123414F5E6DC2DE62069E2340BF54E7DAE41A55D7540C540`、`554D6A358915866B5FBC5BDDA8682F81B01F9619F522AEA7473EBDBBB5B32701`、`D83C7B9C7A8EA035B12F1D7B70F8ED686BAE360DA5B56C3FDF48DAC397B6402D`。项目现有运行时标注 LGPL-2.1-only OR CDDL-1.0；具体修改/再发布义务须独立审阅。默认参数不是所有文件/所有格式的保证。
- RawTherapee `db575c6690d30ce6428c0e1383bec8b8f1fb8d21`，`rtengine/rawimagesource.cc` SHA256 `65E06C3F795E67C16B0354B3F132EC1CF68BA25334F6BEDEF653F5EA01B1DE89`，`scaleColors` 约 2742 行用 float CFA、减黑位后下限0并按通道缩放；约 3550 行路径包含允许负数和超范围的工作空间变换，依赖此前处理和配置，不能简单套用公式。根 LICENSE 为 GPLv3，文件头及发布义务需完整审核；仅研究未复制代码。参考数值程序未安装/执行，不能算通过对照。

## 实测与限制
- 已列名 D800 素材 SHA256 `3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5`，解码前后 hash 相同。输出 7378×4924，RGB 各通道零 [0,0,22]、满值 [641,940,5046]，每通道 36,329,272 样本；这些是最终输出统计，不是 sensor 饱和。白位 4095，黑位读回 0，WB 505/256/352/256。单次 1739.2465 ms（包括解码器再次 hash），进程峰值工作集 1,062,768,640 字节（包含探针复制，不是算法增量），均非性能基准。
- 执行 `dotnet test ... --filter FullyQualifiedName~ProfessionalRawPipelineTests|FullyQualifiedName~RawWideOutputTests|FullyQualifiedName~Pv7PrototypeTests`：13 PASS，0 FAIL，0 SKIP；TRX 附后。它不等于完整 PV4/5/6 golden、真实 RAW 或预览/重开/导出门禁。
- 复现：`dotnet run --project tools/color-engine-baseline/ColorBaseline.csproj -- <明确授权RAW路径> <SHA256>`；只输出 stdout，校验 hash 前后，不保存或修改原片。测试使用 .NET SDK 10.0.401；实际时间与内存随机器及其他负载变化。

## 数据集与门禁
D800 单样本此前用于探索，**仅开发组**，场景身份未知；不可用为保留组。现有授权清单有型号、hash，但未完成逐场景审查、同源关联、授权范围和内容分区，不能把型号/哈希硬分成独立场景。验证/保留组均 NOT_ESTABLISHED；肤色、皮肤高光、中性、红橙紫、蓝绿、暗部、逆光、不同光照覆盖均 UNVERIFIED。参考色卡、C1 同源导出、独立参考运行、逐阶段观测、自然图差异、候选耗时/内存、PV4/5/6 golden、保存重开/预览/导出均 NOT_RUN。不可用合成数据冒充照片覆盖；无配对参考不报真实色差。

## 下一步
基于明确素材清单确定可追溯独立场景并冻结分组；选开发组查阅传感器真实白位和阶段前后值，搭建隔离且版本固定的参考执行链，确认格式/参数/白点；补第二条独立实现的可运行同域对照，再按政策填写节点完整模板。缺少真实阶段观测或许可结论时保持 OPEN，不改默认产品，不提交未经门禁批准的像素算法。
