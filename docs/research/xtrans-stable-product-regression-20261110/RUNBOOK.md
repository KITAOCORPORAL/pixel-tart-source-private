> **2026-11-11：本批已因主进程资源守卫停止，禁止继续以下运行步骤或清除 STOP。** 当前事实与后续边界见 [desktop-02 失败报告](desktop-02-20261111/REPORT.md)。以下保留原冻结计划供审计。

# 原冻结批次的续跑步骤

本文件只描述已授权的新批次；当前状态仍是 0/6。ARTIFACT_ROOT 指现有本地 Guangjing-RawReopen001-ProductRegression-20261110 目录，由本地 CHECKPOINT.json 定位。候选 DLL 与照片仅留本地。

1. 确认可交互的 Windows 桌面已恢复，Computer Use 能读取当前诊断窗口并提交普通文件对话框。当前已有 Capture/Activation 的拒绝访问错误；不得把 Active session 当作通过，也不得修改系统安全设置来恢复。
2. 先读 SOURCE_AND_RUN_LOCK.json、PLAN-A/B/control.json 与全部旧 ledger。以新输出目录运行：

   python tools/XTransProductRegression/verify_evidence.py ARTIFACT_ROOT NEW_VERIFICATION_OUTPUT

   该工具只验证当前未消费批次的 checkpoint，要求 0 consumed / 0 path evidence；真实 RAW 开始后，它会拒绝执行而不会抹掉记录。
3. 首次 A-cold 的旧启动目录已有回执，应使用新 attempt：

   python tools/XTransProductRegression/launch_app.py ARTIFACT_ROOT A-cold --attempt desktop-02

   启动器只开 app，不导入照片、不调用产品方法。它核对冻结文件，拒绝已有 attempt、已消费 step 和 STOP，并保留原启动回执。对其他新 step 可省略 --attempt。不要直接启动 worker。
4. 从真实暗室可见入口导入 PLAN-A 中的 Fuji 输入。等完成后读取 path-evidence 的 ProductPathBegin/End、DecodeReserved、CandidateWorkerExited、CacheStored；核对任务、step、native hash、源 hash、options、RGB16 hash。首次失败即停止。
5. 每个路径在相同设置下从真实导出入口导出两份 TIFF，再保存项目；记录 recipe 的 processing version、参数、候选标识和状态。以 TIFF 解码后的全分辨率 RGB16 比较，另列 BitsPerSample、SampleFormat、SamplesPerPixel、orientation、ICC 内容 hash；不得用文件 hash 代替像素比较。
6. 退出 A-cold 后以 A-cache 重启并通过真实入口打开已存项目。该 step 额度为0，必须命中对应候选缓存；若意外要求 RAW 解码，门禁应拒绝并停止，不能临时加额度。比较与此前相同设置的预览/master 和两份 TIFF。
7. 退出 A-cache 后，先将对应缓存 index 精确备份到本批新证据目录，再只使该诊断 index 的 PixelSha 校验失效，保留原 payload、原 index 备份和 hash。A-rebuild 打开同一项目，应记录 CacheRejected → 一次 DecodeReserved → 新 CacheStored。不得删除候选目录或旧账本。重复同设置导出比较。
8. 使用独立 cache-B 与 B 的隔离数据根，按 B-cold、B-cache、B-rebuild 重复同样步骤；设置与 A 完全相同。候选冷开/重建总计最多4次。
9. control-A/control-B 使用冻结 Sony 非 X-Trans 输入、相同参数与各自隔离数据根。确认 DefaultDecodeCompleted、默认 native hash 与没有候选 worker；各最多一次 RAW 解码。分别导出并比较。
10. 收集每条 ProductPathEnd 的 command elapsed、50ms WorkingSet/PrivateBytes、worker 与父子合计峰值；分别列 cold/cache/rebuild/export。不得使用有空闲等待的进程总时长、源码分配估计或旧原生测量替代产品数据。
11. 真实路径完成后另做完整结果采集：核对每个 reservation 与进程回执、ledger 不超过6、零额外调用、无 STOP、旧账本 hash 未变，并逐条列 TIFF 位深/ICC/像素重复性。当前 verify_evidence.py 的零消费 checkpoint 模式不能替代这项实际回归验收。

所有界面行为通过 Computer Use；合成测试与内部方法调用不能作为用户入口验收。真实 RAW 回归仍使用功能源码 a24fac65；本次文档/工具提交不是新的产品构建。
