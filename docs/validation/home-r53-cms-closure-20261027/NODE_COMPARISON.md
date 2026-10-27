# CMS闭环变更准入记录（实施前）

继承前批 NODE_COMPARISON / PHOTOCRAFT_REFERENCE 的固定版本与许可证，不重复已验场景曝光/缓存/几何研究。

- JPEG：RGB float工作结果 → LCMS相对色度intent1/no-cache/no-opt0x140输出ICC → RGB16量化 → BGR8量化 → Windows WIC JPEG95/444，ICC只描述已经转换的输出码值。解码按未转换编码值比较；另统一到sRGB对照。参考Pillow12.3（PIL BSD-like，固定依赖）/libjpeg-turbo实际运行3.1.4.1@9217719d3a58633923b096af4c1d50d304768a64（先查3.1.3源码af9c1c…，运行版本以Pillow feature读回为准）（IJG/BSD/zlib，研究编码不分发新codec），现有WIC为系统编码。保留旧MAE5/RMSE15门槛，用不同频率样例分离codec本身误差，不把标签当转换。
- 工作空间V2：显式linear ROMM/ProPhoto primaries D50，gamma1，float32；input/working/output三ICC身份+新Version2。仅线性曝光节点（BasicTone exposure其余为0）与几何/Normal有蒙版强度，其他旧encoded工具拒绝；旧V1encoded sRGB/PV4/5/6不迁。参考LCMS2.12@924a020d09bfe468c665467caf24aadeb41ff77c与RawTherapee5.12@a8a3d1bc7b419ed1c974c39276c97e618a4f9a1b iccstore，PhotoCraft7ac7b8b106acccf6826d45ed5c9e481dfe341035 profile/transform/pipeline作架构。MIT既有LCMS随包，GPL只研究不复制。先保持过范围float，输出才量化裁切；预览转换sRGB不进导出。
- 统一结果：profile字节/SHA、float32RGB、独立straight-alpha、原始方向/结果几何/版本ID。背景不明确不允许丢Alpha；V1旧白底仍标为兼容策略，不复用贴纸。出版重采样和水印明确在encoded-sRGB（float重采样，8bitWPF水印蒙版/颜色）域，最后转换输出ICC；不是线性光合成宣称。即时film输入完整冻结文档定义→相同float结果→明确转换sRGB，边框/配方解释sRGB，不读monitor。
- BlendV2：使用既有W3C/PDF .3/.59/.11非分离公式与SoftLight立方toe/sqrt，参考固定PhotoCraft psblend和ImageMagick7.1.2@既有REFERENCE及W3C公开公式，独立实现。不是Pillow不同SoftLight或darktable HSL。调整层effect完整结果再opacity*mask混合，Alpha不再乘一次；旧三模式不变。负/>1在V2 blend输入限制SDR，未受影响原HDR由caller插值保留，不声称scene/HDR混合。

数值/保存重开/输出/失败恢复必须由本批真实增量测试填入最终结果，不能以本文件作为通过证据。显示默认off、物理多屏/ACM/HDR未验保留。

## 本批实测补充（提交前）
- 四频率×四输出profile：不忽略profile的WPF JPEG读回会自动解释颜色，P3 smooth MAE5.398/max50；忽略profile读raw编码后P3 MAE0.473/RMSE0.724/max3。独立Pillow12.3/libjpeg quality95/subsampling0产物与WIC经Pillow读回相同（16份），输入RGB→BGR/RGB16→8正确，JPEG无额外输入ICC转换；ICC bytes相同。灰阶0–.086、饱和色块.160–.361、高频1.047–1.542（MAE8），门槛仍5/15不放宽；PNG/TIFF16全数值0码差。原始失败和其误用读回仍保留jpeg-before。
- 自然图仅已授权Canon的旧512×342派生TIFF，固定hash EEBCE771A08163FB7947B2BE5744410C88B9AD59EA51466A9658477164BDED4B，不冒充全分辨率/相机准确。sRGB/P3/AdobeCompat/ProPhotoCompat JPEG原测试MAE约.745/.691，全部固定预算内；最终发布组件还需重跑本批。
- V2 P3基色→linearROMM D50→P3 maxfloat<.0005。V1 encoded-sRGB转换本身保留负/>1（示例P3绿到sRGB出现-2.90686/1.01827），无工具roundtrip约3e-7；BasicTone/WhiteBalance的EncodeGamut、Levels/Curve采样会压缩/裁切。故V1宽ICC输出不等于宽域工具。V2仅实际线性乘2^EV，Normal opacity*源mask，几何在线性域，未知工具拒绝；显示/film明确另转sRGB，export直接working→target。
- Blend参考补PDFium@a84323421e94f484faca52dd9d027934eba42ab8（BSD，外部隔离TCC编译固定Blend整段body，不分发research binary）；Overlay/SoftLight量化max1.400/1.488码值。PhotoCraft固定float32公式独立numpy port四模式maxfloat1.62e-7，非完整Rust执行。Color/Luminosity的PDFium30/59/11整数ClipColor源码语义核对，非darktable HSL。旧三模式和负/>1 opacity0 exact以及蒙版/重开/导出本批测试通过；Alpha独立不乘双次。非PS HDR全等/像素层全套准入。
- 新成片交互检查为结构/尺寸/位深/ICC字节与完整解码，不伪造像素对照：PixelComparisonPerformed=false。显式Inspect保留全渲染逐像素验证。曝光/几何老版代码未改结果；ProfileV1 DecodeKey字节保留，V2才附工作域身份；旧InstantInput新增字段null不序列化。

## 最终已执行参考与边界
最终发布源码b843670653dcb1899f77b873e31a11455d754f36；tests另补303b562e仅验证，不改src。
- JPEG独立实际runtime：Pillow12.3 libjpeg-turbo3.1.4.1@9217719d3a58633923b096af4c1d50d304768a64；初始查3.1.3不冒称实际运行。product WIC95/444与独立同像素同配置参考raw码完全一致，common sRGB比较已运行，四profile embedding字节相同。新原始失败未覆盖。
- PDFium固定a84323421e94f484faca52dd9d027934eba42ab8的Blend整段及Lum/ClipColor/SetLum原body经类型适配在本地TCC实编译执行（research-only），Overlay/SoftLight/Color/Luminosity max8=1.400/1.488/1.931/3.434，非float等价断言。PhotoCraft固定源码独立numpy float32 port max1.62e-7，未假称Rust完整engine运行。BSD/MIT/Apache研究源码与编译器/EXE均不进产品。
- 线性D50原色profile WIC独立engine原色8bit与LCMSfloat clipped对照max.944码/mean.420码；灰因128/255 vs.5未配对排除，WIC负值裁切明确。Pillow binary不导出cmsCreateTransform的失败保留，不把未执行的LCMS2.19 float对照报通过。线性曝光数学另复用既有两成熟曝光核固定对照，不重写RAW引擎。
- 冻结图像的出版photo float32中心bilinear对2像素输出≤2U16；水印alpha128/255*.5独立合成公式与WPF8覆盖量化≤2/255，原照片不量化到8bit、不压白。出版task、profile UI列表、二次出版均已走真实产品入口测试。
- final main Core68/WPF55 PASS0FAIL0NOT_RUN、新testhost重开1PASS，透明边缘alpha0/64/128/255四mode PNG重开补充1PASS。独立alpha不作为第二个opacity乘子；旧三mode float结果保留。用户完整ExportSelectedCommand含receipt/hash耗时137–169ms，不拿WriteAsync50–55ms冒充总时长；阶段分项保留secondRender0，显式Inspect36–38ms继续可用。
