# ICC转换节点：开源真实数值对照记录（研究用途）

日期2026-10-15。完成一次实际跨实现执行，不接产品默认导入/导出，不修改SyntheticIccMatrixReader或PV4/5/6，PV7仍默认关闭。

## 问题及输入输出合同
节点位置：设备RGB解码后、工作空间处理前；本轮输出统一PCS XYZ D50，不混入显示OETF。输入为原创合法ICC v2.1/v4.3 RGB profiles：D65原色sRGB-like矩阵，经profile的chad适配到PCS D50；TRC为线性或gamma2.2。LUT为原创3×3×3 CLUT，A2B0/1/2均写入；另有v4明确M曲线+矩阵变体。
输入数值：0–1中性灰阶/原色/饱和色；float额外负值、1.2、2.0；uint16输入显式clamp及round-to-nearest量化。输出LittleCMS双精度XYZ、Argyll文本XYZ（默认6位小数）、skcms float XYZ D50，不再做sRGB编码。绝对意图在LCMS设adaptationState=0，相对/感知/饱和为1，BPC关闭，NOOPTIMIZE|NOCACHE|NOWHITEONWHITEFIXUP。不同配置旧结果保留。

## 固定成熟参考和实际入口
- Little CMS2.16，source commit453bafeb85b4ef96498866b7a8eadcc74dff9223；执行conda-forge win64 h67d730c_0的lcms2.dll，经cmsGetEncodedCMMversion=2160；cmsCreateRGBProfile/cmsCreateProfilePlaceholder/cmsWriteTag生成样本，cmsCreateTransform/cmsDoTransform执行RGB_16/RGB_FLT→XYZ_DBL。
- 独立skcms commit97adcbb67e98d633a65b29fd405941970f3b476d；本地Zig0.16 C++17 portable编译skcms.cc+skcms_TransformBaseline.cc。wrapper调用skcms_ParseWithA2BPriority和skcms_Transform，目标skcms_XYZD50_profile，RGB_161616LE/RGB_fff。skcms不提供一般absolute intent/adaptation开关，因此绝对格明确UNAVAILABLE，不伪造一个手写校正。
- 第三参考ArgyllCMS3.5.0 xicclu.exe，固定源/二进制归档hash及License保留。-ff -ir/-ip/-is/-ia -px，0–1设备输入。其icclib不支持ICC v4：真实stderr已记录“ICC V4 not supported”；只有v2数值可比较。

具体组件hash、profile hash、调用完整参数和逐点结果见REFERENCE_COMPONENTS/PROFILE_MANIFEST/COMPARISON；研究源码/二进制仅在artifacts，不进Git/发布。wrapper为本批原创接口调用，不复制GPL/AGPL核心实现。

## 实际结果，不隐藏不一致
7profiles×4intents×2types=56个LCMS组合；每组合12点。Argyll24组合有数值，v4拒绝；skcms42组合有数值、14个absolute组合UNAVAILABLE。
- 线性matrix相对意图：LCMS/skcms在0–1点max绝对差约3.2e-8 XYZ；LCMS/Argyll v2约5e-7，受文本6位输出精度限制。
- gamma2.2 matrix：skcms与LCMS差约1.3e-4，属于曲线参数拟合/float实现差异；不据此宣称整个ICC一致。
- v2 LUT及v4无M矩阵LUT：skcms输出约为LCMS的一半（最大约0.5XYZ），LCMS/Argyll v2互相约1e-5。源码追踪发现PCS标度处理差别：ICC XYZ编码使用u1Fixed15；skcms在mAB带M矩阵时read_tag_mab将65535/32768因子折入矩阵，缺矩阵路径没有同位置因子，mft路径也观察到约半值。这是当前差异定位线索，尚未向参考项目确认规范解释或缺陷归属，不宣称已修复参考引擎。未改reference或乘2掩盖结果。
- 加入合法明确M矩阵的v4 LUT后，相对意图差降到约1.7e-5XYZ；感知/饱和仍约0.00349。两边A2B选择和LCMS浮点/意图处理未完全等价，此部分保持OPEN。更改NOWHITEONWHITEFIXUP未消除，原始轮次保留。
- skcms本身不构成完整意图控制引擎，Argyll缺v4；因此“任意v4 LUT+全部意图/白点适配跨实现一致”未关闭，不能批准通用ICC产品接入。
- malformed：截断两者拒绝；坏声明长度/越界tag时LCMS open可延迟接受，而skcms parse拒绝。open成功不是合法转换批准，错误处理/应用wrapper仍需fail-closed审阅；不把宽容parser当可靠设备profile验证。

## 对现有SyntheticIccMatrixReader
单独生成声明的七tag v2、identity curv(0)、D50线性矩阵profile，实际执行Reader→linear sRGB与LCMS→自建linear sRGB。12点max差0.0004798（含负/超范围），灰18%差约4.3e-5；原因包括fixed16.16 profile矩阵/白点与不同目标matrix/adaptation约定，不扩大容差后称“全ICC支持”。
Reader仍仅这一子集：未知设备声明、v4、LUT、encoded curves/额外tag不准入。它不是LCMS替代品，本轮没有改它的准入或数学。

## 许可证/选择
LCMS MIT、skcms BSD3已读；Argyll xicclu为AGPL3，独立本地进程研究、不链接/打包，icclib文件级MIT单独记录。法律兼容与发布义务仍需项目评审；当前选择“隔离行为对照”，不复用核心代码、不引新产品依赖。未来优先评估LCMS库，需补完整错误处理、归属/组件来源、意图/profile实际通路及与历史版本的明确接入方案。

## 可复现与历史差异
icc_compare.py生成同批profile并调用固定DLL/EXE；skcms_probe.cpp只作接口wrapper。Zig和参考程序身份见manifest。比较速度区分LCMS纯转换与Argyll进程启动，不作性能排名。参考进程Argyll/skcms另对48,000点采样working set/private bytes，见REFERENCE_MEMORY；包含进程启动/文本I/O，不等同纯转换或原生allocator归属，不能宣称内存优化。
历史72 golden在同合并构建实际Passed；产品色彩数学零变更。尚未验证真实照片ICC预览/保存/导出或A7M4绝对色准，CalibrationEligible=false。不要求购买色卡。

可复现补充：生成器固定profile创建日期2026-10-15和空profile-ID，两次独立生成所有ICC文件hash完全一致（DETERMINISTIC_PROFILE_HASHES），保留失败/不同意图结果。内置profile为原创数学夹具，不是研究原件或相机profile。
