# 一次 RGB48 第二次打开持有者观测

这是诊断宿主，不是产品修复、资源PASS或稳定入口。稳定产品继续f84。宿主基于63a8633d中实际f319运行的LegacyExportMemory程序；产品src未变。只增加7个弱GCHandle标签与采完后的诊断结束，不改变像素、ICC、配方、发行解码器、释放等待或原700msUI settling。

## 唯一观测

第一轮已有读取数组、workspace、editor、master包装/其ownedRGB48数组、原始/处理整幅CachedBitmap各有弱句柄角色。没有强/固定句柄，不在暂停边界读取Target，不访问Rgb32。第二次new ushort之前记录映射，继承原诊断暂停供Heap dump使用；采集后结束，不执行第二次分配。退出0也只能叫DIAGNOSTIC_ONLY，不能作为容量门禁通过。

暂停会让其他线程/原生释放推进；本次家庭转储边界约16秒，GC计数虽未变，也不等于原始无暂停时序。负根结果仅反驳捕获时刻该对象持续被托管强根保活，不排除暂停前短暂引用、未标记旧图或原生资源滞留。

原宿主KnownTasks有一个已证实的盲点：用GetField("Completion")查ThumbnailJob，而Completion实际是属性。故原日志四项RanToCompletion不等于所有缩略图完成。本诊断故意不修等待策略，避免通过额外等待掩盖原峰值；观察到根时要沿实际closure/task定位，没观察到则保留此未知。

## 下次公司机只执行一次

公司使用的一页交接与按本机回执解析路径的命令见 [COMPANY_HANDOFF_20261125.md](../../docs/validation/rgb48-holder-boundary-20261123/COMPANY_HANDOFF_20261125.md)。2026-11-25 fetch后，公司最新已提交证据仍为63a8633d；本次未再捕获家庭或公司进程。

在本研究分支上，沿用公司已授权0806D680 RGB48缓存、原f319完整候选和已有dotnet-dump 10.0.750501，不再运行旧中性/Sony/像素/三轮资源实验，不读相机RAW。

```powershell
python tools/Rgb48HolderBoundary20261123/observe_company.py `
  --baseline-launch "E:/Kitao-Deliveries/company-precision-integration-20261123/legacy-processed-three-cycle/LAUNCH.json" `
  --dump-tool "<公司已部署的10.0.750501/dotnet-dump.exe>" `
  --output "E:/Kitao-Deliveries/rgb48-holder-boundary-company-once"
```

运行目录是本研究分支的独立工作树根（含AGENTS.md和tools目录）。以上E盘路径来自原公司回执，须在公司机按原run/迁移回执解析；家庭不推断公司盘现状。需Windows x64、Python ≥3.11、.NET 10 SDK/WPF和已有固定采集器，输出盘空闲须>3GiB。

输出目录必须全新；已有证据拒绝覆盖。工具路径须是已有固定版本的实际路径；若公司机未准备该工具，只补这项前置，不换采集器或跑资源测试。脚本仅编译新的小宿主，**不重建产品**；默认从原LAUNCH.stage取实际运行二进制，App和Core必须逐字节匹配该原run，且App PE产品版本必须为f3190f44；不把完整候选包的80f8ab6c…摘要自动当作原legacy stage摘要。也可用--product-app显式指定另一个逐字节匹配的目录。复制managed-only stage前核验其MANAGED_BINDING，未重建产品。

输入来自原LAUNCH.task；capture.py核payload长度与全摘要，ICC在该脚本内仅检查存在，因此公司命令另按task.iccSha256核摘要。**配方并非从task读取**：Program.Stack()固定沿用63a原LegacyExportMemory宿主的points配方（PV2；WB temperature9/tint−4 → BasicTone exposure0.5/highlights−20/saturation7），与普通TIFF恢复文件的exposure0.1不同。guard保留Private与OS峰值工作集双2GiB/50ms（以及原600s上限），发行raw_r/libraw不得加载。

一个命令执行：构建宿主→受控第一轮实际处理100%/保存导出→第二次数组分配前7弱句柄定位→一次Heap dump→立即结束诊断→离线查询。原候选/原run/稳定f84不改写。转储、图片、payload、全SOS输出只在包外，不能入Git；可回传ANALYSIS_SUMMARY.json的审阅后摘要、对象映射、必要根链、GC计数与绑定。

## 判别规则

- 旧处理bitmap弱槽有效、对象存在，gcroot有非观察器的具体强链：支持“捕获时刻仍有可定位强持有者”，沿该字段/任务/事件定位。若根来自新增诊断强引用，结果无效，不能归因产品。
- 对象存在而gcroot为0，且弱句柄确为WeakShort：反驳“该捕获时刻它仍由托管强根保活”。若owned数组也0根、旧editor字段确为空，则支持这组对象已不可达/尚在堆与提交里的解释；**不证明全部残余Private都是WIC/碎片，不证明公司无暂停瞬间无短暂引用，更不证明无泄漏**。
- 弱槽清零：该角色目标已不在该有效弱槽中，本工具未在捕获前显式清空/Free；不能用别的同型数组替代它。
- GC计数变化、对象无法映射、SOS没有完整根结果或采集被守卫终止：保留为受扰动/不充分观测，不自动重试，不增预算，不改稳定入口。

家庭已完成一次新观测：同f319产品src树、不同本地构建与合成40MP（不是公司0806），六个仍存在的主对象0根，首reader弱槽0；不当作公司通过。详见docs/validation/rgb48-holder-boundary-20261123。
