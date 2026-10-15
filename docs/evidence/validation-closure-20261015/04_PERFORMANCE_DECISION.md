# 阶段4：Structure 分配根因与不改生产决策

固定同 Canon TNAN8886.CR3 hash；源/节点参数/完整尺寸5496×3670不变。新独立进程 full、tile512×128、full-cancel、tile cancel 各一次；包含 runtime allocation tick 类型采样、GC分配累计、25ms WS/private/managed/ledger、10秒无强制GC自然释放。原3秒序列及旧失败原地保留；本轮不跑无限轮挑最好。

|路径|render ms|render allocated bytes|采样峰WS|采样峰private|末端WS（自然10秒）|
|---|---:|---:|---:|---:|---:|
|full|4709.0448|1190717448|1854062592|1844068352|1850368000|
|tile|8772.0523|2249960824|2006159360|1991839744|2002628608|
|full cancel|103.3001|564813584|911302656|1218146304|905609216|
|tile cancel|103.6844|260706808|689201152|908140544|683397120|

full/tile最终float字节hash均CC85E4AA22C9750B3AF0C50DA9BC28BE6C5F507E75645EC318062974D63D2421，与上一批一致。取消均在100ms请求后约104ms实际抛出。不是把小预览当大图oracle。

## 成本归因
- tile把源区域复制成新floatRGB，再调用ColorStudioDevelop.ApplyRegion。该函数分配linearRGB、luminance、structure两张blur单通道数组、detail两张blur数组、outputRGB；每tile至少56字节/扩展像素，加全帧输出12字节/像素。
- halo37，319 tiles，总扩展像素35,807,112，是20,170,320原像素的1.77524倍。算得最低array payload2,247,242,112字节，实测render allocated2,249,960,824字节，接近，支持重复halo+临时数组是主要托管成本。每行/列running-sum重新计算，不能把tile更慢归因成cache命中问题。
- Structure没有scratch缓存；Details字典虽创建，但Develop不使用。不能把不存在的缓存“清理”当优化。runtime采样主要Single[]；事件聚合覆盖解码+analysis+render全进程，字节是采样近似，不等同render精确分配，也不是调用栈或native allocator归属。source行与静态payload估算和GC render分配共同限定归因。
- 资源账本末端为零，但full/tile末端WS仍约1.85/2.00GB，并未回到约0.667GB基线；private也未回落。本轮明确不宣称自然回收已解决。源对象保留且GC/LOH保留页影响这些数值，native分配所有权仍UNAVAILABLE。

## 决策
不改生产代码。扩大tile/复用scratch/新band算法可能减重复成本，但需在所有参数、边界、取消、历史版本及preview/reopen/export维持语义；当前两成熟实现节点对照门禁未通过，不把本次ICC/RAW研究当Structure节点许可。当前只有本实现full oracle，不满足独立两实现门禁。可验证的局部工程方向已明确，尚无获准生产patch。不做无证据的pool/GC.Collect/工作集trim，也不把取消账本归零包装成系统内存回落。

P3源400ms阈值及历史失败保留；本轮用封存发布Core运行完整测试，结果另见04_RELEASE_CORE_SUMMARY/最终测试分类。PV4/PV5/PV6 golden保持；PV7与synthetic reader默认关闭。本轮src/tests内容相对af78bb零变更。
