# 家庭第十批：最终20MP收尾与公开合成诊断（2026-10-30，Europe/Berlin）

**阶段结论：固定PortraA流式研究原型的最终caller20MP合同通过并冻结；跨设备合成包家庭执行完成，公司NOT_RUN。不是通用胶片后端、许可或产品接纳全部完成。**

接续fetch核对 `research/color-engine-home-20261030@575b0b1614010c20bcbd9bf38ac2356d6ac7467f`，远端无新增。AGENTS/POLICY/HOME_ROUND9_REPORT/STREAMING_HOME_CAPABILITIES与最终caller代码已读。旧full AUDITED_STATUS、失败目录、旧source/build/resources完全保留。不重复D80014、BT完整回归、WB余量、曲线或桥研究；无产品src/UI/模型/任务桥/默认PV变化。

## 目标一：冻结一次、实跑一次、固定终点

### 先修重复取消与收尾
新增 `native_wait.py` 把SIGINT/SIGBREAK/SIGTERM处理为设置cancel，不在native worker返回前抛断owners清理。`join_native`循环抵御重复KeyboardInterrupt；输入mapping、callbacks和DLL只在join后释放。真实321×215 held read回调中连续3次定向Ctrl-Break，native−7、join、live0、无final组；另受控raise_signal(SIGINT,SIGINT,SIGBREAK)在真实native hold中也通过。仅证明这些已测检查点，不能称shader任意位置可抢占。

成功预算终点在执行前固定为 **SUCCESS_GROUP_COMMIT**：源准入/hash/finite、native、输入映射释放、raw/TIFF逐行写与全回读、资源cleanup、RSS监测停止join、临时报告serialize/fsync、raw/TIFF与成功report发布后**一次Windows OS高水位检查**。检查通过写预分配4096byte `acceptance.json`回执，绑定report SHA、输出pixelSHA、两个峰值及具体时点。读者必须有report+有效receipt且无FAILED/AUDITED_STATUS；否则不当成功组。回执只是记录这个固定终点，后续console打印和进程退出不重新变成渲染预算范围，不再追加微小范围令门禁无限后移。

主异常与cleanupErrors分列；fail/取消保留partial，无成功receipt；report/第二rename/OS峰值超限等8项安全测试PASS。saved audit4项含截短/layout/hash失败的监测线程stop/join PASS。

### 调用前执行身份
`stream_full_once.py`在launch child之前产生 `execution-freeze.json`，child在进入stream前逐字节再核验。快照SHA **968f46de93e97014fd333905544968401b22491bb0280ac5b6b18ce6f93a09e6**，不是运行后快照。包含9个本地caller/helper/固定99字段源、198个已导入runtime模块、1807标准库.py、Python exe/runtime DLL、NumPy/psutil/Pillow/tifffile包及其native文件1047项、engine/runtime DLL、资源/SPIRV逐项、environment/config、输入manifest/file/pixel、自然源SHA、完整resolved/caps、GPU/driver。实际freeze的所有caller链源码本轮成功后未再修改，当前字节仍匹配。快照是文件一致性/执行身份记录，不是签名或防恶意本机篡改机制。诊断工具另写，不冒充同一执行链。

同一Canon source B7D358...33401，full A 5496×3670、pixelSHA cd47a182...0f50f9，先重新核验source和prepared值，冻结99resolved/资源，不换曝光。上一批10small exact及身份复核可复用，变更后只重跑必要small/failure/重复取消，未再做RAW解码/桥。

### 唯一20MP完整调用实际结果
新目录 `N:/pixart/ColorResearchHome-20261030/round10-full-final-caller-once`；本轮**计划full调用1，实际1，自动重试0**。device256MiB/staging64MiB/caller512MiB/free−256MiB保留，不抬cap、不关其它程序。

|测量|值|
|---|---|
|输出RGBA16 pixel bytes|161362560|
|output pixelSHA|`f5539589141fdd65df6aed587f2e8ab5c5c0f743dc581c1f035f67d1f2e2e9a5`|
|native wall / Python native call|1595.93ms /1596.23ms|
|caller准备至cleanup记录|5598.52ms（包含child冻结再核验）|
|SUCCESS_GROUP_COMMIT时点|约5613.55ms（caller budget对象起点）|
|device peak|43848656bytes ≈41.82MiB|
|staging peak|25523424bytes ≈24.34MiB|
|native显式host vector预算|16496672bytes，不是全部RSS|
|sampled RSS peak|380588032bytes ≈362.96MiB|
|Windows OS peak|380588032bytes ≈362.96MiB|
|live after /negative|device0、staging0、negative不存在|

native init328.32ms/sourceRead94.53ms/upload81.13ms/compute212.44ms/readback51.62ms/store426.09ms/consumer115.34ms，均该调用内计时，不拼旧native与另次I/O。本次caller原报告statusCOMPLETE且有效receipt绑定报告hash。回执的check点明确：cleanup_joined_before_report、report_serialized_before_data_commit、data_renamed_report_provisional、final_budget_before_success_report_publish、SUCCESS_GROUP_COMMIT，全部withinCap。由此关闭第九批最终报告发布的host范围缺口，**只对本固定版本/场景/参数/硬件合同冻结**。

独立新进程saved audit逐3670行raw/TIFF像素hash完全一致，约40.70MB OS峰值，renderCalls=0；它是读取audit，不冒称重新渲染。旧第九批AUDITED_STATUS仍存在，新成功不抹掉旧PARTIAL。

## 目标二：先控制混杂，再执行公开诊断

### 已有公司/家庭记录对齐
`confound-audit.json`逐项比较：自然source/prepared pixel一致、99个resolved（含隐藏default unsharp）一致、全部资源及SPIRV文件hash一致（34个SPIRV）；生成stream8文件中7相同，engine.cpp将授权root字符串换回公司值后hash完全一致，所以授权路径根是已证明的非数学变更。两边记载的Clang20.1.2/glslang16.6.0/CMake3.31.10/Ninja1.11.1.4工具exe哈希相同；Release/Ninja/nativeRAW/desktop flags一致。

仍不能称环境相同：公司GTX1650/572.70/Vulkan1.4.303 vs家庭RTX5060Ti/610.88/Vulkan1.4.341；Python3.13 vs3.12、Pillow12.2 vs12.3。prepared输入byte一致消除了resize值本身变化，但公司第八批没有调用前完整per-object命令/编译器运行库冻结，CPU运行时/构建和driver仍需定位。不能仅以DLL总hash不同解释像素差异，也不能断言全是GPU。

公司自然RGBA16仍不在家：仅知不同hash，firstDifference/changedChannels仍null，无猜测。自然差异不再阻断公开合成诊断。

### 可执行公开包
入口 `tools/cross-device-diagnostic/README.md`。3个明确身份SyntheticPortraCrossDeviceV1：17×33灰阶近零负/>1/BT码向量、19×35原色/高彩、21×37小型边缘图；source不是Canon，manifest naturalSource=null。自然源白名单不删，新增独立私有兄弟symbol只白名单3个pixel hash；家庭实际向原自然入口提交合成身份被拒绝。

新外部diagnostic/trace-v2树与DLL与自然full DLL完全隔离，后者未改。复用原固定film/print数学、99resolved、资源；trace只在每stage后被动readback。原natural entry代码/源白名单仍在。代码生成器增加源hash守卫，允许仅native root literal归一；在clean再生成后8文件hash与已执行诊断源码完全一致，generator-repro已记录。

家庭实际执行：3合成case×rows1/7/17/64，whole/stream全部RGBA16 **0通道差**。每case12个whole阶段＋36个stream band/halo阶段（r7）完整小float文件、shape、hash和首8像素采样；traced whole/stream最终与untraced final **0差**。最终小RGBA16和阶段文件约1.06MB，可入Git。BT另独立mode3直接native decode读取13个join±6ULP输入/输出bits，不改变PortraA的linear输入合同。这里验证诊断执行，不重开BT算法研究。

`compare_rows.py`逐行报告first xy/channel、changedChannels、max/mean/分位数；U16用精确histogram，高阶分位，float使用小完整有限阶段values，非有限拒绝。`compare_packages.py`先输入/99resolved/逐资源和结果hash对齐，再比较whole、各rows stream、whole阶段、stream执行序列阶段与BT。跨设备只量化，无事后阈值；same-device仍0差。比较器4项+包篡改/域/阶段5项测试PASS，home对自身selfcheck0差只是工具复核不是公司结果。

公司合成执行明确 **NOT_RUN**：包内有完整公司build/run/verify/compare说明；无私人素材依赖。公司运行后返回小输出即可定位首个whole阶段分歧；若resources或参数不符先拒绝比较，不直接改曲线/precision/profile。外部资源/DLL不进Git。

## 保留边界和下一阶段
本轮没有D80014重解码、WB余量、B路线/三stock/seed/曲线/桥新实验。保留cameraWb上下文内数组快照不改。没有肤色/独立验证/强高光/物理拟合覆盖；一张剪影不能外推。技术通过与GPL/CC BY-SA/逐产品数据分发许可分别管理；固定PortraA流式研究阶段可冻结，通用胶片后端及产品接纳仍OPEN。UI/项目模型/任务桥/default/PV4/5/6未改，旧outputs/failed/AUDITED_STATUS全部保留。
