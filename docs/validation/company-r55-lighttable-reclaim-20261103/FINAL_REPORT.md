# 公司旧灯台交互成果审查与最小收回

记录：2026-11-03T13:54:22+08:00（Asia/Shanghai）。**四个旧提交已完整推送独立保全分支；仅当前可复现且兼容的两项交互进入产品。没有整树覆盖或cherry-pick，没有新性能提升宣称。** 总体仍PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。

## 1. 基线与独立远端保全
开工当前产品home/r55-canvas-photo-preview-20261101@8d3d8753b7674ed4e6c1d1b69c80d83b9b735d25，实际产品源码3bf5d9a6a10124793434a43d782b22978d8707d0，clean。fetch两远端，读取AGENTS/POLICY、R55大纲、HANDOFF及最新反馈报告。DOCX专项不重新验证；独立Office继续NOT_RUN，本轮Office探测/接管/关闭WPS均0，不因此阻断灯台工作。

旧树E:/Kitao-Worktrees/company-lighttable-lifecycle-20261030真实HEAD27794d2f86313d7231f235b4fd1104eb4e95c106，父链线性：b1443ed4263dee08e31f3602425833522c481d00 →9299749c→a1a2a8f7→2ed1dc35→27794d2f。相对旧上游4ahead，tracked diff0，只有未跟踪证据目录。

先以普通push建立独立保全分支 **preserve/company-lighttable-four-20261103**，远端HEAD=27794d2f86313d7231f235b4fd1104eb4e95c106。仅推这4个已提交C#源码/测试增量，父提交已在远端。没有更新main或当时的当前产品分支，不强推、不改旧提交历史。PRESERVATION_PUSH记录实际ls-remote读回；首次读回命令把PowerShell -split误传为git参数，push本身已成功，修正后只读回一次、未再次push。

远端审查链接：
- [保全分支](https://github.com/KITAOCORPORAL/pixel-tart-source-private/tree/preserve/company-lighttable-four-20261103)
- [9299749c87833b4b637b83d0f086d06ac6e1025f](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/9299749c87833b4b637b83d0f086d06ac6e1025f)
- [a1a2a8f751b83bfce23982784d2d7f3cae86fb2d](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/a1a2a8f751b83bfce23982784d2d7f3cae86fb2d)
- [2ed1dc358cb6f7acaaaa670db25c8233f873f415](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/2ed1dc358cb6f7acaaaa670db25c8233f873f415)
- [27794d2f86313d7231f235b4fd1104eb4e95c106](https://github.com/KITAOCORPORAL/pixel-tart-source-private/commit/27794d2f86313d7231f235b4fd1104eb4e95c106)

## 2. 未提交资料另列，不自动上传
旧树未跟踪目录docs/validation/company-r55-polish-20261030共49文件：30张PNG＋19份文档/收据等。完整相对路径/大小/hash见OLD_UNTRACKED_INVENTORY.json；均仍只本地，**未纳入保全分支或本次产品提交**。PNG未按“看起来像合成”自动批准；可能含自然照片/输出或需进一步审查的内容，全部保守保留。没有上传私人图片、数据库、原始输出或第三方受限资源。

旧tracked源码diff0、HEAD/dirty保持；49文件逐项hash复核不变。旧分支相对其旧上游仍显示4ahead，这是上游指针未改，不再代表四个提交无人可审；新保全分支已经可查看。其他dirty树不reset/clean/stash，不整合色彩研究，不合main。

## 3. 逐提交对照当前产品
|旧提交|有效内容/当前情况|本轮决定|依赖与风险边界|
|---|---|---|---|
|9299749c perf/视觉收尾|JSON状态比较热点、卡片内容key和固定列表复用已有后续2174da07bf1f88bf004d67bf146083d292a8f35b替代；当前还含geometry-only placement验证快路|不移植该提交；用既有相关行为回归确认后续实现仍在|旧提交并非纯性能：还带UI光影/机械动画/状态呈现、诊断profiling和旧面板结构；不能用提交名推断全提交不影响呈现或通知路径。旧指标仅历史，本轮不取代同机新测量，不宣称新提速|
|a1a2a8f7 同项目保存保持选择|当前SessionPath每次保存仍无条件清空灯台选择，实际可复现|只移植保留有效选择的意图，并适配当前结构|旧版仅检查成员ID，不能阻止不同项目复用相同Target/Variant ID。本轮以真实Targets Reset清空选择，保存只检查当前成员；新事件关闭时解绑，不改VM保存/任务引擎|
|2ed1dc35 变换照片测试|仅旧完整宿主截图夹具选择改到旋转照片，无产品运行代码|保留原提交供审查；只复用“明确选中已变换照片”的测试意图|不复制依赖旧100输入、旧样式/测试成员的整套截图宿主。当前新用3个确定性小PNG、27个变换/视口组合验证|
|27794d2f 工具条/标签边界|当前工具条仍按未旋转150高度推算，实际与旋转照片/标签重叠|只收回真实变换内容union bounds定位算法|旧helper以Border照片为前提；当前是独立Image/marks/label层。适配当前Image/标签实际RenderSize，保留现有132照片区/18标签及仿射取样，不抄旧126/6/18卡片视觉布局|

四个旧SHA没有成为当前产品的祖先；本轮是逐段最小移植，不是隐式合并整条旧树。旧源代码和完整父链保留在保全分支，当前接入只有StudioViewingTableView.R55.cs一个产品文件。

## 4. 先复现再接续
raw/baseline-reclaim-v2.trx两项FAIL：
- 保存：两张固定变体已选中，调用与自动保存相同的SaveSessionAsync后，SessionPath通知把SelectedVariant和选择集合清空。baseline-v2/SAVE_SELECTION记录期望ID与实际null。
- 工具条：合成竖幅在37°/0.8缩放下，其实际照片/标签union与工具条相交；baseline-v2/CONTEXT_BOUNDS保留坐标。不是仅依据静态代码猜测。

最小产品改动：
1. 同一工作区保存时，仅当选择已不属于当前Target/Variant集合才清除；首次保存和后续保存命令均保留有效多选。
2. 真实Targets集合Reset仍清除灯台选择、临时查看与既有手势状态。实际LoadSession走Targets.Clear，因此不同文件即使复用完全相同ID也不串选择；关闭时解除外部集合事件订阅。
3. 工具条使用现有照片/标签内容经WPF TransformToAncestor后的实际包围盒，按当前工具条实际宽高优先下方、上方或侧方，并约束在可用视口。大小/布局变化仅更新几何，Margin未变不重复写；不触发照片解码或重建另一套渲染。

没有改photo shader、取样数学、recipe、源文件/固定PNG、Core/VM加载队列、取消/缓存预算或任务桥。界面光影、胶片面板样式和机械动画未搬入，也不另起UI重绘。

## 5. 有针对性的最终验证
最终发布DLL20共享文件逐项hash绑定后，**15唯一方法/15执行PASS，所选0FAIL/0跳过**：当前新增2、后续替代行为2、取消/句柄/缓存3、现有交互保全3、布局保存重开1、独立进程重开1，以及全壳/15区构造/教程退役各1。TEST_SUMMARY列精确方法；不将开发复跑累加，不是全仓泛化验收。

- 同项目首次保存、后续保存命令保留多选和当前固定版本；Undo/Redo后冻结recipe hash和对象引用保持。
- 真实项目B由A文件复制而来，Target/Variant ID相同；LoadSession(B)后仍清空选择及旧Undo。重新选中后再重开A仍不继承旧选择。
- 工具条覆盖800/1366/1920客户端宽、37/90/154°、0.6/0.8/1.4缩放，共27组合，均在视口内、有可用外部空间时不与照片/标签union相交；同一BitmapSource引用保持，ViewingDecodeCount无新增。
- 渐进载入选择不抢占、显式任务取消不发布半成品、close期间decoder结果不回填/缓存释放、mtime和长度恢复后的真实源句柄变更仍被识别，必要回归通过。
- 发布版1366×768和1920×1080两张生产控件OFFSCREEN合成图已打开查看；选中图为明显变换后的竖幅，工具条与标签不相互覆盖。不是自然照片美术签收或真实鼠标/DPI。
- 本轮没有接入性能优化，未跑/宣称新性能成绩。替代行为测试证明复用语义，不把TRX耗时或旧10月30日成绩改名为提速证据。

开发保留项：首版新测试漏using RAWSelectionAssistant.Services导致ViewingPreferences未解析，原编译失败log保留；补引用后原基线得到上述两项实际FAIL，再修产品。未放宽断言。保全push首次读回表达式错误也如实记录，未伪报实际push失败或重推历史。

## 6. 冻结、未执行与交付
- DOCX失效关联/延迟警告源码与既有证据原样保持，本轮没有DOCX专项测试、独立Office探测或WPS操作；Office继续NOT_RUN，不阻断本轮。
- 鼠标、设备DPI、多屏/ACM/HDR仍NOT_RUN。离屏坐标/截图不等于实机。自然RAW、20MP、色彩研究、数学调整均未执行或整合。
- 产品源码 **26c034873a469d428b4e1975d1772a55f0128b66**；测试支持 **918d33025a45b528da417a4cd5f7638a6112e7db**；保全分支HEAD27794d2f86313d7231f235b4fd1104eb4e95c106。文档最终LOCAL_HEAD/REMOTE_HEAD另记，不混为源码身份。
- 完整自包含win-x64诊断包：E:/Guangjing-Lighttable-Reclaim-20261103/Guangjing-R55-LighttableReclaim-Diagnostic-win-x64.zip；294文件，ZIP79,715,385B，SHA256 **CAB9C4743D86472ED8BBE14C3C8FB9A082E7D9023B61CB87AB7386F9FC7D9F70**，目录与ZIP每文件hash读回PASS。
- 同名目录Start-Guangjing-Diagnostic.cmd公司启动。固定相邻UserData-LighttableReclaim隔离根，关闭重开沿用，可仅当前进程指定绝对GUANGJING_DIAGNOSTIC_DATA_ROOT。实际CMD/EXE启动产品SHA日志与正常WM_CLOSE通过，EXE0/CMD0，仅OS烟测。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false；全部模块、策划15项、教程退役保持。无单模块包、正式Setup、私照/数据库或研究资源入包。
- 新报告和大纲/HANDOFF追加使用Asia/Shanghai，历史收据不改；整体仍诊断PARTIAL，不将保全或交互回归写成发行批准。
