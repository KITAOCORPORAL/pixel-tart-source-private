# 家庭第九批：接续公司第八批流式原型（2026-10-30，Europe/Berlin）

**状态：ISOLATED_RESEARCH / SMALL_EXACT / FULL_PIXELS_COMPLETE；完整host门禁仅剩最终报告发布范围的审计缺口，仍PARTIAL；不接产品。**

## 接续和保全
fetch核对公司分支 `research/color-engine-company-20261030@0f3204c09b15e041c8e70cf95ddbee789fcf7cec`，从其HEAD创建独立家庭树 `N:/pixart/guangjing-color-engine-home-20261030`、分支 `research/color-engine-home-20261030`。旧家庭树 `research/color-engine-20261029@ce292f2a` 及旧临时source/build/resources均未覆盖。读AGENTS/POLICY、第八批报告/implementation/contract/reproduce及实际generator/caller。没有重写流式算法，不合产品、不改src/UI/任务桥/PV4/5/6。

所有新工具、源码、构建、资源、像素在 `N:/pixart/ColorResearchHome-20261030`；复制固定工具而非改系统PATH。Vulkan-Headers固定源也复制到N盘。家庭RTX5060Ti/驱动610.88/Vulkan1.4.341、主机约68.48GB内存。Python3.12、隔离NumPy2.4.6与公司匹配、psutil7.2.2、tifffile2026.10.15、Pillow12.3.0。环境/工具hash及新DLL身份见home-round9/environment-and-provenance.json。

固定engine c1447c6，stream generator生成8文件中仅engine.cpp因授权negativeStore根从公司E盘改为家庭N盘而hash不同，其余7项与第八批完全一致。资源集合SHA `60e45914333231a5f76373452dd11ff27cca0e5d7271a0edf25b6ad47e6a10ad` 完全不变。新stream DLL `9f93642e2d5988efc8c555f1c13afd8911cd1669a10e1a606e4a17f819df952b`；whole兼容patch DLL `a77b51a9fe60a543bee2f1d6685ea4e4ba59b7b0c7b2f80e873508f5cc544636`，不冒充公司DLL字节相同。

## 同源恢复
只在授权N:/pixart/RAW内找清单列名TNAN8886.CR3；找到并SHA精确匹配 `B7D358...33401`。原生产decoder+display行输出与公司第六批linear/display master两个hash和5496×3670尺寸都完全一致。冻结 ExistingResearchD65ToD50V1 桥重建A，full文件hash与公司一致、pixelSHA **cd47a182e9814bc45c4130b9b90fc45bca49a06923958df739180699ac0f50f9** 一致；没有换照片/曝光/方向。所有自然small仍同一完整场景缩放，不是多独立场景；无新B路线或额外stock。

初次家庭helper错误按比例round得321×214/641×428，不符合公司指定321×215/641×429，whole前因文件名缺失明确失败。错误目录保留，改为第七批明确固定尺寸，在全新small-inputs-v2生成，三尺寸input pixel hash均与公司相同；没有覆盖或拿旧错误样本替换基线。

## caller与失败提交边界
- stream_full_once准入改为显式异常，Python-O也不绕过；环境config显式传递给子进程，full尺寸固定。
- HostBudget从准备前到native、输出行写、完整读回、释放/清理监测；采样RSS与Windows进程peak_wset分别记录，任一个超512MiB或OS峰值不可得则拒绝。OS峰值含进程imports等此前高水位；不以native显式vector预算替代完整host。
- 关闭输入映射后才进入输出阶段；raw/TIFF都用单行读写，避免完整output mmap/tifffile segments读前膨胀。所有大缓冲留本地。
- 当前最终caller先清理并停止/join monitor，再以OS高水位复核；成功report先序列化/fsync为临时名，预算检查后rename两数据，最后publish report；再查高水位，异常回滚最终名并写FAILED标记/失败report。原primary异常和cleanupErrors分列；cleanup失败不提交。出现report/rename/OS超峰的故障测试不留下可当完整成功的文件组。
- saved_reopen加try/finally结束/join监测，截短/错TIFF layout/hash三类实际失败无后台监测线程；操作明确是**读取旧成片的完整像素audit，renderCalls=0**，不称重新渲染。

## 家庭小图实际对照
家庭同GPU whole/stream：自然321×215/641×429/1200×801，rows1/17/37/113共10case；synthetic97×65负/>1/nearzero/尾行4case；consumer1/3/7/11全部RGBA16 **0通道差**。失败10种（源read、consumer、cancel、effect、unknown-key、route、inputSHA、backendSHA、device/staging硬限）全部失败且native live0/negative删除；cleanup另测失败无最终组。并发第二−30/第一取消−7并join/live0。最终caller同进程顺序两次（consumer3/11）也exact，不能当全图重跑。height215→430 same width321/rows37：device1439776、staging585152、hostvector2360372bytes保持不变，430亦whole exact。raw/TIFF预算门禁安全测试8PASS、saved audit4PASS。

## 唯一完整5496×3670调用
小图门禁后仅发起**1次**native20MP、PortraA、rows127/halo2、consumer7。硬cap device256MiB、staging64MiB、caller512MiB及free−256MiB保留，没有按家庭显存放宽。

|实测项|结果|
|---|---|
|完整pixel bytes|161362560|
|家庭stream pixelSHA|`f5539589141fdd65df6aed587f2e8ab5c5c0f743dc581c1f035f67d1f2e2e9a5`|
|native call|1509.18ms|
|caller总流程（原报告计时范围）|3654.24ms|
|native device peak|43848656 bytes ≈41.82MiB|
|native staging peak|25523424 bytes ≈24.34MiB|
|native显式host vector|16496672 bytes（不是全部host）|
|采样RSS / OS peak|379396096 / 379396096 bytes ≈361.82MiB|
|TIFF写/完整读回|413.41ms /257.32ms|
|read/consumer callbacks|29 /29|
|device/staging live after|0 /0，negative不存在|

native分项init314.51ms、compute200.40ms、readback48.86ms、negativeStore386.28ms、consumer114.67ms；其他精确值以full-stream-original-report.json为准，均来自同一次native而非拼接另次运行。独立saved audit新进程逐3670行raw/TIFF pixel hash一致，约39.91MB峰值、renderCalls0。

### 不隐瞒的最终scope缺口
首次完整caller已经覆盖native+raw/TIFF+全部readback+cleanup并在最后report序列化**之前**记录379.4MB；但成功JSON写出/publication之后没有再采OS高水位。这小于公司上一批“大规模post-native没测”的缺口，但不能叫完全关闭。已保留原始success报告，并在本地输出写AUDITED_STATUS.json明确该全图**不是最终硬化caller全门禁PASS**。最终提交helper对报告序列化/发布也检查并回滚，小图和故障已通过；本批遵守只一次full，不再跑第二次20MP，所以最终发布范围full gate标 **PARTIAL_NOT_UPGRADED**。未把先前原报告success伪改为新代码实跑；运行后/最终加固前source快照与最终source不同明确登记；快照并非调用时封存，不能声称每个helper与当次full逐字节身份已独立证明。

## 同设备与跨设备分开
家庭whole/stream小图exact；家庭whole20MP未运行：原publicABI主存必要source mapping12n+staging24n+ownedRGBA8n=44n约887.5MB（计及必要峰值持有模型，未以实际whole运行校准）>caller512MiB，虽然device76n+96MiB约1.63GB在家庭2GiB预算内，仍不强制上传。full仅stream数据一致性，不能声称20MP whole oracle。

1650公司/5060Ti家庭：同source和prepared输入hash相同，但三small及full输出hash不同。公司full `1c4a97...2a7b8a` vs家庭 `f55395...e2e9a5`；这是事实差异，不提高阈值也不覆盖公司证据。公司raw RGBA16没随Git/本机提供，故不能从hash推首个像素/差异计数；字段明确null，**跨设备逐像素首差仍待公司合法传回输出或小型对齐数值证据**，不能假造0差或归因全是GPU。新构建/编译环境也不同，定位需保留该混杂因素。

## 后续两项原定任务
A BT最小复验：原资源在5060Ti重现join−1ULP错误max5.509642e-5；固定兼容补丁max8.296318e-9，必要pointwise+C ABI 2/2PASS。不重跑全部研究；probe非instrument模式6/7输出不当作断点证据。

B D80014：首次V2真实decode崩溃0xC0000005（惰性cameraMultipler在context Dispose后由JSON枚举）。保留失败目录和最小错误栈摘要；仅改 `cameraWb=context.CameraMultipler.Select(x=>(double)x).ToArray()`，即context内复制值。source/ABI/matrix/WB/effective守卫未放宽，V2实现hash变化被新recipe完整记录。实际重跑7378×4924、8负值/2628>1，masterSHA `C32DD30440583E522B452DDE556FDEEF87D342CEE37142ADB52A9000E0D6782A`；保存、新进程重开、产品实际preview/TIFF16 exact。Ctrl-Break实际取消在unpack后token检查点触发OperationCanceled、recipe未提交；这是实际检查点取消，不声称已穷举每个native阶段。14项V2合同/生命周期测试PASS，产品相关14回归PASS。

## 交付结论
技术门禁：家庭small等价/一条full像素与I/O/BT补丁/D80014V2链有实际进展；最终hardened caller full发布host范围、跨设备首差、whole20MP均未越界宣称完成。旧家庭source/build/resources不动。技术通过不等于代码或资源分发许可；继承第八批GPL/CC BY-SA/厂家数据边界，未提前注册产品、没合产品分支。RAW/私人图/大缓冲/外部引擎资源只本地。下一批最有价值工作：明确授权再一次最终caller full预算实跑，以及拿到公司small raw才能做首差归因；不扩stock/B/seed/曲线/WB余量。
