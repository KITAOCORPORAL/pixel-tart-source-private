# 第七批：安全输入、完整桥与full路径阻点（2026-10-30）

状态 **PARTIAL / FULL_BLOCKED_CURRENT_ABI_BUDGET / PRODUCT_DISABLED**。

基线`research/color-engine-company-20261030` / `6dba62e133f2aff75226661b61dd8850ff5289c7`，fetch后无增量。第六批是1开发场景、30独立配置及预算拒绝的full，不是30场景或full已过。旧树、失败、原资源/兼容补丁保留；不改产品src/UI/项目模型/任务桥/默认链，不注册，不扩D800白名单。

## 1. 首次输入准入与失败处理（实际修改）

新增`NaturalAdmissionManifestV2`，绑定sourceSHA、生产报告SHA、具体输入文件SHA/pixelSHA、shape/length、route/方向/域、桥版本/完整参数、backend及全部resource集合。首次case与reopen都验证manifest外部SHA，不再“先接受任意同shape NPY，再把自己当基线”。

- 读取NPY头后先拒绝错shape/channel/dtype/尺寸乘法/确切文件长度，CPU独立预算先于映射scan；hash以1MiB流读取，finite/stat/pixelhash32行块，不创建全图bool；Windows只读共享锁贯穿检查与上传/渲染，避免检查后被写入替换。
- CPU估计含输入映射/公共CPU结果/块scratch，并独立检查host available；GPU仍沿研究2GiBcap+256MiB安全余量。小case沿原启发gate，不将它升级为full可靠上界；full另用源码/实测决策。
- raw结果与TIFF以逐行迭代写出、segments逐段回读，不再完整NumPy copy、whole percentile或whole readback；重复也逐行hash。**没有声称public ABI内部不再完整CPU输出**。
- 新prepare32行桥/B写出，CPU显式预算包含为保留原Pillow BOX算法的一次单channel全图临时量（不是同时3通道）；声明它而非伪称零拷贝。10个full/proxy/window文件hash与第六批完全相同，数学没改。
- 旧round6文件只允许显式sealer对固定Git manifest、生产报告、旧backend/resource集合逐个核对后另写seal；不改旧manifest、旧case不重新标成V2通过。新prepare直接生成V2清单。旧批量脚本因缺V2合同显式停止，避免再自动提交六full或封任意新窗口。
- 失败atomic JSON，primary异常、cleanup错误分开；result→session→engine全部尝试释放，输入mapping也关。渲染在线程内运行，取消先spk_cancel、等待join后再释放，不在native调用仍飞行时销毁session。写失败报告再失败时向stderr输出主错误并保留原异常，不吞错。
- NaturalInput只允许E:/Kitao-Research内全新目标，源CompanyRAW重叠/根本身/已存在/路径reparse拒绝；逐行取消检查与报告atomic。没有改生产decoder或unsafe Matrix工具。

**实际测试**：24项针对性Python控制通过（修改输入、A/B互换、错尺寸/channel/dtype、NaN、缺项/重复JSON及file/resource项、未知route、资源变更、CPU/GPU预算、截短、block buffer上界、清理错与主异常并存、取消join顺序）；所有无效首次case engineCreated/uploaded=false，只有失败报告无成功输出。NaturalInput4个输出根控制通过，无RAW重复解码。

实际new-manifest小case+fresh-reopen按行TIFF/回读通过，另bounded repeat exact，output hash同第六批；这只验证新合同调用，不是新自然场景或full成功。`verify_round6`后续版本现在stream hash并核对旧首次清单与重开input identity，不只看输出/resolved；对未在原manifest的额外wire窗口仍明确legacy-only，未偷补伪身份。

## 2. 完整桥（实际参考，不改变定义）

细节见BRIDGE_COMPARISON_ROUND7。使用生产原P系数、引擎实际blob sRGB/ProPhoto矩阵和白点，分源逆→XYZ D65→BradfordD50→实际engine输入。1527个中性/原色/饱和/负/>1及自然RGB检查，分别记录分段与组合。

- Colour0.4.6固定commit高层`RGB_to_RGB`/明确自定义实际矩阵色空间实际执行完整path，不只是CAT相等。
- OCIO2.5.1固定commit `MatrixTransform(INVERSE)`由其执行源/目的求逆，GroupTransform/CPUProcessor实际处理所有阶段；其CAT系数不是独立CAT估计器，限制写明。与同NumPy表达式重复计算区分。
- 完整误差Colour约1.78e-15、OCIOfloat约1.78e-7，分段double/组合double≤8.89e-16。
- 完整5496×3670自然输入用16行审计：double与f32微负计数均[0,942981,590359]，final-cast单独新增0；最小G−8.433867e-7、B−2.449454e-6。已指定舍入矩阵组合对source近零通道本来就给负号，不clamp/换矩阵；不叫传感器恢复。旧输入hash保留。

## 3. full分配审查与实测（不是更多代理成果替代full）

资源生命周期与具体数字见ALLOCATION_PATH_ROUND7：

- Vulkan `file_backed`=普通persistent device-local，完整source12N与negative12N常驻；striped仍完整assembledplanes，pool refs0也不释放；strip_budget_bytes不选rows。
- public ABI完整RGB、GPU RGBA16和独立CPU malloc结果同存；host-visible upload/read staging长驻且**不在**公开engine ledger；本机不是device-local staging，分开记账。逐行writer只省第二份CPU副本。
- 新外部`ResearchAllocationLedgerV1`只记录allocator events、stable VkDeviceMemory身份，source/DLL hash留档，不改pixel math或原source2/资源。初版Buffer地址在staging替换移动不稳定，失败日志保留本地，修后重跑8小测；最终交付的8测量又加入首次准备seal准入后实跑，先前诊断日志保留本地，不混作不同场景。
- 321×215、641×429、1200×801 whole与stripe37；641另rows113、budget1，共8测量/5exact配对。完整场景缩放输入保留人物/电线/完整画面尺度，默认unsharp不关，首尾/不整除条带均逐行exact。不是独立640窗口拼图。
- 实测whole device-local peaks=5.62/20.45/69.86MB；stripe37=7.07/24.43/80.89MB。条带在该冻结Vulkan allocator上更高，不宣称条带自动低内存。budget1 vs0分配、输出和rows不变。
- 1200whole另host-staging23.07MB、CPU-result7.69MB；公开返回ledger62.17MB不是所有驻留。稳定事件日志最终live=0，未用全局nvidia-smi used当本进程VRAM峰值。

## 4. 单一PortraA full结论：明确阻点，无full任务

对同5496×3670完整已绑定输入进行了有界finite/hash。N=20,170,320：

- 源码必要device-local同驻下界 **56N=1,129,537,920bytes**；实际保留3RGBpool对应68N及whole实测~72N+表，预期更高。
- 决策时free减256MiB后的安全准入 **1,095,761,920bytes**，2GiBcap保持。
- 下界已超预算，**full engineCreate/open/upload/render都0次**，不是OOM，也不是full输出通过。没有再请求6full、没关其它程序/调driver/静默减尺寸/改曝光/关默认unsharp。

所以本轮交付选项为用户允许的第二项：**可复现明确阻点与最小流式方案**，而不是伪造20MP结果/hash。STREAMING_INTERFACE_PROPOSAL_ROUND7只PROPOSED_NOT_IMPLEMENTED：有界frame row source、negative外部row-store、frame尺度/global常量一次固定、原halo/reflect规则、stripGPU materialise+row consumer，以及先小whole exact再一次full的门禁。没实现/构建流式引擎，不称上游已有。

## 5. 产品评估/保留

输入准入和失败合同已改善，完整桥一致性和分配下界有证据；full产物仍BLOCKED，产品仍“负片/印相模拟·研究中”、默认关闭不注册。能力快照company-round7/R55_CAPABILITIES.json（V3）。

任意seed不支持、alpha丢弃opaque、RGBA16最终clamp非HDR、取消节点粒度、result独立所有权/stride/串行限制不变。LightTable非商业条款、Spektra GPL组合分发、逐profile/原资料权利互不替代；隔离研究继续，代码/资源产品许可单列NOT_APPROVED。

不重查profile负斜率，不跑BT64，WB冻结；D80014缺源无扫描、家庭GPU补丁、有效肤色/强高光/独立自然验证缺口继续保留。产品src/旧PV4/5/6/项目模型/任务桥不改，未整线合入、未跑无关全suite。

本地研究根E:/Kitao-Research/color-engine-round7-20261030，RAW/图像/完整external source/DLL/SPIRV仍本地，Git只有工具/数值/hash/报告。新失败控制、分配测量、旧与新清单、before/after保护、manifest均可复跑；最终提交及远端SHA在交付回复读回。
