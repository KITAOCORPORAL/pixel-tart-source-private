# 自由画布首闭环与普通画笔
继续CanvasDocument/CanvasObject/CanvasEditor/CanvasDocumentStore/FreeCanvasView，不引入Fabric/Konva引擎。固定PixelWidth/Height独立屏幕Zoom/Pan。已有选择/移动/等比缩放/旋转/层级/锁/undo复用，新增Alt自由缩放、尺寸/导入/图层/导出实际入口。

导入PNG/JPEG/TIFF持有自有副本，沿PlanningProjectCopyStorage的安全路径/源句柄/SHA/相对路径验证合同，CanvasPlanningSource绑定；不是把canvas塞进.ptplan v1。失效清旧预览，导出失败不假成功；恢复必须同SHA，原图不覆盖。

源按嵌ICC转换sRGB、独立straight BGRA；显示WPF premultiply，输出float premultiplied bilinear和source-over，最终一次unpremultiply，PNG8含alpha和实际sRGB ICC，尺寸为文档值；不读monitor变换、不经过照片白底。画布当前encoded-sRGB合成，不称线性光色科学合成。图像64MP/16384、画布64MP显式拒绝，逐层解码非无限缓存。

普通画笔独立CanvasObject.BrushStrokes Version1，颜色/文档像素大小/不透明度/硬度，源归一化折线。一次笔划最短距离max覆盖（回折不重复加alpha），不同笔source-over，线性羽化由硬度定义；每笔20k点/层2k笔或100k点限制。预览/输出复用DrawBrush栅格，1笔1历史，保存重开透明导出一致。纹理笔刷未开始。

实机鼠标/DPI禁用，testhost/离屏/OS启动分开；手势/锁/缩放坐标自动测试不当真实指针验收。
