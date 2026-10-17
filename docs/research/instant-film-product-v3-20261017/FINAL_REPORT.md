# Instant V3 产品推进：阶段交付，核心产品接入尚未完成

业务日期：2026-10-17 Asia/Shanghai；主机原始日志时区保留。起点远端feature/kitao-instant-film-evidence-v3-20261016核验为e70d111633b4e41479215f6edd17d70741b09044，无新增提交。独立工作树N:/pixart/kitao-instant-film-product-v3-20261017，分支feature/kitao-instant-film-product-v3-20261017。未覆盖旧工作树/Release/原件，无强推或合并main。

## 软件里现在能操作什么、全尺寸通过什么、还剩什么阻断

|事项|软件里现在能操作什么|本轮全尺寸/实际运行结果|剩余阻断|
|---|---|---|---|
|既有即时成像|原V1/V2八种预设、原PV4/PV5/PV6保持原解释|本轮没有改生产像素源/项目版本|P3和Fuji原生稳定性继承，不放行|
|V3选择/编辑/撤销/项目重开|**尚未实现，不添加假入口**|研究JSON冻结/区域/原尺寸编码不冒充产品项目|逐节点成熟对照/许可和完整性能准入仍OPEN，不能先迁正式依赖图|
|双AI资料|证据库审核工具，不是相纸标定数据|Muse19+Grok22=41记录，39规范文献；10实图/10哈希/10扫描版本；0验证场景对/0标定|Muse无图像/ICC/图像hash；sources/licenses.csv未提供；原评级不升级|
|候选所有权|工具库修复，产品未依赖|持续持有输出/源borrow、缓存替换含活动任务、异常/运行中取消测试；不可变配方/随机geometry|更完整连续切图/失焦/实际产品缓存与任务压力仍未闭|
|原图坐标与条带|研究CandidateRenderSession|原图2像素核，完整坐标/种子，区域/不同条带逐像素一致；原尺寸效果后area缩预览合成oracle1e-6|授权照片完整preview-vs-full oracle尚未跑；不称精确物理PSF|
|三相机×五效果|没有产品V3按钮|Sony_DSC08015、Canon、冻结Fuji共15原尺寸TIFF16+30PNG16/JPEG回读；两次整图pixelSHA15/15一致|是研究条带闭环，不是产品V3操作/项目/导出链|
|成熟实现|无外部GPL源进产品|固定darktable曝光/高斯/颗粒子阶段，RT basecurve/有限高斯，独立stb Perlin实际编译执行|不是完整darktable/RT模块或逐节点全矩阵批准；真实照片/内存/噪声容差待补|
|DPI|本轮逻辑证据缺失已关闭|当前树原生产流程106状态/32DPI截图，17原失败复现后91/91通过，原断言不改|物理DPI和用户前台仍NOT_RUN|

**本批“软件里能选、能改、能保存、能稳定导出V3”的主目标未全部达成。** 所有研究结果均不把该目标写成已实现。详情05_product_gate/PRODUCT_STATUS.md。

## 1. 双AI核验与证据边界

Muse三明确文件逐SHA登记，CSV与JSON.samples完全相同19条、MUSE-POL-0001..0019，完整original/评级/理由保留。JSON对象根和字段名正确解析。Grok完整ZIP11952399bytes、SHA42A13AC29A9DF5CA3627890F81B015DD6F491852FCDBBA3F15ED859466031F8B重新CRC及CSV/JSON/10JPEG核验，不用截断包。文献/图像引用/实际文件/扫描版本/关系分别统计；26配对声明不等于已验证配对，同作者/同workflow不计独立、同站点不当同场景。

业务日期按Asia/Shanghai，2026-10-17有效资料不因柏林跨日排除。交接文本作为资料，不自动执行其拍摄/采购建议。图像许可Unknown，不复制网络图进Git/Release；原件、导出图、数据库和构建二进制不提交。

## 2. 资源修复与热点

CandidateSpatial/Renderer改返回显式CandidateImageLease；输出租约随所有权转移，不再随返回栈释放。源Adopt/Transfer、任务Borrow/Retain、缓存替换/退休与最后borrow释放合同明确；重复Dispose幂等，退休不让活跃任务逃预算。条带删除重复计费。异常、预算拒绝和**已进入任务后取消**通过测试，不以预取消代替。所有账本数字只表示准入所有权，不表示OS已立即释放。

Freeze复制provenance数组和随机geometry为只读集合，按任务冻结；profile palette任务解析一次，避免逐像素LINQ Single。250000次原查找28.1722ms、局部palette .1004ms，语义结果一致；只代表lookup，不推称整图等幅加速。最终候选13/13通过，原测试断言未放宽。

## 3. 全尺寸与内存真实记录

Sony原SHA1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93，冻结TIFF5C06A141...；Canon原B7D35851.../冻结B9A86CC1...；Fuji原52D054ED.../冻结5668EBCE...，输入完整SHA见结果和原清单。三原件收尾SHA重新一致。DSC08015旧产品已成功记录独立于DSC09916拒绝；本轮Fuji只读冻结RGB16，不重新解码污染对照。

五类：modern-itype-v3、modern-bw-v3、sx70-v3、reclaimed-blue-v3、blue-duochrome-v3；每图原尺寸15个TIFF16方向1/ICC精确，32×32局部，冻结配方；另一读取实现重复运行15份整图pixelSHA一致。PNG16逐行回读最大0；JPEG95-444最大MAE1.40982/RMSE2.23399/max28码，既有阈值MAE5/RMSE15不变。

第一次WPF全源读取进程峰值2749120512bytes，保留；改为经TIFF验证的逐行源读取后1986818048bytes。源float按尺寸持续计账，最大约482MB；条带32行/halo/输出行计账，不保留整张候选float输出、不扩大预算、不缩尺寸。进程峰值仍不等于账本，未强制GC声称释放。PNG/JPEG是从已生成原尺寸TIFF逐行消费同一现有产品WIC行编码器，未改生产编码数学；不是产品V3直接导出路径。

## 4. 成熟实现准入状态

固定源码完整commit：darktable fb0eb997e62c6dd52966cd35911c74afa150e209，RT db575c6690d30ce6428c0e1383bec8b8f1fb8d21，stb 2c980bb59875b0d32144a71867fbdebb2f77cd20。实际原函数研究子集在隔离C++ harness编译运行，源码/配置/exeSHA见02_references。RT grain来自darktable，不算独立第二份；stb为Sean Barrett Perlin实现，MIT/public-domain许可与darktable代码分开，未入产品。

19个数值观察包括灰阶/饱和通道/负/>1、flat/impulse/edge以及noise重复性/均值/方差/相关；DT递归高斯无限尾、RT有限3×3、候选有限5×5不同，DT线性曝光、RT shoulder和候选EOTF/创作toe顺序不同，不强求整图一样。仍缺候选逐节点/授权照片的成熟参考完整矩阵、参考峰值内存、噪声/材质准入容差及完整许可决定，因此OPEN。不能把源码阅读升级为执行，也不能把子阶段执行升级为完整算法批准。

## 5. 回归与原失败

|套|发现|选择|执行|通过|失败|条件未执行|缺失|
|---|---:|---:|---:|---:|---:|---:|---:|
|源码Core|1710|1710|1703|1702|1|7|0|
|源码WPF|1628|1628|1605|1605|0|23|0|
|实际发布Core|1710|1710|1704|1704|0|6|0|
|实际发布WPF|1628|1628|1605|1605|0|23|0|
|候选最终|13|13|13|13|0|0|0|
|DPI首次|91|91|91|74|17|0|0|
|DPI真实生产后|91|91|91|91|0|0|0|

源码P3必需TenThousandSyntheticItemsMeetP3QuerySuggestionSmartFolderAndBatchThresholds范围cold62.330，warm61.043/665.788/86.728ms，最慢665.8>400ms真实失败。发布本次通过不覆盖它。23WPF/6发布Core条件测试原名和状态逐项与上一批精确匹配，见CONDITIONAL_NAMES.json。未删除测试或改skip为pass。

DPI17原名/参数原因在DPI_FIRST_FAILURES.json；本树UiReview源ec9b6af4真实生产106状态、32张100/125/150/200逻辑DPI，在非输入desktop进程隔离，不SwitchDesktop、不发送用户输入、不改系统DPI/ICC。metadata/图像SHA/EXE身份全绑定，未复制历史截图。原测试91/91通过，物理DPI明确NOT_RUN。

## 6. 诊断Release与封存

普通输出第一次publish与本轮UiReview占用冲突失败，原日志保留；未杀用户进程、未覆盖旧包。全新--artifacts-path隔离构建成功。

- 状态BLOCKED_DIAGNOSTIC_BUILD_NOT_V3_PRODUCT。
- 源码8f35f5157cd1001368b8a3777d2ba237b666ca5f；后续工具/文档提交不冒作构建来源。
- 目录N:/pixart/kitao-instant-film-product-v3-20261017/artifacts/v3-product/07-diagnostic-release-isolated/win-x64。
- 完整win-x64自包含290文件，EXE D79EB0194B68DED650BADFA137B83A4E685DEC43C101D7E24A8A96DAB8B4ADDE。
- Core 27F809A8E7B5E17C217BC99A9D17BACCA346EEC1039F2A70A45FEE85F0294745。
- InstantFilmCandidate.dll不在产品Release；生产src相对审核基准零变更。
- RAW_TEST_RESULTS.zip1855成员/3983403bytes，SHAA25227BA0EA46199E885422EF9C67D3006654B243CD9D8EE2B57C6D15498D480。精确TRX/discovery/summary/runsettings/stdout/stderr逐回读，无runtime递归/照片/数据库/外部源或二进制。

## 下一轮具体问题

优先完成02准入的逐节点成熟/真实输入矩阵及许可/适用容差，再迁入单一正式模块；随后产品显式V3选择/真实参数/固定种子/撤销重做/项目版本/保存重开及原尺寸直接条带导出、活动任务revision和缓存压力。当前主目标不能靠研究JSON代替。

真实全尺寸预览误差、所有运行中取消/快速切图/失焦/压力合同仍缺；物理DPI/实际前台验收未跑。P3长尾真实失败和Fuji原生不稳定继续发行阻断，不要求购买色卡，不宣称相纸实测标定。

阶段SHA、文件清单/类型、复现命令另附。正常推送并独立查询远端；最终LOCAL_HEAD/REMOTE_HEAD由PUSH_STATUS及回复记录，不强推、不合并main。
