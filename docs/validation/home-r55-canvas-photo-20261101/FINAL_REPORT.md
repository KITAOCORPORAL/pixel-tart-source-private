# 大照片画布固定快照 v3 · 家庭诊断交付

2026-11-01 Europe/Helsinki。状态：**PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**。

## 结论与本次续接
原会话在代码、发布和测试之后因请求体过大中断，没有最终报告和push。本次直接保留已有产品提交与294文件ZIP，不重做灯台、旧保存闭环或研究引擎。核对fetch：company/lighttable-lifecycle-20261030仍b1443ed4，home/r55-planning-canvas-20261030仍40919e48；本家庭照片分支此前未发布到远端。旧工作树和dirty资料仅只读检查，研究线已自行推进，未覆盖或整合。

完成：相关代码审阅、发布DLL读回、补充8轮资源寿命回归、独立新进程重开/手动更新、原始TRX和失败记录归档、能力边界定义源与生成物/HANDOFF/大纲更新、独立数值脚本非零失败语义、完整诊断ZIP逐文件复核。**并非所有数值门禁或实机验收完成**。

## 身份（不可混用）
| 类别 | 身份 |
|---|---|
| 上游文档HEAD | 40919e486c5bc41b0b10d000b2e3caeed15901b4 |
| 上游产品 | 69bbf22935f3471013ad6bb3728f95b8b562acfc |
| 当前产品源码 | 146c0fd254de387ad5ccb9570cff6c54697389d4 |
| 首个普通4:2:0支持测试 | 04eb86be1b3963d3a9b0fe2bc4382c4dbdd90a47 |
| 本次测试支持 | 5d94691b34f8762532199e991d7b2b44852b1b47 |
| 主发布DLL SHA256 | e24a07454a72f3ca286bae6cef22040dae97159189849f32ba89c93a2eda3319 |
| AssetLibrary DLL SHA256 | 0ab50ac55bd80b15ea1a062f56f5b41406cb6db37943a028214f2edbd8c16138 |
| Core DLL SHA256 | bb19806ffcab4cefbc6b93299c1bf2b02ba13c0d7d67ef53882b44c68e4fe19e |
| 最终WPF测试DLL SHA256 | 3195cae5a38ce73ce310ab79ee35ede8a3bc6bd55713b06d0790a204cd72c20c |
| ZIP SHA256 | d394e3ab463531f66eb4ae74ef571becb26bec20754965ef07251a35c560cf5d |
| 文档HEAD/远端 | REMOTE_READBACK.json记录被验证文档提交；最终LOCAL_HEAD/REMOTE_HEAD见最终回传及本地FINAL_REMOTE_READBACK.json |

发布后src与146c0fd2无差异。本次编译仅新测试支持：0错误、387警告，未冒称零警告。20个共享发布DLL与WPF宿主、9个与Core宿主、20个与新测试宿主SHA相同。旧测试DLL身份为历史，不替代最终测试身份。

## 实现与分配
- 新RenderVersion=3，SamplingContract=CanvasJpegNative1024.ArtboardCenters.v3；PreviewParameters冻结源阈值、native约束、ICC、EXIF、32MiB/8项任务缓存和v2输出像素中心。
- 旧v2以输出×32＋整源×32预算拒绝普通大照片。新v3从已hash校验且保持读lease的当前原图调用系统WIC IWICBitmapSourceTransform native reduced IDCT；没有GetThumbnail/GetPreview或外部旧缩略图。24MP 6000×4000实际native1500×1000，32.74MP 7008×4672实际native1752×1168，方向6后1168×1752。1024是请求边长，不是假称输出一定1024。
- 不增加256MiB：输出工作估算＋32MiB缓存预留＋native临时像素×32准入；源<=64MP/最长边16384/文件64MiB；native<=3072边/4,194,304像素。大渐进/非baseline JPEG明确拒绝，不能回退整图多份解码。大PNG沿原预算拒绝；RGB48+ICC既有不支持保持。
- 原ICC→sRGB与方向保留，v3按native采样后转色；不宣称与整分辨率先转色再采样或所有ICC引擎逐位相同。encoded-sRGB域原预乘bilinear/source-over不变，没有扩大RAW/16bit/物理色准能力。
- 小图<=4,194,304像素保持原v2路径；旧v1/v2保存PNG字节不重渲染，原导出没有暗改。层序、Hidden、透明背景、Opacity、固定画板裁切、crop/rotate/flip、软画笔、文字、色板由已有合成器处理。
- 每任务最多32MiB/8来源FIFO，key为安全解析实际路径＋内容hash；每次命中仍重开校验来源lease，不依赖mtime。正常/异常/取消Dispose归零，无跨任务陈旧代理。
- 串行STA/20秒任务期限与既有原子落盘/项目和操作代次保护不变。WIC CopyPixels及编码不能强制即时抢占；只在步骤前后/逐行协作取消，不声称实时硬中断。

## 授权照片与真实链路
INPUT_IDENTITIES.json仅提交hash、尺寸、ICC和方向。24MP/33MP来自已授权既有TIFF照片输出的显式重采样JPEG，**不是原生24MP/33MP相机拍摄证明**。透明PNG是摄影衍生加合成alpha。来源照片、派生输入、预览、项目与私照截图只在本机；未扫描无关目录。

已测：导入项目副本→多图摆放/裁切/旋转/翻转/透明度→保存→真实策划捕获入口→原子插入固定PNG→独立进程重开→保存画布不改变旧PNG→手动更新追加第二版。还覆盖源替换/缺失、项目切换、锁定写入失败、取消、FIFO淘汰、隐藏离线层、旧版本PNG保留、全部模块/策划15与教程退役。

## 实测（同机同输入，不与公司1650比较）
| 指标 | 公司来源的上一轮策划基线v2 | 当前发布v3 | 本次续接发布DLL复核 |
|---|---:|---:|---:|
| 固定快照结果 | 0，预算拒绝 | 1成功 | 1成功 |
| 捕获耗时 | 79.3ms拒绝，不是完成时间 | 631.7ms | 657.4ms |
| 测试进程工作集基线 | 132,612,096B | 151,949,312B | 154,701,824B |
| 测试进程工作集峰值 | 134,291,456B | 235,143,168B | 235,175,936B |
| 测试进程私有内存增量 | 17,420,288B | 118,468,608B | 113,901,568B |

完整应用宿主另测：载入后工作集310,259,712B/私有280,993,792B；照片导入后渲染基线324,743,168B/291,127,296B；捕获峰值412,651,520B/416,169,984B；渲染增量87,908,352B（83.84MiB）/125,042,688B（119.25MiB）。**256MiB不是完整应用总内存上限，5ms采样也不能证明未观测到的瞬时分配不存在。**

保留4个PNG版本时三次连续捕获567.6/923.4/1148.8ms，私有内存258.6/276.5/298.9MB；这些上升包含保留版本和CLR/WIC，不隐去。任务cache残留0不等于应用所有内存归零。

新增8轮不保留PNG的寿命诊断：每轮412–425ms、相同PNG hash、1解码/1命中、缓存峰值6,000,000B/残留0、每轮源可FileShare.None独占打开；显式GC后私有内存414,187,520→392,957,952B，没有8轮单调增长证据。GC仅用于测试观察释放，不是准入或让应用通过的手段，也不外推无限期无泄漏。

取消续接实测请求25.4ms、任务退场179.4ms；原发布169.8ms。请求时WIC具体子阶段未知，不宣称原生解码可被强制打断。

## 回归与数值：严格分开
自动测试权威集合去重 **119 PASS / 1 FAIL / 1 NOT_RUN**（TEST_SUMMARY.json列出每方法与收据）：
- 新发布绑定主要WPF60通过；Core54通过、符号链接权限1失败、LCMS参考输入1未运行；两个完整宿主各1通过；3个独立重开方法通过；能力定义/生成JSON/Markdown一致性1通过。重复夹具建立/复跑不加到去重数量。
- 旧符号链接权限失败未删断言/未提权；LCMS缺显式参考矩阵未编造输入。
- 续接首次独立重开缺KITAO_CANVAS_PRODUCT_REOPEN导致1未运行；补配置后重复使用已更新的大图夹具导致Single失败。保留两份原始收据，全新目录建立夹具1通过＋另一进程重开/手动更新1通过；没有放宽断言或改产品躲失败。
- 既有发布Core的PV/RAW/图链黄金相关测试包含在54通过；不是全量所有历史测试通过。

独立像素结果（原预设未更改）：
| 对照 | 结果 | 状态 |
|---|---|---|
| 两照片WIC native vs Pillow draft | max0/mean0 | PASS |
| 两照片vs FFmpeg native lowres2（该两份源） | max18/17，mean.856/.863，P95=3 | 原max24/mean1.5/P95=3内 |
| 普通4:2:0 vs Pillow | max0 | PASS |
| 普通4:2:0 vs FFmpeg | max40，mean1.633569，P95=4 | **超原阈值，OPEN** |
| 原WIC ICC vs Pillow/LCMS（真实目标ICC） | max3；mean1.023493/.659702 | **超1码，OPEN** |
| 冻结代理float64独立合成 | max1/alpha1，64行边界1 | PASS（不是独立解码证明） |
| 小图两个alpha实现 | max1，alpha0 | PASS |
| 旧导出/小图v2与当前 | 原字节/像素保持，测试通过 | PASS |

verify_photo_numerical.py现对所有门禁计入overallPass，已真实返回exit1；不能因为WPF单元测试通过就说开源先行POLICY全过。既有ICC跨引擎差和FFmpeg色度重建差已定位为观察事实，尚未完成完整域/色度定位与第二独立实现全覆盖，因此不批准科学/发行门禁。**CalibrationEligible=false**。

参考固定完整commit、文件SHA和许可见REFERENCE_PINS.json、NODE_COMPARISON.md；Pillow/FFmpeg仅本机外部数值工具，不复制GPL代码到产品，不把其研究程序/源码/照片加入ZIP。系统WIC不是自行实现通用RAW/ICC引擎。

## 界面与实机边界
继续R5.5既有布局，仅本轮属性名称/数值、图层可辨识名称与隐藏/锁控件语义改进；不重画全套UI。6张合成生产宿主OFFSCREEN图可分发并归档，6张私人照片宿主图只留本地。原会话逐张查看，本次复看1366自由画布与1920两个固定版本；不等同实机鼠标、设备DPI、ACM/HDR、多屏或Grok逐像素定稿验收。

实际EXE与隔离CMD已有OS启动/正常退出0/产品源码日志读回。未启用Browser/WebView/ComputerUse/CDP，不自动开浏览器/资源管理器，没有Setup。

## 完整交付与使用
- 目录：N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64
- 启动：上述目录Start-Guangjing-Diagnostic.cmd
- ZIP：N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64.zip
- win-x64自包含、WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false；294文件/77,254,817B，完整应用模块/策划15/教程退役保留。
- CMD每次创建独立UserData-CanvasPhoto/run-随机目录，避免污染默认用户资料；不是持久使用同一默认资料库的启动器。要复用某一诊断数据根，应显式指定相同隔离根，报告中的重开测试使用固定同一存储，不把随机启动器当恢复证明。
- 私照、临时项目、用户数据、FFmpeg/Pillow引擎、Spektra研究二进制不入包。verify_delivery.py真实逐项核验目录SHA/ZIP条目/3组DLL绑定通过，但这只是完整性，不是数值或物理验收。

## 可复现命令（不打开UI浏览器）
在当前源码根，使用已有包含numpy/Pillow的Python环境与FFmpeg：

    python docs/validation/home-r55-canvas-photo-20261101/verify_delivery.py N:/pixart/Guangjing-R55-CanvasPhoto-20261101 --output N:/pixart/Guangjing-R55-CanvasPhoto-20261101/VERIFY-AGAIN.json
    python docs/validation/home-r55-canvas-photo-20261101/verify_photo_numerical.py N:/pixart/Guangjing-R55-CanvasPhoto-20261101/resume-release --inputs N:/pixart/Guangjing-R55-CanvasPhoto-20261101/INPUTS_LOCAL_ONLY.json --output N:/pixart/Guangjing-R55-CanvasPhoto-20261101/numeric-replay --chroma420 N:/pixart/Guangjing-R55-CanvasPhoto-20261101/inputs/photo24-420.jpg --chroma-native N:/pixart/Guangjing-R55-CanvasPhoto-20261101/release420/photo24-420-native-LOCAL-ONLY.png

第二条预期返回1（实际门禁未过），不通过删用例或调阈值强行绿灯；不要把包含私人代理图的输出目录加入Git。

## 后续明确边界
1. 独立ICC与普通JPEG色度参考门禁继续OPEN；不扩成RAW/16位/色彩引擎大工程。
2. 物理鼠标/DPI/ACM/HDR与全面设计验收未运行；不重启被禁工具。
3. 本轮工作预算和task cache已测，完整应用还会持有PNG历史版本及CLR/WIC内存；不是256MiB应用。
4. 灯台继续冻结，旧.ptplan v1的snapshot-only外带范围不改变；没有偷偷把editable canvas装进旧包语义。

原始收据见RAW_RECEIPTS.json；历史失败保留不覆盖。EVIDENCE_MANIFEST.json记录本批全部Git证据文件SHA，私照不在其中。

能力表维护：已更新capabilities.definition.json并通过现有生成器重建JSON/Markdown，v3验证状态明确FAIL（独立数值门禁），不是只改生成物或把PARTIAL误当既有枚举。旧能力项只沿用定义，未宣称重新测试全部能力。

### 输入原始性补核（不补造素材）
两份已授权原TIFF本来无嵌入ICC，方向分别1/8；本轮24/33MP JPEG使用Pillow LANCZOS重采样、quality94/subsampling0，并显式写入新建sRGB ICC和方向1/6。已测的是摄影内容＋生成ICC/方向回归，**不是已有相机原始ICC/方向的科学验证**；“至少一张此前已有ICC和方向的受支持摄影源”尚未证实，记NOT_RUN，不把新写元数据冒充原始来源。生成配置补在INPUT_IDENTITIES.json。
