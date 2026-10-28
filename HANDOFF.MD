# 光镜开发交接入口

## 公司宽域工具＋R3接续（2026-10-27 America/New_York，批次20261028）
- 从home/r53-stage-b-20261027@068f08734c4d1f3e2d18079a63a215858316d313（产品b8436706）新建E:/Kitao-Worktrees/r53-color-tools-film-20261028，分支company/r53-color-tools-film-20261028。fetch无新提交，旧52树/dirty保全，不合main/强推/研究整分支。
- 补充前置RAW/C1调查dbd19d66按需读取REPORT/CAPTURE_ONE_MANUAL/code证据/递归更正；不重复索要N根，不假定公司有家庭素材。旧uint16clip、PV4sRGB输出、V2原仅栅格、X-T5内存/X-Trans边界都核对，见docs/validation/r53-color-tools-film-20261028/RAW_RECHECK.md。
- RawWideOutputV1默认off研究变体（KITAO_RAW_WIDE_OUTPUT_RESEARCH=1+WHOLE_APP_DIAGNOSTIC）：既有LibRaw线性ProPhotoD65已截断master→精确推导源ICC→现有LCMS线性ROMMD50→目标ICC，不走sRGB显示映射再贴标签；不恢复前端截断、不拟合C1LUT。C1继续NOT_RUN，X-Trans门禁未改。
- V2新WideToolVersion1白平衡（D50参考相对xy/Bradford，不是相机Kelvin）和对比/高光/阴影小集合；Normal强度/源mask/undo/变体/保存/preview/export/filmCurrentEdits同定义。旧V1/PV4/5/6/旧V2曝光逐通道math不变；其他旧encoded工具编辑时拒绝、UI禁用。影调双完整参考执行尚缺，诊断准入OPEN，不作科学色准批准。
- 20MP真实Canon先测V2约7.5s后加面积linear proxy800；快速误差预算实现前固定mean1.5/P954/max32；导出full、停笔精细。最终实数/内存/独立进程见同目录PERFORMANCE/TEST_SUMMARY，不沿用小图宣传全图。
- R3家庭唯一paper selector继承，本轮补stable paper:/frame:ID收藏/最近/搜索/选择反馈、6程序边框唯一左区、右侧重置/项目冻结草稿保存恢复；seed/来源/取消/显影锁/重拍保留。film读整文档+ICC→sRGB，不是显示器preview，也不是完整宽域胶片。
- 自由画布首闭环本轮未开始，不用设计文档冒充。联名/真实扫描授权缺失单列不阻通用资源。旧教程停用、后置完整引导不做。
- 完整应用WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，所有模块及策划15板块保留；无Setup，CMD隔离启动。ComputerUse/Browser/WebView/CDP禁止，testhost/OFFSCREEN/OS启动分开，物理鼠标/多屏ACM/HDR未验。
- 当前权威docs/validation/r53-color-tools-film-20261028/FINAL_REPORT.md、TEST_SUMMARY.json、COLOR_BOUNDARIES.json、FILM_STATUS.md，产品/最终远端SHA交付封存时写入。


## 家庭阶段B CMS收尾＋R3/出版续接（2026-10-27 America/Phoenix）
- 起点home/r53-stage-b-20261027@047e51bf，当前发布源码b843670653dcb1899f77b873e31a11455d754f36；原b6eaa32e成果继承，不重做scene性能/cache lease/thumbnail prefix/crop首ICC。现有独立树N:/pixart/guangjing-r53-stage-b-20261027继续。
- 权威docs/validation/home-r53-cms-closure-20261027/FINAL_REPORT.md、TEST_SUMMARY、COLOR_BOUNDARIES、JPEG_SUPPORT、WORKFLOW、USER_EXPORT_AND_GAMUT、PUSH_READBACK。旧home-r53-stage-b报告的JPEG_BLOCKED/CurrentEdits拒绝是历史，不当当前状态。
- JPEG根因WPF autoICC解释后的码值误对raw目标；IgnoreColorProfile取编码值，保留MAE5/RMSE15。sRGB/P3/AdobeCompat JPEG8实放行；ProPhotoCompat8仅显式警告非默认。四空间PNG/TIFF16 lossless0码差；same pixels/quality95/444独立Pillow/libjpeg3.1.4.1一致，失败样本保留。
- 新DocumentRenderResult float32+ICC+alpha+geometry/version。film Original/CurrentEdits用InstantInput3冻结整文档/版本，full结果转sRGB显影，不用monitor预览；出版actual embeddedICC→encodedsRGB float bilinear/WPF8watermark coverage→targetICC，service/receipt/二次出版/UI列表均接通。照片未降为8bit合成缓冲，水印覆盖8bit明确。
- 新ColorState2真正linearROMM/ProPhoto D50 gamma1 float32最小工作域，仅线性曝光/Normal强度mask/geometry；旧encoded工具拒绝。V1输入本身保留负/>1但BasicTone/WB/Levels/Curve会压缩裁切，输出ProPhoto不等宽域编辑。V1/PV4/5/6默认数学及DecodeKey保持，researchICC/PV7off。
- 四模式Overlay/SoftLight/Color/Luminosity已诊断菜单+setter准入，encodedsRGB调整结果语义，不是PS HDR/pixel-layer alpha/Fill；旧三模式保持。PhotoCraftfloat参考+PDFium固定源码实际TCC研究编译，整数误差保留，研究二进制不随包。
- 用户完整导出命令同机512x342约137–169ms，直接Write~50–54ms、默认secondRender0；显式Inspect全像素还在。取消/故障/spacefull注入不留假成功，不是真填磁盘。相同目录变ICC同名输出仍保护拒绝，不放宽覆盖。
- 本机最终Core68PASS/WPF主55PASS＋透明边缘补充1PASS/0FAIL/0NOT_RUN，另独立新进程重开1PASS像素hash相同；相关授权PhotoGrade用旧固定hashCanon派生manifest已跑，无关24MP/长笔画专项保留不刷总数。完整真实EXE启动只进程验证，鼠标/物理多屏ACM/HDR未验；用户禁ComputerUse/Browser/WebView/CDP不重启用，OFFSCREEN不是桌面。
- R3继续：唯一分组paper selector放左资源（不再重复），右侧Original/Current、seed/随机预览、只读冻结来源/配方、取消/无动画显影/重拍验证；完整原型175清单/真实扫描联名资源授权仍PARTIAL。接下来逐项补V2工具与C授权资源交互，继续D/E，不停在研究报告。
- 完整目录N:/pixart/Guangjing-R53-Home-20261027-CmsClosure/Guangjing-R53-CMS-Closure-Final-Diagnostic-win-x64；CMD启动，PS7.5 Prepare-CMS-Synthetic-Samples只重绑合成图。所有模块/策划15保留，.ptplan不含canvas，无Setup/不能发行。旧教程退役与后置全功能引导待办保持。
- 保全原主211项未提交；只读旧22树核对。并行GUANGJING_RAW_C1_FEASIBILITY外部进到dbd19d66已记，不回退/不整合它。其余树不动，不reset/clean/stash/rebase、不合main、不强推。产品证据分提交/普通push读回。


## 家庭 R5.3 阶段B＋PhotoCraft 首个CMS闭环（2026-10-27 北京时间 UTC+08:00）
- 家庭独立分支 home/r53-stage-b-20261027，树 N:/pixart/guangjing-r53-stage-b-20261027；从公司190d1304接续，未从home stage-a重做，旧树未提交资料保持。
- 产品场景交互9afc5899＋CMS最终b6eaa32e08bb37d79dcdfde7db17613a1558f5c6。权威docs/validation/home-r53-stage-b-20261027/FINAL_REPORT.md、TEST_SUMMARY.json、NONPASS.json；完整家庭包 N:/pixart/Guangjing-R53-Home-20261027/Guangjing-R53-Home-CMS-StageB-Diagnostic-win-x64.zip，CMD/BAT/PS1启动。非公司E盘ZIP、完整所有模块/策划15板块，教程不恢复。
- SceneExposureV1已有独立mask快照UI/清除/锁定/undo/reopen；场景EV曝光前采样复用，Canon同机旧1.59–1.68s→热82–88ms，首114ms/13有效中间帧/含drag精细3.36s，mean.641/P951.730/max12.656，非鼠标/色准。高频反例失败保留并精细回退，GeometryV2也回退；不能宣传所有图90ms。
- cache lease/epoch和HQ owned-result修复，8循环账本0。统一低分辨率stack prefix输入/输出，非RAW final重复效果。普通栅格新显式GeometryV2 crop/straighten/rotate/flip，旧V1不变；reference/spatial未适配栈拒绝不丢层。
- PhotoCraft固定7ac7b8只架构参考；复用已随包LCMS2.12做显式RGB matrix/shaperICC变体，输入/工作sRGB/显示/输出分离，TIFF16/PNG16实际P3/AdobeCompat/ProPhotoCompat转换与嵌入读回；ProPhoto编辑域没做。显示CPU SDR默认off，Windows关联/手动fallback，物理ACM/HDR/多屏未验，不能双转。JPEG门槛失败已阻断，出版resize/水印/即时CurrentEdits对新状态拒绝避免误标。
- V2仅Darken/Lighten菜单放行；柔光/Overlay/Color/Luminosity原型未完整准入。既有PV4/5/6默认不暗迁，researchICC/PV7保持off。继续B/C/D/E，不因物理鼠标/授权资源让无依赖开发停住。
- 家庭权威整合Core55PASS/4NOT_RUN、WPF52PASS/1NOT_RUN、0FAIL；去重补充Core62PASS/3NOT_RUN、WPF53PASS/1NOT_RUN。独立create/reopen均PASS，现场启动只是进程验证。全部未验真实原因保留，不发行、CalibrationEligible=false。推送读回见PUSH_READBACK.json，普通push，不动main。


## 暗室交互与阶段B启动（2026-10-27 America/New_York）
- 同分支接续bc80e556；当前权威docs/validation/r53-interaction-stage-b-20261027/FINAL_REPORT.md、TEST_SUMMARY.json、PERFORMANCE.json。产品4b5f73b726c29b2117e0119333b44f0ef20c0beb，测试追加12eee94e；完整包E:/Kitao-Deliveries/r53-interaction-stage-b-20261027/Guangjing-R53-Interaction-StageB-Diagnostic-win-x64.zip。本轮定向Core17PASS、WPF56PASS/1NOT_RUN、0FAIL，原4历史授权缺口保留。不重做RAW接入/教程退役/设计包研究。
- 覆盖1在途+1最新待处理、同目标允许单调中间帧，换图/变体/节点/关闭严格取消；不先清空闪烁；max768，行采样最多4worker。持续20k点合成输入已测首帧/中间数/最终正确，非鼠标验收。
- RAW800px面积基础图最多2缓存，键含源身份/完整基础定义/版本/几何。快速局部工具同冻结定义、停止400ms后精细；快速为近似需看误差，导出始终原全分辨率。原nearest候选超误差已拒绝。
- 四变体逐帧发布/所选ID加载槽；不是4张全做完才显示。普通精细行带4worker，点工具绕开重复包装；性能/资源数据见报告，不把快预览算精细。
- 图层/蒙版缩略图、选择态、专业节点绑定、4类示波页、工具栏Auto换行、独立C裁剪上下文（自由/比例拖框、旋转/翻转/重置/完成）已接入；画笔公式移Tooltip。裁剪目前线性RAW，任意角拉直/全栅格仍余项，无教程。
- A负责交互/界面/状态一致性；通用ICC/宽色域/scene-linear扩展归B，不无限扩大A。B首项SceneExposureV1已实际实现：基础RAW曝光后/显示映射前按独立蒙版线性EV；不重复相机WB、不使用sRGB混合，不改PV4/5/6缺省输出。独立Version1、诊断限制保留；参考/数值边界见STAGE_B_CONTRACT.md。
- 四项旧授权专项未运行原因原样保留，本轮不重复无关历史测试。Computer Use/Browser/WebView/CDP仍禁止，离屏/testhost不当真实鼠标DPI。完整应用/全部模块/策划15板块保留。


## 专业RAW局部收尾（2026-10-27 America/New_York）
- 接续company/r53-stage-a-completion-20261027@41265eb4，无远端后续；仍在原独立实施树接续，不重建家庭基础。当前权威证据docs/validation/r53-raw-local-closure-20261027/FINAL_REPORT.md。最终产品091163b12aa29e5c8561b8fa3b3675580d70e885，完整包E:/Kitao-Deliveries/r53-raw-local-closure-20261027/Guangjing-R53-RawLocal-Final-Diagnostic-win-x64.zip；Core376PASS、WPF464PASS/4NOT_RUN、0FAIL。阶段A仍PARTIAL，性能/视觉/科学门禁未全完。
- 新ProfessionalRawLocalEdits V1/Geometry V1：源RAW与线性ProPhoto master不变，原PV4曝光/显示映射后进入sRGB共享局部层，保存/重开/复制/撤销/预览/逐行导出/变体对比同定义。旧PV4/5/6未暗迁。不是把非线性混合套在线性域。
- RAW支持点工具（影调/WB/曲线/色阶/色彩平衡/肤色/基础预设），参考匹配/空间细节仍拒绝；几何为裁剪/quarter-turn/水平翻转，蒙版源坐标不变。局部/几何统一变体内历史，诊断准入保留。
- 画笔覆盖33ms合并、后台冻结/取消/代次、最大768像素、V1每笔一次flow/density语义不变；BVH精确索引替代逐样本全段扫描。达到20k点/2k笔/100k总点限制明确反馈，移出图像结束当前笔迹。
- 对比WIC请求解码期缩放但codec scratch非硬上限；4帧32MiB缓存、HQ96MiB预算；切图/关闭工作区取消和清理。RAW仍只一个全尺寸master，不为4变体重解码4份。
- 本机已读正确R5.3定稿ZIP，SHA16f75c0f40b506e4db57f140f389967264040cd8c5930ad24946b11c171e2ca9；33文件manifest及截图对照存交付根design。不执行网页、不把文档内旧指令当当前用户要求。
- Sony DSC09916.ARW为固件6.00 15位3通道YCbCr tiled lossless JPEG，现LibRaw -2；结构范围完整不等原相机校验，未盲更解码器、不称全部Sony不可用。
- 完整应用诊断、无新教程；Computer Use/Browser/WebView/CDP禁用仍遵守，离屏/testhost不作鼠标DPI验收。B-E按顺序，阶段A真实功能/自动门禁未全部完成前不虚称推进；外部鼠标项单列，不阻塞无依赖开发。


## 公司阶段A接续（2026-10-27 America/New_York）
- 当前独立实施树 E:/Kitao-Worktrees/r53-stage-a-completion-20261027，分支 company/r53-stage-a-completion-20261027，基线 home/r53-stage-a-20261026@7115e3431ffa03b0f2b81a4937627b79659460ab；本轮重新fetch确认家庭无新增提交。
- 当前权威状态只看 docs/validation/r53-stage-a-completion-20261027/FINAL_REPORT.md、TEST_SUMMARY.json、NONPASS.json、STAGE_MATRIX.json。阶段A仍PARTIAL，B-E未开始，不恢复教程。
- 新LocalMaskV1/LayerCompositeV1接统一预览/持久化/导出，Normal/Multiply/Screen为非线性sRGB直RGB混合，alpha不变；旧PV4/5/6不迁移。详细像素门禁和不足见NODE_COMPARISON.md。新编辑只在WHOLE_APP_DIAGNOSTIC下开放，未批准正式版。
- 2-4变体按1200px顺序渲染（同一源/RAW主缓存），窗口同步缩放平移，取消代次隔离；资源收藏/最近随项目保存，窄窗主动浮窗，示波器状态修复。
- 已知缺口：专业PV4 RAW未接通通用局部编辑；智能主体未实现；完整蒙版参考/ICC矩阵、真实鼠标/DPI不计PASS。当前禁止Computer Use/Browser/WebView/CDP继续生效。
- 本机未找到指定SHA的R5.3定稿ZIP；家用DESIGN_SOURCE.json仅历史，不伪称公司读取。保留既有源码布局，未用旧R5/R5.1包冒充定稿。
- 旧工作树和未提交内容保留，禁止reset/clean/stash/rebase旧树/强推/合main。统一交付 E:/Kitao-Deliveries/r53-stage-a-completion-20261027；完整应用而非暗室宿主，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false。


## 家用 R5.3 阶段 A 与旧教程退役（2026-10-26 America/Phoenix）
- 实际仓库 N:/pixart/pixel-tart-source-private；独立工作树 N:/pixart/guangjing-r53-stage-a-20261026；分支 home/r53-stage-a-20261026。20旧树与211项未提交文件保全，不reset/clean/stash/rebase、不合main、不强推。
- 再fetch/ls-remote公司 company/whole-app-ui-copy-cleanup-20261026 = 48527fa0b7d3d063d5af5f6b62fa1a18cb36ef66，无后续提交；家庭旧产品均已继承。
- 设计包 C:/Users/Admin/Downloads/Compressed/光镜-UI-R5.3-定稿.zip，SHA256 16f75c0f40b506e4db57f140f389967264040cd8c5930ad24946b11c171e2ca9；实读解压目录 N:/pixart/Guangjing-R53-Home-20261026/design-reference。原型只作设计资料，不执行网页/不复制未授权相框。
- 暗室源码节点592ecccb：同一Editor/ColorAdjustmentStack双模式、串行节点/胶片条、稳定original/main变体、用户/系统锁、确认后归档/本会话撤销删除、已有工作文件900ms防抖保存及正常关窗等待、ActualWidth DIP断点、B画笔/\对比、宽窗440检查区/窄窗主动专注视图。阶段A PARTIAL：真实局部蒙版、通用混合/强度、2–4同步变体对比未完成；B–E本轮未开始。
- 追加产品：旧22步新手分片教程永久退役，新装/升级/中断旧进度不激活；删除自动/手动入口、遮罩/步骤提示、升级提示窗口和产品示例生成器。OnboardingService/TutorialDataService仅留不写盘兼容壳，旧字段/证明盐/路径/原数据不删除、不伪造“完成”。正常分片继续实际扫描/匹配/复制/导报。
- 旧品牌根因是教程/帮助/菜单/弹窗和文件元数据硬编码；用户可见文字、翻译值、安装器名称/发布者统一光镜。内部程序集、namespace、资源key、旧翻译key、迁移/数据库/安装兼容路径不改，已有用户水印/项目名称不批量替换。
- 全部功能完成且交互稳定后，集中制作覆盖光镜所有功能的产品引导与说明教程。
- 上述后置引导以届时实际交付功能为准，覆盖完整摄影师工作流、各模块操作及跨模块衔接。当前不开发替代教程。产品待办 docs/PRODUCT_BACKLOG.md。
- 当前用户全局规则禁止Computer Use/Browser/WebView/CDP；不重新启用。真实EXE仅做OS进程/模块身份启动与空工作区关闭核对；自动完整App夹具不等于鼠标保存重开/DPI。离屏WPF图不作实机截图。
- 最新可运行源码90106143（教程退役d6590c99，后续955548a1仅安装器，src/tests零差异）。本机最终Core211PASS/0FAIL/2NOT_RUN、WPF108PASS/0FAIL/4NOT_RUN；完整App3独立启动夹具PASS不等鼠标验收。
- 完整发布/最终产品SHA、家用实测计数、ZIP/远端读回只以 docs/validation/home-r53-stage-a-20261026/FINAL_REPORT.md、TEST_SUMMARY.json、PUSH_READBACK.json 为准（生成后）；公司216/197仅历史。首版592ecccb已跑Core65PASS/2NOT_RUN、WPF62PASS/4NOT_RUN，后续教程退役须重新绑定最终发布DLL，不能沿用这些成绩。
- 统一交付根 N:/pixart/Guangjing-R53-Home-20261026；最新完整诊断包名称见最终报告。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false / win-x64自包含；不发行、不生成安装包。默认CMD不依赖脚本执行策略，不修改系统策略。
- 下一步先补阶段A批准渲染蒙版/混合与变体对比；像素行为改变遵守docs/color-science/open-source-first/POLICY.md，保留PV4/5/6及默认关闭研究。不得把R3原型175项算WPF完成、把.ptplan v1写成包含自由画布。


## 有效基线与本轮
- 当前公司分支：company/whole-app-ui-copy-cleanup-20261026。源起点home/planning-owned-canvas-20261025@a2c3fd00783b2091ecc7dc3a86e9df52bb7a71ac，已包含自有画布修复8886226d；公司dad3dd4c到起点0落后/4前进。
- 本轮目标：完整主程序可达页面文案/布局减负，保留操作、可访问名称、AutomationId、保存失败、外链/项目副本和包排除依赖。纯文案不引依赖，ColorStudio布局/RAW/ICC/PV4/5/6不改。
- 本轮状态：PARTIAL，产品已交付；Core216/216、WPF197PASS/0FAIL/3NOT_RUN，原生工具阻断，产品/验证SHA和最终诊断入口以本轮简短FINAL_REPORT为准，未执行原生不升级PASS。

## 规则和踩坑
- 旧工作树/未提交/未推送成果只读保留；独立分支开发，不reset/clean/stash/rebase旧树，不合main、不强推。不把所有分支当集成。
- 完整诊断必须WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false；统一隔离启动，ZIP非Setup，构建通过不等发行。
- 已有项目自有参考/画布必须保留身份、根路径、SHA验证与禁止原外链/preview fallback；.ptplan v1不含自由画布工作区，不能写画布跨机通过。
- 画布Editor.Changed曾每次布局变更全量重新校验解码；本轮先测再改，布局合并与显式重载校验分离，不扩展大型缓存或修改解码器。
- 策划搜索框曾Margin162避让四常驻包按钮，本轮改真行布局；独立根操作保留更多菜单及AutomationId，取消仅任务期间。
- 原生工具按本轮真实可用清单选；当前Windows使用@oai/sky via node_repl，不照抄家用unsupported mcp__cua_repljs。一次明确阻点后停止空跑，VM重建不等新进程。
- WPF以夹具独立进程；符号链接权限失败保留原结果，不改策略。历史家用Scenario11 source4f57ca56、10000次1292.9921ms是历史PASS，不是本轮性能，不重复测。

## 既有权威资料
- AGENTS.md：像素节点门禁；本轮不触及。
- docs/PIXEL_TART_HOME_DEV_HANDOFF.md：旧跨机环境约定，时间敏感命令须现场复核。
- docs/validation/home-planning-owned-canvas-20261025/FINAL_REPORT.md、NONPASS.json、DESKTOP_STATUS.json：本轮继承的产品/未验边界。
- docs/validation/planning-bounded-thumbnails-20261023/FINAL_REPORT.md：有界预览与缓存安全合同。
- docs/implementation-reports/WHOLE_APP_PRODUCT_LOGIC_AUDIT.md：旧全软件逻辑清单，非当前验收成绩。
- docs/validation/whole-app-ui-copy-cleanup-20261026/FINAL_REPORT.md：本轮最终简报（交付时创建/更新）。不复制历史报告制造第二套最终状态。

## 本轮已复现
- 200个布局/缩放事件、12自有图触发248次解码；80ms合并后36次（含初始和显式完整性复验），通用provider0调用。显式LoadPreviewsAsync仍逐次核对，布局不会按每事件重新解码。
- 窄窗侧栏原设计会折入抽屉，布局测试须验证隐藏列而非要求隐藏搜索框有宽度。
- 普通当前根导入失败需保留“项目ID冲突”可行动原因，技术详情进日志，不能统一抹成未知错误。

## 本轮交付读法（2026-10-26 America/New_York）
- 产品代码SHA b9cb1e25e811903b117aef3afeb203aadf7d0790；测试合同修订SHA0505216f8aacbcc67ea10f7584a8469767afe923（src零差异）。后续证据SHA/最终分支HEAD见最终报告与PUSH_READBACK，不把文档HEAD当产品源码。
- 完整主程序：E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026/Guangjing-UI-Cleanup-Final-Diagnostic-win-x64；隔离Start-Guangjing-Diagnostic.bat。不是Setup。
- 原生一次点击coordinate input geometry is unavailable，JPEG/画布保存退出新进程/A-B整链均NOT_RUN。内验图片是离屏合成，不是桌面实机证据。
- 先前静态教程逐字断言4项已改成真实命令/状态绑定，安全导出/不覆盖原件回归保留；源副本错误短态+展开详情/日志，不放宽禁止回退。
