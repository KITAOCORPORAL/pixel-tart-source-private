# PlanningOwnedReferenceV1 开源对照与范围合同

## 节点/位置
只在策划自有参考显示/缩略图/只读15区汇总路径加入PNG/JPEG解码准入与EXIF方向归一化，不改Color Studio/RAW/PV4/PV5/PV6配方数学。旧文档自有PNG reader尺寸/格式规则不变。新缓存目录PlanningOwnedReferenceV1，不改原图片字节。

输入：PNG或8位灰度/RGB baseline/progressive JPEG编码字节；1..64MiB，最长边16384/4000万像素，单帧；PNG alpha沿用WIC，JPEG无alpha。WIC原有解码像素域，无新ICC/WB/色彩校正；方向1..8从有界EXIF IFD0读，旋转/镜像只做整数像素置换（不重采样）。输出BitmapSource，方向5..8宽高交换，96DPI；预览PNG缓存和DOCX图像都用同一方向结果。不是通用专业JPEG色彩/ICC精度合同。

参考1 Pillow11.3.0 commit89f1f4626a2aaf5f3d5ca6437f41def2998fbe09，src/PIL/ImageOps.py::exif_transpose / Image.Transpose，测试命令generate_fixtures.py；Pillow HPND许可，研究运行组件隔离在本地，不引入产品/不复制实现。若分发Pillow须保留许可，本包不分发。

参考2 ImageMagick7.1.2-34 commita0c4bf729a26a5c2feb28b0365b47a8db736078a，MagickCore/transform.c::AutoOrientImage / TransposeImage/TransverseImage，magick -auto-orient -depth8；ImageMagick许可/NOTICE已在本地取得，研究运行不分发。自写离散方向映射，未复制参考代码或引入依赖。

复现：generate_fixtures.py创建80x48四象限RGB、quality95/subsampling0、EXIF1..8，每份输入SHA见tests/fixtures/planning-owned-jpeg/FIXTURES.json。Pillow11.3.0与ImageMagick固定二进制生成独立期望PNG。PlanningOwnedJpegTests.EightExifOrientationsMatchPinnedPillowAndImageMagickWithoutSecondRotation逐像素RGB24比较，16组maxAbs8=0、meanAbs8=0（首轮原始ORIENTATION_COMPARISON.json）；最终发布组件复跑另封存。不将该合成几何对照扩大成真实RAW/ICC/相机高精度证明。

异常输入：头尺寸先于WIC限制；EXIF endian/偏移/重复/类型/范围校验；CMYK/12bit/多帧/非PNG-JPEG明确不支持。真实授权RAW/高动态/负浮点/色差不适用本8位策划展示节点，NOT_RUN，CalibrationEligible=false。旧PV数学源码无改动。

决策：限定PlanningOwnedReferenceV1呈现行为接线，仅几何/安全准入对照已验证。未宣称通用ICC/RAW科学节点或替换WIC算法。发布只用原有Windows WIC，研究工具/许可证/原始代码不进产品二进制。
