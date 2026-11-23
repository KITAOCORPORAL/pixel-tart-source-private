# 旧 RGB48 路径：按发生顺序的持有关系

源码以实际候选 f3190f44 为准，记录来自63a8633d；家庭新观测另标，不混同旧11a转储或ac1交接。对象id只在同一进程同次日志内去重，不当稳定地址。

| 阶段 | 对象或缓冲区 | 持有者 | 预计释放点 | 已有证据 |
|---|---|---|---|---|
| 01 旧缓存/冻结载荷读取开始 | 第一轮reader ushort[]，241118208B；此处还不是master | 63a受控Replay.DecodeAsync的Task.Run闭包与局部values，返回RawDecodedImage.Rgb48Pixels | DecodeMasterCore收到并创建独占master后，RawDecodedImage/读取局部退出；实际由GC回收，不由文件Dispose释放数组 | 公司events before-ushort-1→ushort-read-complete-1；宿主Program.cs:150–159；这是合法旧缓存payload重放，非普通legacy UI/发行decoder磁盘cache命中。 |
| 02 规范方向与源图交接 | 第二份独占RGB48 master数组241118208B；非整帧float | RawImageOrientation.OwnUprightRgb48复制/方向置换→HighBitDepthImageBuffer.ownedRgb48；FrozenRawMaster.Image；target.RawMaster | 保留期间按借用使用；target/Editor/缩略图等全部释放后才可达性归零。Dispose清数组字段不等于强制回收 | RawMatchTiff16ProductPipeline.cs:18–26,54–64；RawImageOrientation.cs:33+；公司master id8630212，StorageBytes241118208、floatMaterialized=false。 |
| 03 1600/800代理及目标图 | 1600×1070 float约19.59MiB、800×535 float约4.90MiB，小CachedBitmap/TransformedBitmap | target.Image/TargetImage；Editor._rawPreviewMaster/_interactiveRawMaster、_source/_interactiveSource；缩略图target或任务局部 | SetSource替换、ResetDetailPreview/Editor.Dispose、workspace TargetImage/Thumbnail置空和任务退出；不能仅按字段赋null断言GC | Workspace ActivateTargetAsync:710–726；Tether SetSourceAsync:764–775；公司代理ids35878178/63763137，target小图45083904。 |
| 04 100%原始预览建立 | 整幅RGB24暂存byte[]120559104B→WIC CachedBitmap（最少像素负载同值） | ToVisualRgb24局部数组→BitmapSource.Create拷贝；同一个bitmap id55012273在Editor._source/_fullRawDisplay和viewport.Original同时出现 | 暂存byte[]在adapter返回后无须长期保留；bitmap到SetSource/Reset/Dispose/视口解绑且无其他持有者后可回收。WIC原生资源可能延后 | Tether.DetailPreview.cs:19–40；RawDisplayBitmapAdapter.cs:11–20；公司四处相同id是别名，不算四张整幅图。 |
| 05 处理后100%预览发布 | 处理RGB24暂存byte[]120559104B→新CachedBitmap id44070913 | RenderDisplay条带输出临时RGB24；Editor._matchedImage、viewport.Matched；cacheKey准入时_previewCache.Result；分析缓存/任务依各自路径可能持有 | render结果局部结束；缓存逐出/Editor.Dispose.Clear；viewport及分析引用清除后不可达；任务完成不自动清Task对象的所有字段 | Tether.RenderAsync:1094–1125；HighPrecisionExportRows.ReadRow/FillDisplayRow；公司id记录确认直接字段，未dump确认缓存/分析/闭包实际强链。 |
| 06 首轮保存/导出 | master仍RGB48，不新增整幅float；16行float scratch与逐行RGB48 | 导出scope rows借用master，GraphPixelAllocation与行writer拥有临时条带/行 | using rows/scratch/导出finally结束；借用者不可替别的持有者Dispose共享master | 公司saved/export-finished仍id8630212；公司首轮TIFF全行正确；本轮不重做已有像素实验。 |
| 07 页面解绑、workspace关闭、任务等待 | 旧Page/Window/VM、Editor图及master | 宿主先解绑隐藏ReferenceColorWorkspaceView和可见PhotoGradePageView的DataContext；Window.Content=null后closing局部仍临时持旧VM；Dispose清目标RawMaster、分析、sourceCache、Editor缓存/源/处理图；已识别Task快照待完成 | Release async真正返回后closing/tasks局部可结束；await known Tasks与Dispatcher idle不保证全部未枚举任务 | 宿主Release:102–106；Workspace.Dispose:768–790；Editor.Dispose:1236。注意KnownTasks查Completion字段但产品是属性，漏缩略图任务。 |
| 08 新VM/批次准备阶段 | 新外层VM、暂存staged VM；读取工作JSON后准备恢复目标 | LoadSessionAsync用using staged+_skipSessionPreparation=true先准备；外层新VM字段仍null，实际新解码在staged/新async链里 | prepared成功后AdoptPreparedSource转交并清staged Targets/sourceCache；失败恢复旧状态。新VM日志null不证明所有旧或staged对象均无根 | Session.cs:75–113,121–207；公司第二次读取前holders看到的是新外层VM，不能把这些null单独当旧VM字段已清证据；本次转储才直接dump旧Editor字段。 |
| 09 第二个约230MiB数组分配前 | 公司旧弱对象7个alive；家庭reader弱槽0，旧master/两bitmap/VM/editor在堆中 | 公司实际强根未知；家庭六个标记对象gcroot=0，旧master内部仍指向owned数组但自身无根，故内部边不是外部活根 | 没有强根时等待自然GC/原生生命周期；不能销毁共享master/forceGC/延时等内存下降来制造通过 | 公司before-ushort-2 Private1932525568B=1843MiB，+229.948MiB=2072.948MiB容量算术；家庭捕获六个0根，旧Editor四主字段null，非公司结论。 |
| 10 第二次数组实际申请 | 新reader ushort[]241118208B（对象带头241118232B） | 受控Replay Task局部values；公司实际新分配在ReadExactly之前 | 读取/解码后与阶段01–02相同；本诊断故意在此之前结束，不作容量测试 | 公司STOP2176217088B（2075.402MiB），无ushort-read-complete-2；本次观察没有分配该数组，不称修复或PASS。 |

## 普通TIFF prepared路径（不同路径，单独列）

| 阶段 | 对象或缓冲区 | 持有者 | 预计释放点 | 已有证据 |
|---|---|---|---|---|
| T1 读取TIFF | WIC 16bit CachedBitmap及来源身份 | LoadSourcePreviewAsync→_sourceCache；当前target/Editor源图 | 缓存逐出、workspace.Dispose或成功批次转移后旧source缓存退出；保持失败回滚所需对象 | Workspace.cs:750–765；StudioQuickExport.LoadPreviewSource，CachedBitmap OnLoad；不经Replay new ushort旧缓存入口 |
| T2 新项目准备 | staged目标/Editor/来源cache，加旧targets、oldEditorSource、oldImage等回滚快照 | LoadSessionAsync的using staged与LoadSessionCoreAsync局部；旧工作区故意保留到新准备验证完成 | 成功AdoptPreparedSource/Targets和cache转交后清staged，旧局部随async退出；失败恢复旧状态 | Session.cs:89–113,131–133,154–195,197+；若现有工作区不先Dispose，准备期间允许旧新数据并存 |
| T3 渲染/分析交汇 | TIFF BitmapSource通过ColorStudioBitmapRenderer，可能有Rgb48/Rgba64/float整帧或受限曝光条带；RGB24显示、直方图/分析、视口 | 同一个ReferenceColorWorkspaceViewModel/Editor/缓存/Viewport，最终有共同WPF原生压力与GC提交 | 所有源/处理图、cache、分析及真实任务生命周期结束；不能用RGB48路径根证据套TIFF | ColorStudioBitmapRenderer.cs:13–37；Session.AdoptPreparedSource及共享Editor.Dispose/分析；公司普通TIFF首轮正确后重开STOP是独立失败 |

因此“旧RGB48项目第二次数组申请前”的断点，不是普通TIFF的prepared raster读取断点。两者交汇在工作区/Editor拥有与转交、BitmapSource显示/缓存/分析、WPF/native提交；源数组形式、读取入口和事务保留时机不同。
