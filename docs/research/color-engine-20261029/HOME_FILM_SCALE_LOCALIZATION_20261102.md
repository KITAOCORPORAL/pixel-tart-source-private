# 家庭gray子阶段一次观测与跨端定位（2026-11-02，Asia/Shanghai）

## 结论与停止
**CPU double准备已经出现封存哈希差异；对真正送入前级shader的相同输入/矩阵，可直接成对确认的首个GPU输出差异在spk_tc_b（tc与brightness为同一dispatch的两个输出）。并非首次到LUT shader才有差异，也未分离硬件与驱动。**

家庭仅gray-boundaries17×33一次engineCreate/open/whole，完整capture通过后才比较。家庭observed最终RGBA16与家庭第十一批封存gray的2244通道值全0差，hash一致。至此不再执行GPU、不扩大实验。数学/精度/FMA/优化/profile/曲线/clamp/驱动均不改。

## 1. 正确接续与重复工作检查
公司远端research/color-engine-company-diagnostic-20261102准确为099b2fb8dbd43fa872703cef84e04b3776c96aab；已fetch。旧家庭research/color-engine-home-20261030@93b553d1c22937a2870743a40adc43d6df8b526d clean、unpushed0。检查已附工作树/相关会话目录、授权ColorResearchHome顶层同目标名称和匹配进程，未发现已有家庭同版capture/在途目标。

从公司提交新建家庭独立分支research/color-engine-home-scale-20261102，工作树N:/pixart/guangjing-color-engine-home-scale-20261102。旧家庭树未切分支/覆盖。公司原观测、只读读回、历史失败和完整20MP链保留。

本轮读取AGENTS、POLICY、CHECKPOINT、公司LOCALIZATION/READBACK/REPRODUCE及工具。未访问公司E盘或扫描无关目录；授权家庭根未见迁移的公司完整capture，因此**不调用要求两个完整capture的pairedcomparator冒充成功**。

## 2. 生成/构建/真实身份
使用既有generate_scale_substage_tap.py生成FilmScaleSubstageTapV1。原7文件先核对，其中engine.cpp仅两处authorized-root字面量家庭根→公司根归一，其余数字源码hash一致。新tap9文件在同样归一后与公司逐hash一致；35shader源不变、数值CPU路径不变。

全新外部根N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102，source-tap-v1/build-tap-v1/gray-once。沿家庭封存Release工具路径与flags，1次构建成功（原8warning保留）。C/C++compiler、CMake、Ninja、glslang及11runtimeDLL的跨端hash一致；实际observerDLL不同，家庭SHA **573a96ca466a5867fd177ae01a712301257c64e68e07b00aab0099f740034aac**，记录而非强求DLL相同。

运行前冻结实际home机器/来源/工具/runtime/逐object命令/资源。NVML出现exit255 UnknownError，未修驱动、未伪造公司字段。runner仅增加**身份采集后备**：保留NVML失败，WMI实际查询RTX5060Ti/WindowsDriverVersion32.0.16.1088；捕获后runtime capabilities也确认NVIDIA GeForce RTX5060Ti。不能把WMI版本格式直接当NVML字符串。原runner与家庭runnerSHA及最小diff保留，数值/tap/调用顺序不变。

新CMake内部编译SPIRV与旧家庭内部build全部一致；与真正选用的原resources-patch仅spk_cctf_decode.spv有历史BT补丁差异，本轮input_cctf_decoding=false、BT0。实际runtime73文件（其中34SPIRV）严格匹配既有清单；tc_b/lut2d_cubic/affine3相关SPIRV完全一致。没有拿内部新资源替换冻结runtime。

## 3. 家庭一次观测及完整验证（先于跨端分析）
- engineCreate1、open1、wholeRender1；stream0、BT0；7次新增被动device readback、0新增数学dispatch。
- 底层初始化check_math_mode/probe及整个历史渲染的总vkdispatch未装计数，**UNKNOWN**；不把“whole1”误写为总dispatch1。
- 16个numeric数组长度/finite/hash、17条observation记录齐全。输入device1683个float逐bit等于固定gray。
- matrix9项、runtimeLUT110592项：CPUdouble实际cast→staging上传→device读回在家庭分别逐bit0差。
- 曝光实际scale=[1,1,1]/offset=[0,0,0]；家庭upsample→exposure逐bit0差，exposure→家庭旧compound逐bit0差；公司公开相同路径对公司旧compound也0差。
- 家庭whole-observed.rgba16→家庭封存whole.rgba16实际逐通道0差，校验器sealedSideFinalExact。原runnerhome不做公司final断言，由随后完整homevalidator与额外直接bit比较把关。

完整数据只在N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once。家庭full validator通过；允许发布17文件子集严格复制公司public-subset-index的同一范围。子集有完整run引用但缺4restricted数组，明确isCompleteFullCapture=false，测试确认对它运行completevalidator会拒绝。

## 4. 先常数、后子输出：证据层级分开

### 4.1 CPU准备和runtime常数
|文件/层级|跨端观察|证据强度|
|---|---|---|
|film-sensitivity.cpu.f64，243double|家庭91e3777e… vs公司217c802f…，不同|仅与公司封存SHA比较；公司完整数组不在家，差量/首坐标未知|
|tc-lut.cpu.f64，110592double|家庭5c1db4fd… vs公司260d59c1…，不同|仅封存SHA比较，不能报告不同值数或max误差|
|tc-b-matrix.cpu.f64，9double|相同|两端小数组直接逐字节/逐bit比较0差|
|matrix uploaded/device，9float|相同|两端直接逐字节0差；各端cast/staging/device也实际0差|
|tc-lut uploaded/device，110592float|SHA同为63105d14c4c1f4ba923121cd2de847aefed7f1626afc0d252e73de56125244c3|完整float文件哈希身份相同；**公司文件本体缺失，未逐字节读取两端全数组**|
|曝光scale/offset CPU/upload|相同|直接成对字节比较0差|

因此不能说“CPU准备一切相同”：已观测double哈希不同。但这些doubleLUT差异在当前实际float上传层收敛到相同封存SHA；sensitivity是CPU runtimeLUT准备的输入，不是tc_bshader额外常数。未将其直接当此次GPU差异的原因。

### 4.2 最早可直接成对确认的GPU输出
输入17×33×3 device bytes与小矩阵device bytes直接相同。spk_tc_b只读这些input/matrix，不读runtimeLUT。此时已有：

|输出|分量数|不同bit pattern数|最大abs|最大同号非零ULP|
|---|---:|---:|---:|---:|
|tc|1122|487|1.1920928955e−7|3|
|brightness|561|82|2.3841857910e−7|2|
|upsample|1683|1520|3.8146972656e−6|23|
|exposure|1683|1520|3.8146972656e−6|23|

所有计数来自两端公开小数组实际读取，不是仅hash。tc首差[x3,y0,分量1]：家庭0.5205242037773132、公司0.520524263381958（1ULP）。brightness首差[x8,y0]：家庭6.085873565098154e−7、公司6.085872996663966e−7。二者同一个tc_bdispatch，**不能排tc先于brightness，也不能细分点积/累加/divide/clamp内部第一条指令**。

旧compound首差[x2,y0,R]仍为0.9905756115913391 vs0.9905756711959839，maxabs代表[x32,y0,G]仍为10.75505542755127 vs10.755059242248535。maxabs位置4ULP不等于全图maxULP23，区分保留。

### 4.3 不把全部upsample差异归为tc_b传播
561像素中158像素的tc/brightness两输出逐bit都相同，其中156像素仍有upsample输出差异，共429分量，maxabs1.1920928955e−7。旧首差[x2,y0,R]就在该组：tc/brightness相同而LUT输出差1ULP。

结合完整floatLUT的相同封存SHA，这说明**不能把下游全部差异仅解释为tc_b传入变化**；固定LUTkernel在相同公开输入子集处也观察到输出差异。由于公司完整LUT本体未迁移，依然标注其完整内容比较缺项。没有在此增加counterfactual GPU运行，没有证明全部差异贡献比例。

## 5. 能回答与不能回答
停止分类包含两层：
1. **CPU_DOUBLE_PREPARATION_HASH_DIFFERENT**：最早保存的CPU sensitivity/LUTdouble记录已不同，仅hash层可判文件身份不同，数值内容差异未知。
2. **FIRST_DIRECT_PAIRED_GPU_OUTPUT_AT_TC_B**：相同直接device输入/矩阵下，同一tc_b两个输出已不同；不用等到LUT才归因。

没有证据隔离硬件、驱动、GPU JIT、hostCPUlibm或具体浮点收缩/运算顺序的独立贡献。工具/runtime/数值源码与相关SPIRV相同有助缩小范围，但不能消除device/driver差异。mode记录均fused_fma_runtime1，相关三个shader没有constant_id0；相同specialization记录不等于切换了某数学分支，也不构成bitparity证明。本轮不改FMA/优化/精度/驱动求零，不建立修复需求。

## 6. 失败记录与剩余精确缺项
预GPU：managedworktree工具拒绝sourceRoot后用nativegit新树；NVML不可用后真实WMI后备；第一次SPIRV预检错误把内部build与冻结runtime当同域，修正为分别核验（旧记录保留）。这些阶段GPU0。唯一GPU观测成功，无GPU重试。后续CPU比较器首次U16bitview字宽错误，修正并保留原失败日志；不重渲染，最终重复CPU读回文件hash相同。

仍缺：
- 公司完整film-sensitivity.cpu.f64（243double）、tc-lut.cpu.f64（110592double），用于逐值数/max/首坐标。
- 公司完整tc-lut.uploaded.f32与.device.f32（各110592float），用于**直接完整数组读取**确认；现仅SHA身份相同。
- tc_b内部XYZ/点积/累加/除法等中间值若以后要继续分指令；本轮没有授权扩观察。
- 硬件/驱动/JIT/CPUhost准备的独立变量控制证据。

公司完整capture只在既有公司本地，未虚构访问E盘；Git只允许的17小文件、工具、hash/元数据。剩余缺项不影响本轮一次家庭观测已完成，但不得提升为完整双capture比较。

## 7. 交付与固定边界
- 11项小规模CPU控制通过：完整home、错role拒绝、篡改拒绝、subset不完整、真实身份、窄化上传、hashvscontent分层、tc_b及exposure结论等。
- RAW0、20MP0、六张自然图0、stream0、BT0、SharedIncrementEnvelopeUnsharpV1运行0；候选继续默认关闭/NOT_ADMITTED，产品不接入。
- 1760个旧历史/product文件读回保全，旧家庭20MPfreeze/回执/输出hash保全，旧家庭树clean未推送0。新树唯一既有工具修改为runner身份fallback，不改原generator/validator/pairedcomparator。
- 新检查点/能力表/收据时间使用Asia/Shanghai；旧Europe时间、失败及公司历史记录不回写。
- 本轮完成即停止。最终LOCAL_HEAD/REMOTE_HEAD由正常推送后实际读回；详见home-scale-localization-20261102/remote-receipt.json（生成后）及最终消息。
