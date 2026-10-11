# 预览与真实100%

## 采样与兼容

全尺寸 `DisplayTransform=LuminanceShoulderNeutralGamutV1` / PV4定义完全不变，full RGB16结果和历史SHA一致。新增仅预览质量算法 `DisplayLinearAreaV1`：对每个源像素执行同一full曝光/显示变换→sRGB编码，取float32参照结果，再逆sRGB OETF到**显示线性sRGB**，按输出像素真实面积footprint积分，最后OETF。不是sensor恢复；这样避免先平均scene色值再执行非线性肩部而改变与full参考的关系。

不在gamma编码RGB直接平均；精确box footprint是有抗混叠的区域低通，不是高阶Lanczos。硬边处均值、细线亮度、超饱和边缘会与旧nearest显著不同，不能再宣称proxy/full同坐标误差0。缩小预览的oracle是full显示线性区域平均；native区域才是full同源像素精确一致。旧Render(maximumEdge)仅保历史/快速nearest兼容，不作为高质量或100%。

## 产品100%入口

现有100%按钮现在按DPI设置逻辑Zoom=1/DpiScale，标签以物理像素比例显示。Editor.SourcePixelSize使用真实、按EXIF方向后的原尺寸，不再让1600代理定义全图像素尺寸。viewport/pan/zoom变化把可见原像素Rect传给Workspace：超过代理可用分辨率时明确提示请求原像素。

140ms停顿后读取master真实1:1像素并渲染原尺寸detail patch，Region经过EXIF1–8映射，detail覆盖同一图像空间Rect。拖动时代理作为占位，停止后发布原像素块；没有放大1600后标100%成功。单块最多4Mi像素/宽4096，超大屏/低缩放导致可见区大于预算时仅块内为原像素，状态明确块范围与其余代理，不宣称全屏已细节化。未来多块补齐仍PARTIAL。

## revision/cache/释放

缓存键包括sourceSHA256、DecodeKey、EV、编辑版本ID、zoom、dpi、quality Native100V1、原像素Rect；显示变换当前是Validate限定固定V1。最多3条且64MiB。照片/版本/参数变化cancel并revision递增、清detail/cache；结果发布前核对target对象/definition/version/revision/token/disposed。旧任务拒绝覆盖。

fit代理每次使用RenderPreview(maxEdge1600)，EV/WB更新先800 nearest快速代理，再发布HQ。HQ调色1.7–2.2s（Sony本机），800约旧比例低成本；不伪称无延迟连续交互。快速拖动的细节任务防抖，期间明确代理占位；桌面响应/DPI人眼未运行。

## 测试与性能

合成棋盘格16→4与full每4×4像素在线性光平均oracle1e-6容差一致；一像素细线不消失；EXIF1–8真实坐标映射；RGB色彩/负/高光/旧黄金回归。真人肤色与高饱和视觉不是自动数学PASS。

Sony7028×4688 HQ1600 1724/1995/2028ms，512×512 native region16/16/16ms；产品请求含140ms防抖约202–207ms。CanonHQ1305–1633ms，native14–18ms。记录阶段WS，累计峰值仍由解码约0.91GiB主导。切图、迟到请求、连续曝光更新的命令隔离测试PASS；实际拖动停下、不同DPI、用户视觉批准NOT_RUN。

发布组件复测：SonyHQ1689/1910/2011ms、native512块16/15/14ms；产品原像素请求含140ms防抖Sony165ms/Canon169ms。详见PACKAGED_MEMORY_SONY与PACKAGED_PRODUCT_RESULTS。
