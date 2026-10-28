# 公司宽域工具 / RAW独立宽域出口 / R3资源接续 — PARTIAL，不能发行
日期2026-10-27 America/New_York；批次名20261028。

## 正确继承
家庭home/r53-stage-b-20261027最新068f08734c4d1f3e2d18079a63a215858316d313（实际产品b843670653dcb1899f77b873e31a11455d754f36），fetch无更新。公司独立树E:/Kitao-Worktrees/r53-color-tools-film-20261028，分支company/r53-color-tools-film-20261028；不覆盖旧树，不整合codex/raw-c1分支。

## 实际完成
1. 优先核对dbd19d66旧调查（047e51bf）与当前：uint16截断仍在，旧PV4输出仍sRGB收敛，原ColorStateV2仅栅格，X-T5内存/X-Trans门禁仍在。递归更正978文件/356候选已读，不再用旧212断言场景不足，也不假定公司有N根。C1 Session/TIFF/实验仍NOT_RUN，不拟合LUT。
2. 默认关闭 **RawWideOutputV1研究变体**：现有LibRaw已截断ProPhotoD65 master→由真实LibRaw矩阵推导源ICC→已有LCMS线性ROMM D50→实际目标ICC，显示独立sRGB；没有sRGB压缩后贴宽域标签。真实Canon20MP ProPhoto TIFF16嵌ICC/full读回0差，源hash不变；不恢复前端已截断信息。
3. V2 **WideToolVersion1**小集合：相对白平衡（D50参考xy偏移+LCMS Bradford，不是绝对Kelvin/相机WB）、线性亮度域对比/高光/阴影；同RGB增益保色比，负/>1保留至输出。Normal强度/蒙版/双模式/撤销/变体/保存/预览/全导出/filmCurrentEdits同文档定义。旧V1/PV4/5/6及旧V2曝光逐位回归保持；不支持字段UI禁用并编辑时拒绝。
4. R3家庭成果继承，仅补缺：稳定paper:/frame:ID收藏/最近/搜索、分组和选中反馈；6程序边框唯一左资源入口；右侧重置、项目冻结草稿保存恢复。修复取消预览/保存生命周期丢草稿seed及边框零宽缩略；最终冻结seed42断言。颗粒/瑕疵/表面/高光/双曝光既有入口保留，不重造。
5. 宽域文档完整结果+ICC→encoded-sRGB胶片处理，不读monitor预览；JPEG/TIFF→出版缩放/水印实际ICC读回，来源版本/原图未变。不是完整宽色域胶片引擎。

## 本机测试/性能
- 最终发布DLL29项绑定；选定受影响Core20PASS、WPF22PASS、0FAIL/0NOT_RUN。首轮WPF的两独立方法NOT_RUN严格状态FAILED保留，之后两个独立进程实际PASS才去重计通过，不称全套历史全绿。
- 宽域P3输入→WB/影调+mask→项目→新进程→JPEG8（MAE约0.50/RMSE0.76/max5，阈值5/15不改）、TIFF16（码差0）→冻结CurrentEdits胶片→出版80×56/ICC/水印；RAW宽域新进程定义/完整像素hash精确相同。
- 真实5496×3670 Canon V2：原精细约7487ms；最终首快速365ms、精细3036ms，输入处理29.66ms；线性代理冷137.91ms、热计算119.67ms。快速/精细8位sRGB尺度mean0.001016/P950.002884/max0.35533，低于实现前固定1.5/4/32；不称逐像素相等，导出full。
- 该测试进程峰值1969590272bytes含完整hash结果，产品资源账本313813440bytes单列，不把小图速度当全图，也不当硬内存上限。研究变体创建/解码约8秒仍有等待。
- WB60组nativeLCMS/独立Bradford矩阵max2.23e-15；影调27组与darktable/RawTherapee域/曲线差异明确记录，不硬凑等价。**完整双影调引擎参考执行仍OPEN，诊断准入未删除，CalibrationEligible=false。**
- 52旧树HEAD/status/dirty哈希全保全。无ComputerUse/Browser/WebView/CDP；OFFSCREEN/testhost/OS启动分别标注，不是鼠标或物理多屏ACM/HDR通过。

## 完整交付
产品SHA **2814d49b559d3067da2fb1d7ca8a489fccf3b940**；独立测试路径修订5e2001d6（src零差异）。最终远端见PUSH_READBACK.json。
完整ZIP：E:/Kitao-Deliveries/r53-color-tools-film-20261028/Guangjing-R53-ColorTools-Film-Diagnostic-win-x64.zip
常规CMD：同名解压目录/Start-Guangjing-Diagnostic.cmd（RawWide off）；显式研究CMD：Start-Guangjing-RawWide-Research.cmd。
WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，全部模块/策划15板块保留；包内UserData隔离，不改执行策略，不是安装器。
295文件/74406072bytes逐项hash读回；ZIP SHA256 **a6c8eb15d13eccd9daffc3d97cc20c79a8e92fb9c5367352b8f97da072d19738**。APP_FILES.json列路径/类型/大小/SHA，无UserData/数据库/原RAW。RAW_TESTS.zip保留原TRX/失败/脚本，GPL参考源码/工具不进产品包。

## 下一步/边界
影调完整双参考执行与科学准入、X-T5内存/X-Trans、C1对照、授权扫描/联名资源和物理鼠标/多屏ACM/HDR仍未完成。RawWide为default-off研究，不宣称前端浮点恢复或通用RAW色准。自由画布透明素材首闭环本轮 **未开始**。旧教程继续退役，功能稳定后再集中全产品引导。
