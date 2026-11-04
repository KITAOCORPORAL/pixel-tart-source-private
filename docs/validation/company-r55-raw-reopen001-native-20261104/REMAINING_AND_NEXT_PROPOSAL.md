# 剩余问题与版本化下一步提案（本轮不执行）

2026-11-04，Asia/Shanghai。状态 **OPEN / NOT_FIXED**。

## 已有分流

- 有效Unpack mosaic相同，Process image已经不同。优先调查DcrawProcess，不回头重复托管复制/float/方向/全App，不另换照片。
- 默认unpacker已实测unpacked_load_raw()；CFA filters9/colors3。不能从机型或UserQual=-1直接写成实际插值算法/遍数已测。
- binding静态OMP默认设置的身份已核验，不是新修改，也不是不稳定根因的证据。禁止把原报告“继承6”的措辞修正包装成产品线程bug修复。

## 仍未知的具体范围

1. raw2image_ex（含inline黑位）返回时是否已差；raw/sample域、colors/filters/有效区域在此是否变化。
2. adjust/subtract_black、scale_colors与pre_interpolate后的首差；真正参与的黑白位/scale状态。
3. 实际选中的插值入口/quality/passes、pre/post阶段RGB与有效边界；worker scratch的写前读、边界或调度是否相关。
4. 后处理/convert_to_rgb前后是否首次产生或放大差异。MemoryImage曲线/统计尚未单独作为根因调查，因为image在之前已经不同。

## 提案 NativeBoundaryV3，未获像素变更批准

先由既有研究线在固定LibRaw0.21.1与RT5.11契约对照上审这几个入口，**不再搭第二套实现、不另解码本样本**。输出精确的域、布局、有效区域与源码/许可证依据，并明确RT不是生产同域独立真值。

若原DLL确无可安全使用的内部检查点，再另行提交隔离instrumentation方案：固定源码与patch、编译器/flags/可选组件/依赖/实际DLLhash；具体只读检查点先选raw2image_ex后、scale/pre_interpolate后、插值后、convert_to_rgb后。须论证观测对堆、线程与调度的扰动，声明它是**新二进制观测**而非原生产DLL已实测。原DLL/旧失败永久保留，不能为便利先替换生产库。

方案必须另获执行次数/同一输入授权，先零RAW检查，再严格限定进程与次数，不自动重试。本轮不执行这个提案。当前没有明确所有权/ABI/初始化违约可最小修复；没有依据上calloc、加锁、单线程、调插值/quality/gamma/auto-bright/WB、换decoder或缓存。

任何像素算法/线程/参数/decoder修复仍受AGENTS/POLICY约束：相关的至少两个固定成熟实现、域/顺序/许可与可复现数值依据、独立版本、不迁移PV4/5/6定义。确有批准且证实的修复后才沿原App失败路径验证预览、保存独立重开、TIFF，并发布完整诊断应用WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全模块/策划15/教程退役保持。没有修复不重建包。
