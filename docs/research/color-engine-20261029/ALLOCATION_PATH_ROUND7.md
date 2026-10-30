# 完整输出资源分配路径（第七批，固定Windows Vulkan）

固定engine `c1447c6ab618fa4b1cb11fd2fe12826855007059`。本图基于实际源码及独立allocation诊断，**不是76n+96MiB启发式估算的重述**。源码hash/行锚在company-round7/allocation-source-anchors.json。

## 生命周期图（N=width×height，RGB32=12N，RGBA16=8N）

```text
caller mapped NPY RGB32 [12N virtual/file pages]
    │ spk_open (copies before return)
    ├─ upload staging HOST_VISIBLE [12N, grows-only until engine destroy]
    └─ session.source DEVICE_LOCAL persistent [12N, session lifetime]
         │ film_prefix/full source and stage processing
         ├─ spectral tables, matrices, LUTs [fixed stock dependent]
         ├─ whole work RGB planes [12N each, pooled, refs0 not freed]
         └─ film negative computed [12N work]
              └─ separate cached negative persistent [12N]
                   │ print chain + default scanner unsharp
                   ├─ current RGB [12N]
                   ├─ blur vertical temp + blur out [12N each]
                   ├─ hidden unsharp kept (.7 full-tier pixels/.7 amount)
                   └─ output encoded RGB [12N work, old work pooled]
                        │ materialise
                        ├─ persistent GPU RGBA16 [8N]
                        ├─ read staging HOST_VISIBLE|HOST_CACHED [>=8N; observed12N,
                        │    because an earlier full RGB read grows it and no shrink]
                        └─ malloc CPU result [8N, survives session/engine]
                               │ row hash/write/TIFF/readback (new Python no extra full clone)
                               └─ spk_result_free
```

- Vulkan的`alloc_file_backed/upload_file_backed`只转发普通persistent allocate/upload，**不是**Metal映射文件后免GPU常驻。source和negative均真DEVICE_LOCAL。
- pooled refs0 buffers保留到trim或engine destruction；`end_frame`为空，不收掉空闲全图数组。steady ledger不含upload/read staging，也不含独立malloc CPU result。`pool_stats`是logical bytes，allocation diagnostic记录`VkMemoryRequirements.size`。
- upload staging/read staging是独立映射Vulkan allocations（本机flags6/14，非device-local）；allocator返回时已同步，不代表马上释放。两者并非engine ledger中的pool/persistent。
- public ABI `spk_result`必须是完整CPU RGBA16+stride；调用者逐行写只能避免**第二份CPU拷贝**，不能消掉内部完整GPU materialise/readback或完整CPU result。
- camera/film/print frame参数来自完整source长边；不以独立640窗口替代。设置autoExposure/boost=0/halation=false等固定确定性配方已明确；不开启此前没验证的全局节点。默认scanner unsharp仍空间过程。

## 既有striped到底省了什么

`run_stages_striped`按band执行可带的节点，但每个run仍分配`plane_out.cur`完整12N，必要时还有`plane_out.log_e_film`完整12N。首尾反射只在画面本边缘发生，band halo由相邻节点需求求和；组装后交给下一run。source/cached negative不变，prefix及Whole类节点仍可能完整运行。

`choose_strip_height`明确忽略node_count/budget_bytes，仅requested_rows决定行数。`strip_budget_bytes=1`不自动减内存。striped不是公开流式输出接口，最后仍完整materialise。

## 必要下界 vs 实测/envelope，不能混称

对本固定whole路径，至少：source12N + cached-negative12N + retained RGB work/current至少24N + GPU RGBA16 8N = **56N device-local必要下界**。默认unsharp需两个新RGB输出，且pooled空闲继续保留，实际通常更多；只拿32N(source+negative+RGBA16)会漏当前/保留RGB。

本批whole实测device-local更接近72N+固定表；stripe37由于组装完整平面及保留band pool更高，不能当省内存方案。下界不是峰值预测，72N是本参数/尺寸的观测包络，不证明20MP精确峰值。

20,170,320像素：
- source12N=242,043,840bytes；negative同量。
- 56N下界=1,129,537,920bytes。
- 实测retained3fullRGB加source/negative/RGBA16的68N floor=1,371,581,760bytes；whole72N+固定约1,453,063,040bytes。
- CPU mapped input12N、CPU result8N，host staging至多本路径观测24N，与device-local分开；不能把GPU安全当CPU保证。

## 实际allocator测量（保留完整画面上下文、无容差）

|完整画面输入尺寸|whole device-local peak|stripe37 device-local peak|whole/stripe输出|
|---|---:|---:|---|
|321×215|5,619,264|7,065,056|RGBA16 exact|
|641×429|20,449,360|24,433,808|exact；另rows113也exact|
|1200×801|69,856,512|80,886,864|exact|

8独立短进程测量：whole×3、stripe37×3、641 rows113、641 rows37 budget1。rows37的不整除尾分别30/22/24，rows113尾90；比较逐行包括首尾、自然电线/人物与画面边缘。所有halo/default unsharp未关闭。此输入是完整场景代理，不是独立ROI拼接。

1200whole hostVisible staging peak23,069,056bytes、CPU result7,689,600bytes；公开engine返回ledger约62,166,756bytes，明显不等于所有Vulkan allocations。所有stable-handle日志在destroy后live计数归0，未用全局nvidia-smi used冒充本进程峰值。

诊断库`ResearchAllocationLedgerV1`仅加allocate/free事件，hash见diagnostic-provenance；旧source2/兼容patch/资源不改。首版用Buffer地址在staging替换移动时计数不可靠，原日志留本地，改用VkDeviceMemory稳定身份后重跑8小测。0live只说明跟踪的Vulkan buffer和CPU result已释放，不证明pipeline/driver所有内部资源峰值都被捕获。

## 本次full决策（只有一次可行性判断，无上传）

当前安全准入1,095,761,920bytes（nvidia-smi当前free减256MiB，研究2GiB cap不变）。**56N必要下界已超预算**，实际whole/stripe更高。因此不创建full engine、不上传、不提交6个必拒绝full任务、不关其它程序、不换曝光/效果、不减最终尺寸。

`full-path-feasibility.json`保留同5496×3670完整输入的有界finite/hash及具体快照。没有完整输出或20MP时间/hash，明确BLOCKED_CURRENT_ABI_BUDGET。下一步若做流式研究按独立提案，不能称上游现成能力。
