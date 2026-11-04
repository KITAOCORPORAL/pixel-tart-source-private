# 写前读、边界与并行状态审计摘要

范围为固定LibRaw0.21.1源；生产DLL执行分支和编译宏仍需产品确认。不是sanitizer/动态读写验证，也不把RT初始化差异当缺陷证明。

|位置|明确读写关系|本轮判断|
|---|---|---|
|raw2image_ex395–402→copy_bayer262–296|整个image分配calloc或memset0，随后仅fcol写入；shrink0时行任务各写不同像素；dmax在critical更新|未发现本条件下由未初始化image/无保护dmax导致首差的证据|
|raw2image_start/adjust/subtract|saved状态memmove恢复；inline与显式黑位由条件分流；cblack重复网格的清除时机不同|必须记录实际状态，不能套错“black减两次”结论；没有新缺陷证据|
|scale_colors/setup/loop|sum/dsum先清0，scale_mul正常/回退均4项赋值；循环读 initialized image4槽并逐像素写回|未见明确写前读；wavelet/aber非默认入口要实际记录，不能一般性排除|
|pre_interpolate|unshrink新calloc并copyCFA再free旧；函数内地址用width或iwidth与阶段相关|主要观测风险是读旧pointer或错stride；不能据此称原实现出错|
|XTransrgb|每worker独立malloc；部分tile有效矩形写rgb[0]，再复制完整tile到方向1–3；pass1复制0–3到4–7|未写padding确存在但尚无已证明的有效输出读未写链；不允许hash整个scratch并据此判bug|
|XTranslab/derivative/homo|Lab写[2,m−2)，drv写[3,m−3)并读Lab±1；homo全域memset后内域读drv±1；汇总读homo±2|这些区间契约可定位，非越界证据；尾tilemrow改变与RGB邻域需按分支逐load审查，不宣称全程序安全证明|
|CIELab表|对象tls sentinel初始化，cielab(0,0)在parallel前填表；worker只读cbrt/xyz_cam；NOTHREADS会改为static|当前独立进程/独立context不支持“表跨进程共享”猜测；实际宏未知，不能泛化线程安全|
|XTranstileglobalimage|tile采样/greenbound直接读image；另tile最终RGB写回相同image，读区与邻写区交叠|**明确源级访问交叠；唯一H1待验证**。不同线程确实并发访问、读值语义变化及输出影响尚缺运行事实|
|convert_to_rgb_loop|histogram先memset；RGB3的out三项先算完后写；本版本源码为串行循环|未见写R后读取新R的转换错误或无保护并行histogram证据|
|outerborder|XTransjoin后border_interpolate8；产品外16px0差|不是内部tile边界安全证明，也不能凭首差y499靠近tile起点就确认竞态|

## H1适用边界

H1只在实际XTrans并行路径命中、同阶段输入相同且输出在该段先差时进入产品最小检查。源码T512/step496示例和slot1的上下界语义见HYPOTHESIS_H1.json；这不是从RAW计算出的证据。最终tile写区彼此不交叠，不能错误描述成两个tile最终输出重写同一像素；风险是**输出写与邻tile仍在读取的输入/边界数据交叠**。

RT5.11的输入rawData与输出red/green/blue分开，greenminmaxtile私有，这为buffer所有权审查提供对照。其float/算法血缘/tile114/边界/裁切不同，不构成同域独立运行证明，不移植代码或建议替换decoder。

## 不采用的“修复”

不基于malloc、OpenMP或RT的memset直接加calloc/全局锁/OMP1；不更改quality/passes、gamma/WB或decoder。新增日志也可能改变时序、heap和编译load，须作为隔离观测版本说明。所有缺陷结论继续ROOT_CAUSE_OPEN/NOT_FIXED。
