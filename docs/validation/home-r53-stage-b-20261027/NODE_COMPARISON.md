# 家庭 SceneExposure 交互与 GeometryV2 节点对照
日期：2026-10-27 北京时间（UTC+08:00）。仅诊断版；未批准发行或相机/相纸标定。

## SceneExposureInteractiveV1
- 处理位置：既有 LibRaw 0.21 linear ProPhoto D65 master；black/camera WB/matrix 已应用，基础EV后、既有PV4肩部/中性色域压缩/sRGB OETF前，对显式源坐标蒙版应用 SceneExposureV1。旧精细/导出数学不变。
- 快速数据：至多输出800px的2倍场景采样网格（长边1600），保存场景EV之前的RGB。每次EV先使用同一基础EV/场景EV定义，再逐sample映射后在display-linear sRGB面积平均；不是先平均线性场景再做非线性色调映射。蒙版冻结不变，无第二次camera WB。
- 输出：有界编码sRGB float预览；负值和>1在scene gain保持，直到旧Display裁切/映射；与全分辨率精细不逐像素相等。预设预算mean1.5/P954/max32，8位尺度，未提高。
- 参考1 darktable 5.2.1 `be9171cf8bb2feab8bd115efd9b96a4ce7103039`，src/iop/exposure.c:exposure2white/process，black0时scale=2^EV。
- 参考2 RawTherapee 5.12 `a8a3d1bc7b419ed1c974c39276c97e618a4f9a1b`，rtengine/dcp.cc:step2ApplyTile，无tone/look分支RGB*=exp_scale。本地重新读固定源码，GPL3仅行为研究，未复制/翻译源码或分发其二进制。既有56组实际编译标量对照见公司已提交SCENE_REFERENCE.json，家庭仅重放相同fixture，不冒充家庭完整编译两个RAW引擎。
- 高频反例：29周期RGB在2x采样mean9.643/P9523.478/max31.059，原始失败保留 `SCENE_HIGH_FREQUENCY_REJECTED_2X.json`；不能宣称满足预算。新增对源局部variation的独立风险探测，密集变化及GeometryV2回退精细；反例回退后误差0。启发式不是全部图像误差的数学保证；未覆盖复杂极细蒙版/所有真实相机，准入仍DIAGNOSTIC_ONLY。回退不能保证90ms。
- 真实输入：家庭已有 `N:/pixart/RAW/Real/canon/TNAN8886.CR3` SHA256 `b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401`，不扫描其他私有照片；同机旧路径/新路径同source/EV/800px对照。温度/全部ICC/全相机不是本轮已完成。
- 合成中性/肤色样/饱和/渐变/负值与HDR用于数学/误差，不宣称色准改善。真实WB测试验证DecodeKey重解码及撤销，未测肤色色差改善。
- PV4/5/6缺省SceneExposure=null，不暗迁。V1 gain只用于显式启用；导出一直原full region/row路径。

## GeometryV2：普通栅格/RAW拉直
- 输入：RAW既有线性ProPhoto D65或WIC一次解释ICC的rendered sRGB/D65 float。几何V2在既有Display输出之后做精确double双线性插值；非线性sRGB重采样，不声称linear-light锐度或色准优势；无alpha恢复，现有rendered入口透明按白底解读保持。
- 源坐标：正向未裁源归一化；quarter-turn/flip/crop/±15°拉直，非方形按源aspect旋转，越界黑色；蒙版坐标同geometry映射。输出size由crop/quarter-turn，拉直不自动扩大画布。旧V1的nearest/crop全部保留，默认角度0；旧配方不改结果。
- 两个成熟实现实际执行：OpenCV4.10.0 `67f3511f2354ebfb5f98045ee6153345f632aadd` modules/imgproc/src/imgwarp.cpp remapBilinear/INTER_LINEAR（Apache2）；Pillow12.3.0 `bb1d8e8ab8d29048624d96e3ee53cecf7c13d13d` src/libImaging/Geometry.c bilinear_filter32F（HPND，文件许可证见本地清单）。第三参考ImageMagick7.1.1-38 `b0ab92265bab638e6ecd2f18b45977c38771c671` MagickCore/pixel.c InterpolatePixelChannel/Bilinear，ImageMagick License仅读，不当成已执行第三引擎。
- OpenCV reference wheel SHA已按PyPI digest验证，仅 `N:/pixart/Guangjing-R53-Home-20261027/reference-runtime`；Pillow是现有环境。未引入产品依赖，不随包，不复制源码。
- 实际产品输出与两引擎同RGB float像素中心地图对照：OpenCV使用1/32系数表，与产品exact double不逐位一致；mean0.03228/P950.1113/max0.23599（8位）。Pillow interior max0.0001564。常量边界与pixel center明确；当前样本无越界差，未把它当全边界验证。
- 负值/>1、sRGB/16位/RAW、geometry/preview/reopen/PNG16/TIFF16读回在测试；JPEG是有损容限，未用lossless去要求JPEG逐位。真实RAW带拉直场景曝光TIFF16逐样本检查0差异；完整不同ICC矩阵/广色域仍OPEN。
- 新rendered LocalEdits Version2仅显式新变体，源文件不写。普通PNG/JPEG/TIFF支持入口，当前参考匹配/空间detail栈拒绝转换，不暗烘焙或丢层；不是声称任意历史配方已支持全部裁剪。

## 基础RAW白平衡后续能力
复用已批准CameraGains/LibRaw链与既有解码版本，不新增Kelvin算法。UI独立“RAW基础白平衡”数值R/G/B，应用触发重新解码；相机WB、black、matrix只执行一次。显示后局部WhiteBalance仍既有encoded-sRGB相对冷暖/tint，明确不等同相机WB；旧节点/默认像素不变。保存、切变体、撤销基础WB DecodeKey已专项验证。不是新相机profile批准，也不冒充肤色色差改善。

## 生命周期
快速缓存最多2项：own + borrow lease + generation，Clear退休，不释放正在借用的buffer。最后借用释放Dispose及账本。HQ缓存原来返回共享buffer，修复为缓存own，调用方own副本并记录Work预算，防止Dispose污染变体cache；clear/epoch阻止迟到回填。重复8轮开关/撤销/几何/关闭账本0，managed约6.5–6.9MB趋稳；进程working set有运行时波动，不作为硬上限。含20MP整组测试peak约2.6GB，主要含native decode、两master、测试比对副本，不隐瞒。

## 准入
DIAGNOSTIC_ONLY / CalibrationEligible=false。复杂mask、跨相机、ICC、原生鼠标/DPI、所有历史配方/全模块视觉没有变成PASS；无新增代码许可证传播义务（独立实现，不分发研究源/二进制），现有依赖保留 notices。

## PhotoCraft 追加批：实际采用与严格边界
固定参考 `storytold/photocraft@7ac7b8b106acccf6826d45ed5c9e481dfe341035`（0.16.0，MIT OR Apache2）。全部指定10个文件已实际读取，含profile原字节与content_hash/same_bytes、transform精确float、stage pipeline、GPU matrix-shaper近黑曲线、color模型、psblend、RAW D50矩阵/Bradford、display文档到monitor架构。LICENSE-MIT/APACHE、NOTICE/ATTRIBUTION已读；品牌/字体/图标/合成CMYK不引入。仅独立实现公共API封装/架构，未复制Rust源码、未增加Rust/CMS替代引擎。CMS内置CMYK明确synthetic，不是印刷标定；README列的mpet/BPC/v2absolute限制不当成已支持。
已看真实公开问题#9831（低缩放Threshold灰阶，open：说明预览插值会改变表观，不能将低缩放当原像素）、#1112（Lab thread-local恢复，closed：context状态不能泄露），未追main无限升级。

产品采用：新`DocumentColorStateV1`包含输入/工作/输出完整ICC字节、SHA256、输入策略、引擎版本；显示ICC属于session Layout不属于照片配方。独立颜色变体旧默认null不暗迁；AssignExplicitProfile只重解释原码值再转换，Embedded按内容解释并转换，无标记需确认UntaggedExplicitSrgb，损坏/CMYK/LUT拒绝。

转换引擎复用随包`lcms2.dll`2.12，commit`924a020d09bfe468c665467caf24aadeb41ff77c`对应API，实际SHA87bf94f27f345384420383f247178f825c0101928fedd3a4887ff60e9d4a76eb（2120），MIT版权文本已有随包。旧ControlledIccLinearV1研究哈希/门禁完全不动；新封装只明确RGB matrix/shaper/XYZ v2/v4子集，非通用ICC专业引擎批准。cmsOpenProfileFromMem/cmsCreateTransform RGB_FLOAT intent1 flags0x140、NoOptimize/NoCache BPCoff；按行CPU float，profile上下文和transform生命周期借用已测试。

公开原色兼容profile由LCMS生成（日期固定，内容可重复SHA），不复制Adobe专有profile：sRGB/P3使用D65+sRGB TRC，AdobeRGB-compatible gamma563/256，ROMM/ProPhoto-compatible D50 toe1/16/gamma1.8。**工作编辑仍为encoded sRGB**以适配既有tools，非ProPhoto编辑；P3/Adobe/ProPhoto是实际输入/输出转换能力，不是扩大工作色域的宣称。RAW的现有ProPhoto D65不改名、不套D50矩阵；RAW不接受此rendered ICC指派。

已跑：带P3 TIFF16→RGB_FLOAT input→既有edit→四space TIFF16/PNG16，8份产物ICC byte identity与独立encoded readback全部0码差；新变体VM导出、session重开、输出profile撤销、workspace显示ICC重开均有测试。灰阶/近黑/饱和/肤色色块/负值/>1输入保留矩阵样本；授权Canon同hash派生512px真实RGB图同闭环0码差，不冒充sensor色准。透明输入V1延用white flatten，半透明测试明确，alpha独立保存未完成。

第二成熟独立引擎Windows WIC：RGB48 ColorConvertedBitmap拒绝的失败保留，改8bit独立对照；in-gamut max2.5487/255（含输入8bit量化），out-of-gamut max6/255不同，不报完全一致。Pillow12.3/LCMS2.19是不同release同算法，16对profile/固定样本比较另记，不冒充独立算法。已有RawTherapee5.12 iccstore.cc同样明确intent/profile，不将完整RAW管线当小矩阵等价。一般LUT、ICC多意图/全部输入、广色域编辑准入仍OPEN。

JPEG实际宽色域读回mean5.4156/RMSE7.8625/max49，超出既有MAE5预算：**阻断ColorV1 JPEG输出**，不放宽；默认兼容JPEG不变。ColorV1现只放行TIFF16/PNG16，出版水印/重采样对该新成片主动拒绝，防止宽色域像素被误贴sRGB；即时胶片CurrentEdits尚拒绝ColorState，原图/兼容变体继续可用，不能静默丢ICC状态。

显示产品：复用WindowsMonitorProfileProbe GetICMProfileW，窗口LocationChanged/Activated及3s查询刷新，手动ICC/回退状态可选，CPU精确sRGB→设备RGB，只进入untagged WPF位图，不改原bitmap/edit/export。4张/96MiB缓存上限，账本记录并在关闭/换profile失效；细节尚未转换不混入未转换tile。WPF离屏未发现第二次应用我们的ICC，但系统ACM/HDR/物理多屏未验证；因此用户**显式SDR开关默认off**、注明系统再转换风险。不能据此声称正确覆盖所有Windows显示器。没有统一33³低精度LUT/GPU替代。

混合V2：既有V1三模式逐位保留。V2调节层作用是`before + opacity*mask*(blend(before,effect)-before)`；encoded sRGB，alpha属于caller不再乘一次，不是PhotoCraft Photoshop pixel-layer sourceAlpha或scene RAW。变暗/变亮独立Pillow+OpenCV对照最大0.25/255（输入量化），已进入诊断菜单。Overlay原型Pillow max1/255；SoftLight Pillow max22.96/255，darktable实际为另一公式且opacity²；颜色/明度darktable HSL与W3C .3/.59/.11定义不同，均记录不等价。**Overlay/SoftLight/Color/Luminosity暂不在菜单放行**，原型仅数值/回归，不能宣称全部已批准模式。未添加Fill。

准入复核：菜单及setter现在都只放行Darken/Lighten；Overlay/SoftLight/Color/Luminosity不能从普通控件启用，数值函数/冻结测试保留为研究原型。ColorV1工作域sRGB并非完整宽色域编辑。首个闭环实际用户操作入口为“颜色管理·ICC变体”，支持旧WIC无法预览的带profile输入恢复到显式新变体；不偷偷替换兼容导入逻辑。
