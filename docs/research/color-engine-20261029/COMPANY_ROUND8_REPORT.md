# 第八批：流式原型与一次20MP输出（2026-10-30）

**状态：最小隔离原型已执行；小图whole/stream逐像素等价；20MP像素输出完成；端到端host预算门禁PARTIAL。产品仍禁用。**

fetch分支`research/color-engine-company-20261030`核对基线`078a77f491d60c373989e15e8ea48c5ae5d4182c`，无增量。AGENTS/POLICY、第七批报告/分配图/提案/合同及实际源码锚已读。原source2、兼容patch、allocation树和旧失败保留；本轮不重跑BT64、曲线负斜率、桥验证或WB余量，不扫描D800/其它照片。

## 1. 实际代码，不是再交提案

外部新checkout`E:/Kitao-Research/color-engine-round8-20261030/source-stream-v1`，固定base engine c1447c6...；实现私有导出`spk_stream_research_v1`，版本**SpektraStreamingResearchV1**。generator、caller和数字进研究Git，生成GPL源码/资源/DLL/SPIRV仍本地。8文件generator复现bytes一致，实际source与DLLhash清单可核验。外部API-SPEC增补研究wire，旧ABI没有注册新产品方法。

这次不调用spk_open：film pass分块read_rows→原film节点→GPU读回negative临时磁盘行库；print pass带halo=2读相邻negative，完整画幅尺度固定，原print/Gaussian/unsharp/编码/量化节点处理，GPU只materialise有界RGBA16中心行供consumer。没有完整source/negative/assembled/result GPU或CPU分配。负片磁盘中间量仍随height增长，结束/失败都删除。

严格编译固化唯一99字段PortraA合同和真实resource集合，不允许其它route/stock/效果；输入domain/方向/source/inputSHA/backend自SHA/resource集合SHA完整验证。grain/glare/halation/DIR/autoExposure关，默认unsharp.7pixel/.7amount保持，未知字段明确拒绝。

固定配置所需全局量均inactive或原host常数，未用局部max代替全局；新global effect如要支持须另版完整有界reduce。本版跳过依赖band长边的geometry prefix，但按完整frame设置source/frame长边和pixel pitch，仅identity geometry和A无decode可进入。详细实现见STREAMING_IMPLEMENTATION_ROUND8与STREAMING_BACKEND_CONTRACT_V1。

## 2. 小图与fail/lifetime门禁实际执行

|门禁|实际结果|
|---|---|
|第七批完整场景321×215/641×429/1200×801，多rows|10case whole/stream RGBA16全通道0差|
|synthetic97×65，负/>1/nearzero/原色，rows1/7/17/64|4case whole exact；明确synthetic非额外自然场景|
|同进程顺序重复、consumer分块变化|2次same-process exact，分块1/3/7/11不改hash|
|源read/consumer/取消/未知合同等|10个实际native拒绝或失败，全live0/negative删除，无最终TIFF|
|并发/取消|第二call−30；第一call−7、join后释放/live0|
|rows预算约束|requested4096实际缩为26rows，device16MiB/staging1MiB，输出exact|
|height不变内存|width321/rows37，height215→430，device/staging/host-vector峰值完全相同，height430亦whole exact|

原首尾reflect、halo、未整除尾、人物/电线、默认scanner unsharp和完整frame context都保留。没放宽阈值、没改profile/桥/曝光，也没独立640拼图。

1200×801 rows17/37/113 device峰值约2.16/3.60/9.07MB。不同rows内存随band而非完整height变化。所有返回后的device/staging live0。预算在真实Vulkan allocation requirements位置硬拒绝，含pooled/persistent/staging旧+新替换；source/DLL身份与测量范围区别driver内部驻留，不用全局nvidia-smi used当进程峰值。

## 3. 一次完整5496×3670 PortraA

前述门禁通过后只发起**1次**native20MP调用，requested/actual rows127，halo2，frame20,170,320像素，未缩图/关隐藏效果/加曝光。源是第七批封存同一A完整input，输入SHA和prepared manifest核验。

- 输出完整RGBA16像素bytes=161,362,560，pixelSHA **`1c4a97a126b420fb24561df5cfb03bfc1b3d7648a5ee77c3d13102c48c2a7b8a`**。
- raw行文件与完整TIFF16的3670行全pixel hash一致；独立新进程重开输出也同hash，不只是截图或采样比较。
- native device峰值43,848,656bytes≈41.8MiB，staging25,523,424≈24.3MiB，显式host vectors预算16,496,672bytes；29read/29consumer、live0，中间negative不存在。
- 当时device cap256MiB、staging64MiB、host512MiB且device不超过当前free减256MiB/研究2GiBcap；没关其它程序或换driver。
- native分项约init424ms、source-read75.5ms、upload114ms、compute433ms、readback72.4ms、negative-disk413ms、consumer123ms，native约1967ms。Python全部准备/哈希/输出第一轮约4.0s；不称通用性能，不沿用家庭数值。GPU计算/读回在新接口自身调用点分别计时。

**20MP无whole oracle**：公司原whole该尺寸预算不足，第七批结论不改。此full依据是已通过的小图等价重构+实际完整frame输出身份/I/O一致性，不能写20MP whole/stream逐像素oracle已执行。

## 4. 必须保留的host门禁失败与修正

首轮native完成且device/staging硬门禁通过，但Python post-native还保留输入mapping，输出mmap和TIFFsegments读取共存，OS峰值**659,144,704bytes≈628.6MiB > host512MiB**。原caller只在native poll检查RSS，未覆盖后续I/O，不能以native host-vector16.5MB证明进程整体安全。

已保留原report副本，把原raw/TIFF重命名`.initial-host-overbudget`并写审计report success=false/hostgatefailed，像素bytes未删未改。Git分别保存原native-pixel报告、full-evaluated-result和full-budget-caveat，不隐藏失败。

修正caller：native返回后关闭输入map/锁，再逐行写；TIFF只允许uncompressed rowsperstrip1，通过offset/count有界read代替segment预读；跨全callerRSS监测超过hostcap禁止finalrename/success。失败只留下partial和failure report。

对已存在fullraw在**新进程**运行修正版逐行writer+完整读回，最大row43,968bytes，RSS约38.49MB，hash exact；另一新进程read-only重开约36.23MB也exact。没有第二次20MPnative调用，所以**修正版native+I/O整进程fullhost门禁未重新执行**，最终仍PARTIAL。小图改后caller重跑控制/合成/heightoracle通过，不能替代该fullhost缺口。

## 5. 故障记录与保留边界

实际实现过程有：Windows bcrypt/windows头include顺序首次compile失败已修；staging-budgets最初少算替换时双份stage，低预算硬guard真实拒绝，改rows denominator后同控制exact；初始“staging1MiB应失败”断言错误（实际能在自动缩rows后成功），改用更低cap得到真实拒绝；旧编译和控制目录全部保留，不通过删测试/升阈值处理。最终库5条warning（原C-linkage2条+filesystem u8path deprecated3条）留build-summary。

严格区分：已完成native20MP像素、未运行whole20MPoracle、失败的初始whole-process hostcap、已独立通过的修正版full-I/O、未重跑的修正版组合full门禁。不是再次交第七批提案，也不是宣布生产可用。

产品src/UI/项目/任务桥/默认/PV4/5/6及MatrixV2不改；仅PortraA，不扩B/三stock/grainseed或随机空间。D80014缺源/家庭GPU补丁/肤色高光覆盖继续未运行，WB冻结。LightTable非商业许可与Spektra GPL/逐资源权利独立记录，未获产品分发批准也不阻断本轮隔离技术执行。

能力合同和评估见company-round8/R55_CAPABILITIES.json、PRODUCT_ASSESSMENT.json及STREAMING_BACKEND_CONTRACT_V1。复现、实际失败和hash都进研究分支；RAW/private图/外部源码/资源/DLL/SPIRV只留本地。最终推送/远端读回SHA在交付回复。
