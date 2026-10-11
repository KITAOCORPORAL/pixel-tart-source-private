# 输出格式验证

## 实际能力与生产接线

快速导出与Publishing均从PV4冻结definition/输入合同及版本ID出发。快速直接用master逐行渲染；Publishing先用同definition生成显示渲染sRGB TIFF16中间文件，再按配方做发布尺寸/编码等输出，不调用历史调整栈。中间文件及完成receipt记录RAW定义/输入与fingerprint；Publishing最终receipt继承，而不是仅写PV4名称。

| 路径 | 格式与位深 | 色彩/朝向/质量 | 回读pixel定义 |
|---|---|---|---|
| 快速TIFF | RGB16、原始完整尺寸、None压缩 | 内嵌既有sRGB ICC；保source orientation tag | 逐行pixel hash回读；专项对同full oracle整数样本exact |
| 快速PNG | RGB16、朝向后原尺寸 | Windows in-box WIC PNG RGB48 WritePixels，内嵌sRGB ICC，orientation已落到raster | PNG16每行整数对同definition oracle，max0 |
| 快速JPEG | RGB8、朝向后原尺寸 | Windows WIC JPEG95，明确444（SOF采样字节0x11实际回读校验），sRGB ICC；无EXIF个人metadata | MAE<=5、RMSE<=15（8bit sRGB），max只诊断，不要求exact |
| Publishing original TIFF | RGB16、None、原尺寸 | sRGB / requested DPI；保source orientation | 原始/输出stream pixel SHA一致与ICC/尺寸/朝向一致 |
| Publishing original PNG | RGB8（显式实际能力） | WIC RGB24行写、sRGB ICC、朝向raster、requested DPI | 与TIFF16降量化到RGB8的oracle exact；不是PNG16 |
| Publishing original JPEG | RGB8，配方quality | WIC444，sRGB ICC、朝向raster、requested DPI | 同中间sRGB图8bit oracle，MAE5/RMSE15预算 |
| Publishing resize/watermark | 既有配方/WPF路径 | 真实尺寸/位深/ICC记录；PV4禁止其他ICC | 既有尺寸/DPI回归；新PV4 pixel verifier明确metadata-only，像素视觉/容差PARTIAL |

Publishing PNG16明确拒绝（不能忽略OutputBitDepth再标成功）；快速PNG16可用。PV4输出ICC除sRGB明确拒绝，不只在receipt伪记Adobe/其他空间。原尺寸TIFF16只重写布局/DPI与ICC，像素无重新渲染/8bit退化；缩放TIFF16继续既有float路径，不能保证本批所有空间输出低内存。

## JPEG失败与修复

最初WPF JpegBitmapEncoder默认色度抽样：Sony尚可，Canon高饱和细节MAE6.488/RMSE9.214、max107，超预算而失败；signed通道bias接近0，排除明显整体通道翻转。没有放宽容差。改用Windows原生WIC IPropertyBag2设ImageQuality=.95 / JpegYCrCbSubsampling=3（官方444），保持同RGB8输入：

| 真实相机 | 快速JPEG MAE | RMSE | Max |
|---|---:|---:|---:|
| Sony A7M4 | 1.27414 | 1.69676 | 24 |
| Canon EOS5D III | 2.94654 | 3.90569 | 27 |

PNG16/TIFF16 lossless max0；Publishing PNG8 exact0；PublishingJPEG MAE约1.277 / 2.945，细节见PRODUCT_RESULTS.json。JPEG容差只是已声明数值门，不代表普遍视觉批准；极端图案/新相机若超限会失败并保留来源，不伪装成功。

系统WIC组件是现有Windows提供，不引入第三方codec分发；COM对象/行buffer使用版本化独立适配器，GUID/接口槽/VT_R4、VT_UI1由Microsoft官方SDK头核对（ENCODER_API_AUDIT.json）。仅Windows x64验证，架构/codec拒绝pixel格式会明确失败，无静默降位深。原先批次对JPEG95输出没有黄金压缩bytes；本批新增EncoderConfiguration与压缩444标识，full显示算法未变，不更改PV4 tone版本。

## 原子性、取消、恢复

所有快速新格式先独立随机临时文件，编码/回读通过后move到既有受管理temp，由managed artifact/pending/move/receipt最后完成。已有目的文件拒绝不删除；编码/验证取消清本次临时产物，资源finally释放。Synthetic cancel测试三格式、gate回收、旧文件保全PASS。

真实OS进程PV4 TIFF Encoding/Encoded/Pending/Moved/Receipt分别Exit73：Encoding因没有可信任务记录返回Missing且保留原临时文件；其他四段重启恢复Completed、重复恢复Completed，沿第五批既有策略不扩大清扫范围。流式hash验证不把不可信文件标成功。后续最终完整Core/WPF回归包含清单失败/冲突/恢复边界。

## 明确差异

preview1600采用display-linear area平均，不再nearest，所以不能将proxy/full笼统说误差0。full或native同坐标才可exact；PNG16与TIFF16不同container及朝向写法不要求文件hash相同，只比较规范化pixel。JPEG有损、PNG8量化与PNG16不同，不混淆。真实桌面DPI/显示器ICC和用户视觉批准NOT_RUN。
