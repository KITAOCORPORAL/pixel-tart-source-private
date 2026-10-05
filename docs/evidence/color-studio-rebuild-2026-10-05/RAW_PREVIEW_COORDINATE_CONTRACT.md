# RAW 实际尺寸预览与方向统一

2026-10-05，本轮真实源码核查发现的缺陷修复。USER_VISUAL_REVIEW=NOT_APPROVED。

## 根因

旧工作区只给 SourceImage 一张1600长边RAW显示代理。viewport使用该bitmap尺寸配置Zoom；100%只是1600代理的一像素/DIP，源7028像素无法放大查看真实细节。全float冻结主图虽存在，UI没有放大触发完整render入口。

LibRaw以AutoRotate:false解码，主图保EXIF方向；旧RawDisplayBitmapAdapter仅发布RGB，不按方向变换。TIFF带EXIF方向、另一些导出物理旋转，照片/统计/导出坐标由此不统一。

## 实现

- 新Core `RawImageOrientation.NormalizePixels` 精确搬移float通道实现EXIF 2…8（含镜像），不量化、不改变原件；方向变成1。仅Color Studio `DecodeStudioRawAsync`在获取会话FrozenRawMaster时调用一次，所有导入、参考输出及批量分支使用此wrapper。底层RawMatchTiff16ProductPipeline API仍保原合同，其他模块不悄悄改变。
- 新VM `SourcePixelSize` 总是给viewport真实且归正后的源尺寸。显示代理可变，但中央照片布局/平移/取样坐标使用真实尺寸。100%仍明确为一原片像素/DIP；Windows DPI决定实际物理像素缩放。
- 用户zoom×DPI所需样本超过1600代理能力时 `SetPreviewZoomAsync` 实际选择冻结full float主图执行完整栈，同时创建原片full显示bitmap。该过程不会二次解码RAW；100%真正包含源全尺寸样本，不是把低清图放大或仅改倍率文字。
- 连续调参数继续800交互代理，停下后根据当前detail请求返回full稳定结果；revision/取消保latest-wins。Fit可回1600处理。观察叠加与取样仍沿共享viewport映射，源坐标/原图/结果方向一致。
- 预览缓存key增加冻结RAW代际与渲染宽度，保存完整look/stack/film及引用修改时间。普通128MiB/RAW256MiB预算、最多4帧，33MP full+proxy可并存；重复100%、平移与Fit→100%不重复生成相同完整结果。更大源超预算时仍可能淘汰，不称无限缓存。

## 验证

Core `RawImageOrientationTests.AllExifTransformsKeepTrueFloatSamplesAndBecomeIdentity`：2×3唯一float颜色索引核对全部7种EXIF变换、宽高交换、归正后再次调用恒等。旧direct RAW pipeline方向测试保留。

WPF `StudioRawDetailPreviewTests.RawActualSizeUsesFullPixelGeometryAndRenderedSamplesWithReusableCache`：2400×1200高频合成真实float master→1600代理→100%实际2400图像；输出每个RGB24值与full共享Core管线相等；重复100%与Fit后返回100%同一缓存bitmap；切图取消旧结果。它是WPF行为测试，fixture明确不是相机RAW实机照片证据。

第12统一build聚焦40通过/0失败/1跳过包含该测试，以及ColorRange选区、RAW导入/输出和generic EXIF相关回归。随后RAW缓存预算追加使主任务再build；最终全套状态见主交付矩阵。

全分辨率复杂栈需要实际计算时间（本机33MP点工具约19秒、空间滤镜另有测量），不能把100%加载描述为瞬间完成。Release实际100%、平移、取样、方向及输出检查由主任务使用同一个最终EXE验证。

最终Core全量 `core-full-final-upright.trx`：**1572通过/0失败/6跳过**，49s。已包含RAW归正精确测试、空间V2、兼容、像素处理与现有Core。通过不等于Release桌面运行或用户批准。
