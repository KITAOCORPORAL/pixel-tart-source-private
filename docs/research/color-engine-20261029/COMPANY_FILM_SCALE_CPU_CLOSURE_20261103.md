# 公司完整capture CPU收口与阶段决策（2026-11-03，Asia/Shanghai）

**结论：公司已有完整capture核验通过；家庭完整capture未授权迁移到当前已核查材料范围。确认已有差异层级，保留未决原因，暂不修改像素数学。本轮新增GPU/API/RAW/20MP/六图/stream/BT/锐化候选全部0。完成本次交付后停止数值定位扩张。**

## 1. 正确接续与保全

fetch `research/color-engine-home-scale-20261102`，远端准确为已审查`0fa710e606b962efac66727215fb6c8f0c76a5ed`，无增量。旧公司诊断树现HEAD `099b2fb8dbd43fa872703cef84e04b3776c96aab`，为先前零GPU读回及收据的已推送接续，开工clean、unpushed0。新建独立公司工作树`E:/Kitao-Worktrees/color-engine-company-scale-closure-20261103`、分支`research/color-engine-company-scale-closure-20261103`，旧公司/家庭树不切换、不覆盖。

已读AGENTS/POLICY/CHECKPOINT、HOME_FILM_SCALE_LOCALIZATION_20261102、tools/home-scale-20261102/README与原比较代码。家庭一次gray观测已完成，未重跑。产品src/UI/任务桥/default/PV4/PV5/PV6、旧第十至十三批、先前失败/时间/收据和公司capture保持。

## 2. 公司实际完整材料核验（CPU0设备调用）

精确路径：`E:/Kitao-Research/color-engine-scale-localization-20261102-v2/gray-once`。

先实际读取execution-before/run/input/output和17条观察记录的文件长度/SHA/receipt binding，再执行现有`tools/color-round11/diagnostic/verify_gray_scale_tap.py --role company`：通过。16numeric数组finite/长度/hash、输入devicebits、CPU窄化→staging→device、小matrix、EV0及封存final身份全部符合。validator只读CPU，不import引擎或调用DLL。

家庭公开17文件子集/索引/receipt binding也逐hash核对；本轮没有调用家庭完整validator，因为家庭完整数组不在公司。家庭过去的完整validation只作为**封存报告**引用，绝不说本轮又读取家庭完整文件验证。

## 3. 差异层级与三种证据强度

### A. 本机完整数组对家庭封存SHA（不是双full逐值比较）

|文件|家庭封存SHA256|公司本机实际SHA256|结论/强度|
|---|---|---|---|
|film-sensitivity.cpu.f64（243double）|91e3777e4a7311d4d0af6821894c48caf2023f2a1237a34635db4fbb1f26c073|217c802fb6721a6573ec00c3b7991b1d0ab8588b62a0862443bf8173651b3078|文件身份不同，**差量/首坐标未知**|
|tc-lut.cpu.f64（110592double）|5c1db4fde41b5d92d211ed495642e2e5b17fa5ca06d285e3c648c34a5887e9ee|260d59c1194773a9daa866e64417f1ecd8f0e5103d08b01ace9dfeff42201f23|文件身份不同，**差量/首坐标未知**|
|tc-lut.uploaded.f32（110592float）|63105d14c4c1f4ba923121cd2de847aefed7f1626afc0d252e73de56125244c3|同左|实际公司数组匹配家庭封存上传SHA|
|tc-lut.device.f32（110592float）|同上63105d14…|同上63105d14…|实际公司数组匹配家庭封存deviceSHA|

家庭这些SHA分别与家庭run.observations和local-only索引一致；公司这些文件本体确实存在并已读取，不再只有公司报告摘要。**但家庭完整四数组没有读取，所以不能升级为完整双capture逐值/逐字节比较；changedValues/maxAbs/firstCoordinate均保持null。** 哈希不同不是本轮测出具体double误差，哈希同一是文件身份核对，不伪称两个完整数组内容都在本机。

公司完整capture自身CPUdouble→actualuploadedfloat→device各自bit0差。家庭封存validator也记录其自身bit0差（本轮仅报告引用）。runtimeLUTdoubleSHA不同而effectivefloatLUTSHA收敛相同；sensitivity是CPU准备LUT的输入，不是tc_b直接额外常数，不能把所有输出差直接归其double变化。

### B. 直接读取两端可用小数组（homepublic vs companyfull中的对应文件）

沿用已审查`tools/home-scale-20261102/compare_available.py`中原`compare()`实现，仅提取该函数AST执行；没有重造指标或执行其要求homefull的顶层流程。两端小array文件本体都实际读取。结果与家庭封存比较记录一致：

|数据|变化bit pattern分量|最大绝对差|最大适用同号非零ULP|
|---|---:|---:|---:|
|input1683f32、matrix9的CPU/upload/device|0|0|0|
|tc1122f32|487|1.1920928955e-7|3|
|brightness561f32|82|2.3841857910e-7|2|
|upsample1683f32|1520|3.8146972656e-6|23|
|exposure1683f32|1520|3.8146972656e-6|23|
|scale/offset CPU/upload|0|0|0|
|whole-observed RGBA16|17|1code|U16不套floatULP|

保留两层已成立结论：
1. **首个直接成对确认的GPU操作输出差异在spk_tc_b**，不是继续笼统写compoundstage，也不是首次到LUT。tc和brightness是同一dispatch两个输出，不能在二者之间排指令先后。此kernel只读已直接相同的input和matrix，不读runtimeLUT。
2. 561像素中158像素的tc/brightness逐bit同时相同；其中156像素仍upsample不同，共429分量、maxabs1.1920928955e-7。结合effectivefloatLUT同封存SHA，说明不能把**全部**upsample差异都说成tc_b变化的传播，也不能只归LUT而抹掉前面的tc_b已差异。

各端EV0 scale1/offset0、upsample→exposure→旧compound的关系保持；本轮不重新执行渲染或拆shader指令。

### C. 仅封存摘要/来源记录

家庭完整capturevalidator、原GPU/driver/capabilities与源编译收据是家庭既有报告；公司本机源/实际资源文件可以读取，但另一端不可用源/大常数仍按封存SHA描述。

- 本轮实际公司source文件hash与家庭pre-GPUfreeze的normalizedsource记录一致（engine.cpp只既有授权根字面量允许归一），数值CPUsource和shader源匹配其封存SHA。
- 公司当前selected runtime73文件、其中34SPIRV都对上两端封存集合，相关tc_b/lut2d_cubic/affine3分别一致。
- 家庭内部新build的spk_cctf_decode与实际选用frozen runtime存在旧BTpatch区别，已保留其边界；这里inputdecode=false、BT调用0，不把内部buildtree冒充执行resources。
- 两端mathmode小JSON记录直接读取一致，fusedflag1；三个相关shader没有constant_id0，mode相同不等于所有设备逐bit相同，更不是硬件/驱动贡献已隔离。

## 4. 剩余材料检查及本地交接

只检查已明确授权`E:/Kitao-Research`顶层名称、相关公司readback目录和家庭封存原路径，不扫描无关盘/私人目录。家庭原路径`N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once`在公司不存在；授权root顶层未见homecapture迁移目录，未提供其他授权迁移路径。这个缺项声明仅覆盖已核查范围，不假称穷尽所有磁盘。

因此**现有完整pairedcomparator未执行**，没有伪造home路径、生成home结果或重跑家庭GPU。还缺家庭完整4文件：sensitivity.cpu.f64、tc-lut.cpu.f64、tc-lut.uploaded.f32、tc-lut.device.f32。它们缺失不影响公开小array已经成立的结论。

本机公司capture21文件已按原字节复制为localhandoff并再次用现有completevalidator核验：

`E:/Kitao-Research/color-engine-scale-cpu-closure-20261103/readback/company-gray-complete-handoff`

本地同级`local-handoff-manifest.json`逐文件列格式、C-order/little-endian、dtype、shape、bytes、SHA和坐标说明；原capture不改。完整restrictedLUT/sensitivity仍只本地，未迁移至另一机器，也不进Git。handoff不含engine资源目录、DLL/SPIRV或私人照片；只是用户指定既有capture的本地交接材料。Git只提交该清单和报告hash/小数据描述，不提交完整受限数组。

## 5. 四项阶段决策

1. **已确认层级**：CPUdouble封存身份不同→effectivefloatLUT身份相同；相同直接input/matrix下tc_b两个输出已差异；同tc/b子集upsample仍差异；EV0未新改值。这些不是同一证据强度，表中分账。
2. **完整内容还缺什么**：家庭4个完整数组，故double变化分量数/max/首坐标、两个fullfloat数组直接全内容比较尚缺。当前没有材料就写null，不为“闭环”新跑GPU。
3. **实现错误证据**：当前未建立。无新参数、数据身份或上传链错位证据；跨环境同运算出现描述性差异不自动等于数学实现错误。**保留跨环境数值差异及未决原因，暂不修改像素数学。**
4. **是否值得继续控制变量**：本阶段不再自动继续。进一步FMA/优化/精度/JIT/驱动/shader指令实验只有在另有明确摄影问题/兼容或性能目标且单独授权时才考虑，不是为追逐跨GPUbit一致的无止境验收。最大1code仅固定小输入结果，不外推所有照片，不判不可见/可接受/产品通过；也不要求跨GPU逐位一致才能开展其它研究。

技术一致性研究到此收口。后续摄影效果评价缺**匿名人工偏好、已授权独立样本和明确的细节/肤色/噪声/色相取舍目标**，单列待办，不自行重渲染六图/换图补缺。SharedIncrementEnvelopeUnsharpV1继续defaultoff、NOT_ADMITTED，零值减少不是摄影效果通过。

## 6. 执行/失败与产品/许可

本轮仅CPU读文件、validator、smallarray比较、hash、localcopy。新增engine/open/render/GPUreadback/dispatch/RAW/20MP/六图/stream/BT/候选/build/sourcegeneration全部0；唯一家庭观测历史计数不加入本轮新调用数。CPUcloseout无执行失败；材料缺失不是补测失败，也不隐藏成完成双full比较。

CHECKPOINT和能力表使用Asia/Shanghai新收据，旧NOT_RUN/失败/历史时间保持。产品src/UI/default/PV456、旧实验、旧收据无变更；不注册入口、不改FMA/优化/精度/driver/curve/profile/clamp。研究结论不等于产品接入或代码/逐资源分发许可批准；LightTable非商业条款与Spektra权利不互替。

本轮结束后**停止数值定位扩张**。允许分发的报告/工具/metadata进新公司研究分支；LOCAL_HEAD/REMOTE_HEAD、工作区状态和本地路径由实际push后读回交付。
