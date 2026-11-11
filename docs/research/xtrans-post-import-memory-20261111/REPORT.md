# X-Trans 导入后主应用内存诊断与修复

日期：2026-11-11。任务 XTRANS-POSTIMPORT-MEMORY-NORAW-20261111，新增 RAW 解码 **0**，新增原生库编译 **0**。

状态：**MEMORY_FIX_VERIFIED_IN_CACHE_REPLAY / REAL_BATCH_PREPARED_UNAUTHORIZED**。定位并修复了一个可复现的主要分配路径；原 desktop-02 最后越限仍未唯一归因，不能据此宣称真实整应用资源验收通过。

## 源码与现场边界

- 起点：820b4f0da7f8b741f0547dbfa72d49c0ae3a1105。
- 独立分支：diagnostic/xtrans-post-import-memory-20261111；原产品回归分支仍留在 820b4f0d。
- 最小内存修复：a42cb7cd9633ad065f632172ee137614af151324（四个生产文件＋行为回归测试）。
- 后继任务准入版本化：22a43a339c87297bafe62302901d074188671695。
- 最终修复／待授权构建：b86d02455be77e4ed0f44ddd5b9816681979746e，补齐可见照片页自身的整图请求归属。
- 原 22 项失败现场文件及旧账本逐一核验，原 STOP SHA-256 仍为 D1AA3961C6114248E8B4D1926C3D55362620A2B28D956065C76D0F7E26AE8DC9。旧批次消费仍为1、未消费5、可执行0。未清除 STOP、改写计划、重放原 RAW 或复用旧额度。

独立任务使用现有缓存的只读副本 frozen-cache-copy.rgb48，7752×5184 RGB16，241,118,208字节，SHA-256 0806D680ACC74FACD093DD2280E1C1A82CD18E40D0C27823D8C6C87CC7E3FC7E。重放注入专用缓存读取器；伪 .raf 只含本任务创建的说明文字，用于进入既有 ViewModel 后处理。没有相机 RAW 解码，记录的内存快照均未加载 raw_r.dll。

## 现有时间线

以下时间是原 50 ms 采样器自身的 elapsed，不由文件时间推导。UTC 产品事件与单调采样时钟未原子同步，只作近似对齐。

| 区间（秒） | PrivateBytes 变化 | 解释权限 |
|---|---:|---|
| 1157.215–1157.266 | +465.17 MiB | 导入附近最大的单次提交增长；新重放确认 RGB16→float master 本身约459.90 MiB，但不能用新栈代替旧栈 |
| 1314.116–1314.167 | 1305.28→1319.22 MiB | 导入后一次较早的小增长 |
| 1355.717–1355.768 | 1320.49→1588.04 MiB，+267.55 MiB | 末段主要增长开始 |
| 1355.870–1355.921 | 1588.13→1704.34 MiB，+116.21 MiB | 末段第二次增长 |
| 1356.176–1356.227 | 1706.10→1937.51 MiB，+231.41 MiB | 末段第三次增长 |
| 1356.584–1356.635 | 1913.20→2066.87 MiB，+153.67 MiB | 最后跃升，超过2048 MiB，原守卫停止 |

EXISTING_TIMELINE.json 保留完整候选区间、相邻样本和原产品事件。原进程没有托管/原生分配栈，不能只凭这些涨幅等同于某个 RGB16/RGB24/BGRA32 数组或 GPU 分配。

## 可复现调用者与持有者

实际同步调用栈为：SetSourceAsync → ResetDetailPreview（发布 SourcePixelSize）→ 仍订阅事件的折叠旧工作区 EditorOnPropertyChanged → RequestImageDetail → RequestPreviewZoom → SetPreviewZoomAsync。

MainWindow 同时构造旧工作区和照片页，由可见性切换；折叠的旧页仍收到编辑器通知。它没有有效尺寸，缩放值仍可为默认值1，因此在照片页没有显式请求整图时，触发全尺寸 RAW 显示。主重放直接使用这两个真实视图与同一 ViewModel，旧页 Collapsed，照片页可见。

运行时所有者快照记录 Editor._source 与 _fullRawDisplay 从1600代理转为7752×5184的 CachedBitmap；可见分析还会引用该图。目标 RawMaster 与编辑器冻结输入共用同一 float master，没有发现第二个持久 float master。

使用 dotnet-trace **10.0.750501** 的 gc-verbose，TraceEvent **3.1.23** 解析，配对数据 missingStacks=0。采样取得：

- CacheReplayReader 的 U16[]：241,118,232字节（含对象头）。
- HighBitDepthImageBuffer.ToFloat 的 float[]：482,236,440字节。
- SetPreviewZoomAsync → ToVisualRgb24 的 byte[]：120,559,128字节。
- SetPreviewZoomAsync → RawDisplayBitmapAdapter.ToBitmap → ToArray 的第二份 byte[]：120,559,128字节。

这些是 GCAllocationTick 采样事件，不是全部分配清单。同步调用栈补足了跨 await 后的大数组分配栈中缺失的原始视图调用者。完整栈与分类见 ALLOCATION_EVIDENCE.json、CASE_SNAPSHOTS.json、SOURCE_CANDIDATES.json。

## 最小修复

1. RequestImageDetail 仅允许可见、已加载、尺寸非零的视口请求整图，阻止隐藏旧页错误升级代理。
2. RGB24适配器使用完整、零偏移数组的现有 backing，省去托管整帧 ToArray；切片仍按偏移复制。WIC在创建时拥有像素副本，源数组改写和非零偏移切片测试均通过。
3. 可见照片页根据自身视口状态请求原尺寸，避免移除隐藏页依赖后使其100%操作退化为代理；同样要求可见、已加载和有效尺寸。
4. 编辑器关闭时清除 _fullRawDisplay；工作区关闭时清除分析图像与派生像素/颜色引用，避免关闭后的模型继续持有大图。

未修改解码、颜色公式、处理版本、项目 recipe、候选身份或缓存键。新任务授权逻辑独立提交，不混入内存修复提交。显示位图继续只供显示与分析，未被用于专业导出输入。

## 配对复测

主要短测的重放宿主 DLL 完全相同，SHA-256 09C4FC122AB8993B95DCD1391EA7349610ECD9DF6073969E05FE6CE6D1F6C081。最终短测、初版修复的210秒配对、MainWindow补充对照各自均约束为：宿主及依赖完全相同，只替换旧/新 KitaoPhotoSelector.dll。最终源码另有一次独立210秒确认，不将不同宿主修订混称为同一配对。所有重放均有独立2 GiB守卫、50 ms外部采样和唯一输出目录。

| 指标 | 修复前 | 修复后 |
|---|---:|---:|
| 无显式整图请求的短测 PrivateBytes 峰值 | 1698.36 MiB | 1086.26 MiB |
| 相同场景空闲 PrivateBytes | 1691.10 MiB | 1074.43 MiB |
| 初版修复的同宿主210秒配对峰值 | 1669.50 MiB | 1094.45 MiB |
| 最终源码另行完成210秒与恢复 | — | 1097.27 MiB |
| 原图显示宽度（没有显式放大请求） | 7752 | 1600 |
| 显式整图场景峰值 | 1462.92 MiB | 1471.50 MiB |

短测峰值减少 **612.11 MiB / 36.04%**。210秒观察没有出现同量级延迟增长，恢复后也未触发守卫。中间“仅数组/释放、未含可见性准入”的对照仍触发整图请求，峰值约1683 MiB；它不计为完整修复，表明主要改善来自阻止隐藏请求。

显式整图仍生成7752×5184 RGB24。两端逐行读回覆盖全部120,559,104字节，SHA-256均为 C81DB4E20BAF5E8A6A55CBDD9C844740B717EE5655DC87C7634D39A3CC02D14E。这里是全帧哈希和布局一致性，不冒称两份 TIFF 的逐像素比较。缓存 key、source hash、options hash、候选身份及原 payload hash 保持一致。

补充的实际 MainWindow 类重放使用替代壳层上下文，其两端均停留在代理尺寸，峰值差异较小。它没有复现原整应用越限，不能拿来替代完整应用/用户入口验收。原整应用的服务、窗口交互和诊断开关组合仍须在新批次验证。

## 托管、原生与图形观测边界

快照包括 GC.GetTotalMemory(false)、累计分配、最后一次 GC 的 heap/committed/fragmented、分代收集数、句柄/GDI/USER对象数、VirtualQuery提交分类及位图持有者。自然空闲与诊断强制GC阶段分列；没有向生产代码加入强制GC，也不拿强制GC后的数据冒充自然持续占用。

VirtualQuery 的 MEM_PRIVATE 包含 GC 与原生分配，不是原生堆归因；GDI计数也不是显存字节。对各自最小化进程的 GPU Process Memory 单点采样，dedicated约50 MiB、shared约0.8 MiB，total committed从约235.875 MiB降至77.145 MiB。不同计数器不可与 PrivateBytes简单相加，也不是所有阶段的显存峰值。

关闭及诊断GC后仍有可观的进程私有提交和WPF/图形残余，未取得其完整原生堆分配栈。本轮没有证明原153.67 MiB最后跃升的唯一来源，也没有证明整个应用无原生/图形缓存问题。当前结论是已修复有运行时栈与配对证据的分配路径。

## 测试与新批次

- 5项新内存/所有权行为测试通过：隐藏页不请求整图，活动旧页与可见照片页分别拥有100%请求；适配器不额外复制整帧；切片像素正确；调度器静止后，仍可达的已关闭编辑器不再持有整图。
- 既有 StudioRawDetailPreviewTests 2项、RawMatchTiff16ProductWpfTests 3项通过；未用这些测试替代真实暗室验收。
- 后继任务准入7项通过。Version1旧任务兼容；新任务必须Version2且显式授权；未授权 worker 在输入和预约字段均缺席时，先因授权拒绝。实际拒绝前峰值约30 MiB，新增RAW为0。
- 合计 **17 passed / 0 failed / 0 skipped**（分进程、分套件记录）。早期释放断言在异步回调未退栈时失败的日志保留；最终测试明确等待Dispatcher静止再观测可回收性。

已构建新诊断 app 与托管 worker，沿用原候选 native SHA 157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F 与默认 F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9。新任务 RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111，源码冻结b86d0245，315个文件/绑定项已固定。

当前 AUTHORIZATION=PENDING、AuthorizedForHeavyDecode=false、authorizedHeavyDecodes=0；启动器已实际验证在创建进程、ledger或step目录前拒绝。拟议6次重解码仍为Fuji A/B各冷开与重建共4次、Sony默认对照2次，缓存重开/保存/导出不增加解码。旧批次剩余5次不转移。NEW_BATCH_DRAFT.json 是元数据回执，真正的计划和二进制只留本机独立 DRAFT02 草案目录；较早22a草案已标记替代，仍未授权。

取得新的明确授权后，才更新授权回执和三份计划的授权状态、重封hash及计划SHA，再按 NEW_BATCH_RUNBOOK.md执行。当前尚未执行新 RAW、真实保存重开或 TIFF 导出；候选不接入正式默认版本。
