# X-Trans稳定输入V1：最小产品接入评估提案

状态：PROPOSAL_ONLY / NOT_ADMITTED。生命周期LC-B0/B1/B2的批准不含本提案。本轮没有build候选、加载原生库、解码、替换发行DLL、写新账本或解除STOP。

## 真实已过范围与后继失败

53e2355d的同一Fuji样本（7752×5184，RAW SHA52D054ED…E865）两进程20220/9948在无调试器、真实parallel6/不同worker归属下，P5和serial有效状态一致，P6全部120559104个RGB通道样本逐位相同。P6共同SHA9266ca59…df024。它证明本构建、本参数、本输入的重复性，不证明跨相机稳定或摄影准确。

相对两个已存旧P6仍有197896/197705个像素不同，max870U16，最大点(2501,792,G)，约98.4%差异在几何seam带，但带外3169/3167个像素也变，最大差在带外。旧不稳定输出不是独立真值。最终MakeMemoryRGB16经中性产品渲染后，五处裁片8位最大通道差可达32、成片RGB16可达8107；该域不能与P6的870混算。

后继820b4f0d对a24隔离产品诊断：一次A-cold导入完成、worker exit0，约9992.9774ms，随后父应用超过2GiB并STOP。没有save/reopen/cache/rebuild/实际TIFF/其他RAW通过。现e818 TIFF/sRGB/cache成果和433生命周期修复不能把这个旧STOP改标通过。

当前主集成源e818（文档f665）；后继生命周期功能433/文档9dd7已单独发布但最终普通EXE仍PARTIAL。色彩核对f665→433无差。a24适配器/compile flag不在当前产品源码；不能仅说“开一个现成开关”。

## 原生、布局和依赖差异

|层|当前发行/产品|冻结候选与本轮结论|
|---|---|---|
|默认原生|raw_r.dll SHA F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9；NuGet runtime0.21.1|候选SHA157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F，单独runtime目录；默认文件本轮只读hash仍F500|
|托管绑定|Sdcb.LibRaw0.21.1.7；当前SHA6DF69864…D4A3|已测宿主使用同一binding SHA，但同绑定不自动证明所有ABI/调用模式正确；全结构/导出与依赖身份须随新方案冻结|
|原生源|LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7|稳定serial准备后原image4只读，新增独立RGB3，parallel join后commit，再原border8；原公式/循环保留但依赖/数值结果有意改变，不能说原字节恢复|
|内存布局|LibRaw内部image为4×U16，MakeMemoryImage为3通道RGB48|P5/image4不能直接当RGB48；P6为内部相机RGB域，最终MakeMemoryRGB16还经过WB/矩阵/传递等。外部布局必须W×H×3×2、stride W×6、小端U16、bits16、typeBitmap、3channels和长度/hash一并检查|
|额外分配|默认库原image4及各worker scratch|候选额外6bytes/pixel，Fuji241118208bytes≈229.95MiB加allocator；不是OS峰值增量140.70–141.10MiB。原memmgr不是完整进程预算|
|运行依赖|产品自包含框架与默认VC运行时|已测候选使用MSVCP140/VCRUNTIME140/VCRUNTIME140_1/VCOMP140，.NET10.0.10宿主；旧模块hash已存。新冻结前重核依赖，不从一次环境推断其他机器可用|

0.22.2 ReviewedLibRawCandidate适配器即使在当前源码存在，也是不同合同（6A459…等库、显式gamma2.4/toe12.92），不是本157BBA稳定输入候选，不能混借准入。

## 输出域与参数是首要接入门槛

已测完整configured-output使用UseCameraWb=true、UseAutoWb=false、UseCameraMatrix=true、OutputColor=1、OutputBps=16、Gamma[.45,4.5,…]、NoAutoBright=false、UserFlip0等。全字段快照CA1A7408…B5FFA已复用，不只对比少量常见参数。

- 这对应旧LibRawDecoder ProfessionalDecode RGB48路径；普通8位输出不等于同一数值合同。OutputColor=1只说明该套设置，Gamma=.45/4.5必须按实际记录，不借一个sRGB标签宣称与精确ICC sRGB或PV4数值相同。本轮不更改既有gamma/WB/标记作为补偿。
- PV4高精度ProfessionalRawDecoder不同：camera/autoWB flags均false，UserMultipliers显式应用；ProPhotoRgb、Gamma[1,1]、NoAutoBright=true、AdjustMaximumThr0等。其float master来自U16，合同明确DecoderClipped=true；“linear”不表示保留了所有传感器超白。
- 初始最小评估先限已测legacy RGB48路径，不自动支持PV4/PV5/PV6、RawWideOutput、文档ICC或0222适配器。若目标是高精度RAW，则必须另验证该参数域和颜色处理，不能把现有P6/最终sRGB16直接贴成linear ProPhoto。

## 可复用a24的部分与不能整段合并的部分

可审查复用独立worker、hash/ABI/once-call守卫、cache原子写/污染拒绝、明确诊断compile+环境双门。a24早于最新session cache转移、WorkflowProjectId、sRGB恢复、隐藏预览和433 lifecycle/发布Outcome，不能整提交cherry-pick后覆盖新的Session/Reference VM。

|最小触及范围（待审）|必要合同/跨线归属|
|---|---|
|独立Services/Diagnostics decoder facade、cache与worker project|从冻结a24/53行为小步移植，worker使用自己的candidate runtime；父进程默认raw_r不得共加载/替换。研究线本轮只提案，产品实现另分配写入权。|
|RAWSelectionAssistant.csproj/诊断包配置|显式诊断构建+明确opt-in/清单，默认正式构建关停；只设置env不能启用。worker/native不随正式包夹带。|
|ReferenceColorWorkspaceViewModel解码入口与FrozenRawMaster元数据|每目标冻结decoder/profile身份，与sourceSHA/完整options一起选择；不要用global env替代配方。与LC-B2共享文件，需在最新产品代码后由单一owner集成。|
|Session/Raw definition/缓存key合同|旧recipe缺字段=原default，candidate需要明确新版本/受验证discriminant。旧reader必须拒绝新定义，不能忽略未知可选字段后偷偷用default。允许读旧格式，不暗迁。|
|preview/detail/match/export/publishing冻结|全部消费同一candidate master/身份，禁止预览默认、导出候选，或sourceHash相同就复用旧decoder缓存。保存重开仍选同profile，开启/关闭产生独立缓存。|
|资源/取消/失败|复用已批准生命周期所有权契约；只等待/终止自己启动的worker句柄，不按进程名结束他人。失败保留上一有效结果/工作文件，STOP且不自动换default成功。|

当前FrozenRawMaster含SourceSha/Generation但没有本候选profile；路径或SourceHash单独不足以区分同RAW两种像素结果。建议缓存至少绑定source内容hash、profile/算法版本、candidate native/binding身份、完整options、布局/方向和输出hash。不要只加一个CandidateEnabled布尔量。

## 默认、取消和回退

- Legacy/旧recipe/非目标格式按原default走，不能默认将所有RAF或其他RAW切候选；native header filters9/colors3与冻结输入身份同时成立才属于目标。
- 显式candidate定义缺DLL、hash漂移、无票据、坏缓存、版本未知时先拒绝，保留当前结果；不能静默fallback成不同颜色。用户若要切回默认，应显式创建/选择另一版本，不覆盖原candidate定义。
- token是请求，不是工作完成。原直接P/Invoke阶段间检查不会立即中断native；隔离worker取消/超时只能处理已证明自己拥有的child，退出后等待并保留回执。取消/失败发生在已reserved/entered的重型尝试仍消费，停止本批，无自动退款/重试。
- 不自动用嵌入JPEG或default view-proxy证明candidate预览一致。初始candidate图像预览都从同一个冻结RGB48来；临时嵌入预览若展示必须标为尚未应用当前定义，不作为验收。
- RGB3缓冲、cache读回、父端U16→float/bitmap、原生memoryimage、预览和导出复制可同时存活。旧候选native约771MiB不代表父应用预算通过；先做零RAW的已存结果资源路径检查，再申请冷开。

## 验收矩阵

详见XTRANS_ACCEPTANCE_MATRIX.json，所有新产品项默认NOT_RUN。至少包括：关停default字节/非XTrans隔离、坏ABI/布局/依赖拒绝、cache污染/跨profile拒绝、取消与无效输出不发布、旧recipe不变/新版本旧reader拒绝、同master fit/100%/缩放/编辑/保存关闭/独立重开/实际TIFF及全行/位深/ICC、cold与强制rebuild一致、源文件变化保留旧结果、非XTrans对照、内存与时限、更多XTrans相机/场景/光照与独立摄影评审。

现有两次native0差和61passed/1NotExecuted无RAW套件只按原scope引用，不重跑也不冒充新433兼容。当前五组固定裁片只支持有限局部观察；缺完整预览、脸部/服装/金属/低光/饱和边缘及另一XTrans相机。

## 预算与停止点

预算仅草案，授权0/执行0/不建新ledger。旧company失败批1消费、timerfix2/2、product回归1/6及所有STOP保留，数字余额不等于可执行额度。新阶段0是零RAW静态/受控cached-RGB读回；若用户另行选择实现并批准，第1阶段上限2次（同一A在产品cold与显式rebuild），cache/保存重开/导出不应再解码。扩展B与非XTrans、独立参考另分批提案，不能把已有两次原生成功再跑一次充新实验。详见XTRANS_BUDGET_DRAFT.json。

本轮停在方案。原图库/候选DLL/依赖/源码与结果仍在原本地隔离目录，发行DLL未换，候选默认关闭，未建立新的重型RAW批次。
