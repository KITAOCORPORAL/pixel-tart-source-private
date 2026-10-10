# 色彩准确性测试结果

## 已执行

`LinearRgbColorScienceTests` 验证：

- 各空间中性轴保持中性；
- D50/D60/D65 白点适配往返稳定；
- 线性 sRGB、ProPhoto 和 ACEScg 转换往返误差受控；
- 转换不意外裁切负值和超范围浮点值；
- 非有限输入和非法定义被拒绝；
- 既有调整栈中性参数和处理器回归继续通过。

聚焦测试：43 通过，0 失败。

## 未执行

- 授权 RAW/色卡/灰阶/肤色实拍基准；
- 相机矩阵或 DCP/ICC 对照；
- sRGB、Adobe RGB、ProPhoto 实际导出文件校验；
- 真实显示器 ICC；
- 缩略图、预览和全分辨率导出的像素容差；
- 新宽色域管线的高光和色域映射。

结论：颜色变换基础 TEST=PASS；专业 RAW 色彩准确性 TEST=NOT_RUN，不能声称达到 Lightroom 或 Capture One 级别。
