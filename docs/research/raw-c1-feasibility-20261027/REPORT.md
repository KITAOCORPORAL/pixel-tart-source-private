# 多机型 RAW / Capture One 可行性调查

用户本地日期：2026-10-26，America/New_York；调查批次标识：20261027；状态：部分完成，未改生产调色引擎。真实RAW产品路径已测，C1对照未执行。

## 结论

已实证第一道瓶颈是RAW前端准入：X-T5样本被1GiB工作集估算门槛拒绝，尚未进入X-Trans检查。D800两文件可处理，但master已经过uint16量化和截断，负曝光不能恢复前端丢失信号。

另一个确定限制是PV4 RAW显示和导出先收敛到sRGB。TIFF16不等于宽色域。相机表征为LibRaw内建/嵌入矩阵，未接C1相机ICC。没有C1同源TIFF和足够真实场景，不能判断相机配置、明暗映射、输出管理谁是视觉主因。

下一步只建议 RawWideOutputV1 默认关闭研究变体：分离RAW显示与显式宽色域导出。不拟合C1 LUT、不换矩阵、不先重写曲线。明确LibRaw ProPhoto D65输入与常规ProPhoto D50输出的转换，不能将sRGB贴宽域标签称为恢复。尚不能承诺普遍提高观感。X-T5准入应独立处理。

## 仓库与约定

原工作区 N:/pixart/guangjing-r53-stage-b-20261027，分支 home/r53-stage-b-20261027，HEAD 047e51bf7ad156aaf94427cad93dd45380b6fbd1，调查前后clean。

调查worktree为本报告所在仓库，分支 codex/raw-c1-feasibility-20261027，从上述HEAD分叉。起始聊天父Git无HEAD；设计交付目录并非生产源码。未切换原工作区、未清理旧checkout的未跟踪资料。

已读实际工作区AGENTS.md及docs/color-science/open-source-first/POLICY.md；AGENTS本机未纳入Git，快照为evidence/PROJECT_AGENTS_SNAPSHOT.md。保留PV4/5/6历史结果；PV7与synthetic ICC保持研究门禁。

无Browser/WebView/CDP/Computer Use，没开浏览器、资源管理器、Capture One或现有目录库。证据为evidence/environment.json、code-evidence.json；后者列实际源码路径、SHA256及行数。

## 当前色彩链代码核查

下列路径相对于调查仓库根，完整绝对路径及文件hash在evidence/code-evidence.json。

|环节|真实路径/入口|状态与限制|
|---|---|---|
|输入分流|src/RAWSelectionAssistant/ViewModels/ReferenceColorWorkspaceViewModel.ProfessionalRaw.cs:22 DecodeProfessionalInputAsync|已实现PV4 RAW/rendered RGB/legacy RAW多路。|
|旧RAW|src/RAWSelectionAssistant.Core/Services/RawToJpeg/LibRawDecoder.cs:103-107|camera/auto WB、SRGB、professional16bit/其他8bit，不是PV4 master。|
|PV4参数|同RawToJpeg目录ProfessionalRawDecoder.cs:40-55 Configure|已实现显式user multipliers，auto/camera WB flags off、camera matrix on、ProPhoto color4、Gamma1/1、16bit、NoAutoBright、HighlightMode0、AHD；CameraProfile/OutputProfile null。|
|准入|同decoder:68-73|rawWidth×rawHeight×28超过1GiB拒绝；非三色Bayer、Foveon、X-Trans拒绝。X-T5本次先命中内存估算，不是已支持。|
|WB|同decoder:24-37,74-92 ResolveWhiteBalance|一次metadata gains/用户增益、PreMul读回，拒绝already WB；不是C1 Kelvin/Tint等价。|
|黑白位/相机配置|同decoder、ProfessionalLibRawAdapter.cs ReadColor/CameraMatrixWillBeApplied|已实现LibRaw结构/参数有效性核验；仅内建/嵌入矩阵，无ProStandard/通用相机ICC。|
|master|decoder:90-101；Core Services/Projects/ProfessionalRawPipeline.cs:54-70|RGB16转float/65535、clip/zero计数；不是sensor float，无已丢失信号恢复。|
|曝光/明暗|ProfessionalRawPipeline.cs:75-97 ApplyExposure/Display|2^EV；ProPhotoD65逆矩阵到linear sRGB；Y>.75指数肩部、中性轴压色域、sRGB编码。C1 Film Standard/Linear Response未实现。|
|局部/曲线|同pipeline RenderPixel/RenderOutput；ProfessionalRawLocalRenderer.cs|已有可选节点；本次definition全空，不假设同名滑块等价C1。|
|预览|同pipeline:156+ RenderPreview；VM ActivateProfessionalRawAsync|同per-pixel显示后display-linear面积积分；本次720px为产品函数输出，不是源JPEG。|
|RAW导出|src/RAWSelectionAssistant/Services/ProfessionalRawExport.cs:15-38|非ColorState路先FillRgb16Row/display，再TIFF16嵌入sRGB。RAW显式宽域输出边界未打通。|
|RGB ICC|Services/DocumentColorWorkflowV1.cs:13-49；Core Services/Color/ControlledIcc/LcmsRgbEngine.cs|已有hash固定LCMS2.12、rendered RGB→编辑sRGB→目标ICC/读回。不能说项目没有ICC，也不能说RAW宽域已可用。|
|CMS入口|VM ReferenceColorWorkspaceViewModel.ColorManagement.cs:16-37|CreateManagedColorVariantAsync在WHOLE_APP_DIAGNOSTIC内，明确拒绝RAW，不是全发行版通用RAW功能。|
|显示器|src/RAWSelectionAssistant/Views/PhotoGradePageView.ColorManagement.cs:17及VM|显式SDR ICC默认off；HDR/系统ACM重复转换未确认，本次不测屏幕观感。|
|profile登记|Core Services/Color/ColorProfileRegistry.cs|名称/目录查找已实现，不证明像素已转换。|
|研究占位|Core Services/Color/Pv7ColorPrototype.cs、SyntheticIccMatrixReader.cs|研究原型/合成子集，不是通用相机ICC或已批准节点。|

Core csproj固定Sdcb.LibRaw 0.21.1.7、win64 runtime 0.21.1。实际RawContext.Version为0.21.1-Release；新版LibRaw参考不是运行引擎。

## 官方参考与适用范围

完整链接、commit、实际所读文件hash见evidence/open-source-references.json；许可证固定源收据见license-receipts.json。没有按star选型、没有复制第三方实现入产品，也没有完成两个成熟参考的独立数值对照，故不宣称满足像素修改门禁。

- Capture One Base Characteristics、ICC、ProStandard、Film Curve、导出ICC、process engine、AppleScript、平台差异原文已读。精确URL/更新时间/body hash在capture-one-official-sources.json。ICC、curve、engine共同决定渲染；Generic也机型特定。Linear Response仍有高光roll-off，不是sensor线性。ProStandard跨机一致是官方目标，不是本调查证明。相机ICC与输出ICC不同。Windows不使用macOS AppleScript。
- LibRaw 0.22.2，b93f6e45c194f5df9b02a43b1af9a54b4f41f33f：src/postprocessing/dcraw_process.cpp及官方API数据结构，用于unpack/黑位/WB/scale/去马赛克/输出职责。LGPL/CDDL分支/文件级义务另审，未升级产品。
- RawTherapee 5.11，db575c6690d30ce6428c0e1383bec8b8f1fb8d21：rtengine/rawimagesource.cc getWBMults:716、convertColorSpace:1116、colorSpaceConversion:1129/3202、preprocess:1435、demosaic:1791；iccstore.cc。适用WB/输入DCP-ICC/工作空间边界。GPL仅研究。RawPedia旧链接404，改读官方固定源，不冒称已读失效文档。
- darktable 4.8.1，fb0eb997e62c6dd52966cd35911c74afa150e209：src/iop/exposure.c、colorin.c process:1216/commit_params:1287、官方input-color-profile手册。适用曝光位置与输入profile边界。development网页和旧固定代码不混称同版运行。GPL未引入。
- DCamProf，bffb659d6a0af82f7b6b2e6d0006d1824ac4d398：www/dcamprof.html test target/illuminant/neutral tone、make-profile/make-icc。普通照片不能提供相机表征物理真值。GPL未运行或引入。
- morluto/rea，参考6.8.0 commit a95b83c2b06ef713664d6abb3d29c7c149a2ce01：README、installation的静态证据和后端条件。本机运行配置6.0.0，没有升级重装，不混为参考版。MIT未打包实现。

C1旧链接有403/404，最终通过官方公开articles API取到原文，保存文章ID/更新时间/hash，不存商业全文。内置Web查询无可用返回；实际来源为只读HTTP、agent-reach Jina、官方GitHub API，无照片外传。

## RAW盘点与样本限制

唯一根为既有tools/photo-engine-home/Inventory-Home.ps1:17记载的N:/pixart/RAW/Real。仅根和一级相机子目录，不展开7z、不跟reparse、不扫描其他盘或输出缓存。

- 212候选文件，206 LibRaw open成功、6失败，191个成功归一机型。open不等于unpack或产品渲染支持。分组见evidence/raw-groups.csv，原元数据raw-inventory.json。
- 品牌、LibRaw型号、EXIF完整型号、扩展名、ISO、RAW尺寸有记录。rawCompression/rawBitDepth均unknown：IFD缩略图8bit/容器16bit或文件名12bit不能冒充sensor位深。保留标签原观察，ISO0也unknown。
- D800有9文件，X-T5仅2、EOS60D2、NX5002等；不凑3机型、不混品牌。D800九个编码/裁切变体只作兼容性开发候选，路径/大小/hash见samples-selected.json。X-T5另为失败探针。未给全库hash。
- 两D800真实产品输出人工观察为白天城市建筑、蓝天、亮建筑、建筑深影。肤色/皮肤高光、红橙紫饱和物、白织物、逆光、高ISO、多照明未覆盖。亮建筑不当标准灰。其余变体未逐图审查。
- 相似取景不是同曝光/同sensor样本证明，不用两文件差异证明位深效应。没有跨机配对，不能证明一致性。
- 没有有效独立保留组，没有拟合。编码/裁切变体不能随机分到开发和保留。以后每机6—10独立场景、按拍摄家族约2/3开发和1/3保留，冻结后研究。

## 实际实验

### 光镜产品RAW路径（非C1）

ResponseProbe直接引用现有Core。两D800默认PV4、WB Camera、局部/几何/胶片/ColorState为空，曝光−2/−1/0/+1/+2；10份720px处理输出及一个X-T5失败。完整合同/参数/hash在evidence/response.json。

|源|master满码通道数|零码通道数|EV0 encoded RGB均值|+2EV输出满码比例|
|---|---:|---:|---:|---:|
|D800-12L|6627|22|0.305062|0.021188|
|D800-14L|2628|8|0.283601|0.014358|

满码是最终uint16通道观察，不定位sensor或矩阵溢出。12L/14L仅文件ID，不作位深权威。

- 原始RGB16LE缓冲留本机，每例hash。复用已有LCMS2.12 DLL核验指纹后转Lab D50；3人工ROI×2源×5EV=30条相对响应，relative-roi-results.json。ROI不是标准块/肤色/中性参考。
- 蓝天ROI D800-12L平均L*在−2/0/+2EV为16.39/35.41/65.59；14L为10.73/26.43/51.35。亮建筑12L色度中位6.13/9.73/9.81。只说明各自响应，不是色准或C1差异。
- 蓝天高色度中位色相相对EV0近0，仅本区有效，不外推皮肤/红紫。暗部色相只在两例C*>5时计算，低色度不假报稳定。
- 本地观察图evidence/pv4-exposure-contact.png是8bit显示附件，测量用留存uint16，不用截图。来源权利未知，衍生图不推公开远端。
- X-T5 DSCF0347.RAF实际NotSupportedException：RAW解码工作集估计超过1GiB准入预算，未降低尺寸或位深。decoder:68。没有改门槛或降分辨率掩盖失败。

### 一张全尺寸TIFF

现有FillRgb16Row与AtomicTiffWriter实际生成artifacts/raw-c1-feasibility-20261027/pilot-full-01/D800-12L-PV4-EV0-full-srgb.tif：7378×4924、16/16/16 unsigned RGB、uncompressed、orientation1、217976466 bytes、588-byte嵌入sRGB ICC。SHA见pilot-tiff-inspection.json。

它是Core行数学+writer验证，不是WPF全应用端到端导出，也不是宽域C1基线。reader直接读uint16 strips，不用Pillow RGB的8bit化。同文件Lab比较为零仅identity smoke test，tiff-measurement-self-check.json，不证明独立准确性。

### Capture One

安装file version16.6.1.2962；首查无进程、未启动。Session未创建，实际engine/所选相机ICC/WB/curve/干扰项unknown。本机profile文件名存在不等于会话已选。

C1导出0、曲线/contrast/saturation实验0、C1差异指标0。CAPTURE_ONE_MANUAL.md交付隔离会话、准确设置、首张TIFF16验收、后续20例计划。CSV全部NOT_RUN，±10需本机确认合法及读回；Linear Response仍有roll-off。

## REA证据边界

binary_session服务在线、target未开。注册npx -y rea-agents@6.0.0 mcp。no-install doctor exit1：GHIDRA_INSTALL_DIR未设；Hopper missing、IDA未注册。见rea-ghidra-doctor.json。服务在线不等于后端可用，没有重装整套工具。

C1实验尚未暴露必须反汇编的具体现象，故没有打开其二进制、没有函数发现。只读profile目录文件名，不复制商业内容。后续仅针对profile选择/加载、curve参数、曝光/饱和度具体问题做exact-build/hash/xref证据链，无证据保留unknown。未涉及授权机制、程序修改或资源打包。

## 已确认／实验推断／尚未确认

已确认：仓库/约定/版本；PV4 WB矩阵参数、输出域、display算法；两个D800/10EV输出；X-T5准入拒绝；TIFF16实物；RGB CMS不等于RAW宽域；C1安装/官方脚本平台；REA后端未配置。

实验推断：先拆输出域与准入比盲拟合LUT有依据；ROI响应与固定肩部/压色域机制相符，但无sensor分阶段float，不能精确归因clip；D800似compatibility变体，需家族复核。

尚未确认：C1实际引擎/设置/机型支持及与光镜差异；绝对色准、皮肤高光、多光源/高ISO、跨机一致；WPF预览保存重开导出闭环；提案是否提升保留组盲评。均不以已支持代实测。

无色卡可靠验证：功能失败、数值域、位深/profile身份、WB重复、同源相对响应、冻结定义一致性。受控补拍才可判断：物理中性色、绝对准确、特定照明表征；跨机一致需同场景同照明配对。当前不要求买色卡。

## 第一项小改进验收（提案未实现）

RawWideOutputV1仅master→显式输出，default-off，不换解码/WB/相机矩阵、不拟合C1。

1. 填项目NODE_COMPARISON_TEMPLATE：原色/白点、D65↔D50、OETF、负值/>1、clip/高光、意图/profile hash、方向。不能将sRGB转宽域包装叫恢复。
2. 至少LibRaw+RawTherapee核对输入域；LCMS+第二独立ICC实现固定版本/许可证/数值比较。本报告阅读不算通过。
3. 至少2机型，每机6—10独立场景且≥2保留场景，饱和色/高光分ROI，不藏X-T5失败。
4. 全尺寸16bit嵌入目标ICC逐字节一致；与冻结float同量化最多1码差是待正式验证的初始预算；历史PV4golden/hash不变。
5. recipe预览/保存重开/导出一致；导出软打样和受控预览核对，宽域设备值不直接交sRGB显示器。
6. 保留组指标与人工盲评有效才讨论发布；无真值不承诺色准，无C1TIFF不承诺复现。

## 检查和未完成原因

- 两probe最终build成功。初轮ResponseProbe误用不存在Preview，改为既有RenderPreview后实测；未改产品。
- Python防线9/9通过：uint16低位、8bit拒绝、缺/错ICC、方向、对齐声明、伪hash、ROI、DLL身份。
- 定向项目测试17通过、1失败、3跳过/总21。CacheClearDoesNotDisposeInFlightBorrowAndAccountingReturns共享资源计数预期0实际1GiB；孤立重跑1通过。疑似共享并行状态，根因未确认，不改无关引擎、不称全绿。日志均保留。
- 成功源size/mtime记录未变；OS可能更新access time，不作全部文件系统元数据绝对不变保证。未写/移/覆盖原素材。
- 远端配置且有push权限，但API确认实际public，名称含private不代表私有。RAW/TIFF/相片PNG/商业资源/DLL不入Git，只提交脚本、文档、元数据/hash、小型数值。照片仅本地交付。
- C1首张及完整设置、独立拍摄场景缺失，导致C1基线、保留验证、跨机肤色分析未完成。准备脚本不创建C1会话，不冒充实测。

## 集中缺项

已知仓库、RAW根、安装版本不再问。

1. 如另有实际拍摄库，请一次给一个明确RAW根目录，只扫描该根。每机6—10独立场景、最多3机型，优先缺失内容，没有则记缺口。
2. 按人工清单提供一张C1全尺寸16bit ProPhoto TIFF，加实际engine、相机ICC准确名、curve、WB、干扰项读回。先核验一张再批量。

没有有效保留组、没有拟合复杂映射、没有后台监控或自动续跑。

最终脚本复跑：两D800各五档RGB16输出hash全部与首轮相同，X-T5仍同门槛拒绝，见repeat-readback.json。它只证明同参数重复性，不是跨机一致性。全尺寸pilot接口现额外冻结已测sRGB profile hash，拒绝任意profile贴标签。

日期说明：文件夹/分支中的20261027是已经建立的批次标签；用户日期按客户端上下文记录，主机UTC和上游文章更新时间分别留存，不将其混同为用户本地日期或据此宣称最新版本。
