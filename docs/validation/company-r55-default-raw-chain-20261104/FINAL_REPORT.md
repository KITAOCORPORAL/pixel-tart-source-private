# 暗室默认 RAW 链审查：接续收口报告

日期：2026-11-04（Europe/Berlin）。审查范围已完成；产品状态 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**，不是发行或全相机验收。

## 1. 接续的是已开始任务，不是重新开工

- 原聊天“接续暗室默认RAW链审查”因 413 中断。现场发现 pass4 实际执行、独立进程重开、原失败 TRX、发布 DLL 绑定、27 份 TIFF 派生视图，以及尚未跟踪的审查测试。没有把聊天失败当作未执行。
- 原公司树路径以 SOURCE_IDENTITY.json 为准；HEAD f27f4409 的完整身份见下表。原测试字节原样复制到独立审查分支，旧树未 reset/stash/clean/checkout/merge，旧测试仍未跟踪保留。
- 首次 fetch 遇到 DNS 失败，后续 fetch 成功；实时 ls-remote 确认产品分支仍为 f27f440909fe19ddd5665b167b912c9db9d897d4。这不证明家庭没有工作；家庭本地未推内容 UNKNOWN，未探测 N 盘。
- 60 个既有工作树的 HEAD 和逐行 porcelain 状态与原 closure-audit 相同（仅归一化 CRLF/LF）。这不是所有脏文件内容哈希均未变的证明。完整本地记录见 SOURCE_IDENTITY.json。
- 已读 AGENTS.md、POLICY.md、HANDOFF.MD、docs/roadmap/R55_IMPLEMENTATION.md、docs/current/PIXEL_TART_CURRENT_STATE.md。current 总表的生成基线 c5222046 不是本次源码，未将其历史 PASS 冒充本轮验收。
- 本轮 **新增 RAW 解码 0、产品修改 0、产品重建 0**。没有重跑日历、DOCX、灯台、锐化、20MP 性能或研究六图。

## 2. 实际默认入口与颜色链

新导入、没有已保存 RawDefinition 的 RAW 才适用此表；已有显式变体按其冻结定义恢复，不暗迁。

| 环节 | 实际接线 / 定义 | 颜色域、白点、精度、范围和边界 |
|---|---|---|
| 用户入口 | PhotoGradePageView.xaml:23,26 的“导入/导出”菜单与侧栏导入；R53.Layout.cs:24-30 → ChooseTargetCommand → ChooseTargetAsync:757；LoadTargetAsync:570 共用 ActivateTargetAsync | 自动化调用真实 App/主 VM 的文件选择后入口，不宣称点过文件对话框。默认 UsePhotoGradePage=true、Stable、RawDefinition=null |
| 解码 / WB | RawOrientation.cs:7 → RawMatchTiff16ProductPipeline.DecodeFrozenMasterAsync / DecodeMasterAsync:15-22 → LibRawDecoder.DecodeCore:87-123 | 随包 LibRaw 0.21.1-Release；托管 Sdcb.LibRaw 0.21.1.7。full-size、3 通道 RGB16；camera WB=true、auto WB=false、camera matrix=true、OutputColor=SRGB、OutputBps=16、AutoRotate=false。不是嵌入 JPEG，也不是 PV4 的 ProfessionalRawDecoder |
| 原生映射 / 默认继承 | LibRawDecoder 仅显式设置上述参数；native-initial-parameters.json 保存随包原生初始化 | 初始 Gamma=[0.45,4.5]、NoAutoBright=false、Brightness=1、HighlightMode=0、UserQual=-1、黑位/白位=-1、AdjustMaximumThr=.75，生产代码未显式改写它们。WB/相机矩阵/自动亮度/传递编码已在进入编辑前由 LibRaw 处理。未采集 sensor/WB 前后/矩阵后独立缓冲，实际矩阵、适配细节和逐阶段裁切计数 UNKNOWN |
| master | HighBitDepthImageBuffer.FromRaw；RawImageOrientation.NormalizePixels | ushort/65535 转 float32，名义 sRGB 原色/D65，已显影、非场景线性；float 容器不恢复 16 位量化或前端裁切。两个样本观测范围 [0,1]、负值和 >1 均 0、方向归一为 1。sRGB 字符串不是对原生传递函数的测量证明 |
| 初始版本 | 空栈 Version=2、WorkingSpace=OKLabD65、ProcessingVersion=1 | 空栈保持 master；OKLabD65 是工作表示标签，不代表全程保存宽域线性 RGB，也不表示所有节点都存储 OKLab |
| 基础曝光 / 显示映射 | ColorStudioToolProcessor.Apply:53-74；+0.5 后 ProcessingVersion=2 | 默认 BasicTone 对输入按标准 sRGB 分段 EOTF 解码；Y=.2126R+.7152G+.0722B；e=2^EV，Y'=Y*e/(1+Y*max(0,e-1))；再做影调与 EncodeGamut。它是已显影 sRGB 的显示后曝光/肩部，不是传感器曝光或 scene-linear RAW 曝光 |
| 基础与局部编辑 | ColorStudioRenderPipeline:15-45；ColorStudioToolProcessor；StudioLayerComposite:42-52 | 输入/输出主要为 encoded-sRGB float32；节点临时进线性 sRGB 或 OKLab D65。EncodeGamut 将负值置 0，并按 max(1,R,G,B) 同比缩放再编码；蒙版/图层混合在此显示后链，按 CompositeVersion 走版本化混合。默认栈无局部层；本轮未新跑蒙版/空间工具，不据此宣称全工具验收 |
| 屏幕预览 | RawMatchTiff16ProductPipeline.PreviewMaster:70 / CreateProxy:89；ToVisualRgb24；RawDisplayBitmapAdapter.ToBitmap | 最长边 1600 的既有 nearest 代理，float → round/clamp RGB8 → WPF Rgb24。留档 PNG 最长边 1200。该 adapter 不建立显示器 ICC transform；不宣称校色显示器/OS 最终输出已验 |
| TIFF 导出 | ExportSelectedCommand → ReferenceColorWorkspaceViewModel.cs:818-852 → RawExport.cs:19 → RawMatchTiff16ProductPipeline.ExportAsync:80-86 → AtomicTiffWriter | 同一 frozen master / 栈，原尺寸 3×16-bit 无压缩 RGB TIFF，量化/夹持到 [0,65535]，方向 1，嵌入 SrgbProfileBytes，原子写入及成片收据。本轮只实测 TIFF；JPEG/PNG 代码是 TIFF 中间结果再编码，未算本轮 PASS |
| 保存重开 | Session.cs:123-156 重建目标、恢复定义/栈、ActivateTargetAsync；RawMaster 不作为像素快照写入 session | 原 RAW 哈希/版本/栈恢复，但默认 master 需要重新解码。冻结只覆盖当前 master 生命周期，不承诺跨进程解码像素相同；富士样本在这里违反精确重开预览断言 |

**需保留的域风险，而非本轮擅自修复：** 原生初始化 Gamma=[0.45,4.5] 的继承配置与后续“标准 sRGB”EOTF/profile 假设没有本轮逐阶段数值校准证明。不能仅凭 OutputColor=SRGB 或嵌入 sRGB ICC 认定传递函数严格匹配；也不能由最终 0/65535 反推 sensor clipping。精确归因/更改 gamma、曲线、profile、解码参数必须走 POLICY 的两个成熟实现固定版本比较。

## 3. 默认、显式接入、诊断和未接线必须分开

| 路径 | 状态 |
|---|---|
| 上述 LibRaw RGB16 → float sRGB → 默认基础/局部 → RGB8 预览 / TIFF16 | **默认接入**，本轮实测 |
| “新 RAW 版本” CreateProfessionalRawCommand | **显式接入，非默认导入**：ReferenceColorWorkspaceView.xaml:132 旧工作区按钮，PhotoGrade 可切旧工作区。ProfessionalRawDecoder 的线性 ProPhoto D65、gamma=1、no-auto-bright 与 PV4 定义是另一链，未在本轮激活 |
| 场景线性 RAW 局部曝光 | 受 RawLocalEditing/RawDefinition 控制；暗室存在控件不意味着默认 legacy RAW 启用 |
| RAW wide output | **默认关闭研究**：ColorManagement.cs:38 要 KITAO_RAW_WIDE_OUTPUT_RESEARCH=1，且需已有线性专业 RAW。不能恢复前端裁切，未本轮运行 |
| 栅格 ICC / ProPhoto 输出选择 | 显式颜色变体；颜色管理入口对 RAW 有独立限制。输出 ProPhoto 选项不证明默认 RAW 全程宽域编辑 |
| ReviewedLibRawCandidate 0.22.2 | **未接入默认生产链**：src 下仅定义，显式隔离候选/测试用途；没有自动 fallback，也未替换随包 native DLL |
| PV7、SyntheticIccMatrixReader、研究引擎 | POLICY 仍为 research-only/default-off；本轮未合入、未运行、未提升状态 |
| 完整诊断包 / 本审查夹具 | **诊断限定执行与隔离数据根**，不等于正式发行或物理鼠标验收 |

## 4. 最多三张授权 RAW：实际观察与复用结果

授权来源 docs/evidence/company-validation-20261015/AUTHORIZED_RAW_INPUTS.json。重新校验三张原件 SHA 与清单一致，不扫无关目录，不上传原件/私照/衍生图片。

| 输入 | 原始实测 / 重开 | 本次读取的实际图像与限制 |
|---|---|---|
| company-01 / Sony ILCE-7M4 / DSC09916.ARW | OpenFile=FileUnsupported；NOT_ADMITTED；预览/导出/重开 NOT_RUN | 没有解码像素，不能进行观感结论。producer TRX PASS 只代表收到了拒绝，不代表 Sony 支持；不泛化全部 Sony |
| company-05 / Canon EOS R6 / TNAN8886.CR3 | 5496×3670，producer PASS，PID31816→32300 重开 PASS；保存前后 TIFF 像素差 0 | 实际查看 +0.5 预览/导出整图、饱和夕阳和暗衣局部。暖色天空与暗部彩噪可见；未见足以认定新色相异常的预览/导出差异，没有以主观偏好调默认 |
| company-02 / Fujifilm X-T5 / DSCF0347.RAF | 7752×5184，producer PASS，PID23936→14108 重开 FAIL；状态/版本/原件 SHA 相同 | 实际查看默认预览/导出整图，默认/+0.5 肤色高光，保存前后最大差异区域。高光饱和区与暗部噪声已在默认图存在；+0.5 不会恢复前端丢失信息。稀疏重开差异肉眼不显著，不能把“看起来相同”改成 PASS |

- 6 次 TIFF 对应的 preview/export nearest-sample 比较，最大 RGB8 差均为 0；这不是缩小 PNG 每个像素相等，也不是全部局部/空间工具或屏幕 ICC 测试。
- TIFF 整图派生视图用 WPF 缩放，而生产代理用 nearest；整图细纹/噪声差别不能直接当导出错误。100% TIFF 局部不重采样。
- 27 个现有派生视图 hash 全部复核，实际阅读文件列表在 VISUAL_REVIEW.md；没有声称全部 27 张均已逐张阅读。
- 两个默认无编辑 TIFF 的新增低/高边界计数相对同 master 均为 0。+0.5 的边界变化只相对显影后 master，不是新增 sensor clipping；详见原输出收据。
- C1 / Lightroom 对照 NOT_RUN，原安装元数据不是可用授权/正常工作流证据；本轮未探测授权文件或启动这些应用。

## 5. 唯一优先修复项：X-T5 跨进程重建 master 不稳定

**RAW-REOPEN-001 / P1 / OPEN，发生在重新打开 → 默认 RAW 解码/方向归一 → master 重建边界，早于相同编辑与导出。**

证据链：同一原件 SHA、同一 target/version、序列化调整栈完全相同；保存重开后 preview SHA 从 E2282B5E… 变为 F6C374AA…。默认无编辑 TIFF16 对重开 master：16,344 通道变，max 5,600/65,535，mean 0.0603099787；最大位置 (7581,3980,B)，593→6193，差异均为内部非边界像素。

本次追加**只读原 TIFF 像素流比较**：+0.5 保存前/重开后 20,521 通道变，max 6,844/65,535，mean 0.0720352069；同位置 834→7678。Canon 对照是 0 差。脚本只读既有 TIFF，2 MiB 比较缓冲，没有再次 RAW 开发。

结论不越界：已有足够证据把该失败与“保存丢参数/选错变体/源文件变化”区分，并定位到重建后的 master 已不同；**尚未证明**具体 X-Trans 插值、native 线程/内存、隐含初始化或生命周期哪个因素是根因。不能据此擅自换 decoder、限制线程、放宽断言或持久缓存遮住差异。

后续修复入口：对冻结的该样本在另行批准的小型隔离验证中记录同一 master 的明确原生参数、生命周期和解码前后阶段；按 POLICY 先固定 LibRaw 与 RawTherapee 相关入口/commit/许可及数值比较，再决定行为变更。复验必须保留精确源/变体/栈身份，preview/save-reopen/export 成对，历史 PV4/5/6 不迁移。本轮没有成熟对照许可，不做像素改动。

## 6. 测试、完整包与交付身份

| 项 | 身份 / 实际结果 |
|---|---|
| 原产品分支 HEAD | f27f440909fe19ddd5665b167b912c9db9d897d4 |
| 产品源码 | 38dd3f92bb2db7238f7f058008887929ec971c93；本分支 src/prototypes 对其零差异 |
| 审查测试提交 | 41743c9b00cc28b1f2c463063697528dff4b9efb；原样保留 RawColorChainReviewTests.cs，opt-in，至多 3 RAW，精确重开失败断言不放宽 |
| 测试源 SHA256 | 42F0ABE5E09405F812D9B9C54B6B83680B85A9F89F8A15DC0E68DB7BFBCA31EF |
| 已执行测试 DLL SHA256 | EE2F9D2B5BCBDE825CD8CC1988F125A8AF96E08D95FA90B1FFCA62EA1B9E77AE；pass4 / 当前原树 build 文件一致；没有声称新树重新编译 |
| 原实测计数 | 2 个测试方法、5 次执行：TRX 4 PASS / 1 FAIL；其中 Sony 拒绝观察不能算 RAW 解码 PASS。只覆盖 2 张成功解码样本 |
| 绑定 | 20 个共享托管 DLL 与发布件匹配；追加 raw_r.dll 和 lcms2.dll 字节核对相同，见 NATIVE_AND_TIFF_IDENTITIES |
| 沿用完整 ZIP | E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64.zip |
| ZIP SHA256 | 357D0E9D30BA30CC9C157FE40D66371F6DE7A978E81E8730497286CBA3AFA1A2 |
| 包验证 | 293 个 ZIP 条目/目录文件逐个读回；WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false；全模块、策划15项、教程退役随原包保留 |
| 公司启动 | E:/Guangjing-BusinessFlow-20261103/Guangjing-BusinessFlow-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd；仍指向 UserData-BusinessFlow 隔离根，本轮未执行该 CMD 或重跑业务链 |

独立分支 docs/raw-default-chain-closure-20261104；LOCAL_HEAD / REMOTE_HEAD 的最终读回见 PUSH_READBACK.json（独立后置收据不递归写入自身 commit）。产品分支未合并本审查提交。提交只包括测试、报告、数值/身份收据和交接指针，不含 RAW/PNG/TIFF、数据库、已发布 DLL、ZIP 或研究资源。

本轮额外未验：物理鼠标、显示器 ICC/DPI/多屏、其他相机与家庭素材、实际 C1/LR 同源对照、全部局部工具、全软件再验收。审查交付完成不消除 Sony 拒绝和 Fuji FAIL。
