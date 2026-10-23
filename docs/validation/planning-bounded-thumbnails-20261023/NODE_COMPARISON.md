# PlanningBoundedV2 呈现版本合同

继承PlanningOwnedReferenceV1的PNG/JPEG准入、EXIF整数变换和固定参考。规划展示不是ColorStudio/RAW/ICC节点；PV4/PV5/PV6和既有文档PNG往返零改动。

输入同一受管副本文件的独占写删拒绝读lease，流式SHA256校验后同句柄seek，不分配整份编码字节；上限64MiB/40MP/16384不变。列表512px，汇总2048px，放大4096px上限，原尺寸更小不放大；先指定WIC DecodePixelWidth/Height再EndInit，不先拷贝全尺寸像素。EXIF只作用缩小后有界像素。全进程一个解码门禁限制并发，取消在hash/头解析前后/方向行循环/写缓存前后生效。WIC本地解码调用进行中不能强抢取消，但不会发布取消结果。

缓存PlanningBoundedV2使用真实PNG和.png扩展名，cache receipt SHA校验及预期尺寸检查；key含项目/文档/引用、当前root+relative、源hash、尺寸版本。命中仍先校验同一源副本，并仅解码512px缓存。缓存不作为原来源替代；副本变化缺失则不读旧缓存。

独立比较沿用固定Pillow11.3.0/ImageMagick7.1.2-34（下附原引文），本批新增24MP降采样图的独立方向/尺寸/块均值误差对照；不同采样核不要求逐像素零差，报告实数，不宣称等价色彩/ICC。

# PlanningOwnedReferenceV1 开源对照与范围合同

## 节点/位置
只在策划自有参考显示/缩略图/只读15区汇总路径加入PNG/JPEG解码准入与EXIF方向归一化，不改Color Studio/RAW/PV4/PV5/PV6配方数学。旧文档自有PNG reader尺寸/格式规则不变。新缓存目录PlanningOwnedReferenceV1，不改原图片字节。

输入：PNG或8位灰度/RGB baseline/progressive JPEG编码字节；1..64MiB，最长边16384/4000万像素，单帧；PNG alpha沿用WIC，JPEG无alpha。WIC原有解码像素域，无新ICC/WB/色彩校正；方向1..8从有界EXIF IFD0读，旋转/镜像只做整数像素置换（不重采样）。输出BitmapSource，方向5..8宽高交换，96DPI；预览PNG缓存和DOCX图像都用同一方向结果。不是通用专业JPEG色彩/ICC精度合同。

参考1 Pillow11.3.0 commit89f1f4626a2aaf5f3d5ca6437f41def2998fbe09，src/PIL/ImageOps.py::exif_transpose / Image.Transpose，测试命令generate_fixtures.py；Pillow HPND许可，研究运行组件隔离在本地，不引入产品/不复制实现。若分发Pillow须保留许可，本包不分发。

参考2 ImageMagick7.1.2-34 commita0c4bf729a26a5c2feb28b0365b47a8db736078a，MagickCore/transform.c::AutoOrientImage / TransposeImage/TransverseImage，magick -auto-orient -depth8；ImageMagick许可/NOTICE已在本地取得，研究运行不分发。自写离散方向映射，未复制参考代码或引入依赖。

复现：generate_fixtures.py创建80x48四象限RGB、quality95/subsampling0、EXIF1..8，每份输入SHA见tests/fixtures/planning-owned-jpeg/FIXTURES.json。Pillow11.3.0与ImageMagick固定二进制生成独立期望PNG。PlanningOwnedJpegTests.EightExifOrientationsMatchPinnedPillowAndImageMagickWithoutSecondRotation逐像素RGB24比较，16组maxAbs8=0、meanAbs8=0（首轮原始ORIENTATION_COMPARISON.json）；最终发布组件复跑另封存。不将该合成几何对照扩大成真实RAW/ICC/相机高精度证明。

异常输入：头尺寸先于WIC限制；EXIF endian/偏移/重复/类型/范围校验；CMYK/12bit/多帧/非PNG-JPEG明确不支持。真实授权RAW/高动态/负浮点/色差不适用本8位策划展示节点，NOT_RUN，CalibrationEligible=false。旧PV数学源码无改动。

决策：限定PlanningOwnedReferenceV1呈现行为接线，仅几何/安全准入对照已验证。未宣称通用ICC/RAW科学节点或替换WIC算法。发布只用原有Windows WIC，研究工具/许可证/原始代码不进产品二进制。
