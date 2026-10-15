# 受控 ICC 产品增量交付：PARTIAL / OPEN GATES

业务验收日期／时区按用户要求为2026-10-15 Asia/Shanghai；客户端会话时区另为America/Chicago。旧证据5处Europe/Berlin错误采用追加勘误，未重写旧文件及原始+08日志。

## 身份与可执行交付
- 起点：远端research/kitao-validation-closure-20261015核对为63ca45ababbfc4780fe46febb767ef5ec76d8377。
- 独立工作树：D:/AI AGENT/kitao-controlled-icc-20261015；新分支feature/kitao-controlled-icc-20261015。
- 冻结产品／工具源码：e230038e2b882ef413a1b53972854efbd80e264e。此后只有证据、封存和归档辅助变化；最终证据SHA以包含本文件的提交为准，避免自引用。
- 新产品完整目录：artifacts/controlled-icc/release/win-x64，递归290文件。真实新构建版本2.3.0-controlled-icc+e230038e2b882ef413a1b53972854efbd80e264e；EXE SHA256为77b781afec63a4513929cceff2b2fa48d87a10957cfcc3fbb50978cd646719b4，不是旧EXE改名。
- 独立可执行ICC工具：artifacts/controlled-icc/isolated-icc/win-x64，递归417文件（含测试profile、README、runtime）。工具ProductVersion的SHA后缀被SDK重复，真实值原样记入RELEASE_IDENTITY，不隐瞒。

D:曾耗尽空间。只将本轮新artifacts移至E:/Kitao-IsolatedRuns/controlled-icc-20261015，逐文件哈希匹配并保留D: junction；旧Release、旧分支和用户资料未清理。新测试宿主另留在工作树D:祖先路径，结果写E:。详见ENV_ARTIFACT_RELOCATION。

## 受控ICC增量（不是只有报告）
新增Core中的ControlledIccProfile、EmbeddedIccInspector、ControlledIccTransform、独立版本recipe和tools/ControlledIccWorkflow。支持只读JPEG/TIFF嵌入profile检查；即使不支持，也输出SHA／版本／空间／拒绝码。实际转换只接受两份已评估线性RGB矩阵profile SHA、relative intent1、BPCoff、adaptation1、有限0..1域；库为外部精确哈希LCMS2.16，显式--explicit-research。

授权Canon完整5496×3670照片派生TIFF经线性sRGB→线性ProPhoto→冻结-1EV→线性sRGB；独立直接LCMS与新适配层60,510,960个float完全一致。detached RGB48预览／recipe重开完全一致；TIFF16、PNG16整数码与量化预览完全一致且输出ICC回读哈希吻合，最大量化差7.63314e-6。JPEG8有损差单列（max约.114397，mean约.002496），不叫无损一致。新profile对固定skcms的XYZ D50对照约3.2e-8；结论不扩展到所有版本。原生预取消／处理中取消／取消后复用／Dispose拒绝均实测通过。

原全范围照片ProPhoto回程发生超域，仍明确拒绝；另一个.8*x+.05 headroom照片夹具如实记录准备过程，不是输出修正。JPEG正向输入照片、普通产品预览界面和物理显示校准尚未验收。profile／算法身份见结果JSON。普通Color Studio入口未打开，旧WIC/PV4/5/6不迁移，PV7／合成reader仍默认关闭。

新LCMS2.16打包义务及完整依赖／构建对应审查未关闭，因此新库不随产品或工具包。包中已有的lcms2.dll哈希87bf...属于原Sdcb.LibRaw继承依赖，旧290文件已存在，不是新评估的2f864...DLL；新适配层不会误用该文件。不能把隔离工具称为通用ICC产品功能。

## RAW与Structure
三份授权原件本轮重新核对哈希并运行旧0.21.1、候选0.22.2、RT共享LibRaw及内部dcraw。旧Sony拒绝／候选成功／内部dcrawSony拒绝分别保留；Fuji5184／5178及crop origin差异不后裁图。中间像素阶段、Sony color4语义和精确矩阵来源仍OPEN/UNAVAILABLE。新候选发布义务未关闭，未加入产品模式或Release。

Structure增加tools-only owned scratch候选，20组边缘／ROI／非整tile／参数组合逐float一致。完整Canon三轮：full中位4163.581ms／最差4249.4832；旧tile7140.4473／7173.4916；候选6902.7174／9051.8594。累计分配约1.191／2.250／.283GB，最差采样WS约1.854／2.009／.950GB，冻结floatSHA全部一致。候选虽省内存但不快于full，且最差波动、两个成熟实现完整数值门禁仍未通过，故不接生产。10秒自然释放序列保留，不用账本0冒充WS回落；其他节点预算和全图依赖分列。

## 测试及未关闭验收
|本轮执行|Passed|Failed|NotExecuted|结论|
|---|---:|---:|---:|---|
|源码Core完整1705项|1698|0|7|strict FAILED|
|实际发布Core完整1705项|1699|0|6|strict FAILED|
|源码安全WPF子集|16|0|0|仅16/1626|
|实际发布安全WPF子集|16|0|0|仅16/1626|

原发布Core1696Passed／6NotExecuted是继承结果，未改为本轮；六项也未被研究探针改Passed。原72组PV4/5/6 golden及P3原400ms测试在源码和实际发布Core通过。10128项真实ViewModel范围切换warm约31.37/30.76/30.21ms，历史673.8/2499.7ms失败仍保留。

首次E:测试宿主因旧测试向上查找仓库根，造成Core369Failed、WPF6Failed；原始证据保留。相同发布DLL／测试程序集逐SHA绑定，在D:祖先路径新宿主重跑，未改断言或生产资源，修正后结果如上。

共享桌面Chrome前台idle16ms，未启动产品或发送输入。完整WPF可能显示窗口，按不争抢规则未跑全套；1610项未执行名称和24项实机条目逐项保留NOT_RUN，包括物理DPI100/125/150/200及真正WPS往返。策划图片持久化、搬迁、重复引用、身份拒绝、历史和DOCX后台合同通过，不替代可见交互。全量WPF／Windows产品路径／DPI／WPS门槛仍未满足，不宣称正式发布或全部验收完成。

## 文件与复现
RELEASE_FILES为递归全目录清单／类型／SHA；TEST_CLASSIFICATION及RAW_TEST_RESULTS.zip保留完整发现、结果、跳过、失败、TRX／日志。FILES_AND_TYPES记录本批源码／证据；VALIDATION仅完整性校验，不是产品验收。使用说明见tools/ControlledIccWorkflow/README.md，实验见tools/StructureScratchExperiment。

本批有实际产品Core代码变化，但未开放普通ICC入口，未升级默认RAW，未改变生产Structure。CalibrationEligible=false，不要求购买色卡，不宣称A7M4绝对色准或宝丽来精确复刻。最终仅普通推送新分支并核对LOCAL_HEAD/REMOTE_HEAD，不合并main、不强推。
