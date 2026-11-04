# 可落到产品观测补丁的检查点与有效布局

仅设计，**本轮未安装补丁/构建/运行**。全部函数行号属于LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7；文件/行范围SHA见INTERNAL_SOURCE_LOCK.json。父产品外层ABI证据不自动授权读取以下内部私有字段。生产native构建来源不完整时应由产品建立隔离同版源码观测库，另记新DLL身份，不替换原库。

## 先记录布局，再取样

每个payload前记录：checkpointID、enter/exit/skip、调用序号；width/height/iwidth/iheight/raw_width/raw_height/raw_pitch/margins、image分配extent/代数、真正stride；filters/colors/xtrans6×6及坐标原点；domain、dtype、选中槽映射。使用原产品只读分行写出/全有效域及64行hash方式即可，不再另建框架、不存第二份全图。

**布局类型**：
1. raw_image：单平面U16，raw_pitch是byte stride，active(x,y)要加margins。此阶段产品已经相同，无需本轮重新观察Unpack。
2. raw2image/scale：image为ushort[][4]，每像素8bytes，紧排iwidth/iheight在shrink路径有效；主算法样本是fcol(y,x)一槽，其他槽已由raw2image清0。可另存四槽初始化签名，但不能把空槽0当黑场照片像素。
3. pre_interpolate：可能重新calloc(width*height)并free旧指针；此后算法使用width stride。即使iwidth字段尚旧，也不能错用它读新分配。当前参数源推断shrink0仍要记录实际值。
4. XTrans内部：global image非绿站点slot1=minGreen、slot3=maxGreen只是临时边界，不是绿色/RGBG成片；tile私有rgb/CIELab/导数/homo类型与有效范围各异，不得把未写padding当图像。
5. 插值完成：image物理4槽；colors3时主要输出RGB槽0/1/2，slot3仅内部残留状态，不能作为成片alpha/第4色通道。
6. convert后：同4槽容器的输出RGBU16（clip），并非gamma/autoBright后的MemoryImage。histogram在convert_to_rgb_loop开头memset之后才有效。

没有已确认白点的camera-domain数组写“相机通道/非已表征白点”，不要强贴D65/ProPhoto标签。转换后记录output_color及实际out_cam；只有实际sRGB分支才称输出sRGB原色，仍区分传递函数。

## 按源码落点

|ID|精确落点|捕获内容与用途|
|---|---|---|
|P0|raw2image.cpp raw2image_start:50之后；由raw2image_ex:306调用|saved→current状态恢复、shrink/尺寸与black/cblack/cam_mul/raw_color等小状态，不读未分配image|
|P1|dcraw_process.cpp:43调用raw2image_ex成功，45之后/51之前|**当前最早缺失内部事实**：effectiveCFA/四槽初始化imagehash，subtract_inline、copydims/allocdims/fuji_layout，黑位/maximum/data_maximum|
|P2|dcraw_process.cpp:112之后/114之前|实际black/max及optional前处理完成、scale尚未开始；记录raw2image后到这里有无像素变化，callback是否调用|
|P3a|postprocessing_utils_dcrdefs.cpp scale_colors:221之前|实际WB分支、pre_mul前后、scale_mul4float rawbits、black/cblack/maximum、wavelet/aber；不是手算倍数当实际|
|P3b|dcraw_process.cpp:118之后|scale_colors完成或skip，WBscaled cameraU16 sparseCFA，采用当前iwidth/height布局；有clip/truncate不称floatlinear|
|P4|dcraw_process.cpp:123之后|pre_interpolate返回，新旧pointergeneration及CFA/filters/colors/shrink/mix_green；exp_bef及pre_interpolatecallback尚在后面|
|P5|dcraw_process.cpp:147之前+152–179实际选中调用处|afterexp/callback的插值输入；记录真实entry枚举、effectivequality、passes、calls；不要只记录预计分支|
|P6|dcraw_process.cpp实际选中interpolator返回后、183之前|RGB有效通道和布局；包含interpolator自身border填充，尚未mix_green/postcallback/median|
|P7|dcraw_process.cpp:235之前（232–233callback之后）|所有postdemosaic/fuji/ICC小状态及当前image；区别插值之后到convert前的改变|
|P8|postprocessing_utils_dcrdefs.cpp:103之前，convert_to_rgb_loop入口|effectiveout_cam3×colors有效float值、raw_color/颜色数/矩阵分支；不要hash未用矩阵列|
|P9|dcraw_process.cpp:235返回后、238之前|convertedRGBU16/全初始化histogram；后处理callback/stretch尚未发生|
|P10|dcraw_process.cpp:248返回前|已有Process终点复用；只记录postconvertcb/stretch/fourcolorrestore是否执行及尺寸，不要求再重跑外层流程|

为了尽量少捕获：先使用P1/P3b/P5/P6/P7/P9六个内部有效payload，其余P0/P2/P3a/P4/P8/P10优先小状态。可以在P2/P4已有状态提示跳变时补该阶段hash，但**执行预算、缓冲落盘和进程安排全由产品决定**。上述是位置表，不是研究线启动新实验的授权，也不是每个位置都必须先全图输出。

## 确认实际分支所需的状态

- 黑位：subtract_inline、IO.zero_is_bad、bad_pixels/dark_frame非空及调用、do_crop、copy_bayer/copy_fuji实际entry、fuji_width/layout/shrink、saved/current cblack网格与maximum；记录adjust_bl、subtract_black_internal、adjust_maximum各自调用次数和前后值。
- WB/scale：user_mul、use_camera_wb/use_auto_wb、cam_mul有效性、white8×8分支和as_shot_wb_applied；rawparams fallback选项；最终pre_mul/scale_mul原始float位；threshold/wavelet和aber路径。
- 选择：pre/post filters/colors/no_interpolation、实际quality/fuji_width；所有pre/custom/postcallback存在性与是否调用。当前filters9/quality2或3在无替代callback条件下走XTrans；不能把“条件成立时”写成runtime已记录。
- XTrans：实际passes/ndir、编译T、OpenMP编译宏、region内实际team count/workerID、omp_get_max_threads/动态/嵌套状态。环境OMP6不代替team。记录每top/left tile的拥有worker以及有效mrow/mcol；每worker scratch身份/extent而非将地址数字当算法输入。
- 输出：实际mix_green/postcallback/med/highlight、fuji_rotate前后fuji_width/shape、apply_profile是否调用、raw_color、effectiveout_cam、postconvertcb/stretch。output_bps16是MemoryImage选项，不代表Process四槽已经变为紧排3槽。

## H1的最小加深检查（仅在P5同/P6差且实走相应XTrans时）

先维持原质量、参数、worker策略。以两邻top tile的交叠带作为源级候选范围，可使用HYPOTHESIS_H1.json的静态(500,507,slot1)见证点及产品已公布差异邻域，但不假称见证点本身已差。记录serialgreenbound结束时的只读见证值、实际消费值、对应最终回写值以及tile/worker/pass/phase。线程私有有界记录在region结束后输出，避免引入共享日志锁或边做边扫全图。

**LIMITATION**：LIM宏重复加载可受观测影响；“调用前后读取一次”只能证明该时点值，不是实际表达式使用值。instrument实际load或shadow-read需要产品单独审查代码生成/并发扰动，不能以缓存参数、锁住tile、清零或OMP1作为被动观察。没有符合要求的观测方式时，先交P5/P6等阶段事实，不伪造消费时序。

反证：如果P1/P3/P5已先差，H1不能作为本轮首差；若未选XTrans或未并行，对该次并行假设不适用。若P5同/P6差但该次相关lower-bound读取都稳定，则该次H1未获支持，不擅自增加循环重试。其他阶段差异按firstdifference转交，不同时开展第二假设实验。

## ABI和线程安全约束

C++ get_internal_data_pointer等API、protectedvirtual热点存在，不等于生产C ABI有安全hook。progress callback是在函数中特定位置发出、不能假定是像素阶段终点；custom interpolation callback会替换算法，禁止为诊断注册它后继续称默认算法。不要按另一编译的私有offset/vtable读新字段。新观测库需source/patch/编译宏/flags/依赖/hash与扰动说明，原nativeDLL及全部失败保持。

研究交付不含native补丁，不运行任何RAW或参考程序。当前根因仍OPEN，不能据源级风险批准锁/清零/decoder替换。
