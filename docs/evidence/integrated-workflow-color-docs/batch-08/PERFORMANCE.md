# 资源与性能结果

## 预算实现

StudioGlobalResources是进程级静态准入账本，跨工作区master/细节/HQ缓存/工作预留共用总额2GiB。master类总额1GiB，细节+HQ类192MiB；可见细节与本工作区旧块缓存合计64MiB。淘汰先释放长期缓存，再拒绝无法满足的请求，绝不降低原尺寸或位深。HeavyWork串行decoder/HQ/native块/导出/Publishing；try/finally归还。

解码工作预留1GiB；RAW unpack后按raw尺寸×28进行保守准入（极大图可能明确拒绝，不降质）；JPEG按完整尺寸估计。Export按完整输出尺寸预留master+行/codec工作量，Publishing按输入/输出尺寸估算。

**边界**：这是显式缓存与工作缓冲准入，不是CLR/WIC/LibRaw/OS总工作集硬上限。异步UI淘汰到Dispatcher执行、GC/native回收时机，以及未纳入的历史工作区缓存仍可能使进程峰值高于账本。尚未测跨多个真实桌面工作区的全应用峰值；此要求单列PARTIAL，不能以原512MiB/工作区当全局预算。

## 相同输入前后对照

隔离相同.NET10 console harness，旧发布batch07 Core和本批Core各进程，6000×4000浮点确定性输入，EV=.5，HQ1600，4次。无照片加入提交。

|项|旧batch07|本批|
|---|---:|---:|
|第1次ms|1480.2497|907.3567|
|第2次ms|1421.4651|883.2883|
|第3次ms|1431.2285|872.7049|
|第4次ms|1432.1462|887.1915|
|第4次累计peak working set bytes|403386368|405966848|
|第4次托管分配bytes|20489696|21219344|

每一次像素SHA相同：7CC382438E13D3DE5017EBD64416F1E8A79B882076A4C64AD40CF491877A5579。此输入约38%时间下降；内存略升，如实记录。不是两实际RAW的2秒基线同文件对照，不宣称所有场景同幅提速。原始JSON随证据保存；最终发布组件另复核身份/输出。

减少了重叠box footprint重复显示变换、每目标行缓冲申请；缓存命中省重复完整扫描。维持full显示算法与抗混叠参照，不将tone移至平均之后。

## 实际RAW新效果

EOS 5D Mark III（5796×3870）、ILCE-7M4（7028×4688），固定seed20261012即时i-Type创作，三格式完整编码+回读测试：TIFF16/PNG16 max0；JPEG MAE约2.4913/1.0233、RMSE3.5178/1.4372，未放宽门。

单次完整导出+额外Inspect约Canon15.8/26.0/24.7秒、Sony23.1/37.6/38.9秒（TIFF/PNG/JPEG顺序）。包含新效果逐像素解析缺陷与重复验证，不与旧无效果编码混比。新效果大图首次生成和导出仍明显计算耗时；4秒显影只在ready后开始。没有承诺即时无延迟。

## 相同Sony RAW实测复核

同一DSC04831.ARW（7028×4688），同一probe，旧发布Core与新Core隔离进程，曝光0/.25/.5对应HQ：**1636/1867/1889ms → 1092/1262/1227ms**。累计峰值976052224→976388096 bytes（近似持平，未下降）。两次完整TIFF16 SHA均2F9F37A699E7B37335AF0F1DC220BD9227BA35A321B2F2A93F05A4487EDB109A，源文件不变。

512块16/14/15ms→27/25/25ms：统一输出坐标/即时层分支带来native块额外成本，虽仍毫秒级，明确记录为本场景退步，不掩盖为全面提速。原始REAL_PERFORMANCE_BEFORE/AFTER.json保存全部stage/内存；RenderRowAggregate在Encoded内部，不能重复累加。

## 发布组件验证

源码身份c3c1ed7，发布Core/主DLL的SHA与隔离测试目录逐一一致（PACKAGED_COMPONENTS.json）。发布Core专项33/0/0；发布WPF合成/产品专项13/0/3（3项真实opt-in另跑）；发布实际2相机专项3/0/0，4m36s。所有实际输出与保存重開结果见PACKAGED_REAL_INSTANT、PACKAGED_PV4_RESULTS、PACKAGED_SAVE_REOPEN。不是启动应用EXE。

发布Core隔离Sony复测HQ1195/1249/1242ms，累计峰值976240640 bytes，原PV4 TIFF SHA仍相同；与旧对照相同source。此复测与其他测试同时运行，作为身份/数量级验证，不是无负载严格基准。
