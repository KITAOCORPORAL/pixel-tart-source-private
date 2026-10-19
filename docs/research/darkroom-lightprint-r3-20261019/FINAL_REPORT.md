# 暗室 / 光片第一版家用试用交付

实际记录UTC：2026-10-19T14:26:08.022924+00:00；Asia/Shanghai：2026-10-19T22:26:08.022924+08:00（真实换算）。

## 试用入口与当前结论

EXE：`N:\pixart\Darkroom-LightPrint-Home-20261019\app\KitaoPhotoSelector.exe`。根目录`启动暗室.cmd`可双击。离线交付页：`N:\pixart\Darkroom-LightPrint-Home-20261019/交付预览.html`，请在系统Chrome手动打开；助手未打开浏览器/资源管理器。图片、示例项目和三格式导出位于materials目录。独立试用设置根：%LOCALAPPDATA%/PixelTart.DarkroomR3.HomeTrial，不覆盖前轮/正式数据。

**产品已实现可运行首版；本次尚不能宣称完成全部验收。** 程序化流程与最终EXE启动通过，1366/1920截图为真实WPF离屏渲染；用户要求的实体鼠标操作和真实操作录屏因官方Computer Use native pipe不可用，未执行。已按工具规范轻量重试、重置再初始化共3次，均同错误，未改用自制注入；没有制作离屏幻灯片冒充录屏。详见COMPUTER_USE_UNAVAILABLE.json。

## 源码与家里工作保护

家里主仓库N:/pixart/pixel-tart-source-private位于integration/pixel-tart-developer-preview，存在3组未跟踪资料，原样保留；不reset、不clean、不覆盖。原续接工作树kitao-v3-corrections-colorpage-20261017干净。正常fetch及独立ls-remote确认本轮基准feature/kitao-v3-corrections-colorpage-20261017仍是80040a6230d32cb9056e31a0ed7fda878e042b99，无后续远端差异。

新独立worktree：N:/pixart/darkroom-lightprint-r3-20261019，开发分支feature/darkroom-lightprint-r3-20261019。产品冻结源码 **95a6320b6543da2a49ec40c9d05f870e9e181728**，后续只文本证据/指南，不改src。最终完整290文件发布；EXE SHA256 **651835FA2D2EE21164EFBCD2E96C01ADA8028B8A29F64A0FEE1AEE4B344390FB**，Core **6BF58204CDFC531584EAE98EB8A99FFFBEC7DF9138FA7C862E9549C1F4A4DD92**。发布DLL注入测试/审计宿主后逐文件hash对齐，FINAL_BINDINGS.json；完整文件表RELEASE_FILES.json。

## 设计输入与最新授权

两ZIP按原文件SHA解压到仓库外N:/pixart/design-input-darkroom-20261019，先核对UI包CODEX-REFERENCE/decisions/spec/原型源码/覆盖表，再读r3.4d2的README、QA、sources和系列总览文件。参考优先级按用户要求R3.1结构→R3.2显影变体→R3.3选择器→R3.4清单。文档中要求把未实现能力伪装为可用的文字**不执行**，用户任务优先。

最新用户直接确认两包已获授权、可入交付素材，记录USER_CONFIRMED_AUTHORIZED；助手没有独立核验合同，不推断期限/地域/再分发条款。当前版仍按用户末句只开放五组相纸＋六通用框。未纳入角色框是版本范围选择，不能再描述为“用户未授权”。旧ZIP原件ship=false文字保留历史来源，不篡改。

数量差异：用户描述六系列60，实际UI包frames-collab-pack.js为98项/8个主系列＋其他联名1；独立r3.4d2 QA为9系列85项，包内审核还引用未提供的d1b六系列65。**没有可确认的六系列60逐项清单**，不凑造60项。两者权利按最新确认登记，但角色素材都留仓库外，当前通用包不加载、不复制；后续指定子集再逐条接入。清单、原ZIP SHA和各系列统计见INPUT_RECONCILIATION.json。

## 实现范围

1. **点相纸/框立即待显影预览**：绑定现有SelectedInstantProfile/相纸参数和通用FrameId，自动异步快速预览，再完整源1600高质量核对；90ms仅合并连续点击，不是显影展示等待。token＋draft revision＋activation revision＋target/version＋源SHA/第二源SHA全部匹配才发布。旧任务先取消/释放，最后点击生效。预览不写RawDefinition或EditVersions；快速结果明确标快速，完成后标预览就绪，不把缩小预览充作原图导出。
2. **R3主结构**：去掉旧左编辑轨，中央大图、底部胶片条，右侧“调整／光片／图层／风格／变体”五页签，可拖右面板分隔宽度。缩放/平移/fit/1:1/分界滑杆沿现有视口；光片显影按钮固定底部，1366不必滚到底。普通胶片启用/方案/强度在风格，独立一次成像在光片。未接通工具提示，不放可点假控件。
3. **显影→独立变体**：复用原GenerateInstantFilmAsync准备/提交事务、undo/redo、源身份/导出，不替换引擎。抽出相同BuildInstantDefinition供实时预览和显影冻结。新LightPrint1写入Project，显影后相纸/框/光学/缺陷/种子setter锁定，UI显示摘要并隐藏原配方编辑器。“从原图重新拍一张”沿配方生成新种子/待显影，显影新建EditVersion，不改旧变体。首版以既有EditVersions承载独立变体；按变体评分/色标与完整堆叠分组仍未实现，不能宣称全量C1 variants。
4. **动画**：相机动画偏好写独立用户设置；关闭时显影计算完直接提交，无强制计时；打开复用原展示，可跳过。不改变像素/配方历史。相机选择SX-70/i-2是已有外观偏好，不宣称物理相机标定。
5. **框与窗口**：六款项目原创procedural框，使用既有Paper3透明coverage合成，方/圆窗，不读取任意ZIP角色路径。不是任意PNG框导入引擎。选择器中所选框是权威，移除会被覆盖的重复框色/形状控件。
6. **窗口进阶调色**：新LightPrint1.WindowAdjustments只允许已有基础影调/白平衡/曲线数学，作用于已显影photo块再走原方/圆coverage和纸面；框外所有像素前后逐码相同。显式“应用到光片窗口”，同变体内历史undo/redo。新节点组合显式版本，不改旧V1/V2/PV4/5/6解释。额外中间缓冲有预算；取消/失败先准备后提交，旧定义/图像/历史保留。完整其他工具/几何变换未接通，不假装支持。
7. **保存和导出**：保留单文件CAS/文件锁、内嵌版本权威、隔离项目准备/移交、UUID临时和无覆盖发布、独立输出校验。保存/导出先取消待显影预览恢复有效版本，避免把草稿误当已显影。三格式来自同冻结definition，不从屏幕bitmap导出。

## 可用相纸 / 相框

相纸共5组真实已有响应：经典彩色（i-Type/600共用）、柔调黑白（三型号共用）、复古暖彩SX-70、浅蓝单色Reclaimed Blue、深蓝双色Duochrome。16张设计清单逐条登记于PAPER_GAPS.json：未实现yellow/purple/green及其余Duochrome不开放假卡片。创作模拟，非真实配对标定。

六通用框：经典暖白方窗、中性白方窗、冷白方窗、暖白圆窗、点纹方窗、柔金属圆窗。精确ID/颜色/图案/窗口/权利状态见FRAME_CATALOG.json。正式目录仅此六框，不含用户ZIP角色图或可安装角色扩展包。

## 验证（最终DLL/最终EXE）

|套件/操作|通过|执行失败|条件未执行|
|---|---:|---:|---:|
|WPF光片/窗口/安全/历史/既有RAW专项|13|0|1|
|Core旧V1/V2/PV4/项目/TIFF兼容专项|27|0|0|
|双照片程序化操作闭环|1流程|0|0|
|实体鼠标与真实操作录屏|0|0|2项未执行（环境阻断）|

WPF套件1项是原授权原生RAW corpus环境未启用，详见CONDITIONAL_NAMES.json；含NotExecuted整体runner仍FAILED，不能报全绿。TRX/discovery/测试assembly/hash逐项核对，无缺失或超时。首次01样例count预期未计原图版本失败、12/13用于竞态门控在UI线程阻塞的测试失败保留，不删除旧结果；最终对应例已修正并通过。20-final-wpf首次runner未构建导致discovery失败，构建官方runner后21-final-wpf完成，原错误日志保留。

两张不同授权Canon/Sony冻结照片，核验冻结SHA，转最大边1000评审副本；不是原生RAW重复或原尺寸新验收。真实WPF命令完成：导入/切图→选相纸变图→选圆框变图→快速连续选择最后生效→关闭动画显影→相纸和框锁定→窗口调色→窗口undo/redo→重新拍摄第二变体→切回旧变体→保存重开→PNG16/TIFF16/JPEG。结果WORKFLOW_RESULT.json；原完整项目及图片仅本地。

PNG16/TIFF16逐码误差0；JPEG MAE **0.555412**、RMSE **0.991633**，沿MAE5/RMSE15门槛，未放宽。输出767×884，不暗降原图；这里输入本身是已标明评审副本。对方/圆框的全部coverage=0像素都检验调色前后相等，不只检查两角。快速/HQ最新预览与同输入显影结果hash测试一致。

独立alpha参考21组：实际既有CandidateTone.Mix对Pillow12.3.0 AlphaComposite和FFmpeg9.0.2 overlay，含透明/半透明/不透明、原色/中间色；8位量化code差≤1。固定commit/源码/许可证SHA继承前轮真实源证据，ALPHA_REFERENCES.json与REFERENCE_SOURCE_PINS.json；参考仅外部测试、不复制GPL。它证明组合口径，不代表完整新工具算法准入/相纸科学标定；工具数学沿旧实现，组合新版本记录COMPOSITION_ADMISSION.md。

8张截图是实际发布DLL的1366×768/1920×1080 WPF离屏渲染（暗室、实时圆框、窗口调色、变体），已看图，不是原型/设计稿，也不冒充真实鼠标截图。实体录屏未交，不制作离屏动画混淆。

最终EXE隔离隐藏启动10秒存活、STARTUP_OK路线ReferenceColor，CloseMainWindow正常退出，见STARTUP.json。不会用这个启动日志替代物理UI交互验收。

## 文件与类型

本地LOCAL_FILE_LIST.json列最终本地交付文件的类型/字节/hash，含构建、图片、示例项目、输出与文本。核心app目录290个EXE/DLL/运行时/许可证等文件；materials为授权照片副本、WPF截图、示例.ptstudio.json、PNG16/TIFF16/JPEG及导出receipt/managed artifact。Git只提交源码、文本元数据和纯TRX/日志ZIP，**不提交照片、角色图、项目数据库或构建二进制**。证据ZIP成员全部text extensions并逐成员hash/解压读回。

## 尚未完成/明确范围外

- **首要验收缺口**：真实鼠标操作、物理前台双尺寸截图和简短真实录屏未完成，官方桌面native pipe故障。需要连接恢复后补验，不能宣称用户整套完成标准已闭合。
- 设计六系列60清单与实拿98/85不一致，授权已确认但此版只通用六框；未凑造60或临时扩角色框。
- 完整R3所有调整工具、直方图常驻/拖排序、变体评分色标/堆叠、任意PNG透明框导入、窗口几何高级工具尚未实现；五页签只是首版接通范围。
- 并行变体基于既有EditVersions承载，窗口内部undo/redo运行期有效；历史步骤栈不承诺全量跨重启序列化，重开保证当前冻结图和变体不丢。
- TIFF3受限输入、Fuji原生重复性、显示ICC分歧/物理DPI、真实断电/长稳依前轮边界继续开放。不宣称精确胶片、DWG全链或通用ICC。

**结论：可运行家用首版，程序化主要闭环通过；用户要求的物理鼠标与录屏验收仍待补，非完整正式发行批准。** 正常推送开发分支，独立核对HEAD另见PUSH_STATUS.md；不合并main、不覆盖家里原有工作。
