# 公司第四批接续报告（2026-10-30）

**交付状态：PARTIAL / OPEN_RESEARCH_ONLY；产品接入保持禁用。** 本轮完成研究定义/生命周期加固、BT可复现根因与隔离补丁、公司设备验证和契约更新；D80014新候选端到端因缺授权源文件NOT_RUN，不拿旧阶段通过或合成重开冒充。

## 1. 正确接续与保护

- fetch `source-private/research/color-engine-20261029`，远端仍`ce292f2a067e77d1d48fcb5e90fb18b9d642466f`，相对指定commit无增量。
- 独立工作树`D:/AI AGENT/worktrees/color-engine-company-20261030`，分支`research/color-engine-company-20261030`，不切换/清理/覆盖旧树。初始worktrees/refs/HEAD/dirty/remotes已存`company-round4/*before.txt`。未reset、stash、merge、clean。收尾逐树branch/dirty条目与起始一致；一个并行产品chat仅新增upstream跟踪注记，作为外部状态变化单列，不归为本研究编辑。
- 已读本仓库AGENTS、POLICY、ROUND3_REPORT、BACKEND_CONTRACT_V1、REPRODUCE_ROUND3与实际工具/产品调用实现；后取固定Spektra源码并核对其AGENTS、ARCHITECTURE关键段、Windows文档与ABI。其macOS/Xcode全套与Python原引擎oracle不在本机条件内，明确NOT_RUN。调用gen-project后Xcode工程无差异，未改其UI。
- **家庭已成功构建SpektraLab、完成9组渲染，原版CTest10过1失败**，不是“未构建”。旧SPEKTRALAB_PREFLIGHT只属更早记录，本轮不以其阻点覆盖第三批已执行。

## 2. 公司设备（不是家庭性能迁移）

实际GTX1650、4GiB、NVIDIA572.70（Windows32.0.15.7270），Vulkan instance/device API1.4.303；有可用compute GPU。系统另有MuMu虚拟显示，非研究渲染设备。记录loader关于旧Bandicam layer/registry的警告，不安装/替换驱动。

使用隔离clang20.1.2、glslang16.6.0、CMake3.31.10、Ninja1.11.1.4、Vulkan-Headers `b5c8f996196ba4aa6d8f97e52b5d3b6e70f7e4e2`；工具包hash与家庭固定包一致。Python3.13.13、NumPy2.4.6、Pillow12.2.0、tifffile2026.10.15；.NET10.0.302来自workspace `.dotnet`。没有持久修改PATH、SDK或产品环境。客户端日期按Europe/Berlin，Windows主机+08时钟单列。

## 3. D80014缺源与矩阵float加固

只核对已知`N:/pixart/RAW/Real/nikon/...D800_14bit_FX_LOSSLESS.NEF`及工作区明确素材根对应路径；均不存在。公司CompanyRAW只枚举一级名称，5项ARW/RAF/CR3，无D800；未读这些图像、未扫描无关目录、未自行下载另一个“类似样本”。精确SHA与核对范围在`d800-source-availability.json`。

因此本轮实际候选`解码→float→preview→save→新进程reopen→TIFF16`的D80014状态为 **NOT_RUN_SOURCE_ABSENT**；旧stage14通过只是旧阶段兼容证据。无已授权且场景明确、可作为独立验证组的自然输入，未拓展布局/相机/白名单，未伪造开发/验证划分。恢复来源后按REPRODUCE_ROUND4显式跑新候选；不自动沿用旧阶段结果。

原MatrixDecodeResearchV1只记少量字段，不能完整重建算法。新增V2完整有效设置、WB、方向、矩阵/系数、native/managed依赖与研究实现/源码hash；旧V1只读archive、不升级/不重新解码；未知/缺失关键参数拒绝。新定义严格比对运行时实际参数，ABI/源SHA白名单继续保持。细则见BACKEND_CONTRACT_V2。

真实所有权问题已确认：HighBitDepthImageBuffer.Dispose会释放GraphPixelAllocation lease，scope可复用原数组。baseline现在在Dispose前ToArray；回归实际污染复用数组而copy不变，disposed访问拒绝。回调生命周期采用测试过的install/body/detach/clear finally；取消不跨native ABI抛出，逐行float和TIFF、产品preview传token。没有宣称真实D800 native中断验收已跑；缺源仍NOT_RUN。

实际 .NET build通过。14项加固测试通过，6项旧recipe拒绝通过；另独立fixture 8×4含负值/>1，在真正新进程完整域/尺寸/全数据hash/finite校验后实际产品preview/TIFF16与保存前一致。`synthetic-reopen.json`明确synthetic=true，不能替代D80014。历史观测参数/matrix的冻结检查通过，缺V1字段只从冻结定义补入测试，非重新实拍/解码证据。

初次14项测试有1项仅错误信息断言不匹配（未知版本已被拒绝，实际“Unknown legacy field”而测试期望“identity”）；保留初始失败日志，改断言后通过，不改算法门槛。

WB余量第三批已实际生效且明显改变去马赛克/显示，本轮不调参、不重跑收益、不合并matrix统计；拒绝产品应用结论保持。

## 4. BT：先证根因，再隔离修复

### 实际条件表

|条件|原版|隔离补丁|
|---|---|---|
|家庭RTX5060Ti/610.88，第三批固定build|10/11，case114/index13约5.50964e-5失败|NOT_RUN|
|公司GTX1650/572.70，原固定glslang参数|11/11|11/11|
|公司同设备，只把decode shader用同glslang `-Os`编译|10/11，case114/index13约5.51020e-5失败|11/11，原阈值不变|

公司原始SPIRV SHA `4b71817eed371b305d985eae01b92b75692a56f1bd07ad9edf72a59e0a130f3d`与家庭记录一致，源hash也一致，不能把公司原版通过说成家庭失败不存在。DLL构建hash不同已分列，不能套家庭DLL allowlist。

`bt_probe.cpp`围绕断点±6ULP，CPU双精度算式、float常数约定、输入/输出bit-pattern、分支均保留。原C++ float断点`0x3da66555`=0.081247963011264801，index13输入`0x3da66554`=0.081247955560684204，CPU期望线性段0.0180551012357076。公司原SPIRV运行读回断点`0x3da66555`；`-Os`常量折叠后`0x3da66554`，严格`bp>value`变false进入power，输出0.017999999225139618。受控复现证明**断点重新计算/折叠的1ULP变化导致分支翻转，rounded BT本有不连续，放大成5.51e-5**；不是泛指GPU精度不足。家庭具体driver机器码未捕获，精确哪一步JIT优化仍为推断，不能声称已证明家庭全部机制。

隔离SpektraBtJoinFloatV1只用bit-pattern固定原float兼容断点。补丁`-Os` index13输出0.018055101856589317，误差6.20881718e-10；完整pointwise130cases通过，原阈值未动。source/patch/SPIRV/tool hashes在清单。补丁源码由工具仅在外部checkout生成，GPL引擎实现/资源/DLL不移进产品src或包。

### 两参考门禁与语义边界

Colour0.4.6 `0c6f868...`实际Python函数、FFmpeg n7.1 `b08d796...`原C函数隔离编译运行；下载源码按完整commit读回hash与执行件相符。具体入口/许可/数值见BT_NODE_COMPARISON_ROUND4和bt-reference-comparison。

Colour使用double rounded常数断点0.08124794403514049，与旧float测试不同；FFmpeg使用连续常数、负输入clamp，也不是可混用曲线。NumPy float32 pow亦得`0x3da66554`，不能拿任意libm重算合同身份。**补丁固定旧兼容语义，不宣称它比Colour更“规范”或唯一标准BT曲线**。规范意义曲线若以后要改，必须另版另门禁。

## 5. 公司小图候选/重开/导出与中性下降

原/补丁均运行linear、display-sRGB、neutral_log三输入×Portra/Endura、Ektar/UltraEndura、TriX/Multigrade三胶片=9组。关闭grain/glare/halation/DIR与autoExposure，输入配方/全resolved参数/hash落盘。每组重复、独立进程重开（核验input/DLL/resources hash）及sRGB ICC TIFF16逐像素回读均通过；9组原/补丁bit-exact。这9组不进入BT输入decode，不拿它掩盖修复会改断点像素。没有借用家庭耗时，不给普遍性能结论。

公司ABI另测alpha .125→65535、grain_seed=42拒绝且参数不变、原/补丁render中取消−7并等线程结束才release。上游生命周期5case通过（copy ownership、multi-session/cache ledger、session/pool、独立result活过engine等）；no_halation/no_couplers rows1/7/17与whole/reprint逐像素一致。随机seed扩展未做，能力仍UNSUPPORTED。

中性log渐变Portra11、Ektar7个通道邻接下降均−1code（TriX0）。独立trace checkout不修改输出，回读仍与原render bit-exact；log_exposure递增而film_curves_exact已递减，最终编码float亦降。保留每个index/channel邻域全部阶段值与hash，证明不只是16bit量化，也不由BT补丁引起。具体profile曲线/拟合原因和可见程度尚未闭合，**不改阈值/资源、不直接认定视觉缺陷**。display输入TriX另有下降，不能把neutral_log的0推广所有recipe。

## 6. 许可与产品边界

技术实验不等于代码/资源批准。继续沿SPEKTRALAB_LICENSE_REVIEW逐项门禁：GPL代码组合分发、CC BY-SA资源与数字化资料表权利独立审查。DLL、独立进程、烘焙LUT不是自动免责方案。本轮只交自编研究驱动/变换工具、数值、hash和文档；外部完整源码、资源、图像与二进制留本地隔离根`D:/AI AGENT/tmp/color-engine-company-20261030`。

BACKEND_CONTRACT_V2给产品会话可评估的最小调用、已测/不支持能力、RGBA16 clamp、alpha丢弃、取消粒度、释放与保守线程限制。不重写灯箱/8bit胶片适配/UI/项目模型/任务桥，未宣称接入完成。产品src与指定base无差异，旧PV4/5/6及第三批配方历史不改。

## 7. 未运行/残余门禁

- D80014新候选真实全链、真实RAW运行中取消/异常：缺授权文件，NOT_RUN。
- 自然开发/验证保留组、跨机型/布局：NOT_RUN，不撤白名单/ABI。
- 家庭补丁复验、Metal/Python全图oracle、主观偏好/C1/相纸物理校准：NOT_RUN。
- 产品灯箱/项目保存重开与默认链集成、完整旧golden回归：NOT_RUN（产品未修改）。
- seed新版本、随机空间效果所有条带/尺寸规则、普遍性能/物理VRAM门禁：NOT_RUN。
- 代码及资源商业分发许可：未批准。

完整执行/失败/未运行与artifact清单见company-round4；最终独立分支推送SHA及远端读回在本次交付回复，不将提交自己的hash写进自身文件造成伪循环证明。
