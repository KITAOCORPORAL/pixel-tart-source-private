# 单一相关对照：初始化、X-Trans、并发与buffer

目标仅 RAW-REOPEN-001。引用 L/S/R 编号见 SOURCE_LOCK.json；P01-P04 见 PRODUCT_EVIDENCE_BINDINGS.json。以下为**静态代码/契约对照，不是新执行结果**。参考均固定版本；不推荐升级或替换。

## 节点比较模板的本轮填写

- 节点/版本：现有 LibRaw 0.21.1 native + Sdcb.LibRaw0.21.1.7 的默认RGB16重建边界；不新建像素算法版本。
- 位置：RAW → identify/open → unpack → native develop → memory RGB16 → managed复制 → float master → 方向归一；编辑、预览和导出在后。
- native输入：文件/解包传感器原始域，CFA/黑白位/WB由文件和native参数确定；不是给RT/LibRaw demosaic同时提供了相同预处理mosaic的实验。
- 生产输出：3通道uint16 interleaved RGB48，无alpha；camera WB/matrix及默认显影已处理；OutputColor=sRGB/D65名义原色，继承native gamma/auto-bright等，非scene-linear；Float master只是/65535容器，不能恢复前段范围/精度损失；nativeUserFlip=0后产品统一方向。
- 参考1：LibRaw0.21.1，cccb97647fcee56801fa68231fa8a38aa8b52ef7，L01-L14，双许可LGPL2.1/CDDL1.0；不是已证明与包DLL可复现构建同源的二进制。
- 参考2：RawTherapee5.11，db575c6690d30ce6428c0e1383bec8b8f1fb8d21，R01-R16；GPL3+的应用自身float demosaic，输入端可共用LibRaw。其vendor头标记0.21.0-Beta1，不等同生产native0.21.1。
- 桥接参考：Sdcb43e9c1771c2b1387c2170e084e433d8bcef51cab，MIT，S01-S08，NuGet/托管文件信息绑定。
- 决策：只做行为/契约对照；不复用/翻译第三方像素代码到产品。不将非同域RT最终RGB误当数值oracle。
- 输入与授权：只引用产品已授权company-02及company-05的既有证据；source/stack/variant和完整输入身份以产品commit收据为准；未重新读取原RAW。
- 数值执行/编译器/耗时/内存：本轮NOT_RUN；没有生成中性/负值/高光夹具，没有测参考程序性能，不能拿文档阅读耗时当算法性能。
- 逐阶段误差：仅复用产品已发布master/TIFF差异；native逐阶段数据缺失。未造表补齐。
- PV4/PV5/PV6：不改代码/配方/默认，不重跑golden，不声明本轮新验收。
- preview/save-reopen/export：继承产品已记录Fuji FAIL、Canon对照0差；不替代产品复现或放宽断言。
- 科学/许可：CalibrationEligible=false；许可证相关路径和义务见LICENSE_BOUNDARIES.md；未批准产品分发。
- 准入：**CODE_CONTRACT_REVIEW_ONLY / INDEPENDENT_NUMERICAL_NOT_RUN / NO_PIXEL_CHANGE_APPROVAL**。

## 维度矩阵

|维度|实际生产 + 固定LibRaw/Sdcb|固定RawTherapee|可用结论及不可混用部分|
|---|---|---|---|
|新实例/初始化|P01在同步DecodeCore中OpenFile；S01调用libraw_init；L01/L02新建对象、清零数据/内部/回调、设置默认|R01/R02初始化RawImage；enableLibRaw时new LibRaw，R06 unique_ptr持有|生产没有同一context复用证据。构造对象初始化与内部每个tile scratch初始化不同，不可一概而论|
|参数默认|S02从实际native读取OutputParams再Commit；P01只改明确字段；L04确定quality继承规则|R12默认THREE_PASS及自有参数；R16默认启用LibRaw；RT的raw processing由自己的preprocess/demosaic链驱动|UserQual=-1不等于某算法。RT THREE_PASS不能倒推生产同遍数或同默认|
|RAW unpack路径|默认从Open/identify经load_raw分派；S08 DecoderInfo能观测选中的unpacker，但原收据没有|R02/R03使用LibRaw识别和CFA；R04/R05仍保留DCRAW fallback/LibRaw分支|共享依赖必须揭示；两个项目不是两个完全独立的解包器真值|
|X-Trans分派|L05按filters/no_interpolation、回调和quality有序选择，进入XTRANS后再按quality选passes|R08按sensor type和RAWParams方法选FAST/ONE_PASS/THREE_PASS/dual等；R12默认THREE_PASS|生产filters/colors/fuji_width/实际callback分支未记录；RT默认也不是本轮对指定RAW的运行事实|
|X-Trans输入/算术|L06/L07在已scale/nativeimage上用ushort RGB候选、shortLab、float导数等，内部范围/归一流程固定于自身链|R09/R10/R11是自有float mosaic/planar RGB，含RT预处理、相机系数和自己的初始化策略|原始域、精度、黑白位/曝光/矩阵/转移函数与阶段不同，不能对最终TIFF直接定同域误差阈值|
|scratch初始化|L09 malloc per-worker存储；L07拷已有image RGB、L08清homogeneity再合成|R10 malloc per-worker；R11每tile先清首RGB plane再填其CFA颜色，其他scratch复用|可疑点只能列候选，malloc不自动等于未初始化读取；不能凭代码差异提交calloc补丁|
|并发|L11允许每线程独立实例；同实例跨线程需外部同步。P01整个native流程同步跑在Task.Run体；无本入口自定义native回调|R05在LIBRAW_USE_OPENMP时对部分LibRaw操作mutex；RT自有xtrans另有worker并行|RT锁是其集成做法，不构成必须全局锁生产API的证据。DLL有VCOMP导入不证明此样本竞态|
|原生输出所有权|L10新分配memoryImage，caller用clear_mem。S04 AsSpan借用native；S05 Dispose匹配释放；P01复制RGB48后using逆序释放processed再context|R05借用libraw raw/image，R06保留owner；R07复制入RT自身float allocation；native借用不能在owner释放后读|实际生产成功路径符合“复制在free前”。RT借用raw_image不代表可把生产AsSpan跨Dispose持有|
|失败/取消|S01 open失败cleanup；P01 using覆盖后续异常/取消；token在native调用之间检查|RawImage析构按decoder分清free/image和自有数组，unique_ptr处理LibRaw|当前生产不是提前释放中的异步native调用；没有native中途取消保证，不将它当根因|
|颜色/输出|生产继承Gamma0.45/4.5、auto-bright等，经native develop到RGB16；之后float表示|RT自有黑白位、WB、矩阵、浮点demosaic/颜色处理与编码配置|颜色域/顺序不同；本轮不把RT表现写成生产颜色基准，也不因此改gamma/profile|
|依赖版本/编译|native包无commit/flags证明；对应上游release锁定；托管源有commit。实际DLLhash和PEimports已读|R13/R14/R15默认bundledLibRaw，支持systemLibRaw和OpenMP；本轮未构建|源版本锁定不是二进制复现声明；RT系统依赖切换需新记录，不能暗换参考|

## 已知行为与未证假说

**已知实现行为**：每对象状态初始化；native显影前后顺序；X-Trans条件分派；各自并行/tile/数值域；独立memoryImage与caller释放；RT与LibRaw的部分共同依赖。它们解释比较边界，不解释此次具体随机差异。

**未证实假说**：X-Trans scratch未写先读、OpenMP race、压缩解包器状态、MSVC浮点环境、ABI不符、原生依赖加载漂移、同名库fallback、托管拷贝/归一后的修改。当前没有成对有效参数/内部阶段buffer，不能任选一种当根因。

**没有发现的明确契约违约**：默认入口共享同一RawContext跨工作线程、在processed.Dispose后读取其Span、没有调用Unpack就process、没有复制输出就返回native引用。此结论只针对审查源码中的现有默认成功路径，不代表全部产品/所有native内部已证正确。

**不批准的捷径**：换decoder/版本、关OpenMP、全局锁、改插值参数、改gamma/profile/曲线、缓存最终像素绕过重建、放宽exact断言。这些会改变性能/行为或遮住失败，均没有当前根因依据。
