# 策划项目副本功能交付

2026-10-22 Europe/Helsinki。**产品实现及受影响自动验收PASS；实体桌面NOT_RUN/BLOCKED；完整项目便携和正式发行未完成，整体PARTIAL。**

## 起点与产品改动

fetch后validation/guangjing-whole-app-followup-20261022最新HEAD 6cdb3f7bab745dc28df9797f278c85967d130a29；company/planning-summary-images-20261021为13ec9ca1c678622e5c7b884d8f69882c409c33a4；src/tests完整diff为空。新独立工作树E:/Kitao-Worktrees/planning-project-copies-20261022，产品分支feature/planning-project-copies-20261022。44旧树及423未提交文件逐hash保全，未使用已有脏owned-resource树，不merge main、不force/reset/stash/clean。

**产品提交 3710317f8c2681141cb7d0e959e78ab9f9bb6037**，16个产品/新增测试文件，505增49删。证据和工具另提交，未把发布二进制、数据库或私人素材入Git。

## 用户入口与行为

现有参考图/灯位图/服化道“添加本地图片（副本/外链）”、文件区“添加文件（副本/外链）”，及镜头本地参考添加，均先原生选文件，再明确询问导入项目副本；否时再确认仅外链/取消。不替换现有资源模型，不静默转换外链。

PlanningDocumentReference/PlanningAttachment附加ProjectCopy收据：ProjectId、DocumentId、原ReferenceId/AttachmentId、Kind、OriginalPath、RelativePath、SHA256、字节数、ImportedAt。原Source/Path和原引用ID保持不变。StorageRoot仅运行时绑定，不持久绝对目标根；相对副本位于当前PlanningProjectStore根project-copies/项目/文档/类型/引用ID/hash.extension。

复制源以FileShare.Read拒绝普通写删，完整流式hash/长度/时间戳检查、64MiB限制、重解析祖先/设备/备用流拒绝；独有.part并fsync后无覆盖提交最终副本，最后同项目门禁原子替换策划JSON。批次失败/取消/保存失败在同一gate回滚本次新增副本，旧引用/文件保持。没有完成复制不创建有效收据。不同名同hash同来源幂等；同名不同来源按ID/hash隔离不覆盖；同ID换来源或重复来源内容变化均明确拒绝，不悄悄更新旧快照。新图/附件只在存储提交后进入VM，复制期间新编辑合并保留；既有资源undo/redo重绑根。

保存关闭重开与搬迁根后按当前根解析，校验缺失/变化时明确不可用且不回退到存在的旧外链源。普通附件按校验后路径打开/定位；未导入外链保持旧语义。15区汇总注明副本/外链/原来源/相对路径/hash；参考按参考区、情绪板、灯位、造型或镜头实际引用分类呈现；普通附件不嵌入内容，只列名称/状态。

自有参考嵌图/缩略图复用既有安全PNG规则：单帧、64MiB、最长边16384/4000万像素；非PNG保留完整可访问副本，汇总明确不在当前嵌图范围，不自动转换。原PlanningDocxAdapter/Pages/摄影DOCX往返/PDF组件零diff，安全PNG共有decode抽取但限制不改。

## 本轮最终发布组件回归

|范围|程序集总发现|选中/实际|通过|失败|未运行|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|受影响存储/策划Core|1731|36/36|36|0|0|0/0/0|
|受影响策划/文档WPF|1729|84/84|84|0|0|0/0/0|

29条最终发布DLL与测试宿主逐hash绑定，最终再次读回相同。新Core8项、WPF3项已包含在36/84内，不重复相加。只跑受影响夹具，不重复扫描122动作或全软件全套。原始适配器发现、TRX、runsettings、日志、完整命令及每个测试身份封存RAW_TESTS.zip，统计由实际适配器(testId,FQN,displayName)多重集合核对。

覆盖重复/并发导入、同名冲突、同ID换来源、来源变化、缺失/损坏副本、锁定无权限/OS拒绝、超限/空文件、复制途中取消、保存失败、源/目标重解析点、伪造路径/身份。锁拒绝使用真实FileShare.None/无删除共享，不修改系统ACL；没有声称所有真实NTFS权限组合或恶意并发重解析竞态已实机穷尽。

首轮新Core5/7、WPF2/3原始失败保留：取消TaskCanceledException是OperationCanceledException子类、Windows拒绝替换可报UnauthorizedAccess而非IOException、新导入失败正确不产生空策划文件。仅新增测试对标准类型/空状态假设修正，原旧回归断言未改，后续最终11新测试全部通过。编译变量遮蔽/缺测试布局助手原日志也保留。

## 合成项目实际闭环与输出

最终测试通过生产选择命令导入一张PNG参考和普通TXT附件，另选一张图仅外链；真实JSON store/隔离SQLite保存并新VM重开。完整合成项目目录复制后搬迁（包含合成SQLite及planning副本），删除原测试外部源，再以搬迁数据库/当前根新VM重开、WPF查看参考区/文件区、汇总导出。源hash在复制前/后核对一致；独立脚本再次核对搬迁副本hash，ReferenceId/AttachmentId/DocumentId稳定。没有改个人源文件，删除仅测试合成外部源用于排除意外回退。

项目ID83fded10-42ad-418f-894f-70547e3cecbf；文档c045240f-b83b-b211-4ac3-54a7897ccf82；参考d3f4ea07-2f32-4bbe-8c59-ad7a690e4867；附件40dcd692-05f7-4975-a8e7-4edb4b21ed56。

PNG源/副本SHA256 663a7d1c0cb9333f210b635ba072777d11e3152f6bbac6f4bd8b81bb5e30e0b7；TXT源/副本4d2c2f8482beac7983b4c95e76904dbde267a7b53da3328776ad6839aec1d87b。实际DOCX15标题、2张drawing/media，分别04参考图/06灯位图；普通TXT有名称/状态/hash，外链缺失明确显示且不嵌入。DOCX SHA256 c9bd54e2c1ed0e4d5a99678205dbf40606300964f5dce9cf6bc7a13d69c54744。导出前后策划JSON hash不变。

六页产品回读PDF由独立Poppler渲染检查，无明显空页/重叠，两个自有参考图正常。仅文档渲染检查，不是Word/WPS兼容或实体桌面验收。合成完整目录、DOCX/PDF和RESULT另交本地Synthetic-ProjectCopies-Sample.zip，数据库不入Git。

## 实际桌面与边界

新完整EXE实际启动，7加载模块hash与包一致。初次get_window_state返回accessibility:null/screenshots:[]；刷新选择后专用capture报FrameArrived timed out: timed out waiting on channel。未猜索引点击、未通过设置绕过首次教程，人工选择/系统文件框/打开附件/搬迁操作均NOT_RUN。旧geometry unavailable历史错误仍保留在基线，本轮不冒称重新得到该错误。

普通失败/取消已验证，但硬断电/进程强杀时可残留未引用.part或完成但未提交的orphan；它们不会被解析为有效引用，尚无自动崩溃清理UI/事务恢复日志。无跨进程存储互斥/恶意内核级路径竞态保证。暂不提供一键完整项目导出/导入/跨机DB迁移用户入口；本轮自动测试的完整合成目录搬迁不等于通用项目便携交付。非PNG不自动嵌图，库引用/财务附件等未选资源未扩展。相机/服务/WPS/安装/全软件发行原阻断保持。

## 新完整诊断包

Release win-x64 self-contained，WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，统一启动器隔离UserData。290应用文件、264声明运行依赖缺失0，完整壳非策划单页。包与合成样例身份/逐文件清单、ZIP读回在ZIP_IDENTITY.json、SAMPLE_ZIP_IDENTITY.json。不是Setup，不能发行。源码/产品提交、证据提交与包身份分别记录。

## ZIP封存读回

完整诊断ZIP 76823749字节，323文件，SHA256 f384a99a7e5886cfc1c5a35cc5cd02721fd3624a3aff4a061cfd4f9d112bef60，逐文件读回全部一致，不含UserData/数据库。完整合成样例ZIP 1144011字节，11文件，SHA256 10a28e76dae5f53a7c531ec52f37684989dc0d4555b885c06eb5242db9dcfaad，包含明确合成SQLite/JSON/副本，另包本地交付、不入Git。ZIP内报告为打包时快照；外部最终报告后附ZIP自身身份，避免自引用。
