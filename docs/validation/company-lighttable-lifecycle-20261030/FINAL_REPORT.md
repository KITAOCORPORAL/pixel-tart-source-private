# 公司灯箱生命周期、渐进观片与有界资源
2026-10-30（Asia/Shanghai；原始执行主机收据保留其时区）。**本批修复已交付；整体 PARTIAL_DIAGNOSTIC，不能发行。**

## 接续与范围
- fetch全部已配置相关remote，远端家庭home/r54-design-fixes-20261029仍为7a6b98e9d48e3f21f91cd69449939b26531e5804，无额外增量；家庭产品f70ecf7d1179b6f42c721a787a4a2d7a7acc8109。公司f8beaa37是家庭祖先；从家庭HEAD新建company/lighttable-lifecycle-20261030，E:/Kitao-Worktrees/company-lighttable-lifecycle-20261030。
- 所有共享Git工作树先枚举并记录dirty，旧公司树干净；其他树未提交内容不动，没有reset/clean/stash/合main/强推/研究整分支整合。独立输出E盘，不访问或假设N盘可用。
- 已读AGENTS、HANDOFF、R54及灯箱Spektra总纲、家庭报告、产品总蓝图；公司本地R5.4第9章/v2草图及R5.3 HTML仍有效。暗室阶段、完整四轮量测、旧三编码A/B不重复实施；只作必要宿主回归。

## 实际代码修改
1. 详情、比较、幻灯、刷新、返回和关闭共用代际/取消所有权。成功与失败发布都验证当前所有者；Compare/Stop使在途详情失效。Refresh交错不会回填旧快照，关闭解码不可回填或重新缓存。取消令牌由创建请求finally释放，不在别的请求里提前Dispose。
2. PrepareViewingTableAsync逐项发布、先排可见区域；实际控件先布局占位，视口/缓冲区外卡片移除。默认100项摆放下当前只实现30卡；不是限制“最多32”而漏掉重叠可见项，密集重叠会实现全部真正可见项（仍最多100固定项）。滚动使旧加载失效并只加载当前视区需要项；关闭释放控件、frames、代理、身份表和缓存账本。
3. 失败卡按target+variant逐项重试，不重跑全部；同源两个失败版本互不清除。单workspace串行render门，RAW全局HeavyWork串行，无100路解码；代理≤64MiB、总体表面按数量约32MiB、缓存有原资源账本预约。**缓存不是进程硬上限**。
4. 固定source/完整recipe/edge仍为缓存键。本地NTFS/ReFS可依赖文件句柄的volume/fileID/length/write/change-time（同长度且回写mtime也能检测变更），读租约禁止写入；首次hash，缓存命中仍重新取可靠身份，渲染后复验路径。无法取可靠stamp/网络等场景回退完整hash，不宣称全盘都能免hash。源变化明确失败，不自动替换固定配方。素材库来源仍走既有repository解析。
5. 只新增观片路径：保留LibRaw版本及全部HalfSize=false、WB、输出色彩/16位/native处理参数；完成原生全解码后从native RGB16直接提取原有nearest样本，仅复制小RGB16/float代理，不复制全尺寸RGB16及float master。不采用嵌入JPEG、不称已调色的JPEG占位为最终。仍用冻结look/stack/film原渲染。未扩原图ROI、未改LibRaw算法、未加色彩数学。按既有全局预算以raw尺寸×28估算预约，超预算明确拒绝不降位深。
6. 观片元数据：正常方向读取保留；MetadataExtractor可独立恢复异常时只读取有界标准TIFF IFD0显式有效Orientation；大小端1–8、重复/非法/缺标记拒绝测试。MakerNote/XMP不解码猜测；无法确定明确RAW_VIEWING_METADATA。正常缺方向标签仍沿已有规则1。旧全尺寸/导出分支数学保持。

## 确定性验证与已有胶片链
TaskCompletionSource可控完成顺序，不靠长sleep：A详情→比较→A成功及失败；A/B反序；刷新反序；关闭真实注入解码；停止幻灯后旧完成；渐进首帧；滚动取消；代际与cache释放；同长度改字节+还原mtime；逐项重试/同源双版本失败。13项业务/lifecycle，加教程可见检查共WPF15通过。
固定快照→已有8位创意胶片→新结果变体→灯箱→暗室→保存→独立进程重开仍通过；任务中心完成/取消失败无半成品保留。FilmBackend源码本批无改。SpektraLab仍未加载/未打包；不宣传RAW/16位/宽域后端，不改跨崩溃恢复任务系统。
最终绑定7项目发布DLL：Core29、WPF15、独立重开1、实际性能/原生重复/取消4、完整宿主1，**50项唯一PASS/0FAIL/所选集合0NOT_RUN**，非全产品全量。原始TRX与hash见TEST_SUMMARY和test-results。OS EXE启动/WM_CLOSE退出0与日志产品SHA读回PASS，仅启动关闭，不是实机交互。

## 公司同机同输入前后
家庭100自然图不在公司可访问材料；不扫描无关私人目录。公司固定已有5张CompanyRAW（含20MP CR3、Fuji RAF/大图、Sony ARW），另自建100个不同1600×1000 JPEG压力样本（不同内容/hash，**不是100张自然RAW**）。固定输入清单见两份MEASUREMENTS。baseline是公司现机编译家庭源并绑定发布程序集，不复用家庭49.33s。

|集合/指标|公司基线|公司最终|
|---|---:|---:|
|100自建JPEG成功/失败|100/0|100/0|
|100项VM首帧/全部完成|349.8/349.9ms|54.8/332.2ms|
|实际冷控件首图含Render|356.7ms|40.2ms|
|冷占位布局|基线没有占位卡|22.4ms，25占位卡|
|冷控件全完成（含首图渲染）|404.0ms|555.2ms|
|100项实际visual tree数量|494|214（最终30个视区卡）|
|warm refresh含布局/最后一次Render|60.3/22.1ms|82.1/21.2ms|
|RAW5成功/解码失败/预算拒绝|4/1/0|3/1/1|
|RAW5首帧/全部完成|16.84/16.84s|5.05/10.86s|
|RAW5采样WorkingSet|max 3,784,835,072 bytes|max 797,732,864 bytes|
|RAW5采样Private/Managed|3,787,218,944/2,243,947,496|760,107,008/29,908,208|

首屏明显提前，但渐进发布/布局有成本：100项冷控件全完成 **555.2ms vs 404.0ms**，不是全部指标都变快。20ms采样不是OS连续峰值；private减managed只给运行时/原生/碎片等估算，不是独立native allocator测量。预算减少一次超大RAF实际解码，不能把全部内存下降归因复制优化。

公司Sony DSC09916.ARW：RAW_NATIVE_FILE_UNSUPPORTED、OpenFile/FileUnsupported；非元数据或预算。LMAN1714.RAF估算2,894,282,496 bytes，超过现有全局2GiB（且需现有缓存预约），明确预算拒绝。两张其他RAF和CR3成功。当前5图样本对照不是100RAW流畅门禁。
立即取消命令约1.78ms（自建100）/1.47ms（RAW缓存请求）。另取消probe在dispatcher让出后退役约2.19ms，**没有证明已进入长native阶段**；原生API仍不可抢占，若正在Unpack/DcrawProcess需等其返回，不能声称2ms终止原生解码。可控阻塞测试证明取消后不会发布且账本释放。

## 像素与七张家庭失败的边界
- 100自建JPEG新旧逐位相同，CR3代理逐位相同；固定RGB16→原nearest代理大小/所有8方向逐位相同。没有重写像素数学。
- 两张Fuji RAF跨次原生解码存在极少量不同像素；旧产品完整解码重复也复现（BASELINE_NATIVE_REPEATABILITY）。新路径独立两次同样出现。不能声称所有RAW新旧hash相同；也未据此调LibRaw算法。PIXEL_PARITY与原生重复读回单列，这仍是自然RAW严格可重现的OPEN项。
- 家庭7张：SIGMA_DP1/SD10/SD14/SD9_SRGB/DP1S与POLAROID_X530共6个旧“无法安全解码”记录没有native stage/code；**公司没有这些文件，不能确定不支持还是损坏，也不能声称已恢复**。LEICA_M240为元数据解析越界；已增加有界明确方向恢复并测合成TIFF，但该实拍DNG未复跑。HOUSEHOLD_SEVEN_FAILURES逐个保留NOT_REPRODUCED_SOURCE_UNAVAILABLE，未将它们计为预算失败。精确调查需这7份授权文件或带native stage/code的新收据，本批不猜结论。

## 设计读回、剩余与包
本机读取公司设计文件/hash见DESIGN_READBACK。实际生产控件灯箱/100项及完整宿主1366/1920已打开查看：专业图仍在照片/胶片条之间；即时左相纸、中成品、右相机/相框/参数，不借性能重排。灯箱保持家庭原Canvas摆放，不新建图库/页面/比较引擎；滚动仅虚拟化。不是Grok逐像素最终美术签收。控件/OS测试与ComputerUse/Browser/WebView/CDP分开，本机禁用工具未使用。
剩余：家庭7图精确根因复现、Fuji原native重复性、超预算高像素RAW安全支持、专业RawDefinition路径暂存内存、不抢占native段、完整100自然RAW/真实鼠标/DPI/ACM/HDR验收。原图ROI、Spektra引擎门禁/胶片宽域、跨崩溃任务恢复仍后续。本轮不会要求采购色卡或把近似当实测。
产品源码 `9f7ef4e45c383b66b048d1a183bc41d5e0a61d6d`，测试支持提交08d76a6212975c63718686eb21e6cd5ae9fbee5c已包含于产品树祖先；文档HEAD和远端另记。完整自包含win-x64：`E:\Kitao-Deliveries\company-lighttable-lifecycle-20261030\Guangjing-Company-LightTable-Diagnostic-win-x64.zip`，隔离CMD在同名目录。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；7个项目模块集合核验与家庭包相同，策划15的SectionOrder和其全部模块代码未改，旧教程退役。无Setup、无照片/RAW/UserData/研究资源。
ZIP 77,106,495 bytes、295文件，SHA256 `0ef735c734c210a4f7750ec003284bdf447e65b13a0b7bfffe2821feb70551d8`；逐文件解压流hash通过。诊断版不发行。
