# 第二次 RGB48 打开前：谁还持有第一轮整幅图像？

研究分支 `research/rgb48-second-open-holders-20261123`，独立工作树基于公司集成证据 `63a8633d`。产品src树与实际候选 **f3190f44 完全相同**，本次只新增诊断工具和报告，不改数学、ICC、旧配方或发行解码器。**稳定完整产品继续 f84，没有推广研究构建。**

## 结论先限定对象与现场

**公司实际候选的已有记录还不能回答“第二次数组申请前，旧处理图被哪条强根保活”。** 它们证明第一轮活跃时的字段持有与图像身份，之后仅有已枚举任务状态、新VM的空字段、旧弱引用仍活着；没有该公司边界的强根查询。

本轮补了一次已授权家庭合成40MP、f319源等价但不同本地二进制的诊断。第二个ushort[]尚未申请时，7个弱句柄精确标记旧对象：

| 角色 | 捕获结果 | 能说明什么 |
|---|---|---|
| 第一轮读取者的ushort[] A | 有效弱句柄槽为0 | 这份读取数组在捕获时已不在弱槽中；并非仅凭Task完成猜测释放 |
| 第一轮独占master数组B | 堆中仍有241118232字节UInt16[]，gcroot=0 | 数组含241118208字节像素，但没有可定位托管强根 |
| master包装 | gcroot=0，disposed=false，内部ownedRgb48指针仍精确指向B | 不可达包装内部的引用不是外部活根；不能为降峰擅自销毁共享master |
| 原始整幅CachedBitmap O | 对象存在，gcroot=0 | 无可定位托管强根；不是说WIC私有提交已释放 |
| 处理整幅CachedBitmap M | 对象存在，gcroot=0 | 在本家庭捕获时刻，反驳“这幅旧处理预览仍持续由托管强根保活” |
| 旧Editor、旧Workspace | 两者均存在且gcroot=0；旧Editor四个主图/源字段直接在dump中证实为null | 这次确实检查了旧对象，不是把第二轮新VM的null误当旧对象证据 |

还按>=100,000,000字节枚举Byte[]/UInt16[]/Single[]：只有上述一份UInt16[]，没有该量级Byte[]或Single[]。不是全WPF/native资源普查，也不能把全部残余Private归给WIC。

**时序限制不能省略：** 本次沿用诊断暂停来采Heap dump，实际边界约16.14秒，GC计数前后均12/7/4，但其他线程/原生释放可能推进。根结论只属于该捕获时刻和家庭材料，不能直接推翻公司无暂停瞬间的短暂持有，也不构成容量PASS。采完即诊断结束，未执行第二次大数组申请；不以延时、重启或GC制造通过。没有重跑旧中性/Sony/像素或三轮资源实验。

## 必须分开的四份证据

| 记录 | 实际产品/材料 | 已证实 | 不能移用的部分 |
|---|---|---|---|
| ce9c38e7研究转储 | 该旧转储App PE版本实际为11a424c9；家庭材料；旧float路径 | 旧float数组与旧VM gcroot=0 | 不是新RGB48 backing的公司f319持有关系 |
| ce9研究最终交付 | ac1c57e9；家庭synthetic40/Sony有限范围 | 处理3轮/中性3轮/Sony2轮的受控门禁通过 | 不代表普通EXE或公司原件；不能再用旧11a失败拒绝这个研究成果 |
| 55564444公司交接 | ac1c57e9；已授权0806D680公司缓存 | 首轮成功，第二读仍STOP，边界约1826.64MiB | 不是f319运行记录，不与后者峰值拼接 |
| 63a8633d公司集成 | 显式三补丁实际候选f3190f44；公司缓存/普通TIFF分开 | legacy首轮真处理100%及导出，第二读STOP；普通TIFF首导全行通过后重开也STOP | legacy是受控VM，普通TIFF又是另一条prepared路径；不升级为公司完整门禁通过 |

本次家庭诊断的产品源树与f319相同，App/Core二进制分别9613544b…/4e3162ed…；已知公司完整候选包App为80f8ab6c…，原legacy run的App/Core摘要以其本机原LAUNCH为准，不能仅由包摘要猜。因此不称复用了同一公司二进制。精确SHA与Git对象绑定在 PROVENANCE / EXISTING_EVIDENCE_BINDINGS。

公司f319 legacy第二次申请前Private为1843MiB；新ushort数据229.948MiB，相加下界2072.948MiB，与实测2075.402MiB STOP相容。这个算术不是强根证据，不说明哪些native分配或GC空洞造成剩余提交。阶段标签覆盖申请及其后的读取；无ushort-read-complete-2不能把STOP精确锁定为new指令本身。

## 两个会导致误判的日志盲点

1. 宿主 `Release` 已把静态Vm置空，随后创建新外层Vm再调用LoadSession。`before-ushort-allocation-2`里那些null读的是新外层Vm；解码还发生在它的staged workspace里。旧对象是否清空必须另有旧地址/弱句柄证据，本次才直接dump旧Editor。
2. `KnownTasks`用 `GetField("Completion")`，而 `ThumbnailJob.Completion` 是属性。`ScheduleTargetThumbnail`的闭包可捕获source/master，Dispose取消/移除job不等于等待Completion。四项RanToCompletion不等于全部后台任务结束；缺TaskId/continuation/结果对象图。**本次故意不修这个等待策略**，否则可能改变原失败条件。

WeakReference.IsAlive只证明弱目标未清除，不证明强可达；日志Role只是类型名，不是稳定对象地址，多个旧小代理/版本也混在同一类型列表。RuntimeHelpers身份hash只用于同次日志佐证，不能跨进程对号。新观察用WeakShort句柄映射同一dump中的地址，且不在边界读取Target或触发Rgb32展开。

## 不把三种“缓存”混作一个持有者

公司本次legacy run的磁盘0806载荷由受控Replay直接读入；workspace构造把该IRawDecoder直接交RawMatch管线，staging也转交同一个decoder。仓库的RawDecodeCache类虽有512MiB字典强持RawDecodedImage的能力，但没有证据表明它参与此run，不能凭类名把它列作活根或清它求过关。

workspace的_sourceCache是路径/mtime/hash到BitmapSource的另一条栅格来源缓存；此处RAW marker分支不走LoadSourcePreviewAsync，普通TIFF则走它。Editor._previewCache又是(source,key,result)显示缓存；三者不能互相代称。sourceCache的128MiB与previewCache的128/256MiB条件都允许保留一份超预算图，不是进程硬容量上限；本次没有清缓存或改变策略。

## 源码顺序表与普通TIFF的交汇

完整五列表见 [OWNERSHIP_TIMELINE.md](OWNERSHIP_TIMELINE.md)，同时提供对应JSON。阶段从Replay读取→复制owned RGB48→Editor源/处理预览→缓存/导出→页面解绑/Dispose/任务→新staged准备→第二次申请。

普通TIFF走LoadSourcePreviewAsync/StudioQuickExport/WIC，接ColorStudioBitmapRenderer，不是Replay的new ushort入口。正常prepared重开为失败回滚保留现有工作区，新准备成功后才AdoptPreparedSource、转移目标/cache、清staged和旧目标master。它与legacy交汇在Editor/BitmapSource/分析/视口及提交生命周期，而非同一个源数组读取点。公司普通UI记录明确未测内部全尺寸处理预览，不能拿其“100%动作”标签冒充legacy内部校验。此路径本轮没有新抓根或重跑。

## 公司机下一次只做一个观测

[一次实验命令与支持/推翻条件](../../../tools/Rgb48HolderBoundary20261123/README.md)。

`observe_company.py`必须拿原63a实际run的LAUNCH作为baseline，逐字节校验原run的App/Core摘要并核PE版本f319、复用其中task/已授权0806数据与配方，仅编译小诊断宿主，不重建或替换产品。对一个全新输出目录执行一次捕获与离线分析；稳定f84保持，不新增RAW，2GiB/50ms守卫保持。

可证伪的判断：若旧M出现非观察器强根，支持具体holder解释并沿根链定位；若M存在但0根，且句柄/捕获/GC计数有效，则反驳该捕获时刻M持续强根保活，支持“已不可达但尚占堆/提交”的有界解释。若暂停前临时引用、缩略图缺项、GC/采集扰动或未标记旧图仍无法判断，必须保留未知，不能据此宣称无泄漏或把Private差额全部归WIC。工具受扰动/守卫中止则保留失败，不重试过关。

## 不变项

src/tests/extensions无修改；像素数学/处理顺序/ICC/解码器/旧配方不动；无forceGC、cache清理、预算增加或原项目覆盖；旧实验、STOP、dump和候选均保留。新dump约1.48GB、图像及全SOS仅在LOCAL_ARTIFACTS本机路径，不入Git。当前稳定产品入口仍f84，本分支是观察工具，不是待发布产品包。
