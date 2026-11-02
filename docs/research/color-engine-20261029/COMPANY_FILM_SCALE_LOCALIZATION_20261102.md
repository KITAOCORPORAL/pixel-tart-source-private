# 公司film_scale_and_expose内部定位（2026-11-02，Asia/Shanghai）

**结论：公司侧差异承载已收窄到Hanatos upsample完成输出、曝光之前；跨端首个具体operator/CPU准备仍未确认，需要家庭一次同版gray被动tap。本轮没有算法修复依据，不修改数学。**

## 1. 接续和边界

fetch `research/color-engine-company-diagnostic-20261102@7309911e1ad501b517877a073a86e468a9507941`，无更新，起始clean/unpushed0。读取AGENTS/POLICY/CHECKPOINT、公司公开诊断报告、build-confounds、failures-and-remaining与固定实际源码。

家庭第十至十三批及公司完整三case诊断、构建、收据/失败保持不动。产品src/UI/模型/任务桥/default/PV4/PV5/PV6/MatrixV2不改。SharedIncrementEnvelopeUnsharpV1仍defaultoff、NOT_ADMITTED，无新候选/包络/曲线/旧unsharp改动。RAW0、20MP0、六张自然图0、stream重跑0、BT0。

## 2. 先做已有证据分析：GPU0

主输入严格既有gray-boundaries17×33RGB。whole比较1683个float值，1520值不同；原首差(2,0,R)，最大abs差坐标是x32、y0..16、G共17个位置。本轮用代表(32,0,G)，没有扩数据集。

|位置|家庭→公司|abs差|相对差（对称max分母）|binary32 ULP|
|---|---|---:|---:|---:|
|首差(2,0,R)|0.9905756115913391→0.9905756711959839|5.96046448e-8|6.01717229e-8|1|
|max代表(32,0,G)|10.75505542755127→10.755059242248535|3.81469727e-6|3.54688633e-7|4|

按同号有限值分组，近零描述界1e-6不是接受阈值；相对差在任一为0时不定义，异号不把orderedULP当同量级误差。当前whole：
- nearzero非零363值，345变化，maxabs1.13687e-13、maxrelative4.23224e-7、max5ULP。
- 正常负数3值，0变化。
- 正常正数1317值，1175变化，maxabs3.81470e-6、maxrelative2.41285e-6、max23ULP。不能因abs小忽略量级，亦不因nearzero relative较大认定错误。
- 任一为0/跨符号类别本观察点均0值；完整量级与r7的3个band统计在offline-gray-analysis.json。没有跨设备PASS阈值。

输入file/pixel、99resolved和73resources/34SPIRV都exact，已可排除这些身份不一致。**无法据同配置证明实际上传CPU矩阵/LUT字节一致**：旧家庭和公司包只存compound-stage output，未存运行时setup和tc/b中间量，所以已有材料不足以找首个子操作。

## 3. 固定实际操作顺序，不凭stage名猜

源码锚和flow见source-anchors.json、operation-flow.json：

### CPU准备/上传

1. `film_sensitivity`从profile log_sensitivity做native double pow10/NaN处理，并按实际cameraUV/IR/filter分支/照明计算；不是PythonPillow算的。
2. `SetupCache::tc_lut/build_tc_lut`读取blobHanatos谱表，按film sensitivity/adaptationwindow权重做81波长收缩；actual默认无spectral Gaussian blur、adaptation surface关；input xy gamut compression及bilinear重映射仍参与。最终runtime LUT是double192×192×3，**不是原blobhash就等于最终LUT字节**。
3. `tc_b_matrix`用film参考照明D55求xy，实际input ProPhotoRGB→XYZ/CAT16，row-major double9矩阵。
4. `upload_persistent_f32`在native `upload_data`把double实际窄为float32写staging，再原Vulkanflush/copy上传；本轮直接观察postcaststaging字节及device读回。
5. `node_exposure`对冻结EV0计算`pow(2,0)`，通过原Blur::affine上传float scale/offset。

### GPU三子操作

1. **spk_tc_b**：rowmajor XYZdots→brightnessXYZsum→分母floor1e-10→chromatic tc→tc clamp；输出tc2/b1。
2. **spk_lut2d_cubic**：tc×(side−1)→MitchellB=C=1/3的4×4权重、LUT mirror indices、累加/weight_sum归一→乘brightness，输出upsample RGB。
3. **spk_affine3**：input×scale+offset，输出compound-stage RGB。

冻结resolved sceneLatitude=false，因此之前可选scene量映射没有运行；inputCCTF=false/identity geometry不在该stage新造其它数学。

Python/NumPy只加载已验证binary32并经ctypes传输，input-device exact证明本次数据未变；Pillow仅记录版本，**未进入本数值路径**，不作为同等嫌疑。NativeCPUsetup中的pow/illuminant/window/contraction/gamut以及GPUdots/divide/Mitchell都真实参与，仍是应区分的解释。OS/driver/GPU/CPU编译/JIT未隔离，不泛称GPU精度问题。

## 4. 唯一一次最小被动观测

外部独立`FilmScaleSubstageTapV1`，root `E:/Kitao-Research/color-engine-scale-localization-20261102-v2`，source/build/输出都新建。从已封公司diagnostic源码复制的原entry/数值core保持；只加：CPUhost数组写出、相关postcaststaging字节写出、7个实际device readback、相关pipeline creation数学模式记录。

原shader源码/原SPIRV资源、CPU hanatos/setupcache/numeric/colour/params数学source、Release -O3 -DNDEBUG/编译工具/优化开关/dispatch顺序保持。没有改变shader/FMA精度模式或参数。不同DLL版本hash和完整pre-run身份见public-gray-taps/execution-before.json。sourcegenerator对家庭仅允许原两处授权根字面量不同，其余source身份必须匹配，不借家庭常数当公司。

实际公司GTX1650/572.70：**engine_create1/open1/whole_render1**；stream0/BT0/其它case0，无自动GPU重试；新增7个GPUcopy/readback、0个新增数学shaderdispatch。初始化math probe仍原有，未独立计底层dispatch总数。最终RGBA16与本基线公司gray封存结果逐通道**0差**。

### 实际值链

- `00-input.device.f32`与prepared input bits exact。
- tc_b矩阵：CPUdouble9窄化 vs实际uploaded9 vsdevice9，逐bit0差。
- runtimeLUT：CPUdouble110592窄化 vs实际uploaded110592 vsdevice110592，逐bit0差。
- 实际exposurescale[1,1,1]、offset[0,0,0]；`03-upsample`与`04-exposure`1683float逐bit0差，后者又与公司封存compoundstage逐bit0差。
- 所以**公司侧差异承载已经在upsample完成输出出现，曝光不增改这次二进制值**。与家庭compound比较仍1520通道/max3.81470e-6；但这不是homeupsample成对比较，不能越级说跨端首差就是LUTshader。
- 首差位置input[.18,.18,.18]→tc[.445643097,.520524263]、brightness.518009961→upsample[.990575671,.967955351,.897750080]，曝光原样。
- max位置input[2,2,2]→相同tc、brightness5.755666256→upsample[11.006396294,10.755059242,9.975000381]，曝光原样。

### 实际数学模式

engine真实capabilities报告`Vulkan probe passed (precise product + fused fma; full shader parity pending)`，相关pipeline fused_fma_runtime1/specialization_id0/bool1。**tc_b/lut2d_cubic/affine3三个shader都未声明constant_id0**，所以该值不是这里选择某另一种运算分支的开关；mathprobe通过也不证明matrix/divide/Mitchell跨端bit相同。未变模式试图归零。

## 5. 可排除与精确缺项——到此停止

现在可排除：input/99resolved/resource身份不一致；公司CPU已捕获double窄化/actualstaging/devicecopy偏离；公司EV0 affine新增该误差；观测改变公司最终像素；首差只在unsharp后才出现；Pillow参与该数值计算。

**尚不能排除**：家庭CPU准备已生成不同矩阵/LUT或窄化字节；家庭tc/b输出首先不同；LUTinterpolation/brightness multiply首先不同；家庭曝光输出改变二进制值；driver/JIT/hardware/compilernative作用各自贡献。

家庭对应tap缺失，因此本轮回答是**部分缩小位置，尚未确认成对首个operator/准备环节**。必须提供家庭单次gray同版tap和真实pre-run身份，再先比较actualuploadedbyte、再比较tc/b→upsample→exposure。不能伪造家庭常数、用CPU手算代GPU或把公司数据贴成home。

此时**没有证据要求算法修复**，本轮没有pixel修复、profile/curve/clamp/floatingmode/driver变化。若以后要控制变量，先另列假设与单最小实验；不把max1U16的小包结果推广所有照片或自动宣称可见性/产品通过。

## 6. 失败/保全与交付

一次pre-GPU准备路径错误：错误拼出的HOME/workspace外部repo不解析，初目录/emptystdout/setup-failure保留，0build/0GPU；显式读取现有diagnostic和CMakeCache的真实路径后新-v2目录准备成功。构建成功但原8warning（C-linkage/deprecatedfilesystem）保留；唯一gray执行无失败重试。离线分析脚本首次repo.parents层数错误、只读proof首次GBK读取UTF8失败均修正后只做CPU读取，0新增GPU，不隐瞒。

Git只提交工具、公开gray输入/子输出/小矩阵与曝光tap、metadata/hash和report。**完整runtimeLUT及film-sensitivity数组仍只本地**，不把profile-derived资源当公开输入任意分发；local-only索引给hash、形状和已计算bytecomparison。public-gray-taps是明确subset，run.json引用的完整localtap不能在Git缺文件时伪报完整validator通过。

家庭最小接续工具、完整localcapture validator/pairedcomparator和精确缺项在REPRODUCE_SCALE_LOCALIZATION_20261102。公司不继续增加GPU/范围，旧三case/第十至十三批及公司收据保持hash。更新CHECKPOINT/能力表使用Asia/Shanghai，不改历史时间/NOT_RUN。

研究结论不等于产品接入或GPL/逐资源分发批准；LightTable非商业条款不替代Spektra权利。完成后独立研究提交推送并读回LOCAL_HEAD/REMOTE_HEAD。
