# RAW-REOPEN-001 调用链与旧夹具核查

基线：产品源码 38dd3f92bb2db7238f7f058008887929ec971c93；审查提交 e2437d2300b37872d5b66482a721e3b6463f1ce4。下列相对路径均以本诊断工作树为根。逐文件 SHA 见 SOURCE_IDENTITIES.json。

| 检查点 | 代码/证据 | 本轮判断 |
|---|---|---|
| 实例/初始化 | LibRawDecoder.DecodeCore 每次 local using RawContext.OpenFile；包装层固定 commit 43e9c177… RawContext.cs:463-482 每次 Initialize(None) → OpenFileW，失败回收关闭 | 不复用 native context；没有发现上一张图参数对象复用。两个成功进程各一次 Open/Unpack/Process/MemoryImage |
| 参数赋值 | DecodeCore:109-120 只设置原有选项；包装层 RawContext.cs:594-608 从当前 native 读 OutputParams → configure → Commit → ProcessDcraw → 释放临时字符串 | 本轮观测并比较 configured-output 全部属性、open/unpack/process 后 output/nativeOutput/unpack/color 字段；不以输入 SHA 相同推定参数相同。原 pass4 仅初始化参数，不能补写其未采集的有效参数 |
| 顺序/释放 | Open → metadata/orientation → Unpack → DcrawProcess → MakeDcrawMemoryImage → ToArray → 返回。C# using 逆序先释放 processed、后 context；包装层 ProcessedImage.FreeImage 是 ClearDcrawMemory；context Dispose 是 Recycle + Close | 没有发现读取已释放指针；托管数组不借用 native lifetime。新观测中 native 前/后复制 hash 相同，返回出 using 后数组 hash 也相同 |
| RGB16 layout | processed.Bitmap/3ch/16bit；required=width×3×2×height；DataSize >= required；copy 只取 width×height×3 ushorts | 实际 width7752/height5184/stride46512/有效字节241118208、DataSize241118208；没有 padding/stride 截断。两次 copy 完全精确 |
| 托管所有权 | pixels48=Span[..samples].ToArray；HighBitDepthImageBuffer.ToFloat 是新建 float[]，逐项 ushort/65535f | 不共享 native 内存、不从旧池领取这个 master。全部120559104通道的 float 期望值/uint16 往返检查均0错 |
| 方向归一 | RawImageOrientation.cs:9-11 对非2..8直接返回源；否则源/目的独立分配与逐坐标拷贝 | 本样本原 orientation=1，NormalizePixels 与原 master 同对象且 hash相同；方向不是本次分歧来源 |
| master生命周期 | FrozenRawMaster session-owned；导出校验源 SHA，复用对象；Session staging 先完整加载、移交 exact targets，再清 staged.Targets/ActiveTarget，避免 staged.Dispose 释放移交目标 | 原测试走默认 RawDefinition=null，不混入专业 RAW；跨进程必然重新解码，没有持久像素缓存。没有发现此次失败可直接最小修复的生命周期错误 |
| 同一目标并发 | Workspace.cs:581-600 对 RAW 先 await LoadThumbnail 再 Activate；非RAW 才并发。ScheduleTargetThumbnail 捕获当前 master/冻结栈，缩小 render 不另解码 | 不重做已存在的导入串行修复；不能因此宣称全App无竞态。新的无App单次串行解码已经复现，App双解码不是必要条件 |
| 原identity TIFF | pass4/company-02/default-output.json 为PV1空栈、原尺寸16bit orientation1；源SHA、版本ID、TIFF完整文件SHA重新匹配。旧CompareMaster读明确default-export路径，不是+0.5输出 | 原identity TIFF/master比较没有发现选错文件；其PV1空栈渲染clone/16位往返来自同一冻结master。原收据没有 native阶段hash，新的两个独立master不覆盖原样本证据 |
| 原栈/变体/输出 | 测试 await LoadTarget/LoadSession；Render await ApplyCommand（工具节点时可执行且allowConcurrent=true）/AnalysisWork；Export await生产命令且校验回执SourceHash/EditVersionId/RawDefinition | pass4参数/版本一致证据可信范围保持；没有把保存前后成片hash不同单独视为像素不同，本轮另有 native原像素证据 |
| 后台未等待任务 | 旧夹具没有显式 FlushThumbnailsAsync/全后台任务barrier；AnalysisWork只代表分析任务，不能声称全部后台活动静止 | 缺少完整旧App任务事件轨迹，不把它补写为PASS。但缩略图读取捕获master且输出独立；新进程无WPF/缩略图/保存任务仍native不同，旧夹具的后台任务不能解释全部现象 |

## 保留的精确未知

- 首个**已观测**差异在 MakeDcrawMemoryImage 返回的 native RGB16，尚未在本轮观测 Unpack 后 sensor buffer 或 Process 后 imgdata.image。因此不能把具体内部子阶段、X-Trans线程竞态或未初始化内存写成已证明根因。
- 环境中继承 OMP_NUM_THREADS=6；两次均相同，本轮没有设置、删除或修改它。12逻辑处理器不等于实际native worker数；未测worker数和调度。两个进程串行不等于native单线程。
- 原生参数中的 XmpDataPtr 只是不同地址，XMP span getter不适合反射调用；本轮只保留指针/长度，未复制XMP内容。它不等于有效输出/WB/unpack参数变化。
- 包装层来源是固定NuGet仓库commit的静态参考，不是满足POLICY的 LibRaw+RawTherapee 成熟数值对照；本轮未引入新依赖、未改变算法。
