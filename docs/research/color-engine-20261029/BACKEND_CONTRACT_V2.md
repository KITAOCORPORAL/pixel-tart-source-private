# 后端研究契约 V2（2026-10-30，公司接续）

状态 **OPEN_RESEARCH_ONLY / PRODUCT_DISABLED**。本文件补充并收紧V1，不改变PV4/5/6、既有8位胶片适配、灯箱UI、项目模型或任务桥。V1中第三批家庭实测保留；公司不借用家庭设备通过或性能。技术门禁与许可门禁分开。

## MatrixDecodeResearchV2 / BaselineResearchV2

- 默认关闭，只存在`tools/color-research-decoder`。仍是固定LibRaw0.21.1 x64、Sdcb0.21.1.7及两个D800 SHA白名单；原始7424×4924、输出7378×4924、pitch14848、零margin、Flip0、最终CFA2492765332、black0、指定white与RGBG ABI守卫保留。未扩展其它布局。
- 原生WB/去马赛克仍uint16；matrix候选只在矩阵前回调生成float32独立数组，native流程随后完成。不能恢复上游WB裁切，不是完整float demosaic，不宣称C1色准。
- 输出master：RGB little-endian IEEE754 float32，LibRaw特定ProPhoto D65线性（不是标准ProPhoto D50 ICC），标称1对应65535，允许负与>1，alpha不存在，方向1。预览/显示sRGB；TIFF16是实际产品Display后RGB16，不是HDR浮点。
- 新显式定义的`algorithmIdentity`包含：所有有效OutputParams（含native处理后Gamma派生项）、完整显式unpack设置、camera/applied/scaled WB、black/white、方向/布局、RgbCam及最终9个matrix系数、ProPhoto系数、矩阵/研究实现版本、实现及依赖源文件hash、输出目录全部managed/native DLL hash、运行框架/架构与CoreLib hash、preview edge800/EV−2、输出域与arithmetic说明。研究assembly/core assembly均在依赖清单。实际观测先与冻结定义比对，未知或缺失关键字段拒绝，不在重开时猜默认。当前严格二进制/source身份：换构建需保留对应工具环境，不静默重封旧recipe。
- 保存：recipe.json + master.f32 + preview + report；重开在新进程核验定义、尺寸、全float长度/完整SHA、加载后再次SHA、所有通道finite，然后调用同产品RenderPreview、FillRgb16Row、TiffExport并比对旧hash。重开不再解码源。新SyntheticReopenFixtureV1为8×4独立测试版本，只测同完整域/负值/>1/reopen路径，绝不能当D800实图。
- V1读取规则：只读已保存浮点archive；保持MatrixDecodeResearchV1/WbHeadroomResearchV1及其旧mode配对规则，要求原字段与rawRecovery=false、禁止未知/重复字段，检验既有固定源/backend/domain/尺寸/hash/finite。不把缺算法身份的V1叫完整可重建合同；禁止从它重新解码或升级成V2。没有旧master则不伪补。其新进程自然图重开本机缺旧缓冲未运行。
- baseline所有权：不再取得backing array后Dispose并继续借用。必须在Image仍有效时ToArray，随后Dispose；池复用回归实际覆盖GraphPixelAllocation同数组复用、污染原数组而owned-copy不变、disposed访问拒绝。当前decoder自己new数组不代表通用image合同不会回池。
- 取消/失败：Ctrl+C设置本进程token；native回调每次/矩阵逐行检查，异常不跨native边界，保存callbackFailure并返回非零；DcrawProcess异常优先还原取消/回调错误；finally解除native handler再清managed handler。安装、body、解除各异常均测试clear必执行。Unpack仅前后检查（不是可抢占），baseline沿原decoder取消粒度；preview传token，TIFF逐行检查。文件可能保留未完成目录，失败无report通过标志、禁止覆盖重跑；不自动删除证据。原生取消/异常发生在真实D800解码中的验收因缺源NOT_RUN；helper测试不能替代。
- WB余量：旧WbHeadroomResearchV1保留，V2只增加合同/生命周期，不调参、不重跑自然图、不与matrix合并收益；第三批真实差异及拒绝产品应用结论不变。

## SpektraWindowsResearchV1 + 隔离SpektraBtJoinFloatV1

- 上游固定`c1447c6ab618fa4b1cb11fd2fe12826855007059`。原始source2始终clean，补丁/trace在另两个外部研究checkout。DLL/SPIRV/source/resources/tool hash分别见company-round4清单，不能用commit不变冒充patched bytes未变。
- 公司GTX1650/572.70/Vulkan1.4.303：原版及补丁常规CTest11/11；原版`-Os` BT pointwise真实失败、补丁同条件通过。家庭RTX5060Ti原版10/11与9组实际渲染保留，不倒写未构建，也不把公司通过抹掉家庭失败。家庭补丁复验NOT_RUN。
- 已通过（公司限定配置）：关闭grain/glare/halation/DIR、autoExposure=false的三输入×三胶片9小图，重复bit-exact、resolved recipe新进程重开bit-exact、TIFF16读回bit-exact；原/补丁9组一致。输入为合成linear ProPhoto D50与显示sRGB，不是自然图验证集。BT断点probe单列，不能因9组不走BT而宣称修复未改像素。
- ABI输入packed RGB/RGBA float32；`spk_open`复制输入。线性域桥接沿V1完整D65→D50约定，显示sRGB只能称创意模拟；不重复WB。任意grain_seed **UNSUPPORTED**：公司直接发送42被拒绝且session不变。固定初始rng_state不是用户seed；未扩展seed版本。
- 输出`spk_result.rgba16`：结果自身拥有的CPU RGBA16，row_stride_px以像素计，逐行访问width有效区域；to_rgba16 clamp[0,1]后*65535+0.5量化。输出sRGB D65（在此配方），非HDR float；输入alpha丢弃，结果alpha65535，公司实测.125→opaque。产品透明输入须拒绝或显式独立mask，不能隐含保留。
- 创建/释放：engine_create→open→render→逐行复制→result_free；session_release→engine_destroy。result可活过session/engine且仍须result_free；公司5项生命周期及ledger测试通过。out_json caller-owned用spk_string_free，engine-owned schema/capabilities等不可free。
- 取消：另线程spk_cancel，节点边界返回−7，不是GPU shader抢占；公司原版/补丁均真实render中−7，等线程join后才释放session。不要同时set_params/render/release同session。研究调用保守串行，一个engine worker；更广多engine/多session并发能力未批准。spk_last_error为thread-local，失败线程及时读。
- 条带：公司原/补丁no_halation/no_couplers，rows1/7/17对whole/reprint逐像素一致；只覆盖报告参数，不推广随机grain/glare或任意尺寸规则。未实施seed扩展，故无seed条带合同。
- Portra/Ektar中性log渐变下降单列：公司各11/7个通道邻接−1code；trace输出与原版bit-exact，log_exposure上升而film_curves_exact阶段已下降，最后float也下降，所以不是只有量化或BT修复造成。尚未定位具体profile/曲线拟合误差和视觉影响，不改阈值、不称已证明肉眼缺陷。TriX neutral_log本次0下降，但display-sRGB配方亦有下降，见完整数值。

## 产品最小评估调用与门禁

研究入口：`company_matrix.py <pinned-checkout> <build> <isolated-resources> <new-output>`及`company_abi_probes.py`。前者给全resolved params、输入/DLL/resources hash、新进程重开及TIFF16；不能拿`run_matrix.py`里家庭DLL固定hash直接套公司新二进制。

最小C ABI顺序：create(resources,NULL)，构造spk_image{float*,w,h,3}，open(engine,image,完整params,&out_json)，复制并string_free(out_json)，render(session,"full",&result)，依stride复制RGBA16，result_free，session_release，engine_destroy。每一步检查错误；成功/异常/取消都做各自所有权释放。产品调用者必须另行版本化输入域/方向/alpha、完整resolved recipe、backend/resource集合及确定性策略。

尚缺：授权且场景明确的自然开发/验证划分、D80014候选全链、家庭补丁/跨Metal oracle、完整性能/尺寸空间效应矩阵、产品项目与灯箱集成验收、GPL代码组合分发批准、逐资源权利批准。全部继续禁用；独立进程、DLL或烘焙LUT不自动解决许可。不得整分支合入产品。

## 2026-10-30 第五批：R5.5能力增补（不升级既有算法）

产品评估名称仅 **“负片/印相模拟·研究中”**，研究默认关闭，不注册Spektra、不改UI或任务桥、不扩产品输入范围。机器可读清单见`company-round5/R55_CAPABILITIES.json`。

- neutral下降已补根因：Portra/Ektar原`density_curves`采样表本身有局部负斜率，减nanmin+正确分段线性插值会保留；两点原profile邻域、NumPy/SciPy固定参考与真实shader复现已完成。CDF模型及layer和单调，但当前grain关闭film路径不使用它们替换sample表；本轮**无曲线修复版本/资源修改**。不平滑/钳制，不以1code声称可见或物理色准失败。
- BT追加**完整受支持引擎路径证据**：外部研究显式`input_color_space="ITU-R BT.709"/"ITU-R BT.2020"`与`input_cctf_decoding=true`，关闭grain/glare/halation/DIR/autoExposure，原/补丁/各decode-Os四条件。断点图/渐变/负值/>1/饱和mixed RGB/高度1与7共64个小图render，均RGBA16保存、99字段resolved参数及input/backend/resources hash、新进程完整render重开与TIFF16回读通过。它们是研究输入，不扩产品适配承诺。
- 公司普通原/补丁一致；受控-Os断点图只在实际decode输入bits`0x3da66554`改变，final RGBA16 Portra最大9、Ektar最大4code。源码固定scanner unsharp默认{sigma0.7pixel,amount0.7}仍在链内，它不是grain/glare；被动trace显示差异在`print_scan_finish`扩散到邻列，最终量化只留下部分像素。gradient实际decode亦有差异但RGBA16量化后本组0差异，不能称未经过BT。
- 兼容断点`0x3da66555`不等于double rounded常数/连续规范约定；继续沿BT_NODE_COMPARISON_ROUND4语义边界。家庭RTX5060Ti补丁仍NOT_RUN，不伪报跨设备通过。
- 任意grain_seed仍UNSUPPORTED，不扩seed；alpha仍丢弃、输出opaque；RGBA16最终clamp不是HDR；spk_cancel在节点边界、释放须等render结束；result独立所有权与stride规则、串行session限制沿本契约。不能从测试确定性配方推广随机空间效果/任意尺寸稳定。
- D80014沿第四批NOT_RUN_SOURCE_ABSENT，本轮未扫描/重试；V2身份/所有权/回调修复保留而不重复整轮。WB余量冻结，无新参数/收益合并。LightTable非商业条款与Spektra GPL代码及资源权利是**不同事项**，任何一项都不能替代另项准入；DLL/进程/LUT也非自动许可解决。

有界根因与BT全链数据见COMPANY_ROUND5_REPORT、CURVE_NODE_COMPARISON_ROUND5与REPRODUCE_ROUND5。产品src/旧PV4/5/6、旧recipe全部保持。


## 第六批：授权自然图的隔离实用评估增补

最新R5.5能力清单为`company-round6/R55_CAPABILITIES.json`（ResearchCapabilitiesR55V2），第五批清单作为历史不改。产品仍“负片/印相模拟·研究中”，默认关闭、不注册、不扩产品输入范围；本批未改UI/模型/任务桥/默认处理链。

- 只使用用户明确授权的CompanyRAW根：1张当前生产decoder可解码的Canon EOS R6暮色逆光人物剪影，5496×3670，开发样本而非保留验证集。人物存在但无可用肤色照明、没有强高光stress覆盖。其它授权项的打开失败/预算拒绝/metadata-only单列，不更换解码器或放宽MatrixDecodeResearchV2 D800白名单。
- A：真实生产LibRawLinearProPhotoD65 master（已有uint16WB/demosaic/matrix裁切保留），按既定桥接D65→BradfordD50→engine标准ProPhotoD50；无额外WB、不做Display。B：同master通过实际生产RenderOutputPixel EV0 Display一次所得sRGB，作为创意输入由engine解transfer一次。B不是原生RAW胶片模拟，A/B不合算收益，不拿输入域差当色准误差。
- A桥接在黑暗近零处出现最小−2.45e-6小负值，全部对应原同通道<1e-4；既有舍入矩阵链结果保留，不夹成0，也不称高光恢复。独立Colour Bradford检查与桥矩阵差≤1.08e-17，4源坐标写出核对exact。实际生产master无>1/1满值，输入裁切能力不由这张暗照片证明。
- 6代理（1200×801，明确BOX平均）+24原始采样640×640同坐标窗口完成，另8重复和8新进程重开，成功调用TIFF16均全像素回读一致。原始采样窗口只证明局部输入，不包含全画面光谱/空间上下文，不称全图crop验收。图像只在授权本地研究目录，Git仅报告/数值/hash。
- 六个5496×3670全分辨率render在上传前被当前GPU预算拒绝：保守估算1,633,607,616bytes，安全余量后允许883,949,568–892,338,176bytes；无静默降尺寸。不是宣称该设备/引擎永远不支持，也未执行/验收全分辨率条带省内存方案。strip_budget_bytes本固定源码不自动选择rows，产品不能当现成预算控制器。
- 三胶片沿固定相纸，grain/glare/halation/DIR/autoExposure关闭；完整99字段resolved、隐藏scanner unsharp{0.7full-tier pixel,0.7amount}、DLL/resource/input hash记录。该native窗口unsharp提高边缘/噪声对比，前后线性RGB最大差A0.00863/B0.00841，本点集无新负/>1溢出；不拿截图证明物理拟合或偏好。
- 任意grain_seed仍UNSUPPORTED、alpha丢弃/opaque、结果CPU RGBA16最终clamp非HDR、取消节点粒度、复制后result_free及独立所有权/stride/串行session限制均保持。没有seed扩展或随机效果尺寸保证。
- 代理时序只适用于这张公司场景/尺寸/参数：init138–163ms，open/upload275–294ms，render+readback87–92ms；RSS峰值约147–158MiB，engine分配ledger约59–70MiB（不是物理VRAM峰值）；不外推到全分辨率/家庭GPU。内部upload/compute/readback分离不由当前C ABI提供。
- 代码与逐资源分发许可仍未批准；LightTable非商业条款不是Spektra许可。曲线原因/BT64组不重跑；D80014缺源、家庭补丁、自然肤色/高光/独立验证及全分辨率门禁仍开放；WB余量冻结。

## 第七批：安全输入准入及full阻点（不改变输入数学/产品链）

最新能力快照为`company-round7/R55_CAPABILITIES.json`（V3）；此前第六批1开发场景/30独立配置，不是30场景或full输出已过。旧结果及失败保持不重贴标签。

- `NaturalAdmissionManifestV2`首次case也必须绑定准备manifest及其SHA：source/production报告、具体文件SHA+pixelSHA、shape/dtype/length、A/B route、方向/域、桥版本/系数、backend和完整resource列表。不是先接受任意同shape NPY后再自封重开基线。旧round6只有显式 frozen-evidence sealer可核对后绑定，不自动迁移。
- 先校验NPY头/shape/dtype/尺寸溢出/确切长度、CPU预算，再32行块hash/finite/stat，之后backend/resources和GPU准入；拒绝不得创建engine或上传，无成功标记。24针对性控制含篡改/互换/非有限/缺失重复/预算/异常清理。失败atomic report保留primary error，cleanup errors单列。
- 输出只允许E:/Kitao-Research下新路径，禁止CompanyRAW重叠/已有证据/reparse；NaturalInput输出验证与取消逐行检查。session/result/engine cleanup尝试完整，render取消先cancel并join再release，不因cleanup失败吞原异常。
- 准备32行块、B写出分块；代理为保留原Pillow BOX数学只保留1个CPU已预算channel clone，不暗换缩图算法。统计/重开避免整图bool、percentile/复制，TIFF逐行写/segments回读，但**public ABI内部仍完整CPU结果**。
- 完整桥补Colour/OCIO实际入口、blob实际系数、各阶段/组合/中性原色饱和负/>1/自然样本；double负数量也为[0,942981,590359]，不clamp或换矩阵。新有界准备全部10文件hash与旧一致。详细语义/参考限制见BRIDGE_COMPARISON_ROUND7。
- 固定源码Vulkan file_backed=普通persistent DEVICE_LOCAL，striped仍完整source/negative、assembledplanes、RGB/RGBA16与CPU结果；strip_budget_bytes忽略不控rows，不能称低内存流式能力。allocation-ledger独立外部诊断库测8smallruns并释放归0；whole/stripe5配对RGBA16 exact，默认scanner unsharp/首尾/不整除/完整画面尺度保持。
- 当前5496×3670必要device-local下界56n=1,129,537,920bytes，超过本次安全准入1,095,761,920bytes；实测whole~72n、stripe更高。**一次可行性决策后full engine/open/render全0次**；未加cap、没关其它程序、不降尺寸、不改曝光/隐藏效果。没有本轮20MP输出/回读，明确BLOCKED_CURRENT_ABI_BUDGET。
- 最小外部`SpektraStreamingResearchV1`方案只为PROPOSED_NOT_IMPLEMENTED，不是上游或产品能力，无新streaming源码/二进制/像素通过声明。任何实现须新版本/源码与DLLhash/小whole输出exact/halo与frame/global参数/取消释放及full逐行结果再验。

任意grain_seed仍不支持、alpha丢弃opaque、RGBA16最终clamp非HDR、取消节点粒度、result独立所有权与stride/串行session限制不变。曲线/BT已闭合不重跑，WB冻结，D80014/家庭GPU/有效肤色强高光缺口保留。UI/模型/任务桥/默认链不改，仍“负片/印相模拟·研究中”、不注册。LightTable非商业条款不替代Spektra代码/逐资源许可，技术隔离验证继续、产品分发未批准。

## 第八批：最小流式接口已实现，full像素完成但host门禁PARTIAL

最新能力快照company-round8/R55_CAPABILITIES.json（V4）。`SpektraStreamingResearchV1`已在新外部固定checkout实现/编译/执行；不是把第七批提案再写一遍，也不是上游或产品注册能力。完整独立研究ABI合同见STREAMING_BACKEND_CONTRACT_V1，源码/DLL/资源身份及8文件generator复现见本批清单。

- 只允许已冻结PortraA、线性ProPhotoD50、方向1/finite负/>1、prepared input/backend/resources hash、完整99resolved及hidden defaults；unknown/effect拒绝。grain/glare/halation/DIR/autoExposure关，defaultscannerunsharp.7/.7保持，不扩B/三stock/seed。
- 不调用spk_open上传fullsource：两pass有界rows、negative临时磁盘store、GPU只strip+原halo2，完整frame长边/pixelpitch保留；直接复用原film/print/Gaussian/reflect/encoding/quantise，consumer只中心行。不内部组装fullGPU/CPUresult。已active全局量为inactive或原host常数，没有strip-local统计代替global。
- rows真受host/device/staging预算，硬Vulkan allocator含所有pooled/persistent/staging及替换旧+新；每strip清空空闲pool。callback借用指针只在回调有效，异常return非0，不跨C；poll取消后join再卸载，nativebusy并发−30，顺序repeat可复用，success/fail/cancel live0/negative删除。
- 10natural小图＋4synthetic负/>1/nearzero＋2sameprocess重复wholeexact，height215/430同width/rows峰值不变；rows4096预算压到26仍exact。没有取消unsharp或放宽差异。
- 同5496×3670只1次native流式，raw/TIFF全像素hash1c4a97a1...48c2a7b8a，device43,848,656/staging25,523,424bytes、live0。没有该尺寸wholeoracle；依据仅小图等价实现＋实际full输出身份/读回。原publicABI预算阻点仍事实，但新研究入口已绕开full常驻。
- 首轮callerpostnativeI/O保留mapping+预读导致RSS659,144,704>host536,870,912：native完成不等于整个预算门禁通过。原report副本/像素保留并审计success=false，raw/TIFF明确重命名.initial-host-overbudget，不能当全门禁PASS产物。
- 修正caller关闭inputmap后有界rowTIFF写/offset回读，跨post阶段hostguard超cap阻止最终rename。对已保存fullraw在独立进程writer+readerexact约38.49MB；独立只读重开exact约36.23MB；**未发起第二次20MPnative**，故修正后组合fullhostgate未闭合，能力仍PARTIAL。不能以分阶段分别通过冒称组合全链已验收。

研究默认关闭，不注册产品、不改UI/项目模型/任务桥/默认/PV4/5/6；alpha丢弃opaque、RGBA16clamp非HDR、任意seed不支持、node/strip取消及所有权限制保持。D80014/家庭GPU/有效肤色强高光缺口保留，BT/曲线/桥不重复，WB冻结。LightTable授权与Spektra代码/逐资源权利独立，隔离实验继续不被商业许可批准阻断，产品分发仍未批准。
