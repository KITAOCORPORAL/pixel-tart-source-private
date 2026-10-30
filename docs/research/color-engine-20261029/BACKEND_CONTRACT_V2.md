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
