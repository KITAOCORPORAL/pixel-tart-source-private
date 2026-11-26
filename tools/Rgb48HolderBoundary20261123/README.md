# 一次 RGB48 第二次打开持有者观测

这是诊断宿主，不是产品修复、资源PASS或稳定入口。稳定产品继续f84。宿主基于63a8633d中实际f319运行的LegacyExportMemory程序；产品src未变。只增加7个弱GCHandle标签与采完后的诊断结束，不改变像素、ICC、配方、发行解码器、释放等待或原700msUI settling。

## 唯一观测

第一轮已有读取数组、workspace、editor、master包装/其ownedRGB48数组、原始/处理整幅CachedBitmap各有弱句柄角色。没有强/固定句柄，不在暂停边界读取Target，不访问Rgb32。第二次new ushort之前记录映射，继承原诊断暂停供Heap dump使用；采集后结束，不执行第二次分配。退出0也只能叫DIAGNOSTIC_ONLY，不能作为容量门禁通过。

暂停会让其他线程/原生释放推进；本次家庭转储边界约16秒，GC计数虽未变，也不等于原始无暂停时序。负根结果仅反驳捕获时刻该对象持续被托管强根保活，不排除暂停前短暂引用、未标记旧图或原生资源滞留。

原宿主KnownTasks有一个已证实的盲点：用GetField("Completion")查ThumbnailJob，而Completion实际是属性。故原日志四项RanToCompletion不等于所有缩略图完成。本诊断故意不修等待策略，避免通过额外等待掩盖原峰值；观察到根时要沿实际closure/task定位，没观察到则保留此未知。

## 下次公司机只执行一次

原 `63a` 的 task 没有 `iccSha256`。公司从本研究分支工作树根执行：

```powershell
python tools/Rgb48HolderBoundary20261123/observe_company.py --company-63a
```

见[公司交接与回传要求](../../docs/validation/rgb48-holder-boundary-20261123/COMPANY_ICC_HANDOFF_20261126.md)及[原预检停止记录](../../docs/validation/rgb48-holder-boundary-20261123/COMPANY_PREFLIGHT_20261126.md)。有原字段必须核对指定 ICC 全摘要；缺字段的例外只认原 LAUNCH 固定字节、原 App/Core、授权缓存、同次首导 TIFF 的固定全摘要、原完成侧车和内嵌 ICC。新任务使用经核对的 ICC 副本；这一绑定不能证明旧宿主实际读取过该文件。脚本没有跳过 ICC 检查或替换固定摘要的选项。

`--company-63a` 解析原 `VALIDATION.json` 和已部署采集器，选择固定的全新输出目录。若原证据按公司回执迁移，显式入口为 `--baseline-launch`、`--output`，非标准工具路径加 `--dump-tool`；任何缺字段的 LAUNCH 仍须逐字节匹配同一固定摘要。`--product-app` 只接受同一二进制。原首导侧车的 `OutputPath` 必须仍指向所核对文件，不能改侧车来迁移。一次执行只构建隔离诊断宿主，不重建产品。双 2 GiB / 50 ms 守卫及 600 s 上限、`points` 配方和原等待均不变。

家庭仅运行小夹具：`python -m unittest discover -s tools/Rgb48HolderBoundary20261123 -p test_company_preflight.py -v`。保留 11 月 25 日交接和全部历史 STOP，不再执行旧的缺字段即停命令。

## 判别规则

- 旧处理bitmap弱槽有效、对象存在，gcroot有非观察器的具体强链：支持“捕获时刻仍有可定位强持有者”，沿该字段/任务/事件定位。若根来自新增诊断强引用，结果无效，不能归因产品。
- 对象存在而gcroot为0，且弱句柄确为WeakShort：反驳“该捕获时刻它仍由托管强根保活”。若owned数组也0根、旧editor字段确为空，则支持这组对象已不可达/尚在堆与提交里的解释；**不证明全部残余Private都是WIC/碎片，不证明公司无暂停瞬间无短暂引用，更不证明无泄漏**。
- 弱槽清零：该角色目标已不在该有效弱槽中，本工具未在捕获前显式清空/Free；不能用别的同型数组替代它。
- GC计数变化、对象无法映射、SOS没有完整根结果或采集被守卫终止：保留为受扰动/不充分观测，不自动重试，不增预算，不改稳定入口。

家庭已完成一次新观测：同f319产品src树、不同本地构建与合成40MP（不是公司0806），六个仍存在的主对象0根，首reader弱槽0；不当作公司通过。详见docs/validation/rgb48-holder-boundary-20261123。
