# 家庭第十三批：受约束 unsharp 是否值得继续

日期2026-11-01（Europe/Helsinki）。基线research/color-engine-home-20261030@3f7fe01beafc4ed83d113013ab00b300292a7461，fetch/本地/远端一致，开工clean。产品src/UI/任务桥/default/PV4/PV5/PV6、第十批完整20MP和第十一/十二批证据均不修改。

## 最终判断
**一项候选技术成立，但V1过于保守，不批准摄影效果或产品接入。值得保留“可选约束锐化”的研究方向，不值得将当前V1直接作为推荐锐化。** 原因不是仅看零值：V1消除本六图新增下冲，却在理想阶跃/脉冲/饱和阶跃上完全退回原输入，并削弱细节增益，仍放大噪声，部分边缘色相响应明显不同。只封存结果，不在本轮继续调参或建立第二个候选。

## 1. 两个成熟实现：源码与真实数值运行
固定源码现成功取得：curl直连先重置/超时，随后公开HTTPS获取固定路径成功，未读取token、启动浏览器或改持久网络配置。文件、依赖、完整commit、许可证和编译器/exe/函数body hash见reference-files.json、reference-build-v2.json。所有外部GPL源、生成harness、二进制仅本地。原函数body保持，适配层只实现框架类型/allocator，未用自写公式替代参考。

### darktable
- commit fb0eb997e62c6dd52966cd35911c74afa150e209，src/iop/sharpen.c，原process（264–381）、init_gaussian_kernel、commit_params（383–393）。源SHA256 **360e3051f38cc7ba20031d6ddc0898b47aeb28f5720c39b6420c90aec5b4b31e**。
- Lab L*名义0..100，只改L、a/b原值透传。UIradius是sigma，内部radius=2.5*UIradius，rad=min(12,ceil(radius*roiScale/iscale))；本次sigma.7、amount.7、threshold0和.5L*，rad2。高斯截断与边界：先竖再横，2px边缘复制，不是当前引擎reflect。tiling overlap=rad。
- threshold是对差分绝对幅度减阈值的软门槛，不是局部上下界。10→90L*阶跃：threshold0输出约[-2.04245,102.04244]；threshold.5仍约[-1.69245,101.69244]。低幅细节/噪声被阈值抑制，但大阶跃下冲仍在。

### RawTherapee
- commit db575c6690d30ce6428c0e1383bec8b8f1fb8d21，原ImProcFunctions::sharpening（351–490）与sharpenHaloCtrl（40–101），源SHA256 **eca57400ccc9d2a5523070f60bddf34749446bebacb299bf024af29b0a4a44be**。真实gauss.cc、Threshold::multiply和buildBlendMask编译执行。
- Lab内部L=327.68*L*；sigma=radius/scale，amount按百分数。本次radius.7、scale1、amount70%，contrast=0明确走原all-one blend分支，edgesonly/rld/preblur关闭。Open threshold[0,0,false]用于控制，正常阈值[20,80,2000,1200,false]，halo100用于约束对照。不是RT应用默认配置，未将其默认disabled/amount200/contrast20冒作本次值。
- .7处真实标准Gaussian走递归实现（本harness标量CPU非SSE），与当前FIR不是同一个滤波。halo使用平滑邻域与滚动min/max再回拉，外2px原生L保持。输出L*适配层的缩放往返可造成≤7.6294e−6边缘舍入差；已与实际边缘锐化变化分开统计。
- 同10→90L*阶跃，open输出约[-2.47963,102.47963]；正常阈值约[7.54204,92.45796]；halo100为[10,90]。halo100也使该阶跃和脉冲退回原幅度，展示“无光晕≠保留全部锐化增益”。

### 实际比较范围和不可对齐项
9夹具×5配置共**45次CPU原函数运行**：平坦、阶跃、脉冲、饱和Lab边、负L*/>100、边界、低细节、细节、已知噪声。全有限、a/b逐值透传。reference-cross-comparison给每夹具threshold/halo/两实现的max/mean差异；不是只阅读。

不能把L*=100*linearRGB当实际色彩转换：少量scalar形状探针仅数值归一对照，明确标注，不做RGB/Lab成像等价或色准。饱和Lab与饱和linearRGB也不是配对真值。两个完整应用、ICC变换、SSE与标量一致性均NOT_RUN。本轮是相关具体算子的固定域比较，非复现其照片管线。

两参考及依赖均GPL3-or-later；仅本地行为研究，不复制进产品，组合分发未获兼容批准。原引擎及资料许可仍另门禁。完整节点模板填写见UNSHARP_NODE_COMPARISON_ROUND13.md。

## 2. 唯一候选定义（运行前冻结）
名称 **SharedIncrementEnvelopeUnsharpV1**，默认关闭；主入口需要显式版本化启用参数，缺项在GPU前拒绝。原PortraA没有新增开关、后端或默认值。

输入：既有print_linear，线性sRGB D65 float32，允许负/>1，orientation1。原sigma=.7px、amount=.7、Gaussian truncate3及反射语义保持。先用原GPU blur/lincomb算legacy U。对RGB各通道以原5×5邻域min/max求“允许U-I增量的最大缩放”，取全通道共同最小gain；gain1直接返回U，gain0直接返回原I，中间gain乘(1−2^-20)安全量后I+gain*(U−I)。不是每通道各自clamp，不整体抬黑，不在最后裁负。

借鉴的是参考展示的局部环境约束需求，**未移植Lab阈值、RT滚动环境公式或其色彩数学**。新策略独立写作。共享gain仅近似保留RGB增量方向，不保证色相不变，更不是皮肤保护。

**尺度限定**：本V1仅完整输入stage ratio1，sigma.7、包络半径2、halo2。内部条带携带真实邻域，只有全图边界reflect；产品800/1600或缩放预览NOT_RUN/NOT_ADMITTED，不假装已完成尺度一致性。

候选源码/SPIRV/runner/exe哈希、固定参数/资源清单见candidate-definition、candidate-build-v3与evaluation-plan。代码/执行器均隔离于N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1；旧source/build/resources不覆盖。

## 3. 冻结A/B执行与技术结果
先9合成fixture whole/rows7，之后仅取既有6张1200边代理的**已保存print_linear阶段**做whole/rows17/rows37。不是重解码RAW，不重新执行上游胶片/纸张链，不是再造一轮六图兼容验收。

- 6张输入均按第十一批stage SHA重验，原source/73项资源+其中34SPIRV保持。
- 每图legacy scan_finish、encoded及最终RGBA16与旧保存结果**全部通道0差**，证明独立重放未移动默认结果。
- 候选和legacy的whole/stream在float、encoded、RGBA16三层均**全部通道0差**，尾行/边界包含；未放宽门槛。
- 合成与自然完整图候选均无新局部包络外值、无非有限；负值/>1夹具仍保留原负/>1，不是globalclamp。
- 全部执行结束device pool live/persistent=0。15项只读专项测试最终通过，包含默认关闭拒绝、实际字节重比、原结果不漂移、负/>1、退化保留、保存重开与TIFF16读回。
- 新进程测试重新读取保存candidate float/NumPy及16bit TIFF，逐值相等；PNG是明确8bit round展示。**TIFF无嵌入ICC宣称，非产品项目保存重开/色彩管理导出验收**。

## 4. 改善与退化同时保留

|开发图|锐化负分量|最终至少一零值像素|候选平均梯度/未锐化输入|相对旧输出Lab色相变化P95*|
|---|---:|---:|---:|---:|
|canon600d|643→0|308→0|1.115×|2.381°|
|canon6d|1517→0|1088→0|1.098×|2.676°|
|canon5d3|3822→0|3338→0|1.214×|12.837°|
|nikond5100|700→0|350→0|1.114×|2.276°|
|sonynex5r|22324→0|11563→0|1.162×|8.166°|
|canonr6|0→0|0→0|1.223×|6.350°|

*输出Lab只是同源编码结果的响应差，统计a/b彩度均>2的像素；不是DeltaE色准。最大色相差可接近180°，尤其饱和/近裁切边缘，不隐藏在平均数后面。中位数0也不等于全图无影响。

### 细节与光晕
- 0.1→0.9线性阶跃旧输出[-.0204245,1.0204245]，候选[.1,.9]；脉冲旧[.359024,1.136320]，候选[.4,.9]。
- **阶跃、脉冲、饱和阶跃候选全图等于原输入**：并非代码全局关锐化，但在这些关键结构上效果等同不增加锐化，必须视为明显过保守代价。
- 合成正弦细节std增益：旧1.09786×→候选1.04977×；低幅细节相近。自然图只保留原平均梯度“增量”的44.7%–67.0%，不是保留了相同比例的真实细节。梯度包含噪声，不能拿它当视觉清晰度证明。

### 噪声与偏置
已知合成噪声std增益旧1.47229×、候选1.24829×，仍放大噪声，不是降噪器。自然不同图的均值变化有正有负，NEX5R候选相对未锐化meanY约−1.00789e−4，不能声称全局亮度严格守恒；也不是抬黑换取零值少。

### 色相、肤色
共享增量方向在有效幅度≥1e−5的点余弦接近1，最大分量残差约3.75e−8，但这只描述线性增量方向；改变增量幅度和避免裁切仍会改变最终色相/彩度。未加肤色识别/保护。

沿用Nikon小肤色ROI [310,390,35,60]：184/2100像素相对legacy改变；色相P95约0.592°，最大约9.853°；L变化约[-.415,.650]，彩度约[-1.533,.994]。自然皮肤仅这一有限shade-lit arm/hand，不是受控肤色集，不能据此批准。

### 画面观察与匿名偏好
已查看原固定6个局部的匿名X/Y对照，候选花卉暗侧极黑点减少、颗粒化/边缘锐利程度变化；雪景/花瓣和细噪声存在软化取舍。肤色与R6小差异不能仅凭缩图判断。图与映射仅本地review-v1；人工偏好NOT_RUN。应询问：哪个版本的叶片/花团细点更自然？花瓣纹理是否损失？手臂/篮球边缘是否过软或有彩边？噪声变化是否可接受？不得由小怪自行判更高级。

## 5. R6仅整理准入，不第二候选
沿第十二批固定资料整理profile有效log域[-3,4]、低端比例、hidden defaults；R6_EXPOSURE_ADMISSION_ROUND13.md与r6-admission.json。225000分量中19567≤−3，8.6964%；不自动曝光、不外推、不减黑、不改曲线。R6作为同一锐化候选开发A/B参与，但不宣称恢复其曲线丢失信息。六张始终开发集，不改称holdout。

## 6. 实际资源与调用数
- 原参考CPU进程45次；候选合成stage18次、自然stage18次，共**36个GPU阶段重放进程、4088次dispatch**，每进程同时算原/候选+原编码/打包。
- RAW decode0，上游film/paper管线render0，20MP0，新增照片/stock/相机0，像素候选1。
- stage组合A/B耗时：whole20.11–28.49ms，rows17 90.84–128.14ms，rows37 51.34–72.70ms；不含同一范围的完整管线，不比较纯候选速度，不外推性能。
- whole设备pool峰值最大65,361,648bytes，stream最大3,513,648bytes；OS进程高水位最大148,418,560bytes。**staging峰值未单列测量**，不能用pool/OS替代。
- 本机RTX5060Ti，WMI驱动32.0.16.1088，运行时GPU身份也实录；公司公开小包继续NOT_RUN，不重建诊断框架。

## 7. 失败、边界与后续决定
保留首次参考adapter编译失败、候选host编译失败与version日志类型修正；均未改原算法函数body或候选数学，只最终v3执行。新增default-off测试首次因子进程没有NumPy路径失败，修正测试环境后通过。全部旧日志保留。

ROI描述性包络首次以局部crop边缘统计，看似有越界；这是错误邻域上下文，原报告保留warning，review用**全图真实邻域**重算均0。技术门槛从始至终使用全图，没改阈值。低幅增量角度的浮点病态也保留原值并单独给有条件统计，不混为色相保持承诺。

当前决定 **KEEP_FROZEN_RESEARCH_ONLY / NOT_ADMITTED**。方向值得继续考察，但V1不能成为默认或已接纳产品候选。下一步先获取匿名人工偏好和独立验证场景，明确“限制光晕/保留细节”的目标；若以后修改包络允许量、阈值或尺度，必须另显式版本、预先冻结门槛，不能本轮悄悄调到通过。实际preview尺度、产品项目保存重开、ICC导出、取消/并发、跨设备、独立摄影评价和分发许可仍OPEN/NOT_RUN。

详见home-round13/的固定数值/哈希与执行收据；自然图、ROI、stage、原参考源、shader/host候选源和二进制全部只本地，不上传Git。Git只自编生成/调用工具与汇总证据。最终LOCAL_HEAD/REMOTE_HEAD通过实际push读回交付。
