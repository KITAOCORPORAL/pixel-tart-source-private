# 预览与导出比较

共同入口：ProfessionalRawPipeline.Render(master,definition,maximumEdge)。曝光、空间转换、肩部、色域压缩和sRGB编码顺序完全相同。预览1600边nearest中心采样；全尺寸逐像素，朝向处理后呈现。缩小采样不是全图像素一一对应；不比较不同内容照片。

## 可检查容差

| 比较 | 容差/规则 | 实测 |
|---|---|---|
| full与preview相同中心采样坐标的float sRGB | exact，因无空间算子 | 6 RAW previewError=0 |
| TIFF16回读与round(float*65535) | 0 integer LSB | 6 RAW readBackError=0 |
| PV4 WPF preview和同definition Core oracle（朝向一致） | RGB8 exact | 2真实相机PASS |
| 保存重开/重新解码 | 相同decoder/config/file exact | 6 RAW重复解码true；2相机产品重开RGB8 exact |
| RGB8 preview与RGB16输出 | 分别量化；理论约0.5/255 + 0.5/65535（归一值） | 定义解释，不冒充所有桌面呈现逐像素测量 |
| JPEG | 有损压缩，不能要求exact；编码设置由既有StudioQuickExport定义 | 本批RAW→JPEG视觉/误差 NOT_RUN |

真实full文件测试使用Core writer；产品测试另走Workspace→ExportSelectedCommand→AtomicTiffWriter→受管理receipt，确认TIFF16、非空sRGB ICC、朝向、定义及PV4、源hash。两类证据各自记录，不混同。

TIFF16保存的是**显示渲染sRGB**，不是线性ProPhoto交换文件；内嵌现有sRGB profile，不提供显示器ICC/软打样。TIFF保留EXIF orientation，PNG/JPEG使用既有朝向规范化。桌面OS/WPF的插值、缩放、DPI、显示器配置可能额外改变屏幕观感，未做人眼批准。

PV1/PV2固定黄金hex仍保持；PV3实验数学未改。RAW new path没有借用ToSrgbOutput硬裁切基准。
