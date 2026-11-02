# 公司公开合成小包：真实执行与家庭/公司比较（2026-11-02，Asia/Shanghai）

**结论：公司已真实执行并独立验证；同设备whole/stream及trace保持0差；跨设备出现已量化差异，最早记录分歧在film_scale_and_expose。跨设备不制定PASS阈值，不把差异直接归因显卡。第十三批候选仍封存NOT_ADMITTED。**

## 1. 保全与正确接续

用户已审查家庭`research/color-engine-home-20261030@93b553d1c22937a2870743a40adc43d6df8b526d`。本次fetch后remote仍同SHA，无新增。开工原公司工作树HEAD`0f3204c09b15e041c8e70cf95ddbee789fcf7cec`，status clean、unpushed0、无未跟踪文件；既有round6/7/8本地输出与失败不改。家庭第十批20MP及十一/十二/十三批所有Git证据保留，不把旧公司NOT_RUN或网络收据删除。

从已审查家庭SHA新建独立公司工作树`E:/Kitao-Worktrees/color-engine-company-diagnostic-20261102`，分支`research/color-engine-company-diagnostic-20261102`。这是研究接续，不切换/合并/清理旧树，也不把其它分支视为产品已合并。AGENTS/POLICY/CHECKPOINT、HOME_ROUND13_UNSHARP_REPORT及第十一批实际诊断工具均读取。产品src/tests/UI/项目模型/任务桥/default/PV4/PV5/PV6无差异，诊断工具源码本轮0修改。

本轮RAW解码0、20MP0、六张开发图0、第十三批candidate0、新锐化candidate0。未扫描其它照片目录，未修改驱动或持久网络配置。家庭私人A/B已知目录未在公司迁移，人工偏好保持NOT_RUN，不用本地替代照片/重渲染六图填空。

## 2. 公司构建与执行身份

公司实际：`PC-20250309WEIZ`，Windows10build19045，Intel i5-12400F（6核12逻辑），NVIDIA GTX1650，驱动572.70（WMI32.0.15.7270），Vulkan instance/device API1.4.303。loader关于registry layer/Bandicam API1.2警告保留，不改系统配置。Python3.13.13、NumPy2.4.6、Pillow12.2.0。

已核对旧公司frozen stream source8文件全hash匹配、73runtime resources（含34SPIRV）与诊断合同完全匹配。旧公司库没有所需synthetic sibling/trace exports，不拿旧natural-only DLL凑执行。按**现有**`tools/cross-device-diagnostic/build_package.ps1`+generator在全新`E:/Kitao-Research/color-engine-company-diagnostic-20261102-build`准备main/trace，未复制家庭DLL或身份。

- engine base commit：`c1447c6ab618fa4b1cb11fd2fe12826855007059`。
- 实际clang20.1.2（LLVM-MinGW20250402）、glslang16.6.0、CMake3.31.10、Ninja实际version string `1.11.1.git.kitware.jobserver-1`；Vulkan-Headers commit `b5c8f996196ba4aa6d8f97e52b5d3b6e70f7e4e2`。
- main DLL SHA `76f3818077e522e4bc618623a5df930301eb6a5c88028152a61232db79144e41`。
- trace DLL SHA `f74b584c24a1904f5f20f9a9d2f272d591a4eda6d4c768a5851a8d59ccc97589`。
- CMake Release、`-O3 -DNDEBUG`及逐对象命令、工具binary hash、runtime DLL、source hash、resources/SPIRV完整集合均在运行前冻结。两库各有8条编译warning（C-linkage及filesystem deprecation），不是零warning；构建成功无失败重试。

随后**仅用修正后的**`tools/color-round11/diagnostic/run_company.ps1`，显式提供Checkout、Library、TraceLibrary、Resources、Output、Python。未照抄旧README历史执行命令。Output是全新`E:/Kitao-Research/color-engine-company-diagnostic-20261102-run`，入口执行次数1，自动重试0。预运行`build-before-run.json`先于render，真实machine/GPU/driver与execution-before-render记录公司角色。开始收据时间 **2026-11-02T13:50:00.346756+08:00（Asia/Shanghai）**。run.json原UTC保留，新增收据明确上海时区，不改家庭历史时区。

## 3. 公司包独立验证（实际数据，不信metadata自报）

固定公开输入仅3个：gray-boundaries17×33、saturated-colors19×35、edge-small21×37，全部3通道linearProPhotoD50、finite可负/>1，不是自然照片。完整99字段PortraA及默认scanner unsharp不改；资源/SPIRV与stage合同不改。既有独立BT13值mode3小探针作为原包内容执行1次，不构成重跑BT64调查或新增配方。

对每例：
- whole vs rows1/7/17/64实际读取RGBA16逐通道0差；4rows门槛不放宽。
- traced whole、traced stream-r7 vs无trace whole逐通道0差。
- 原natural-only入口拒绝synthetic身份，3次GPU前拒绝，自然白名单未削弱。
- 每例12whole stage+36stream band/halo stage，3例共144个float文件；case唯一/顺序、stage顺序/域/shape/长度/hash/finite/min/max/sample、输入bytes与prepared来源、role/build身份都经共享`validate_package`重新计算通过。

封存家庭V2包亦独立验证通过；复制到Git交付目录的公司包又独立验证通过，不因路径迁移略过检查。现有validator22项负控制和两侧比较器3tests（含左右坏包/错role）通过，全部CPU读数据，无新GPUrender。没有重建诊断框架或改阈值。

## 4. 家庭/公司量化结果（left=家庭，right=公司）

每个case的whole与全部rows1/7/17/64均出现相同的跨设备最终差异；**同设备各自仍0差**，故这不是本次发现的whole/stream不一致。

|case|RGBA16通道总数|变化通道值数|首差x,y,c（home→company）|max绝对U16差|mean绝对差|P99 / P99.9|
|---|---:|---:|---|---:|---:|---:|
|gray-boundaries|2244|17|(32,0,1) 55954→55955|1|0.0075757576|0 / 1|
|saturated-colors|2660|31|(5,0,2) 4249→4248|1|0.0116541353|1 / 1|
|edge-small|3108|31|(8,9,0) 55856→55855|1|0.0099742600|0 / 1|

以上是**通道值数，不是像素或场景数**；max1不自动等于PASS/不可见/bug，也未定跨设备容差。完整0/.5/.9/.95/.99/.999/1分位数、firstxy/value和whole/4rows数据在home-company-comparison.json。

### 首个分歧stage

三case的whole和stream-r7都从第0记录stage **film_scale_and_expose** 开始有float差：

|case|whole该stage变化通道|首差x,y,c / 绝对差|stage max / mean绝对差|
|---|---:|---|---|
|gray-boundaries|1520|(2,0,0)，5.96046448e-8|3.81469727e-6 / 1.17246377e-7|
|saturated-colors|1232|(0,0,0)，1.49011612e-8|1.52587891e-5 / 6.78386801e-7|
|edge-small|2250|(0,0,0)，1.90734863e-6|3.81469727e-6 / 1.41637743e-7|

该stage是**复合阶段**，包含光谱upsample及exposure，不能直接说具体shader或setup就是根因。最早差异发生在锐化之前，因此不要以其下游差异重做第十三批candidate。所有后续阶段的max/mean/quantiles及第一个stream band分歧保留，未自行“归零微差”。

既有BT13值probe中只有index10变化：0.018000004813075066→0.018000010401010513，max5.58793545e-9，mean4.29841188e-10；这只是包内附带测量，不推翻/重开已闭合BT兼容断点问题。

## 5. 已匹配因素与剩余混杂

**可证实相同**：engine base commit、main/trace各7项source清单除engine.cpp授权根字面量外的bytes；把公司engine.cpp中两处E:/Kitao-Research换为封存家庭N:/pixart/ColorResearchHome-20261030后，整个文件hash即与家庭相同；clang/CMake/Ninja/glslang列明工具文件bytes、非路径配置/flags、所有非engine runtime DLL；73resources/34SPIRV；输入/完整resolved；runner/comparator源hash。

**不同或未独立隔离**：
- GPU GTX1650 vs RTX5060Ti；driver572.70 vs610.88。
- Windows10build19045 vs Windows11build26200；公司CPU实录，家庭包未含CPUmodel，不能猜同CPU。
- Python3.13.13 vs3.12.10，Pillow12.2.0 vs12.3.0；NumPy同2.4.6。
- 授权目录常量、对象命令绝对路径、build时刻与最终engineDLLbytes。相同源码/flags/工具bytes不证明最终binary等价，也不能把路径哈希不同直接说数值body不同。
- 冻结家庭run记录的package_validation及两个负测试hash来自较早修复前版本；本轮当前shared validator独立验证两包。run_diagnostic/compare_packages/compare_rows源hash与封存home一致，工具改动没有偷偷成为GPU算法解释。
- 家庭包没有本轮等价的Vulkanloader/API/CPUmodel记录，明确missing；runtime/driver内部JIT或CPU setup常数未隔离。

因此仅结论“跨设备/环境有差、最早记录于film_scale_and_expose”，**不能直接归因显卡或泛称GPU精度问题**。如下一轮授权具体定位，优先固定setup常数、tc_b矩阵/光谱LUT插值/曝光子节点及其边界输入，以新增最小tap/数值对照分开CPUsetup、shader运算与runtime影响；本轮未执行该扩展。

## 6. 调用数、失败和不做范围

一次入口完成：3whole+12mainstream+3tracewhole+3tracestream=**21个成功Portra render API调用**；另1既有BTprobe=**22个成功GPU产物API调用**。3自然入口拒绝发生GPU前。每次engine创建还可能运行math-mode初始化probe；**底层vkdispatch及初始化probe未单独instrument，不把22误作GPUdispatch总数**。

GPU执行未失败、自动重试0；新构建成功但有warnings。验证/比较均通过，跨设备出现差异不是比较器失败。负控制预期拒绝单列，不能称包执行失败。RAW0、20MP0、六图0、候选0。

SharedIncrementEnvelopeUnsharpV1继续KEEP_FROZEN_RESEARCH_ONLY / NOT_ADMITTED / default-off；不调包络、不改曲线、不关旧锐化、不加第二候选/产品入口。零值减少不能替代细节/肤色/噪声/人工偏好。家庭私人A/B没有迁移，人工偏好NOT_RUN。

## 7. 交付与下一步

Git交付公开小包174文件（约1.58MB）、144stage、buildprovenance、来源/调用/验证/比较/混杂收据及本报告；无私人照片、externalengine资源、DLL/SPIRV。CHECKPOINT仅在顶部追加当前公司EXECUTED检查点，下方NOT_RUN/旧网络/旧远端历史原样保留。能力表独立更新，不改旧第十三批冻结状态。

公司结果路径：`E:/Kitao-Research/color-engine-company-diagnostic-20261102-run/run`；研究分支交付副本`docs/research/color-engine-20261029/company-diagnostic-20261102/company-package`。外部构建留`E:/Kitao-Research/color-engine-company-diagnostic-20261102-build`，旧树保全见preservation文件。

**本轮唯一重点已完成。下一步若继续，应定位film_scale_and_expose的具体子阶段；不需要重复这个3case包，不重跑20MP/自然图/候选。** 产品默认关闭；LightTable条款与Spektra GPL/CC BY-SA/资料权利互不替代，隔离DLL不自动解决许可。最终LOCAL_HEAD/REMOTE_HEAD与干净状态实际push读回交付。
