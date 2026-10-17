# 宝丽来 V3：正式产品接入交付（仍是发行阻断诊断版）

业务日期2026-10-17 Asia/Shanghai，主机日志原时区。fetch核验feature/kitao-instant-film-product-v3-20261017仍为a51bcf96efc0e3d03438138dfa525ab61c7449b7；独立树N:/pixart/kitao-instant-film-v3-live-20261017，分支feature/kitao-instant-film-v3-live-20261017。未覆盖其他树/未提交内容/旧Release，不强推不合并main。

## 软件现在能操作什么、原尺寸通过什么、还剩什么

|事项|最终分类|实际结果/证据|限制|
|---|---|---|---|
|五基础V3正式入口|产品已实现|现有Color Studio即时成像面板明确V2/V3、五类选择；真实ViewModel/命令非默认调节、固定seed、重新显影、撤销重做、保存项目重开|前台物理操作未跑，不称正式发行|
|单一正式模块|产品已实现|全部候选源迁Core/Projects/InstantV3，tools不再有重复数学代码；产品不启动外部工具，候选DLL不入Release|namespace保留兼容旧候选JSON；旧V1/V2/PV4/5/6不暗迁|
|原尺寸直接三格式|产品已实现并真实数据验证|Sony_DSC08015/Canon/冻结Fuji×五类，15组45次ProfessionalRawExport直接条带TIFF16/PNG16/JPEG编码回读|PNG/JPEG各自条带生成，不读已写研究TIFF；源只冻结Fuji，不修原生不稳定|
|真实预览/1:1/局部|验证通过|15组actual产品preview与完整TIFF线性光area独立缩图最大2.115396983526363e-6，固定容差2/65535；合成区域/条带/整图合同|完整扩展极值混叠/审美矩阵仍缺|
|六组旧版显色|产品已实现|Womb01351.Stock0…5编码域公式复现，42灰阶点误差≤.000051，Stock2中途负/>1保留，最终输出裁切|不是旧App实际运行输出；不按冲突编号认作SX70/Go/iType|
|创作显色/光学|产品已实现|分区、底雾、趾肩/密度、冷暖、独立seed漂移、过期/褪色、线性预显色暗角、显影后边压暗|不宣称相纸测量、年份/小时或慢门推断；BW保持无彩|
|纸面/覆膜/构图|产品已实现|纸基/纸纹/划痕/扫描尘、压纹、程序皮/点/条/箔、外亮线、覆膜强度/角度/宽度、矩形/圆窗口、crop/zoom2|内窗口独立pressed dark rim未完成；不是物理BRDF；缺第三方纹理不伪造|
|原创程序缺陷|产品已实现（创作近似）|铺展、密度带/滚轴、光雾、干岛、月牙、蓝遮光失败、ridged-noise结晶、边漏液蓝前锋/琥珀主体；独立组件/子流，默认0|未逐像素复刻旧软件，结晶不是物理树枝生长模型；部分几何固定程序位置|
|多曝|基础算子可运行/产品未完成|线性光混合与域合同测试|第二照片选择/双源身份/项目链未接，不能称软件可操作|
|完整LUT/特殊贴图|缺失资源|保留三33³摘要/风格数据、七附件SHA和冲突|不由采样点造35937格LUT，不添加动物/达摩/妖怪假入口|
|逻辑DPI|验证通过|本树原生产器106状态/32逻辑DPI新截图/metadata/当前EXE身份，91/91通过|物理DPI/用户前台未运行|

## 一、附件接收与准入

附件7份来源，原名/长度/SHA/内容类别见01_intake/FILES.json，PIPELINE.md与PIPELIN1E.md相同，只计6独立文档。全部作为资料，不执行文件内指令。独立公式、静态分析宣称、推断、提案分开；工作空间/相纸编号/Blue冲突不按自信措辞裁决。逐项动作/产品入口/实现位置/验证状态见ABSORPTION.md。

当前9输入（6合成域+3精确冻结授权照片中心patch）实际执行固定DT曝光、RT basecurve、DT递归高斯/RT有限3×3、DT simplex/stb独立Perlin；固定完整commit/输入域/配置/代码许可与参考身份沿用，复测BEHAVIOR.json，活进程峰值为采样下界。最初管道死锁/退出后读peak异常保留，改并发排空stdout+活采样，未把源码阅读当执行。基础创作合同域/有限支持/重复/BW/预览/编码门禁单列；不要求创作外观整图等于参考，也不称完整darktable filmic/RT App通过。

GPL参考仅外部研究可执行，源/exe不入产品。正式Core为原项目自有实现，旧软件只按附件数学关系独立实现，未复制完整shader/贴图。创作/原创形态不代表物理相纸标定。Muse/Grok既有41记录/39文献/10实图/10扫描/0标定继承，不重新升级。

## 二、产品、配方、资源与异步

显式InstantFilmDefinition.AlgorithmVersion3嵌完整CandidateRecipe；组件版本/预设/身份/seed/geometry保留，unknown拒绝。缺省null扩展保留已保存候选像素解释；zoom需paper2，paper1不同zoom拒绝。默认V2不暗迁旧项目。用户选择V3后暴露真实适用控件，三语言资源覆盖，不靠修改语言测试关闭失败。

正式ProfessionalRawPipeline的fit/region与ProfessionalRawExport调用同一Core session；现有UI Generate经过revision/target/version检查后才提交，取消/切图/失焦生命周期不让旧任务覆盖新照片。真实专项：五类、非默认seed/旧风格/材质、history、重开、三格式、运行中显影切图取消；合成VM测试不是物理鼠标验收。

资源：保留source Master以相同Entry pin复用，不重复收费；退休含活动任务继续计账，独立源reserve。输出lease→可Dispose高位buffer明确移交；V3不进入旧无所有权cache；region/导出strip/scratch持续持有。21候选/Core所有权/版本/公式测试通过，包含真运行中取消、并发session关闭/退休、预算拒绝、buffer transfer。账本不当OS已释放证明，不强制GC做通过证据。

## 三、真实产品45格式输出与预览

三份原件SHA收尾不变：Sony1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93；CanonB7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401；Fuji52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865。DSC08015不替代DSC09916；Fuji效果只冻结旧输入。

每组真实+.2EV/非默认乳剂/固定seed，ColorStudioSessionStore保存重开recipe精确一致、64×64区域、fit512，15组45格式输出尺寸/方向1/ICC精确。TIFF16/PNG16逐码0；JPEG最大MAE1.44254，原MAE5/RMSE15门槛不改。直接正式产品接口为每格式独立渲染条带，不读中间研究TIFF。15完整输出缩图oracle最大2.115396983526363e-6<2/65535。完整配方/种子/receipt/hash/耗时/内存见05_results。

实际进程峰值2071248896bytes，ledger最大485013504；源、scratch、strip、输出预览由各责任者持有，不放大预算/半精度/缩原尺寸过门槛。合成视觉V2/V3对照在本机artifacts/v3-live/06-product-visual/SYNTHETIC_PRODUCT_COMPARISON.jpg，已目视检查五类；不包含私人图、未提交图像。

## 四、完整回归和失败保留

|套|发现|选择|执行|通过|失败|条件未执行|缺失/超时|
|---|---:|---:|---:|---:|---:|---:|---:|
|冻结源码Core|1711|1711|1704|1704|0|7|0/0|
|冻结源码WPF|1631|1631|1608|1608|0|23|0/0|
|前版发布Core|1711|1711|1705|1705|0|6|0/0|
|前版发布WPF|1631|1631|1608|1608|0|23|0/0|
|最终实际交付Core|1711|1711|1705|1705|0|6|0/0|
|最终实际交付WPF|1631|1631|1608|1608|0|23|0/0|
|最终交付Core共用候选套|21|21|21|21|0|0|0/0|
|最终交付产品/语言专项|1631|12|11|11|0|1|0/0|
|当前逻辑DPI|91|91|91|91|0|0|0/0|

raw runner因NotExecuted保留FAILED，不把全任务写全绿。23WPF与6发布Core原名和状态精确不变，CONDITIONAL_NAMES.md/json。早期源码语言测试失败保留FIRST_WPF_FAILURES，补26新标签三语资源修正，非放宽断言。早期并行build锁testhost失败日志保留，改独立artifacts-path，不杀用户进程。

P3核对followup后仅复用精确排序索引和regression，未合并整分支。原665.788ms/400门槛失败不删。本轮源码cold2.459、warm2.154/2.128/2.087；前版发布cold4.379、warm3.701/2.718/2.574；最终交付cold3.375、warm3.643/4.039/2.540ms全部报告，400ms不改，不当作历史长尾根因全闭。Fuji旧原生不稳定仍独立发行阻断。

## 五、桌面与DPI准确状态

原只读桌面查询2026-10-17T16:30:35.2664347+08:00，idle16ms，实际存在新输入，未具备安全独占，故不向用户桌面输入/启动前台，不改ICC/物理DPI/日常数据库；非笼统未授权。逻辑生产器运行独立非输入desktop、未SwitchDesktop，106状态/32逻辑图当前身份；91DPI通过不等于物理缩放或前台操作通过。NATIVE_STATUS.md按项说明。

## 六、完整交付目录身份

状态BLOCKED_DIAGNOSTIC_BUILD_WITH_PRODUCT_V3：包含真正Core V3入口，不是旧研究诊断包，但仍不正式发行。

- 构建源码7087a50cc70b6dbc27b083dd45c013e6946096b6；之后只工具/测试/文档，无src变化。前版完整回归+最终包完整回归/专项/候选分别报告，不混身份。
- N:/pixart/kitao-instant-film-v3-live-20261017/artifacts/v3-live/06-delivery/win-x64，win-x64自包含290文件。
- EXE SHA420329D3BE362C0037B3E681415738387C8C0963D069291D391C55BB38198D4C。
- Core SHAD3F5D79A25BEB48992941D901A1D3B3FC77F8982A135C74E1B878659946787D3。
- 无InstantFilmCandidate工具DLL/新LibRaw/外部GPL参考进入Release。
- 原测试ZIP4600成员/8422581bytes，SHA9DE6E4467240481E69EFA1A33FD597BA4E80BEBF6C9D8DFC83567618A88F5A87；逐TRX/FQN/参数行/计数/hash回读、每个zip成员SHA回读。
- 仅精确文本测试结果/日志/summary/discovery/runsettings，无照片/原件/数据库/ICC/raw buffer/凭据/构建二进制/外部源码。逐文件类型与SHA另附FILES.json。

## 七、仍未完成

完整第二照片多曝入口、内窗口独立pressed边暗线、缺陷完整可编辑几何/更真实树枝结晶、缺失完整LUT/第三方贴图、全部扩展极值/混叠视觉矩阵、前台/物理DPI。CAPABILITY_STATUS.md逐项标产品实现/部分实现/缺资源/未运行，不用装饰功能掩盖缺口。

本轮基础五类**已接软件产品操作和直接原尺寸出口**，与上批仅研究输出不同；但全部用户扩展目标未全达成、正式发行仍BLOCKED。用户无需补旧源码、Mac实测、新样本或买色卡。最终正常推送并独立ls-remote核对，不强推不main，最终SHA另记PUSH_STATUS。
