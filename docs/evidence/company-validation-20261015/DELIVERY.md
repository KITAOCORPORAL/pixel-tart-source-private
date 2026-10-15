# 公司合并验证、性能闭环与ICC研究交付

日期2026-10-15（Asia/Shanghai）。**产品审阅状态：REVIEW_BUILD_WITH_OPEN_GATES**。严格测试仍因NotExecuted为FAILED；ICC研究有未对齐差异，Windows/WPS实机未执行，不叫正式发布或全量验收通过。

## 1. 身份与历史保全

- 指定远端基线integration/kitao-night-ab-company-20261015，经fetch核验实际HEAD为8c3986e52c501b86111aa7124af9f9f24379659a。
- 新独立工作树D:/AI AGENT/kitao-company-validation-20261015；分支integration/kitao-company-validation-20261015。其他工作树/未提交资料、旧Release、A/B分支均保留，无reset/stash/clean、无强推或main合并。
- 原合并提交dc67b443b57a8ebe78af640d6f28030a47403ae9，双亲b1630f0e55780d746d4377d209899a09d2773b0e和419c9c165b119fc992ee34e9e947b05f93835e6d。
- 初始合并版完整源码/发布身份为8c3986e；性能修复86c986c及popup精确身份修复3d526ae分别提交。**最终产品/测试冻结SHA：3d526ae7e7589a0d735689883dcf62a4ef272720**。后续研究工具/证据提交不改变src/tests。
- 独立detached helper worktree D:/AI AGENT/kitao-company-merged-release-20261015 @8c3986e只用于第一阶段原合并Release，保留构建来源，不冒充最终版。
- 已读AGENTS/POLICY/节点模板；本轮生产色彩数学、PV4/PV5/PV6与PV7默认状态零变更。

## 2. 合并后的实际测试

不是相加A/B旧结果，也不是只读分类JSON。AB_EXECUTION_AUDIT和FINAL_AB_EXECUTION_AUDIT从真实适配器发现及TRX逐名核对：A新增7/B新增8均各一次Passed，源/发布同构建中都存在。合并分类取共同祖先+7+8集合并集，原分类未变化，新CompanyQueryAggregate/CompanyScopeSwitch两项追加必需自动测试。

|轮次|Passed|Failed|NotExecuted|strict状态|
|---|---:|---:|---:|---|
|初始8c3986e完整Core（1701项）|1694|0|7|FAILED|
|初始8c3986e完整WPF（1625项）|1602|0|23|FAILED|
|初始实际合并Release Core|1695|0|6|FAILED|
|初始实际合并Release WPF关键|26|0|0|PASSED|
|性能修复中间WPF|1602|1|23|FAILED/BLOCKED|
|最终3d526ae完整Core（1702项）|1695|0|7|FAILED|
|最终3d526ae完整WPF（1626项/212类）|1603|0|23|FAILED|
|最终实际发布Core完整|1696|0|6|FAILED|
|最终实际发布WPF关键|36|0|0|PASSED|
|最终发布程序集身份单例|1|0|0|PASSED|

发现与结果逐项一致，没有漏跑/额外测试/超时/缺TRX。最终P3必需400ms与72项PV456 golden在源码和发布Core都实际Passed。UNEXECUTED逐名列原因/条件；29个独立外部/人工缺口不改成Passed。FINAL_REVIEW_GATE为errors0、REVIEW_BUILD_WITH_OPEN_GATES。

中间真实popup失败完整保留于FAILED_ROUNDS和RAW_TEST_RESULTS.zip。测试原先枚举全局Popup“最后一个”会误拿仍关闭中的旧菜单；改owner.ContextMenu明确当前菜单身份，并断言复制前disabled、复制后CanPaste、新菜单实例、paste后2对象与缩放原断言，finally关闭。不是删除断言或让失败变条件项。修复后重新完整执行，不用专项通过覆盖旧失败。

## 3. 400ms性能问题

详见PERFORMANCE_ROOT_CAUSE及performance/原始数据。
- 原10,000项必需测试按预热后3次最慢，有限3轮新夹具，门槛不变，全部Passed，逐次耗时保留。历史673.8/2499.7ms仍是失败，不因本次通过消失；历史缺SQL/负载分段，无法证明唯一成因。
- 额外10,128条数据（10,000未归档/128归档）仓储全库4999、当前49；warm all约19.2/18.1/19.2ms。实际SQL/参数/EXPLAIN显示Rating索引命中、ORDER BY临时Btree，count/page各约8–20ms。冷仅指新进程首次，不强制驱逐OS cache。
- 同数据真实ViewModel/Dispatcher链路复现warm482.9/467.3/472.5ms；主要阻塞为集合大小统计在UI线程完整分页/重复count，以及范围切换多余global collection计数。修复为同筛选SQL SUM字节聚合+仅scope导航复用全库计数，普通修改刷新仍重算。
- 同输入修复后warm29.6/29.3/29.6ms；global counts、数量/排序一致，Core SUM对分页oracle和WPF变更刷新合同通过。没有修改像素/门槛，不把历史仓储峰值硬归因于这个UI瓶颈。

## 4. ICC开源数值对照（已执行，仍研究）

Little CMS2.16（固定source commit/conda DLL hash）+skcms固定commit97adcbb…本地编译exe+ArgyllCMS3.5.0官方exe独立运行；不是只列项目名。节点记录、许可证、入口、参数和命令见ICC_NODE_RECORD/ICC_NODE_COMPARISON、icc/REFERENCE_COMPONENTS。

原创数学ICC v2.1/v4.3：matrix线性/gamma2.2、LUT及显式M矩阵LUT，共7个合法profile；4个intents×2个输入类型，共56组合、每组合12点；float包含负/超范围，16bit明确clamp/round。固定创建日期和profileID，两次生成hash一致。profile原件不提交，只交生成脚本及hash/逐点结果。

- LCMS/Argyll v2有24组合可比，Argyllv4明确拒绝。
- skcms有42组合可比，14个absolute组合无等价控制标UNAVAILABLE。
- 线性matrix约3.2e-8XYZ差，gamma约1.3e-4；无M LUT约半值，显式M relative约1.7e-5，感知/饱和约0.00349。PCS标度/A2B/意图差异仍OPEN；不乘2或修算法掩盖。
- malformed长度/越界在LCMS可open/建transform，skcms拒绝，说明需要明确fail-closed应用边界，不能据成功建句柄批准任意profile。
- 现SyntheticIccMatrixReader真实运行只接受声明七tag v2线性夹具，拒绝其余样本；与LCMS线性目标12点max约0.0004798，未扩写成通用ICC。
- LCMS MIT/skcms BSD3；Argyll CLI AGPL3/独立icclib MIT，隔离进程研究；不引新产品依赖、不打包参考源码/DLL/EXE。参考内存采样仅工作集/private，不冒充allocator归属或性能优胜。

完整任意ICC输入/profile/意图/显示与导出仍未接默认产品，PV7默认关闭。没有实测色卡的A7M4绝对色准/宝丽来精确复刻未证明，CalibrationEligible=false，不要求购买色卡。

## 5. 真实RAW及大图

公司授权5份原件全部与原清单SHA匹配（AUTHORIZED_RAW_INPUTS），不是用别处照片冒名。只读观测Canon5496×3670、Fuji7752×5184最终RGB16成功；Sony旧LibRaw仍RAW_NATIVE_FILE_UNSUPPORTED，原拒绝保留。黑位前/矩阵前等阶段UNAVAILABLE；不从最终图反推sensor值。原件未修改/上传/提交。
候选0.22.2固定exe和DLL哈希本机找到且一致，许可分发未批准；本批未重跑下一阶段RAW双实现候选对照，默认解码不切换。

大图选择Structure节点：同Canon5496×3670，独立full/tile输出float字节SHA完全一致，max绝对差0。tile7.22s/full4.23s，tile峰值working约2.01GB/full1.85GB；此次tile更慢、内存不更好，未改生产空间算法。取消约110ms，3秒无强制GC序列与ledger归零分开报告，OS回落/原生allocator归属未证明。Details full本机约18.43s可运行，旧BUDGET_REJECTED原样保留，其他节点全尺寸oracle未做。

## 6. 最终合并Release（使用这个目录）

- 完整目录：`D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/sealed-release/win-x64`
- EXE：同目录`KitaoPhotoSelector.exe`
- 产品版本：`2.3.0-company-ab+3d526ae7e7589a0d735689883dcf62a4ef272720`
- EXE SHA256：`003ac23a2f1e440e4ac71e94e9f0270300819d8215ecdcea8f96471d68de1663`
- 290文件完整SHA/大小、主要7DLL与实际组件testhost及测试DLL哈希见FINAL_RELEASE_FILES/IDENTITY。自包含win-x64、非单文件，必须保留完整目录，不提交包。
- 初始merged-release（8c3986e）独立保留；A/B旧Release不被复用或覆盖。中间86c986c发布因真实Failed已阻断，没有把它批准为最终版。

## 7. 实机边界

检查工具/桌面窗口可用，但前台Chrome且近期有输入，按不争抢规则停止，不启动产品抢前台、不读取无关正文或保存私人截图。产品启动、工作台→策划/Color Studio、真实图片插入/搬迁/保存重开/导出、切图取消、100–200%物理DPI、WPS编辑导回均NOT_RUN。A自动迁移/OpenXML、后台WPF和XPS不是实机验收。

## 8. 交付与开放项

阶段提交：a629c30初始合并证据；86c986c P3；2b489ba ICC/RAW/空间探针；3d526ae popup修复；ec89124固定参考获取；最终证据HEAD在回复核验。原A/B分支不改写。
文件类型、源码/合并取舍见FILES_AND_TYPES/SOURCE_AUDIT；分类并集原证据保留；原始测试ZIP、每项NotExecuted、ICC逐点/失败/版本/许可、性能环境/SQL/计划、RAW来源/哈希和空间释放数据完整交付。OPEN_ITEMS/FAILURES_AND_OPEN_ITEMS列科学和人工缺口。
研究原件、授权照片、数据库、凭据、候选解码器、参考源码/二进制、发布包不入Git。
