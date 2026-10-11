# 内存与导出实测

平台：Windows10.0.26200、i7-14700KF、64GiB、.NET10.0.401、LibRaw0.21.1x64。全部Release命令probe、相同Sony DSC04831授权副本、EV0/相机WB、7028×4688/RGB16。并行测试负载、GC和文件缓存会影响耗时，不作为桌面响应批准。

## 重现与定位

第六批原发布Core重新执行旧raw-perf：峰值2156822528字节（约2.009GiB），decode1016ms、full2075ms、旧write+validate724ms。独立逐阶段旧Coreprobe重现：encode临时数组/像素副本增加，整文件readback后峰值2102181888字节（约1.958GiB）。前后比较使用后者独立场景，不能混用不同峰值承诺固定降幅。

| 阶段 | 原完整缓冲旧Core | 新逐行路径 |
|---|---:|---:|
| Decode+master | 1052ms，返回WS447057920 | 1006ms，返回WS449064960 |
| Full场景render | 2117ms；WS844443648 | 不创建full float图；按行在encoder回调计算 |
| 编码 | 636ms；WS1240178688 | render+encode2457ms；WS451252224 |
| 验证 | 276ms；WS2101633024 | 122ms；WS451334144 |
| Hash | 406ms；WS2102181888 | 288ms；WS517566464（此前已做预览/细节，不能当纯导出驻留） |
| 进程累计峰值 | 2102181888（1.958GiB） | 976109568（0.909GiB），由decode阶段主导 |

累计峰值本场景降约53.6%；这是实测，不是通用保底降幅。输出SHA256前后均 **2F9F37A699E7B37335AF0F1DC220BD9227BA35A321B2F2A93F05A4487EDB109A**，完整文件等同，位深/尺寸没有降级。旧流程约4081ms（1052+2117+636+276-不含hash），新约3586ms（1006+2457+122+1-不含hash）；环境波动使速度结论仅baseline。

## 产品端内存限制

两相机快速导出/回读/Publishing连续运行的WPF testhost累计峰值1870680064字节（约1.742GiB），高于隔离TIFF0.91GiB；OnLoad codec读回与Publishing多Bitmap缓冲仍是剩余开销。格式输出不会降尺寸/位深掩盖。这是含多个测试/处理阶段的累计值，不是单格式各自峰值。

## 新decoder阶段

MEMORY_AFTER_SONY.json：Unpack165ms/约102MiB；WB/去马赛克/矩阵543ms；nativeRGB16 61ms；floatmaster142ms，仍同时含native RGB16与float，短峰值约923.5MiB；context关闭后WS约428MiB。此decode复制尚未流式，未把约0.91GiB峰值说成全管线几十MiB。

Canon5796×3870：decode阶段总1284ms；render+encode1704ms；validation84ms；峰值683253760（约0.636GiB）。原比较Sony，Canon单列不是跨内容像素比较。

## 代码变更

- TIFF不增加第三方编码器：保持自有baseline uncompressed RGB16布局，一条strip逐行顺序写，每行width×6 bytes。BuildEntries不再分配假RGB整图，删除full ushort/full byte副本。
- AtomicTiffWriter用有界目录/ICC/逐行完整读取验证；边写边生成像素SHA256，回读stream像素SHA256比对；源/成片hash继续SHA256.HashDataAsync(Stream)。受管理artifact→pending→move→receipt流程保留。
- PV4全尺寸输出不创建fullfloat显示图。JPEG/PNG也走系统WIC WritePixels逐行；JPEG95明确444，PNG16；读回验证仍使用WPF OnLoad解码，实际codec可能保完整图，这是PNG/JPEG和Publishing的剩余内存边界，不冒充完全stream decode。
- PV4本工作区仅活动master；非活动与切旧版本释放引用，512MiB/master超限明确失败不降低图像；native/编码共享HeavyWork并发1。strip、COM对象、stream及取消临时文件及时释放；CLR回收时机不保证立刻归还OS。
- 原像素细节cache64MiB/最多3条、单块4Mi像素/最大宽4096，切图/定义/取消/释放清空。fit代理1600、快速800；4线程区域积分，不保full输出。

产品端真实各格式耗时/进程累计内存见PRODUCT_RESULTS.json，不用隔离TIFF峰值冒充所有编码器/整应用峰值。全尺寸Publishing缩放/水印仍有WPF完整缓冲，PARTIAL；original TIFF特殊路径逐行转写。内存目标依据复现制定：移除导出额外整图副本、维持RGB16与原尺寸/像素；这部分PASS。普遍低内存、多工作区合计预算及OS释放仍PARTIAL。

## 实际发布组件复测

PACKAGED_MEMORY_SONY.json使用发布目录Core/Sdcb/native DLL逐文件同SHA：decode各阶段170/560/61/142/104ms（合计1037ms）；render行CPU聚合2192ms，render+encode2430ms（其差238ms包含I/O与边写hash/调用开销，不伪称纯encoderCPU）；stream验证121ms、hash311ms；peak975904768字节（0.909GiB），encode后WS451145728字节。旧完整TIFF SHA再次一致。PNG/JPEG/Publishing最终发布专项数字另见PACKAGED_PRODUCT_RESULTS.json，不与隔离TIFF峰值混用。

发布WPF组件真实产品流水累计峰值1873846272字节（约1.745GiB），不是单格式隔离峰值。SonyQuick JPEG5498ms/TIFF3400ms/PNG8207ms、native含debounce165ms；Canon3984/2288/5683ms、native169ms。这一Release组件速度与Debug产品数据分别保存，不能无条件将差异归因一个优化。
