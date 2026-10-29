# 产品胶片适配边界 v1
研究负责人只改引擎/验证，产品侧只维护变体/项目/交互/任务；研究契约BACKEND_CONTRACT_V1只读保留。
FilmBackendBindingV1当前只承认guangjing-existing-film / PixelTartFilmV1 / BuiltInV1。输入是固定源照片target、variant、文件hash与完整快照hash；native8位不透明栅格经已有ICC路径转换至sRGB D65；输入/输出8位、preserve-source alpha（首批严格opaque）、sRGB profile hash，paper=none，完整参数JSON及seed。
任务桥记录生命周期；CancellationToken在计算/身份边界生效，取消后不加变体；先完整计算，再验源，最后在UI上下文一次提交新版本+观片项。所有临时像素内存随task释放，无源文件写入。新版本的FilmBackend元数据随项目/版本保留，未知version/engine/profile域拒绝；对应胶片节点系统锁定。
Spektra候选另定义engine和resource hash、profile/输入矩阵、返回stride/释放函数、opaque输出、取消完成前不释放session等，由研究合同决定。现有候选无用户grain seed，不可假装本产品seed映射可直接使用。许可及失败门禁未过前registry.Available=false，不返回伪结果。崩溃前未提交任务没有成品，需显式重试；不自动恢复外部GPU进程。
