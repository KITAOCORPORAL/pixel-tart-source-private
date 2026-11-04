# 阶段静态审查与补丁边界

2026-11-05，Asia/Shanghai。复用研究线0e0723df…，不是第二套源码研究，也不声称研究线已审核本次新补丁。固定LibRaw cccb97647fcee56801fa68231fa8a38aa8b52ef7；18个锁定Git blob SHA256全部通过（SOURCE_AND_DEPENDENCY_IDENTITY）。源码在仓库外共用根，没有复制RT GPL实现。

## 检查点（固定源码原始行号）

|检查点|dcraw_process.cpp位置/域|取样|
|---|---|---|
|raw2image-after|43行raw2image_ex成功返回、检查rc之后；可能inline减黑|四槽U16只取fcol(y,x)对应唯一CFA槽，不是最终RGB|
|black-after|81/82行条件减黑区域之后、adjust_maximum之前|同上，记录subtractInline/black/cblack/maximum/dataMaximum，条件区域可能跳过|
|scale-after|116行scale_colors条件区域之后、pre_preinterpolate回调之前|CFA单槽，原WB/scale按实际调用处理；noAutoScale记录跳过状态|
|pre-interpolate-after|123行pre_interpolate返回后|仅CFA9/colors3/shrink0/同尺寸契约成立时取单槽|
|interpolation-enter|实际dispatcher leaf调用前，含custom callback leaf|quality读74/77行原计算结果，命中真实leaf才记branch，XTrans passes记录原表达式实值，不猜机型|
|interpolation-return|同一leaf成功返回后|仅已审XTrans分支允许RGB012，排除第四槽；异常不伪造return|
|convert-rgb-before/after|235行convert_to_rgb两侧|dense camera/intermediate RGB至原输出矩阵后的U16 RGB；不是MemoryImage曲线后RGB16|

布局依据：raw2image.cpp:390..402原分配/初始化保留；misc_demosaic.cpp的pre_interpolate与utils_dcraw.cpp的fcol；xtrans_demosaic.cpp:428 border_interpolate(8)；postprocessing_utils.cpp RGB循环及postprocessing_utils_dcrdefs.cpp convert_to_rgb。原算法、初始化、线程及原计算语句顺序不改，新增块仅包围原leaf；移除标记恢复原文件字节。

## 有效域与限制

- 每阶段只对自身有效域逐行SHA256，LE U16，逻辑x=16..width-17/y=16..height-17，四槽源读取后单行紧排更新hash；第四槽、padding、外框不入hash。
- 早期仅已定义CFA槽，dense仅RGB012。inset16不是产品裁切；不覆盖16px外框，“首差”仅限采样检查点内域，不证明整图/全部隐藏状态/scratch相同。
- 不同width/iwidth或height/iheight、shrink/fuji_width非0、非CFA9/colors3、未审leaf均INVALID，空hash不是相同。其他实际branch只记名字，不谎称其域已审。
- raw2image时quality/pass=-1表示未解析；后续quality读原局部变量，passes只有真实XTrans leaf的值有效，其他-1不代表实际遍数。
- 比较须先核对source/patch/binary、全部参数、stage序列、布局/CFA、branch/quality/passes及环境，不能跨域逐字节横比。当前RAW runner/比较接线未完成，无新首差结论。
- 只写hash/元数据，无私有像素副本。确需分歧局部须预先设计同次运行有界取样，不自动追加完整显影。

## 扰动、非等价与未验证

BCrypt对象、row vector分配/释放、额外读取、写盘/fflush、调用及原有回调可能扰动堆、cache、调度。补丁不安装/替换算法回调，但不能宣称零扰动；差异减少/未复现不是修复。

原NuGet native源码commit/compiler/flags/patches未知。新MSVC/OpenMP和上游默认可选组件组合仅为具名候选，与含lcms2/jpeg8/zlib1/VCOMP140依赖的原DLL不等价。本轮C++未编译，真实ABI/branch/passes未测；宿主编译或JS合成检查不替代原生自检。静态自审不替代研究线复核/POLICY数值准入，不改算法。
