# 第三批实际结果（2026-10-29）

状态 **OPEN / 本批无候选通过产品门禁**。冻结产品基线 f8beaa37、接续研究3d39dc9e；不改产品src、Grok布局、PV4/5/6及旧recipe。矩阵、WB余量、SpektraLab分别评价，均为独立默认关闭研究工具。

## 1. 矩阵float：闭合一段真实验证链

共享 `stage_validation.py` 用显式异常替换assert，统一 analyze/render/ROI/理论scale输入验证；检查源/DLL/架构、域、四阶段唯一、六文件唯一、固定布局、长度/哈希、候选sidecar、非有限值。18项针对性测试（含篡改、重复、缺失、错域、长度、布局、优化模式）见 round3/shared-validation-tests.txt；测试使用稀疏占位文件和mock哈希检验分支，同时小文件真实SHA测试，不当自然数据测试。真实stage10全哈希分析通过。旧大缓冲及失败目录未清理，所有像素仅本地。最终回归17 PASS/0 FAIL，未声称全套PV4/5/6 golden全部重跑。

`DisplayOracle` **直接调用产品实际函数**，不是复制公式。全图 36,329,272 像素 × 3通道 × 整数/float × EV0/−2 四路，Python Display对产品函数最大绝对误差≤1.999e-15（预先固定门槛1e-12）；无阈值放宽。整数输入同时复现产品 `/65535f` 的float32归一化（不是以float64替代），四份完整统计/哈希见round3/display-full-*.json。

新 `tools/color-research-decoder` 在单次原生解码进程的矩阵前回调计算float并衔接产品后续，**不再要求先写uint16缓冲然后离线重算**。这仍不是LibRaw内部float去马赛克：WB/黑位/AHD维持整数，原生整数矩阵还会在回调之后运行完成原流程；float数据只在研究返回值/新变体使用。`MatrixDecodeResearchV1` master SHA 与上一批候选一致 DD923C...2D228D，负值22、>1通道6627。

实际调用产品 `RenderPreview` 面积采样、`ApplyExposure/Display`、`FillRgb16Row` 和 `TiffExport`。研究桥接借用历史精度合同以调用现有函数，已在源码/契约明确：OutputBits16是原native精度，Image和新recipe是32f；不注册产品默认。保存浮点master+域+hash+版本；新进程重开实际检验recipe/主数据SHA、重新生成预览与TIFF16，矩阵/基线/余量三种均预览bit-exact与导出像素hash一致。此处是**研究变体重开**，没有测试灯箱或产品项目格式。另6项实际CLI recipe拒绝测试覆盖未知版本、默认开启、错域、错backend、长度及mode/version组合，不读RAW且均拒绝通过。

800px产品面积预览EV−2：matrix vs baseline平均绝对差1.1357e-5、最大0.0023716，8位不同像素3719；它说明变化可落入显示值，不证明审美收益。TIFF16是显示映射后sRGB16；负/>1只在float master保留，不能将该TIFF称HDR。

### 无阶段落盘/第二次解码的性能
三个独立进程/路径，decode窗口均含一次源hash、native解码和返回float，source准入hash另在计时前；不含master/preview/export落盘。D80012开发样本，三次中位：baseline1790.87ms、matrix1746.20ms、headroom1733.14ms；matrix相对−44.67ms，不可把少量缓存/GC/实现差异外推为普遍提速。峰值decode baseline约1062.8MB、matrix约837.3MB、headroom约837.2MB（十进制，进程不是算法独占增量）。统计见decoder-performance.json。每种运行master hash一致。

## 2. WB余量：真正设置生效，但不批准产品

从固定LibRaw0.21.1 `postprocessing_utils_dcrdefs.cpp` 确认highlight=0以WB最小值归一，highlight=1以最大值归一，且只有>1路径才触发重建；不复制Spektra GPL代码，独立调用已有参数。实跑 `WbHeadroomResearchV1`：actual HighlightMode=1，pre_mul=[1,.5069307,.6970297,.5069307]，float恢复1.97265625；其余WB、gamma、autoBright、矩阵、AHD参数读回保留。取8870位置×4通道，scale预测与实际35480样本 **0差异**，uint16满值计数4405→0（metadata白位不是物理饱和校准）。随后真实重新去马赛克，再float恢复尺度+矩阵。

恢复后的AHD采样26610通道中26428不同，平均13.396编码差、最大63741.05；全图matrix/headroom线性平均绝对差0.00010445、最大0.88670。暗部残差均值3.233e-5、RMS1.157e-4，是**算法变化而非传感器噪声估计**；色边局部梯度差最大0.5577，仍未有独立真值判断哪个正确。800px预览最大差0.2872，明显不是“无损可逆余量”。无法在没有自然验证/噪声真值时接受代价，**本批拒绝产品应用并结束本批候选调参**；只保留现有实验，不同matrix合并统计、不称完整float前端或高光恢复。

## 3. RawTherapee参数对齐

固定5.11/db575c...及上批二进制，在新pp3显式设置Camera WB、AHD、border=0、曝光0、禁自动曝光/恢复/曲线/CA/镜头/锐化/降噪、工作ProPhoto、输出RTv4_sRGB，执行实际CLI并 `-O` 导出resolved pp3。输出7378×4924与探针区域同尺寸；profile/hash和完整resolved配置附round3。Camera WB是选择模式一致，**未证明其实际通道增益/曝光归一与LibRaw完全相同**；RT内部RAW/float去马赛克、相机表征、色适应、sRGB变换/隐式非线性仍不等同产品Display。不给最终差值归因矩阵，不报告C1色差。

已授权两个D800仍仅开发兼容样本；现清单其他型号没有本批已核实场景/布局，未绕过守卫扩大，验证/保留组 NOT_ESTABLISHED。皮肤/独立机型/绝对色准/C1/人工偏好 NOT_RUN。

## 4. SpektraLab从预检变为本机实跑

完整checkout固定c1447...，source tree f2d76...，无git submodule，vendor已在725文件hash清单中；source clean。本地CMake3.31.10+Ninja1.11.1.4+LLVM-MinGW20250402/Clang20.1.2+glslang16.6.0、Vulkan-Headers固定b5c8f996...；**非作者GCC工具链**，构建参数/工具SHA/源码资源及59个二进制shader hash全部附档。引擎像素输入构建成功，native RAW和UI均OFF，没有替换产品LibRaw。RTX5060Ti、Vulkan1.4.341，驱动610.88。

CTest **10 PASS/1 FAIL**；重复失败同值：BT CCTF join case114 index13 got .0180000048 vs .0180551012357076，delta5.50964e-5 >1.12639e-7。初步定位BT断点float/编译器GPU数学分支，不认为已修复，不更改源码/阈值，也不因本轮sRGB/ProPhoto组合成功宣称全引擎通过。

三组实际资源：Portra400/PortraEndura，Ektar100/UltraEndura，TriX400/MultigradeIVRC grade2（M0,Y68,printExposure1）。本轮仅肉眼核实该D800画面是城市建筑/尖塔/蓝天云，没有人物，因此不能声称肤色覆盖。原28资源未改；scratch overlay product profile并合并neutral filters，原/overlay hash均记录。每组分别跑D800自然图641×427线性D50、其已显示sRGB创意照片、193×129合成负/超范围渐变，共 **9/9实际渲染**；固定完整resolved params，关闭随机grain/glare及halation/DIR作点色彩测试，每个热渲染3次均RGBA16 bit-exact。输入D65→D50适配矩阵从blob实际原色/白点定义推导，详见契约；sRGB照片从产品固定tone得到，不能说RAW等价。

ABI本机检查：create/open/render、错误stock/channel/未知seed、不支持功能拒绝、stride、复制/释放、结果跨session/engine有效、10次重复pool live/pending=0且ledger总量稳定、另线程cancel实际−7、输入alpha.125输出65535。取消粗粒度节点边界，不能中途释放session。结果最高CPU RGBA16、最终clamp[0,1]再量化已定位shader；不保留HDR和输入alpha。

空间实验独立：17×31、193×129、385×257边缘强亮点；grain/halation强度0与quiet完全相等；halation1/2有实际变化且确定性重复相等；halation条带1/7/17对整图0差；上游7类条带/重印全比较通过；grain同session三次hash不同，新session初始固定rng_state首抽可用于同seed条带对比（1/7/17均0差），**C ABI无任意固定seed接口**，grain_seed明确拒绝。不可将此包装成已支持用户seed或LUT全空间效果。

本机1281×853合成/halation开启：初始化154.31ms、open/upload171.25ms、首次render含回读83.31ms，热21.85~23.70ms，额外CPU copy1.24~3.09ms。公开ABI无法再分解内部读回；另一9组合的文件写出实测附JSON。nvidia-smi global used轮询8032~8150MiB包含其他程序，非进程VRAM峰值；allocation ledger也不是物理驻留峰值。没有沿用作者硬件成绩，没有全分辨率RAW性能推断。

**建议：Spektra暂缓产品接纳，但可保留为外部研究oracle/候选。** 许可证兼容未批准、BT边界失败、任意grain seed缺失、自然验证集与独立模型oracle缺失。矩阵float可进入更广验证；WB余量本批拒绝。三者不宣称C1色准、PS等价、物理宝丽来拟合。





### 本批仍未关闭的边界
- 新研究解码候选实际端到端/重开测试仅D80012；旧阶段提取已通过的D80014不自动变成新候选端到端通过，需单独下一轮运行。更广场景/机型、产品灯箱/项目序列化未接入，不能称产品集成完成。
- 余量试验的暗部RMS/梯度是处理差而非噪声真值；人工色边和偏好未评，拒绝产品准入不是断言其所有图都更差。
- 18项共享验证测试覆盖明确拒绝与Python优化模式。manifest/hash是本地内容一致性，不是数字签名，也不声称能防止恶意同时改写数据与manifest。
- Spektra全局VRAM轮询与ledger、未对齐的外部RAW模型、固定seed缺失和许可不适配均保留；本次成功渲染不抵消CTest失败。中性log渐变另测Portra/Ektar出现少量−1编码下降，TriX各通道无下降，原值见spektra-neutral-ramp.json；不改阈值强行通过，不把这些数值当色卡/相纸校准。
