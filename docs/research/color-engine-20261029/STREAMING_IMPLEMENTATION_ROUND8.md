# SpektraStreamingResearchV1：实现与对照记录（第八批）

状态 **EXECUTABLE_ISOLATED_PROTOTYPE / SMALL_EXACT / FULL_PIXELS_COMPLETE / END_TO_END_HOST_BUDGET_PARTIAL**。不是产品或上游正式能力。

## 版本与修改面

外部固定base c1447c6ab618fa4b1cb11fd2fe12826855007059，新`source-stream-v1`独立checkout保留原source2、兼容patch和allocation诊断树。只在研究分支提交自编generator/driver、hash、数值和本记录；实际GPL engine修改/资源/DLL/SPIRV全在本地。generator从clean固定source生成8个文件（含外部wire契约增补），逐bytes复现一致。

没有改曲线、输入桥、profile、film/print曝光、编码/量化公式。公开旧spk_open/render路径未替换，新symbol只供研究调用。shadow-only自然样本仍1场景，不能据14图像对照说14场景。

## 原路径复用而非数学再实现

- 第一次有界read_rows→原`film_segment`：原upsample→exposure→log→film curve等同顺序。被冻结配置中global autoExposure/boost/halationHeadroom均inactive，曝光归一的host常数仍原print_prefix；未知effect/param全部resolved精确比对拒绝。
- film前不调用依赖输入band长边的原geometry prefix，而以完整frame w/h设置`source_long_edge`、`frame_long_edge`和pixel pitch；本版本只允许identity geometry、无input decode的A。没有用独立window模拟frame。
- negative只顺序写临时磁盘row store；GPU不缓存整幅。第二遍读negative strip+原`print_scan_finish_demand`给的halo=2，原print_segment带默认unsharp(.7pixel,.7amount)和原Gaussian/FIR/reflect；首尾用frame edges、内部strip边缘丢halo，不反射出虚构上下文。
- 原spk_to_rgba16量化仅strip输出；consumer只收到中心有效行，borrowed buffer同步消费；没有materialise完整CPU结果。

## 相关参考与门禁依据

本节点是数据流/分配重构，不新造色彩数学。独立逐像素oracle为未修改固定Spektra whole（对应原先固定兼容patch资源，版本/源/DLL/hash清单），不能拿stream自己输出当oracle。保留先前POLICY已完成的相关Colour/OCIO完整桥和固定数学参考，不重跑BT/曲线研究。小whole/stream精确测试门槛为**0个RGBA16通道差**，未放宽。

完整“第二个独立光谱胶片引擎”/Metal全图oracle未运行，因此这只是固定实现的等价重构，不声称物理拟合准入。原Gaussian来源、radius/reflect与近identity矩阵都直接调用原固定路径，不将另一个项目公式拼入。

## 小图/失败/内存实测

- 自然完整场景输入321×215、641×429、1200×801，rows1/17/37/113（按矩阵10case）全部whole/stream RGBA16 exact，首尾及不整除尾行都比较，人物/电线在完整缩放场景中，非ROI拼图。
- 合成domain-stress97×65加入负/>1/nearzero/原色，rows1/7/17/64四case也whole exact。其manifest明确synthetic diagnostic，不冒充额外授权自然照片。
- consumer I/O subblock1/3/7/11变化不改输出，2次同进程顺序重复whole exact；并发第二call−30、第一call取消−7后join/live0真实测试。
- 源读错、consumer错、取消、未知字段/route/effect、inputSHA错、backendSHA错、staging硬预算不足、device预算不足共10原生拒绝/失败；没有最终TIFF。callbacks异常被捕获并转返回值，不跨native边界；中间negative文件移除、device/staging都归0。
- rows4096在device16MiB/staging1MiB下实际缩减到预算允许值，whole exact。最初staging按单次拷贝估算不够，在真实硬guard被拒绝后把upload/read两份及替换余量纳入，保留失败，不增加门槛。
- 同width321、rows37，frame height215→430，device/staging/host-vector峰值完全相同；更高帧也有独立whole oracle exact，证实没有随完整height增长的GPU source/negative/result。磁盘negative容量仍随height增长，明确不是所有介质总量O(strip)。
- Device和staging在实际VkMemoryRequirements分配处硬限，全部活对象含pool、persistent、staging替换旧+新都计；每条带释放后trim空闲。计的是显式Vulkan buffer实际allocation bytes，不是driver内部所有对象或物理VRAM驻留。

## 20MP结果与不能省略的host门禁失败

同5496×3670PortraA只1次native full，rows127/halo2；完整输出pixelSHA `1c4a97a126b420fb24561df5cfb03bfc1b3d7648a5ee77c3d13102c48c2a7b8a`，raw行文件和TIFF16全部像素相同。device峰值43,848,656bytes，staging25,523,424bytes、native显式host vectors16,496,672bytes，native结束live0。

这不是20MP whole/stream oracle：公司没有whole-render该尺寸。依据是10+4小图exact、尺寸/完整输入hash/回调完整性及full输出独立重开一致。

首轮caller的post-native I/O仍同时保留输入mapping、output mmap和tifffile segment读取，OS RSS峰值659,144,704bytes>host512MiB。**初次整个full进程host门禁失败**，不能用native16.5MB变量预算掩盖。原report已另名保留，审计report success=false，原raw/TIFF移到`.initial-host-overbudget`清楚命名，内容hash不变。

修正caller提前关闭输入map、直接一行TIFF strip offsets读取，增加跨post-native阶段监测超cap禁止finalrename；小case重新验证。对既有20MPraw在独立进程实际执行新逐行writer/readback，最大行43,968bytes、RSS38.49MB，hash exact；另一独立进程read-only重开RSS36.23MB也exact。**没有第二次20MP渲染**，因此修正版native+I/O组合full host门禁仍未闭合，保持PARTIAL，不宣称所有预算通过。

## 产品/许可边界

仅PortraA/确定性配置，未扩B/三胶片/seed/random。alpha丢弃opaque、RGBA16最终clamp非HDR、node/strip取消/严格所有权规则见STREAMING_BACKEND_CONTRACT_V1。产品UI/项目模型/任务桥/默认链及PV4/5/6/MatrixD800工具未改。

许可证技术实验分账：Spektra GPL/逐resource CC BY-SA与资料源权利产品许可未批准，LightTable非商业条款不替代；不因此停止隔离技术验证，也不以DLL/外部进程/LUT自动解决。
