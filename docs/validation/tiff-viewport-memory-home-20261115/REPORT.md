# TIFF 视口与导出内存复测回执

2026-11-15（Europe/Berlin）。受控 WPF 窗口流程通过；公司原图不可用的边界保留。

产品补丁：`edd6d25e`、`e28074f1`。最终产品源码 `e28074f18ea6d76d7715451bce89d6c80c3cc2fd`，基线 `bdc913c9`；基线 src/tests 与统一源码 `939d540c` 逐字节一致。诊断工具和回执单独提交。

## 恢复与旧证据

原树 `N:\pixart\guangjing-product-functional-home-20261115` 及未提交 HomeFunctionalClosure 工具保留，本轮在 `N:\pixart\guangjing-tiff-viewport-memory-20261115` 独立分支继续。R3 四组 STOP/EXIT 与汇总中的 PID、退出码、守卫原因、峰值、RAW 限额逐项核对一致，私有内存分别为 2051.59、2165.74、2218.28、2052.70 MiB。原非视觉真实 TIFF 两次导出仍为通过，但不据此提升窗口验收。详见 R3_VERIFICATION.json、PRESERVATION.json。

## 分配证据及补丁

完成的 7 秒 EventPipe 轨迹经 TraceEvent 4.0.0 解析，37 条大分配样本、缺失栈 0。`StudioQuickExport.LoadCore → ProcessForExportAsync` 分配 byte[241118208]（对象含头 241118232 字节）；`InstantFilmJpegInput.FromRendered → DecodeProfessionalInputAsync` 分配 float[120559104]（对象含头 482236440 字节）。这是采样分配证据，不是完整分配普查，也不定位 GPU 或全部原生堆。早期长轨迹因守卫终止而截断，未当作可解析证据。

1. 渲染输入“当前调整”改为复用按路径、修改时间和 SHA 绑定的冻结原始栅格，避免显示源存活时重复 Load/整帧托管 staging；必要的 float 主缓冲仍按原公式建立。
2. 导出清单改为仅读取已编码文件的尺寸和存储位深，不再完整解码并脱离同一导出文件。编码器、通道舍入、像素公式及 ICC 行为未改。
3. 独立重开初测发现临时工作区只移交编辑器图像，未移交源缓存；`edd6d25e` 首次导出已通过，但重开仍触发守卫。`e28074f1` 移交同一缓存并在事务失败时恢复旧缓存，成功后清空临时工作区引用。失败记录全部保留。

没有修改 ColorStudioBitmapRenderer 或颜色算法；未提高守卫，实测 TIFF 周期未强制 GC。

## 输入和实际输出

合成输入为 7752×5184、RGB48/16 位通道、方向 1，尺寸与公司 Fuji TIFF 相同，沿用既有 sRGB ICC。场景内容、压缩和相机历史不同。家中 Sony 文件 SHA 与 PortraitHighlights-20261111/input-manifest.json 完全匹配，真实存储尺寸 7028×4688、方向 8，规范化尺寸 4688×7028。均为已显影 RGB16，不是传感器线性输入。

两组都执行适合窗口 → 100% → 20% → 100% → 实际 TIFF 导出 → 保存关闭 → 独立进程重开 → 相同缩放 → 再次实际导出。两份输出逐行读取全部存储通道，解码像素含 alpha、归一 RGB、尺寸、RGBA64/每通道16位、方向1和ICC一致；归一RGB还与输入按方向规范后的像素完全一致，原输入 SHA 未变。ICC 为3144字节，SHA `2B3AA1645779A9E634744FAF9B01E9102B0C9B88FD6DECED7934DF86B949AF7E`。

| 输入 | 首次私有峰值 MiB | 重开私有峰值 MiB | 结果 |
|---|---:|---:|---|
| 同尺寸合成40MP | 1229.76 | 1239.48 | 全部检查通过 |
| 已授权Sony TIFF | 1231.16 | 1244.32 | 全部检查通过 |

2 GiB=2048 MiB。沿用 50 ms 进程私有提交及 OS 工作集峰值守卫。阶段表为该阶段采样的私有提交峰值；完整私有/工作集阶段值与全部原始采样见 RECEIPT.json 和 receipts。

| 输入/进程 | 载入/重开 | 适合窗口 | 首次100% | 导出 | 保存 | 自然关闭 |
|---|---:|---:|---:|---:|---:|---:|
| 合成/首次 | 720.15 | 1211.13 | 1224.38 | 1229.76 | 1205.60 | 1225.50 |
| 合成/重开 | 742.62 | 1218.29 | 1234.53 | 1227.40 | — | 1239.48 |
| Sony/首次 | 817.55 | 1225.91 | 1225.28 | 1231.16 | 1199.13 | 1223.80 |
| Sony/重开 | 837.28 | 1234.62 | 1235.32 | 1227.65 | — | 1244.32 |

受控宿主实际装载 PhotoGradePageView，并保留折叠旧视图；记录 IsVisible/IsLoaded、视口尺寸、缩放及内容绑定。窗口在屏幕外，文件对话框固定，不能称为人工桌面/文件选择器验收。自然关闭后私有提交仍高，未声称长期多项目无增长。

## 回归与整合

定向 TIFF/方向/位深/ICC/缓存及移交回滚测试15/15通过。扩大回归32/33通过；SrgbUsesOriginalFileNotCurrentEditsAndUndoRestoresPixels 在未改动 bdc913c9 上单独复跑仍失败，错误逐字相同，保留为原有 sRGB 曝光问题，本补丁不修像素行为。新增 RAW 解码0，高光1bfdfbe9保持未合并研究状态。

产品线可在匹配的939d540c或bdc913c9基础上依次 cherry-pick edd6d25e、e28074f1；也可 git apply --check TIFF_MEMORY_MINIMAL.patch 后应用合并补丁。合并补丁已在未改动基线完成 apply --check，未改变基线工作树。PRODUCT_COMMITS.mbox 保留两个产品提交。诊断回执提交不含实际照片、生成TIFF、运行库或 nettrace 本体；这些本机文件的路径和 SHA 见 LOCAL_ARTIFACTS.json。复测入口见 tools/TiffViewportMemory/README.md。
