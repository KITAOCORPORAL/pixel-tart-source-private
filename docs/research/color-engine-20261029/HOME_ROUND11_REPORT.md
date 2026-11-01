# 家庭第十一批：诊断门禁独立重验与六张自然覆盖（2026-10-30，Europe/Berlin）

**结论：第十批固定PortraA流式20MP合同保持冻结；第十一批诊断工具修复并在家庭执行；公司NOT_RUN；六张开发照片均whole/stream0差，但发现暗部底色/细节压缩与新增零值问题，不批准一般摄影表现或产品接入。**

## 0. 开工核对与禁止重跑范围
fetch核对 `research/color-engine-home-20261030@07df3c56c551c38893abc1e4b7eaefb8e4a83be4`，本地/远端一致，无新增，开工clean。已读AGENTS/POLICY、CHECKPOINT、HOME_ROUND10_REPORT及能力合同。当前GPU是家庭RTX5060Ti/610.88，非公司GTX1650；公司执行不在此机器上伪造。

本轮 **full20MP engine调用0次**，未改第十批执行链、freeze、报告、receipt、输出、失败或AUDITED_STATUS。`final-round10-preservation.json`重新读取文件hash与receipt binding确认通过，这是read-only保全，不是性能/渲染重测。所有新代码在 `tools/color-round11`，证据在 `home-round11`；旧 `tools/spektralab-research`、`tools/cross-device-diagnostic`及home-round10不改。产品src、UI、默认recipe和PV4/5/6不改。

## 1. 诊断入口修复

### 明确本次执行机器，不再写死公司状态
新 `diagnostic/run_diagnostic.py`：必须 `--device-role home|company`、`--build-provenance`。记录实际hostname/system/architecture、nvidia-smi GPU和driver、Python exe hash/version/包版本、harness与工具源码hash；运行前build identity由新collect_build_identity采集，含主/trace库和runtime DLL、CMake flags、逐对象编译命令与digest、compiler/glslang/ninja可执行hash、source hashes、resources/SPIRV。run将构建清单原bytes和execution-before-render存入新包，比较验证其匹配，不把旧家庭清单抄成公司已执行。role是明确调用参数+机器记录，不是地理位置认证或数字签名。

家庭V2已实际执行公开3case×4rows、被动stage、BT小诊断，role=home，最终屏幕输出也打印真实executedDeviceRole。新输出是round11/diagnostic-home-v2-complete，旧家庭run字节没改。oldhome-vs-newhome比较经新门禁仍全阶段0差，这只是家庭兼容性，不是跨GPU结论。

### 左右包独立重算，verify/compare共享同一函数
`package_validation.validate_package`为唯一入口；verify_run与compare_packages均使用，每包先各自通过，才跨包统计。

- 直接读取whole与r1/7/17/64重新算0通道差，直接读取traced whole/stream重新算0差；不信comparison=0或trace布尔值。
- 每包input.npy与锁定公开fixture file/pixel/shape/dtype/domain/orientation一致；固定manifest、完整99resolved、73资源/34SPIRV合同以digest锁定，不只要求左右彼此相同。
- whole12stage、stream r7的36stage数量、唯一文件、执行顺序、band+halo尺寸、域、hash、文件长度、finite及实际min/max/sample都独立验证；BT真实bits/shape/mode/finite核验。
- V2角色与命令期望role、机器GPU/driver、execution-before-render和run嵌入内容、build清单hash和库/resource匹配；未知role、缺工具身份拒绝。
- 旧V1仅显式 `--allow-sealed-round10` 且run.json恰好等于封存第十批home hash才接受，仍重验实际输出，不为任意旧记录猜role。

**22项独立校验测试PASS**。包含同步改stream像素+hash仍拒绝、trace final修改、重复/缺失case/stage、错shape/domain/role/GPU、finite/截短/input、资源和resolved同时伪改。另一个真实CLI测试对坏左包/坏右包两个subcase均拒绝，没有生成comparison。Python-O也不绕过，不改变0差门槛。

## 2. 公司交接
当前家庭环境，公司新诊断状态 **NOT_RUN**。公司可运行新 `diagnostic/run_company.ps1`（仅语法解析，未在公司执行），实际先采本机build identity再调用role company，独立verify通过后与封存home比较。

输入和资源/hidden default未换，same-device仍0差，cross-device只量化/定位，无事后PASS阈值。GTX1650/5060Ti差异仍包含driver、编译/runtime混杂，公司像素缺失时firstDifference/changedCount保留null。本轮没有猜首差，也没有调shader precision、曲线或profile。完整公司命令和build新目录要求见 `tools/color-round11/diagnostic/README.md`。

## 3. 自然摄影覆盖：独立准入、最多6张

6张全部来自既有已授权清单SHA匹配；没有扫无关目录。5张是THIRD_PARTY_TEST_FIXTURE/LOCAL_COMPATIBILITY_TEST_ONLY，CanonR6为company-05；照片/RAW/图像文件均只留本地，不发布。每张先用既有ProfessionalRawDecoder/RenderOutputPixel EV0准备，黑位/WB/AHD/矩阵uint16损失仍保留；方向逐行用原生产映射，6D原orientation8转为stored1。固定ExistingResearchD65ToD50V1桥，最长边1200的全场景BOX代理作为胶片输入。全RAW准备可能大于20MP，但**未对full-size照片执行胶片20MP实验**。

新增独立 `NaturalCoverageAdmissionV1` / `NaturalProxyAdmissionV1` / `NaturalPhotographyStreamV1` 和新DLL兄弟入口，只允许6个精确sourceSHA+proxyPixelSHA+shape组合。原Canon entry源码前缀byte exact且守卫仍在；4个实际错误source/pixel/shape/原入口拒绝新来源控制，read callback0/live0。不将新图假装旧Canon身份，不移除原白名单。generator加固后在clean外部树再生bytes与已执行版本8源hash完全相同。

固定Portra400/PortraEndura 99resolved，原默认unsharp、EV0、方向/白点/编码保留；无新stock/effect。whole用原patch DLL；stream新自然准入DLL只是新增入口，film math/shader/profile未改。每图rows17和37全行比较（包含首尾、尾行、seam）**0通道差**，有限输入全通过；另在同6图既有1200px代理上运行只读whole12stage有限性观测，72个stage文件均无非有限，且traced final与原whole0差；这不代表每条算术指令或stream内部全部节点都单独观察。阶段自然像素仅留本地，Git只统计/hash。最大native device约3.60MB、staging约1.68MB，live0/negative删除。未经实际测量的独立传感器噪声/物理色准仍NOT_RUN。

|样本|实际观察/覆盖|代理尺寸|whole渲染+拷贝ms|stream r17/r37 ms|输出任一通道零值像素|其中proxy全部0..1且无原master端点足迹|
|---|---|---:|---:|---:|---:|---:|
|Canon600D白花|亮花瓣/暗绿背景/细纹边缘|1200×799|61.71|606.66 /457.94|308|308|
|Canon6D雪中礼物|白雪、彩色包装/暗部|801×1200|59.74|795.94 /528.32|1088|1018|
|Canon5D3背光郁金香|粉红/红、高反差/亮背景|1200×801|81.97|672.94 /458.82|3338|446|
|NikonD5100户外篮球人物|有限手臂/手部肤色、亮天/头发、品红衣服|1200×795|60.08|602.09 /433.70|350|350|
|SonyNEX5R街道花丛|高彩花叶、车/楼高光、细边缘|1200×799|59.95|591.46 /448.28|11563|11562|
|CanonR6黄昏剪影|极暗车体/剪影/电线/渐变天|1200×801|62.10|608.26 /431.37|0|0|

计时范围不同：whole是native渲染+owned结果拷贝；stream还含引擎创建、严格资源hash、两遍带halo磁盘/consumer，因此不能把表当纯算法性能横比。单顺序render进程每图采样RSS约190.56→286.15MiB，OS为整个进程累计峰值约216.71→286.15MiB，非每图独占增量。RAW准备是另进程原生产合同，峰值约493~676MB单列在production-decode-reports，**不冒称RAW解码也受512MiB流式caller cap约束**。与第十批20MP完整host验收不同实验。

### 既有剪切与新增端点分开
已有master满值：5D3绿色约1.053%、D5100蓝约3.293%、NEX5R蓝约4.437%，它们会影响高光色判断，但不等于传感器饱和，也不能说可恢复。输出这6张均无65535通道，不说明高光信息都保留（前端丢失仍在）。

新端点采用保守proxy足迹：将原master各通道0/1 indicator经相同BOX传播，只要一个足迹含旧端点就标contaminated。即使proxy输入3通道严格在0..1、全部足迹无旧端点，仍有表中新增output0；这定位为处理后端点，不证明是哪个节点或一定由final clamp（还可能curve floor/量化）。Git统计和first proxy坐标在concrete-findings；自然坐标不是synthetic真值，不算色卡。

## 4. 实际看图结论，不以“胶片风格”掩盖问题
已实际打开before contact、前后contact、人物before/after全图，以及6张各2个同坐标1:1局部（12区），文字在visual-review。没有用户偏好批准或校准显示判定。

**首要下轮任务：定位低信号区域的底色/细节分离。** CanonR6车体proxy ROI [0,500,300,250]，基线线性显示Y中位0.000503→输出0.004264（约8.48倍），Y标准差0.004706→0.000620（比值0.132）。肉眼可见原车体层次变成近匀蓝灰底；不是“更暗”一句话，也不是输出零值问题。whole与stream共同出现，排除本次条带不一致，但不能直接归因具体curve/scanner/gamut节点。

**第二任务：定位花丛/阴影的新增黑点端点。** NEX5R约1.206%的像素至少一个output0，11,562个没有旧master端点足迹且proxy全部内部，首坐标[1051,2]，须另轮阶段采样查哪个固定节点首先压到0，不能从输出猜。局部花叶细节更难区分，无whole/stream seam。

肤色小ROI [310,390,35,60]：显示线性Y中位比0.664，明显变暗；实际局部没见独立突兀肤色跳变，但无正面人像/控制光/配对真值，**不能批准肤色准确或外推所有肤色**。背光花卉/天空偏色同时有既有前端通道截断，绝对色相误差无法归因。雪景白偏冷/暗属观察，没色卡不称准确。

暗部自然图没发现可直接指认的周期条带，formal smooth-gradient banding并未由自然直方图证明；seam全图exact是代码一致性证据，不是所有观感都好。连续曲线/肤色校正、自动曝光等数学不在本轮修改。若后续修复会改像素，需按POLICY先给两个成熟实现固定域对照，另立候选和门槛；本轮不调参。

## 5. 覆盖缺项和交付边界
有限覆盖6个开发画面、肤色仅1张shade-lit手臂/手部、强亮区已有前端损失；没有独立拍摄场景来源审计、holdout/验证拆分、控制肤色/高光真值、色卡/同源C1、物理胶片/相纸拟合。各图不得从探索改称保留组。sample group和缺项在coverage-ledger。绝对色准、人工偏好、跨相机一致性均NOT_RUN。

第十批20MP保持已冻结，不因第十一批工具升级要求重跑；本轮frame cap1200自然case，full20MP engine calls0。公司诊断NOT_RUN，家庭真实role记录和修复工具可交公司执行。PRIVATE原片/照片/局部/大buffer和外部engine/资源/DLL不进Git，只清单/hash/config/指标/文字及公开合成小包。代码/资源分发许可独立，研究一致性通过≠产品后端注册/商业许可。产品UI/src/默认recipe/PV4/5/6及任务桥不变。


## 6. 2026-11-01 续接封存（Europe/Helsinki）
原第十一批渲染证据保持不变。此次补齐最终只读验收与源码身份：43 项唯一测试通过，6 图已存像素的 18 次比较全通道 0 差，72 阶段 finite/hash/统计重验通过，第十批冻结文件一致。新增审计器的阶段编号错误及修复回归均保留，没有重渲染或重解码。详细范围、失败和结果见 home-round11/resume-20261101/CLOSURE.md；最终源码身份以同目录 tool-identity.json 为准。公司、摄影效果和产品接入边界不变。
