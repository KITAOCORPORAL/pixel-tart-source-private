# 公司原RGB48载荷复验与产品线交接

2026-11-23，Europe/Helsinki。交接材料已完成；产品集成候选已核对，但公司原载荷重复重开资源门禁失败，产品稳定性准入保持PARTIAL。

本轮接续fix/precision-reopen-rootcause-20261122@ce9c38e7404d74d146ec918f862c46a38adfb85b。cf5→11a→ac1的旧根因与限定修复门禁不重查、不重跑；只增加公司授权材料身份检查、明确产品增量/依赖、以及公司原载荷的有限验证。产品源码始终ac1c57e9689428cea3f6938e06ad26209c7fc3d5，没有新增产品修复。

## 材料与来源

报告指定cache-A索引e1fbf5...json存在，index SHA256为C0A3BCE4111DF4830057FBF3B1D9D2725577AB7FBD50DF84951C1F17926EC510。其b94b118361e245b19f169897ae5a7353.rgb48实存241118208bytes，完整像素SHA为0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E，与上游公司记录完全相同。布局为7752×5184、RGB交错ushort little-endian、46512bytes/行、无头、方向1、编码sRGB。索引内SourceSha/DecoderIdentity只作为既存缓存来源记录保留，没有读取对应RAF或调用其中的X-Trans decoder。

旧报告列明的家庭synthetic40/Sony33来源、载荷、ICC路径逐一检查存在性；公司N:路径缺失，没有据此生成替代输入。新有限验证直接读原0806载荷，不能与家庭合成/Sony门禁混称。OS既有sRGB ICC为3144bytes，SHA2B3AA1645779A9E634744FAF9B01E9102B0C9B88FD6DECED7934DF86B949AF7E，与旧报告profile一致。

公司旧保存文件frozen.ptstudio.json为中性stack/look/rawDefinition=null、film关闭，原文件未修改。当前回放处理配方是既有最终工具已封存的两节点：WhiteBalance temperature9/tint-4，再BasicTone EV0.5/highlights-20/saturation7，ProcessingVersion2。二者明确区分，不假称原中性配方本就含这些编辑。

## 同源码有限结果

原工具构建至公司独立stage，程序集版本2.3.0+ac1...。stage只含托管资产，IRawDecoder为读取原RGB48的替身；仅新建说明文字marker给既有sourcehash协议，不是相机RAW。没有重解码、新RAW批次、旧STOP账本或余额、全局cache清空、强制GC、LOH压缩、裁工作集、GC环境变更。原2GiB Private/OS peak working set双守卫、50ms与600s watchdog均不变。

1. 原Core全帧数学参考oracle单次通过，给出独立的TIFF与直接float→RGB24摘要。
2. 处理后100%→保存→首份真实TIFF→释放→第二次同进程重开，在before-ushort-allocation-2阶段PrivateBytes越限。第一轮真实viewport Zoom1、matched完整7752×5184、编辑开启；master为241118208bytes RGB48且floatMaterialized=false。release-tasks-observed中的已识别Task均完成。
3. 原宿主只在整轮RESULT写displaySHA，失败前该文件未落盘。因此仅补充已有processed-once模式的一次首轮观察取得显示摘要，没有重试失败的重开或追加循环。该观察通过。至此停止资源运行，不添加中性三轮或更多重开。

|运行|采样Private峰MiB|OS工作集峰MiB|Private余量MiB|工作集余量MiB|结果|
|---|---:|---:|---:|---:|---|
|全帧Core参考|1996.90|2019.96|51.10|28.04|exit0|
|原载荷重复重开|2059.05|1615.63|-11.05|432.38|守卫STOP/exit1；第二轮未完成|
|首轮显示观察|1835.47|1517.73|212.53|530.27|exit0；只证首轮|

数字是每个独立进程50ms采样的Private峰和OS累计峰，不是同一时刻可相减的峰，也不冒称单操作耗时。失败状态保留；无guard阈值调整。

两个实际首轮TIFF（失败轮已完成的一份和独立首轮观察的一份）分别与Core参考逐样本比较120559104个16位通道样本，差异均0；尺寸/16-bit三通道/方向1/像素完整摘要/ICC3144bytes及完整SHA均一致。两份实际首轮导出SHA和参考探针见COMPANY_COMPARISON/output-probes。

首轮观察的完整viewport RGB24 SHA为BECC4E5B42A288364C0CD411765F82995CB6F8BC66E6C3422F48888241B2EE98，与原Core直接float→RGB24参考一致。其IsVisible/IsLoaded/Zoom1/full matched记录均验证；显示色彩转换开关按原门禁为false。这是受控WPF宿主的显示样本比较，不是普通产品EXE或屏幕物理色准验收。第二/第三轮显示和导出没有完成，不能补作通过。

## 新失败的最小定位

首轮释放后Private约1826.64MiB，已识别任务均完成；第二次读取前新VM master为null。此时若再申请229.95MiB完整ushort数据，仅相加已达约2056.59MiB，超过2048MiB，与新的2059.05MiB观测相符。没有ushort-read-complete-2事件；phase是上一记录边界，不能据此精确断言某一原生分配的所有权。

这份新证据没有推翻“首轮master仍使用RGB48”，但证明家庭fixture的131MiB最小余量不能外推为公司原载荷重复重开通过。本轮没有dump、profiler、诊断暂停或重新调查旧heap根，没有将剩余Private全归给WIC，也没有证明新的引用泄漏。NEW_FAILURE_BOUNDARY.json保留GC提交/碎片和阶段实测，足够产品线针对新容量失败继续定位；不自动启动新结构修复。

## 集成与剩余门禁

PRODUCT_INCREMENT.md和INTEGRATION_CHAIN给出三笔完整SHA、文件列表、测试、补丁及依赖。组合patch在临时索引上应用到d07和公司73988e4f后src/tests与ac1完全一致，原研究/产品树分别保留，未合并、未覆盖发行DLL。代码候选可供产品线显式审查合入，不能把这个apply检查称为产品已集成交付。

原封存Core85/WPF20回执只做源绑定引用，未重复执行。本轮公司首轮数值/显示/ICC通过与重开失败单独交付；未提升整体验收。空间/图/生效蒙版/复杂混合及直接Rgb32整帧兼容展开保持旧资源边界，细节见API_MEMORY_BOUNDARIES.md。

产品线还需处理本次公司原载荷重开容量失败，并在真正集成后的完整EXE完成最终验收。高光未重跑180面板、未调Knee/Amount、未开发V2；锐化V1继续封存；X-Trans未接线，发行解码器未替换。新RAW=0，旧批次/余额未使用。
