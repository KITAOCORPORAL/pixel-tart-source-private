# Pixel Tart 人工 Native 验收

全部真实输入由你操作。观察器只记录，不会点击、拖动、修改参数或替你判定视觉通过。
测试图和初始节点由既有 acceptance fixture 预置；它们不是你的操作证据。

## 顺序

1. 吸管：Fit 中央、边缘；Zoom 后；Zoom+Pan 后；增加取样、减少取样；图像外/留白；Escape。
2. 节点：第一到最后、最后到第一、中间向前、中间向后；Undo、Redo。每次等预览处理完成再继续。
3. 3D：展开“3D 色彩空间”，亲自点击“生成当前模型”；左键 Orbit、Shift+左键 Pan、滚轮放大/缩小、重置、适合；改窗口大小；连续操作。
4. 页面：Workbench、Asset Library、Tether、Planning、Online Selection、Color Studio、Publishing、Settings。
   查看文字裁切/重叠、按钮遮挡、Header、Inspector、图片、Popup、滚动、留白、对比度和中文。
   精确 1180×720／1600×920／1920×1080 尚不具备用户点击尺寸入口；观察器记录实际尺寸，不会伪称精确矩阵完成。

无需抄坐标或颜色。完成后直接在对话里回复，例如：

```
Eyedropper：通过
Node Drag：通过
3D：通过
Workbench：通过
Asset Library：有问题，具体位置……
Publishing：有问题，具体位置……
```

每个 gate 最终要结合你的操作确认和 observer evidence；全部初始为 PENDING。
观察器最多运行四小时或直到关闭本次产品进程；不会控制其他应用。可在本目录创建
`STOP_OBSERVER.txt` 仅停止记录，不会关闭产品。没有截图、没有自动视觉批准。
