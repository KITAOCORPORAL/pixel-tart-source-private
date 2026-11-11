# 新批次准备与授权边界

新任务：RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111。源码 b86d02455be77e4ed0f44ddd5b9816681979746e；候选研究仍为53e2355d，native库与绑定沿用既有hash。

当前状态 DRAFT_PENDING_USER_AUTHORIZATION。实际构建及计划位于本机独立 DRAFT02 目录；NEW_BATCH_DRAFT.json 给出315项hash绑定。授权额度为0、计划AuthorizedForHeavyDecode=false。未启动app、未创建新ledger、未进行真实RAW处理。旧批次STOP及一次consumed必须保持。

## 当前已准备

- Version2任务准入：新Task和Version不可替换；显式授权标志缺席/false时先拒绝，再考虑输入、文件和预约。
- 新诊断app和托管worker，WholeAppDiagnosticBuild=true、XTransStableInputV1DiagnosticBuild=true。
- 默认raw_r.dll仍是F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9。
- worker候选仍是157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F，Sdcb绑定6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3。
- PLAN-A/B/control、SOURCE_AND_RUN_LOCK、AUTHORIZATION、OLD_STOPPED_BATCH_BINDING全部已写；cache-A/B/control和新ledger与旧现场分离。
- launcher实际拒绝pending草案；没有创建ui-steps或ledger。

## 取得新的明确授权后

1. 先fetch并检查本机新回执，核验原22项现场、STOP、一次consumed及原候选。复核新构建315项和Fuji/Sony实际输入hash。授权不能由计划字段本身推定。
2. 将AUTHORIZATION.json更新为GRANTED_BY_USER并记录该次明确授权；将SOURCE_AND_RUN_LOCK.authorizedHeavyDecodes设为获准额度（拟议6），再设置三份Version2计划AuthorizedForHeavyDecode=true。将授权回执的新hash更新到三份Files，重封每份计划SHA；所有文件和源码必须固定后再运行。
3. 拟议分配仍是 A-cold1 / A-cache0 / A-rebuild1 / B-cold1 / B-cache0 / B-rebuild1 / control-A1 / control-B1。不得转入旧批次剩余5次；不得清除任一STOP。任一真实失败按新批次规则停止。
4. 使用支持Version2的tools/XTransProductRegression/launch_app.py启动新attempt。启动器要求计划、额度及授权回执一致；不要直接开worker或借用旧Task/ledger。
5. 从真实暗室可见入口完成Fuji A首次打开；先确认代理尺寸与无隐藏整图请求，观察导入、空闲、最小化/恢复的主进程内存，再保存工程、实际导出两份TIFF16。
6. 通过真实入口保存重开，验证候选cache命中且不发生新解码。缓存失效测试先备份诊断index，只使该index校验失败，保留payload与历史；A-rebuild只允许一次重解码。B以独立cache重复。
7. Sony control-A/control-B各最多一次默认解码，核对DefaultDecodeCompleted、默认native身份、无候选worker。
8. 每条路径实际导出两份相同设置TIFF，以解码后的全部RGB16像素比较；位深、SampleFormat、SamplesPerPixel、方向和ICC分别核验。预览/保存重开/缓存/导出各自记录耗时与主进程、worker、合计峰值。等待时间不得计作冷开。
9. 用户显式100%和回到适合窗口均要从可见照片页操作，核对全图、五处ROI、细纹/肤色和接缝；固定裁片仍取旧CROP_PLAN_FINAL同位置，不能用离线图替代实际暗室截图。
10. 保持每进程2 GiB守卫与worker600秒限制，原阈值不放宽。特别关注完整MainViewModel/诊断构建下原1355.7–1356.6秒类似迟发跃升；缓存重放没有证明其唯一来源。

新批次没有在本轮执行。内存修复的源代码和缓存复测不等于真实RAW/TIFF产品验收；完成并回传新批次结果后再决定后续接入。
