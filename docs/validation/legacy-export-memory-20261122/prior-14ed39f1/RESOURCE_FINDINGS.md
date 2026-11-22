# 零RAW父应用资源定位：已定位的堵点与未能证明的部分

本轮不加载/调用LibRaw native解码器，不打开原RAF，不创建重型账本。宿主通过IRawDecoder替身只读既存0806D680…RGB48，调用e818冻结生产VM、真实照片页/隐藏旧页、Core预览/保存/导出。依赖以已发布托管文件装配，raw_r.dll未复制；因两轮守卫终止，末尾模块清单未完成，不补造“全程模块普查”。这是受控回放，绝不是普通EXE或新candidate接纳。

## 旧820b4f0现场实际说明什么

完整读完26,626个约50ms样本、7条路径事件、worker result/resources、STOP，并核对22项失败现场的长度/hash不变。worker30028 exit0，CacheStored，DarkroomImport completed；父在导入结束后约199秒才触发PrivateBytes2,167,267,328（2066.87MiB）。时钟起点非原子同步，因此199秒仅是粗略前后关系，不当内部阶段独占计时。

worker峰值Private约858.50MiB，父子重叠合计采样峰值1265.55MiB；这两个峰值不保证同一时刻，不能相减算父峰值。worker已退出后，原生image4/scratch/候选独立RGB3不应假设仍常驻父。原cache/输出文件在磁盘不等于额外常驻RAM。旧最后约2秒从1320MiB进入1704/2066MiB，但无分配栈/堆根/图形事件；不能把原触发硬归为导出、截图或某个泄漏。旧现场没有导出路径证据。

## 这轮新增的可复现实测

|范围|结果|峰值Private|准确断点|
|---|---|---:|---|
|e818实际VM+照片页，冻结payload读取/流式cache写/100%请求/保存后导出|2GiB guard STOP|2430.46MiB|保存完成后，进入原生产导出调用，尚无完成输出。phase字段是上一完成的session-saved，不误说保存本身分配2GiB。|
|新进程打开上一轮已保存文件，仅代理预览，实际TIFF导出，再同进程释放后重开|导出成功，后续guard STOP|2073.79MiB|导出完成时1841.33MiB；Dispose后自然空闲仍约1842MiB，再次cache-read-2时越限。|

独立重开恢复1个目标、原配方和共享master；实际TIFF7752×5184/RGB16/方向1，全部241118208字节像素hash与既存RGB48相同，ICC3144字节身份一致。输出的SourceHash属于宿主合成marker，不冒称新RAW身份/真实解码。零RAW读取次数不是新解码次数。

两轮不强制GC、每目标进程2GiB/600秒、50ms样本；STOP及未完成状态均保留，不放宽守卫。没有用“移除100%页面后导出成功”反向升级第一轮通过，也没有自动重试同一次失败；第二轮回答的是独立重开/代理导出/同进程重开未覆盖路径。

## 堵在哪里：对象和分配必须分开数

7752×5184：一份RGB48为241118208bytes（229.95MiB），一份floatRGB为482236416bytes（459.90MiB），一份RGB24最小像素为120559104bytes（114.97MiB）。

- Cache payload替身读取确实仅一份ushort[]+1MiB块，流式写没有另一整幅byte[]；这不是candidate cache协议/路由已经产品准入。随后HighBitDepthImageBuffer.FromRaw把U16转成完整float；输入U16和新float可在转换时重叠，异步/GC回收时机不等于最后调用返回。
- 快照中target.RawMaster和Editor._frozenRawMaster是**同一id**，不是两份459.90MiB。1600代理float约19.59MiB，800交互代理约4.90MiB。
- 100%时Editor._source与_fullRawDisplay是**同一CachedBitmap**，不双计；还有约115MiB托管RGB24临时数据和WIC拷贝/图形资源。主图1600旧Bitmap仍可能由TargetImage/交互TransformedBitmap持有。直接请求.2而页面仍有自身视口事件，不能仅据此说普通页面缩小失效。
- **关键剩余路径：legacy RAW导出调用RawMatchTiff16ProductPipeline.Render→ColorStudioRenderPipeline.Render。即使中性Preset，先source.Clone，再ApplyHighPrecision内GraphPixelAllocation.Copy，各一份459.90MiB；同时原冻结master仍活着。返回值还构造完整RGB24显示adapter约114.97MiB，尽管导出只用ProcessingPixels。** 第一轮导出前约1498MiB，随后两个近完整float级别提交峰跃迁并越限，与该精确分配链相符。
- GraphPixelAllocation无活跃Scope时回退new float[]；这条legacy入口没有自动Enter/完整工作集准入。RawMaster也是legacy持有路径，不等于已注册预算的LinearRawMaster。不能把常量MasterLimit或外部guard当作这些所有临时分配已被生产池覆盖。
- TIFF writer从float逐像素写流；本轮未发现它再申请整幅RGB48。主要压力在writer之前的全帧渲染/显示副本与现有预览/GC提交重叠，不是因文件名叫TIFF就等于已修的rendered-raster导出通路。
- Dispose清引用不保证OS立即decommit。第二轮1537MiB managed估计含未收集垃圾，不是堆根普查；“Private不降”单独不能证明泄漏。同进程再分配的容量峰值确已失败，因此不能依赖强制GC让门禁通过。

## 最新修复覆盖到哪里

a24→e818改了RawDisplayBitmapAdapter精确数组复用（少一次约115MiB托管复制）、隐藏旧viewport的detail晋升、_fullRawDisplay释放、栅格冻结源/读头、session prepared转移、sRGB通知和隐藏film预览排队。它们有原证据，保留，不重写为新色彩算法。

但a24→e818的legacy RawMatchPipeline/ColorStudioRenderPipeline/float转换/AtomicTiffWriter链零差；e818→433同样未改这些链及DetailPreview。433的canvas关闭drain与legacy暗室全帧export不是一个scope。当前快照sourceCacheCount0、matchedImage为空、master共享，已经排除“当前宿主重复的栅格源cache”作为这次两个fullfloat分配的解释。两轮实测足以说明既有修复**没有覆盖所有legacy RAW后处理峰值**；不能说明它们无效，也不能给旧postimportSTOP唯一分配归因。

## 最小后续建议（未实施、需另审）

1. 先限定legacy RAW中性/点节点导出：给只消费高精度输出的调用避免生成完整显示adapter，明确只读borrow/owned输出合同；中性节点可考虑无副本直通，但不能直接return master后让using output.Dispose误释放编辑/撤销仍持有的master。
2. 对全帧临时float明确范围准入与生命周期，尽量复用有限工作缓冲/逐行或已验证tile路径；不能用“加GraphScope”就假定OS提交会降低，池保留自身也占预算。边界/空间节点需要独立像素一致门禁，不扩大为色彩数学重构。
3. 同进程关闭/重开保留具体in-flight任务/共享持有者证据，等完再释放本scope；不要清全局cache或forceGC。先补分配/所有者标记到这个已复现legacy链，旧STOP本身继续UNKNOWN具体触发。
4. 修复被批准后才用这两条零RAW路径复验，再另决定candidate适配与新cold/rebuild预算。两轮失败不解除旧任何STOP或恢复数字余额。
