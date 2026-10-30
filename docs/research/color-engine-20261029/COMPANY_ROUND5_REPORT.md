# 第五批公司色彩研究：有界根因与BT完整路径（2026-10-30）

**状态：VERIFIED_BOUNDED_RESEARCH / PRODUCT_DISABLED；仍非产品接入批准。**

本轮接续`research/color-engine-company-20261030` / `be2c33b6b23986b1275ad29244e0cab3cc41412b`，fetch后远端无增量。原独立研究树起始clean，保留全部旧证据和外部source2/source-patch/source-trace；不改产品src、UI、任务桥或Spektra注册，不碰PV4/5/6/旧recipe。R5.5能力标签只列“负片/印相模拟·研究中”。

## 保留的第四批结论

V2完整身份、baseline Dispose前复制和回调生命周期保持，本轮未重复整轮加固。BT受控`-Os`导致断点从`0x3da66555`折叠为`0x3da66554`、1ULP分支翻转已经证明；旧兼容补丁不更改。公司普通原/补丁11/11、受控Os原10/11→补丁11/11及家庭原10/11的历史均保留。本轮只补实际BT全路径与有界回归，不能写成泛化的“GPU精度问题”，家庭RTX5060Ti补丁仍NOT_RUN。

公司本轮设备仍GTX1650/572.70，Vulkan1.4.303，实际版本/hash见company-round5/environment.json。客户端日期Europe/Berlin，主机+08时钟单列；不更换驱动或持久环境。原始外部source2保持clean。

## 1. 曲线下降：直接来源已闭合，上游物理来源不臆测

### 实际消费路径

固定Spektra commit `c1447c6ab618fa4b1cb11fd2fe12826855007059`：

1. `Profile::from_json`读取profile `data.density_curves` (256×3)，逐通道减nanmin。原小负数密度不是在此被逐点clamp，减常数不改变斜率。
2. `build_interp_tables`以原log_exposure / density_curve_gamma为横轴（本配方gamma1），double算inverse-step，上传float32表。
3. 真实`node_expose_log`先`log10(max(exposure,0)+1e-10)`；这里的RGB是相机光谱响应/曝光后的三通道，不是显示sRGB或TIFF16。
4. `node_film_curves`调用`spk_curves.comp`右偏二分、分段**线性**插值；端点固定。没有运行时cubic spline，也没有从CDF重拟合film表。
5. 开关关闭DIR/grain后进入enlarger spectral→paper exposure→paper自己的CDF曲线→scan spectral→RGB/gamut→scanner finish→CCTF→RGBA16。paper拟合与film采样表不能混为同一个节点。

### 资源与拟合区别

|固定资源|原采样表R/G/B下降区间数|最小原表邻接差R/G/B|
|---|---|---|
|Portra400|25 / 21 / 27|−2.7996e-4 / −2.3890e-4 / −2.7749e-4|
|Ektar100|27 / 23 / 25|−2.7941e-4 / −2.4008e-4 / −3.1192e-4|

两个runtime profile与上游spektrafilm固定`3bb2c2d2801ff68b92019cf1dbcbb133d60832bc`完整bytes一致，其profile最后修改`ad5c8d2b5bbf295f6d3912091f3b137b0aef805a`（refit/update license）。Spektra bake脚本是copy_json，不是这一批Vulkan移植新造非单调。来源hash见profile-lineage.json。

同资源`density_curves_model`的正幅度/正sigma CDF和`density_curves_layers`之和在256点上单调，layers和/CDF相差≤2.23e-16；C++真实模型评估与SciPy ndtr相差≤4.45e-16。原sample与CDF最大残差约0.0036–0.00454，所以它们不是同一条表。当前grain关闭film路径根本不把CDF/layers替换进来，不能误称“运行时CDF拟合过冲”。替换成CDF会改变原模型/结果，未经新版本验证不做。

### 有界最小复现和两个实际参考

复用第四批neutral 17×1025的已有log_exposure/film_curves_exact tap与hash，只取11/7个输出下降附近输入；不重跑巨型全图日志。新小C++驱动直接调用原loader/table builder、实际GPU曲线shader，再读取小query结果。Portra534、Ektar531个RGB查询包含受影响点、256节点、255中点、域外和mixed-channel输入。

- **NumPy2.4.6 / `b832a09cf2a169c833dd2371e7c07aa00b293242`**，实际`np.interp`。
- **SciPy1.18.1 / `e4e854eaa8f18d807cd3496028e257e36caa93cc`**，实际`make_interp_spline(k=1)`→BSpline评估；端点使用相同常数延伸，不用k3。

源码下载与安装Python模块只有CRLF/LF差别，规范化内容完全一致；原始/规范化hash、二进制hash和许可证分别记录。它们是该1D插值问题的独立实现路径，不假装独立光谱胶片引擎。上游Python原引擎未装Numba，完整Python运行NOT_RUN，来源文件阅读不冒充运行参考。

实测：原double表与float32上传表都复现负斜率；两参考误差≤8.89e-16，GPU未越出相邻原表值范围，受影响点与旧GPU trace **bit-exact**。受影响点GPU vs原double最大差Portra7.77e-10、Ektar1.65e-9，而对应密度下降约1e-5，不能把运算舍入当直接原因。

两点最小fixture以R通道index11→12为例，Portra原density0.00042035843→0.00029579627，Ektar0.00042205657→0.00029559178；旧实际query递增时，两参考输出分别下降约2.48242e-5 / 2.52023e-5，不需要GPU/显示/量化即可复现。fixture和独立小脚本在研究分支，可无外部engine重跑。

**根因结论：这批观察到的下降，直接由固定原始profile采样表的局部负斜率经正确线性查表传递。无插值过冲证据，不是BT补丁或16bit量化造出来，也不是float舍入凭空产生。** 现有仓库没有完整原始测量/数字化/生成闭环，不能进一步断言负斜率是物理胶片真实能力、哪一步拟合错误或有可见缺陷。

决策：**不修曲线、不改profile、不平滑/钳制/放宽阈值**。本轮没有新的曲线候选版本，故不虚报其全场景影响验收。

## 2. BT输入完整路径：不再拿旧9组合代替

显式使用引擎支持的`ITU-R BT.709`与`ITU-R BT.2020`、`input_cctf_decoding=true`，film为Portra/Endura及Ektar/UltraEndura。grain/glare/halation/DIR/autoExposure关闭，普通原/补丁、只替换decode shader的原Os/补丁Os四条件；旧resources与build不覆盖，复制至round5独立overlay。

共**64个小case**：两色域×四条件×两胶片×（断点7×21、渐变9×257、相同断点高度1、饱和/混合RGB3×12）。断点含±6ULP及负值、0、.018、.081、.18、.5、1、2；渐变含−.01..1.2并放入一处断点mixed RGB；饱和图含原色/间色及负/>1。没有扩产品输入范围。

每case实际：input→完整engine→CPU RGBA16复制/释放→原始RGBA16与sRGB ICC TIFF16保存→完整99字段resolved参数/input/DLL/resources hash→**真正新进程重新open/render**→RGBA16/TIFF16像素回读。64/64保存和重开一致。隐藏内部默认（如scanner unsharp）以固定source hash单列，未伪称99字段JSON包含全部内参。

### 原/补丁实际改变范围

- 公司普通原/补丁：所测16配对（含补充输入）均bit-exact；补丁普通/Os亦一致。这里不包含家庭设备。
- 受控Os原/补丁，断点7×21：BT709/2020的Portra各21像素变化、最大9个U16code；Ektar各21像素变化、最大4code。输出变化列8/9/10、行0..6，alpha不变。
- **真实完整路径内decode tap**确认：只有列9、输入bits`0x3da66554`在原Os与补丁Os不同；原Os power0.017999999225139618，补丁linear0.018055101856589317。其余输入包括`0x3da66555`分支不变。
- 被动完整stage trace不改变最终output hash。差异到`print_linear`仍只列9；`print_scan_finish`扩散到列7..11，来源是固定scanner unsharp {sigma0.7pixel,amount0.7}默认，并未因grain/glare关闭而自动关闭。最终编码float列7..11有差，RGBA16仅保留列8..10。不能把整个final变化范围误写成decode分支范围。
- 渐变中实际decode列128（同bits）发生差异，经scanner邻域传播仍有微小float差，但最终RGBA16本组全0差异；这不是没走BT，而是后续处理/量化未留下U16变化。
- 同断点内容高度7与1，所有tested variants/spaces/films每行exact（16项）；只说明此复制内容/尺寸条件，不推广任意尺寸/缩放规则。
- 饱和/混合图Os修复最大U16变化：BT709 Portra6/Ektar7，BT2020 Portra4/Ektar3；对应变化9/12像素范围见bt-supplement，负/>1端点不被偷偷删除。没有以这些code差作可见性或色准结论。

`SpektraBtJoinFloatV1`仍固定**旧float兼容断点**，不把它叫唯一规范曲线；Colour double rounded常数和FFmpeg连续常数约定差别沿第四批记录保留。家庭RTX5060Ti补丁复验NOT_RUN。

## 3. R5.5能力及许可边界

更新BACKEND_CONTRACT_V2增补与R55_CAPABILITIES.json，产品仅“负片/印相模拟·研究中”，默认关闭、不注册。任意grain_seed仍不支持且无扩展；alpha丢弃/opaque、CPU RGBA16最终clamp非HDR、节点边界取消、result独立所有权/stride/释放、串行session规则保持。

**LightTable非商业条款不等于Spektra GPL代码组合分发批准；Spektra代码许可也不等于每项profile/数据源权利已批准。** 互不替代，DLL/进程/LUT不是自动免责。NumPy/SciPy BSD参考和GPL来源资料仅隔离研究；external engine/resources、大图、SPIRV/DLL不提交产品或研究Git。

## 4. 执行/失败/未运行

已执行：原有trace hash校验；实际曲线probe与两参考；两点最小复现；profile来源bytes核对；64+64 render/reopen及TIFF16；actual BT decode/传播；小范围原/补丁pointwise各1/1；证据校验脚本通过。没有全套重复加固或巨型日志。

本轮诊断错误：一次inspect函数对NumPy dispatcher不适用、raw URL DNS查询失败、初始参考Python原始hash因CRLF不一致、含中文能力JSON最初系统默认GBK编码后显式转换UTF8，均保留事实；git获取固定上游成功，规范化源校验通过。不是像素门禁失败，无阈值更改。decode-trace build保留原有两条C-linkage warning，不叫零warning。原Os历史失败仍保留，未掩盖。

未运行/不做：D80014 **NOT_RUN_SOURCE_ABSENT**（沿第四批，不扫描/不重试）、真实D800取消、自然验证集与物理校准、家庭补丁/Metal全图oracle、新曲线修复、seed扩展、完整任意尺寸/空间效果验收、产品UI/项目/任务桥接入与全套PV回归。WB余量冻结，不调参、不合并收益。

产品src/tests和decoder V2相对base零差异；并行产品R5.5树在本轮新增其自己的dirty文件，已只读记入preservation-check，不改动/还原它们，也不把这些产品改动归入研究提交。最终独立研究分支推送及远端SHA在交付回复中读回，不将本提交自hash写入自身内容。
