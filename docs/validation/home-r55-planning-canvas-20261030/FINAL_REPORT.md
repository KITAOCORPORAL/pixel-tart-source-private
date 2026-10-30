# 自由画布 → 策划画布版本：渲染一致性与保存闭环

日期：2026-10-30，本机Asia/Shanghai。状态：**功能修复已交付；完整应用仍为诊断版，不发行**。

## 身份与开工检查
- fetch核实上游 `home/r55-lighttable-20261030@eae800e73c3947fa9c836b7efd56a27e341fad05` 无新增提交；产品1ec06eb5作为历史基线。
- 新独立树 `N:/pixart/guangjing-r55-planning-canvas-20261030`，分支 `home/r55-planning-canvas-20261030`。不覆盖旧树、未reset/clean/stash/rebase、不强推/合main、不整合色彩研究分支。旧树dirty在WORKTREE_SNAPSHOT，研究会话自身进度不当本产品成果。
- 已读AGENTS、POLICY、R55_IMPLEMENTATION、上批FINAL_REPORT和R55_SPEC；实际打开R5.5原档自由画布1366、情绪板引用拼贴草图。权威ZIP继续aafe6fe6567bf33c609d76eda3f46e0d18e68f8a8af2de42defdd049454a014c；未回退旧设计。
- **产品源码 `69bbf22935f3471013ad6bb3728f95b8b562acfc`**（dea26b27主实现，69bbf229修透明对象DrawingImage bounds）。测试支持 `f720f1a87a314bf719b7b17a62cecad451a3e435`（仅tests，产品src无差异）。最终文档HEAD以推送读回另记，不等于发布编译身份。
- 灯台主干冻结，Viewing/LightTable/Film管线源码未修改；本轮仅Canvas/策划快照与必要状态/保存边界。

## 复现与根因
旧PlanningCanvasSnapshot对420×300混合画板产1260×1260；包含Hidden离线图时抛IOException，取消隐藏图后仍错比例；旧逻辑按对象包围框扩画布、烘焙(20,23,27)暗底、忽略Hidden/Opacity并缺画笔分支。BASELINE_REPRODUCTION与baseline.trx保留真实失败。当前CanvasPixelRenderer.ExportPng已具备软笔与alpha处理，原导出像素hash为E4C3A7DA…CDD11，不能另造第三套合成器。

## 实际修复
### 共享内容/采样合同
- PlanningCanvasSnapshot新适配器仅调用CanvasPixelRenderer.RenderPreview +现有PNG编码/sRGB ICC，移除旧独立合成器。既有StraightExport调用同一RenderPixels，原尺寸时scaleX/Y=1，公式/层序/量化不改。
- 同文档依ZIndex顺序，Hidden不读源；Opacity、软画笔、文字、原顺序色板、旋转、flip、crop、方向1–8、Monochrome、原WIC嵌ICC到sRGB都使用现有合同。固定PixelWidth/Height边界，越界内容裁切；输出透明alpha，UI深灰Border不进入PNG。
- 自由画布显示过去重新排序色板/额外色板标签减高度，与导出不同：现调用公共DrawCanvasContent/DrawObject。保留文字/画笔的完整透明对象bounds，防DrawingImage把仅有墨迹拉满对象。选框/锁/错误/属性/图层依旧为UI装饰；编辑视口允许看到画板外对象，这是编辑状态，不进快照。
- 项目自有副本继续CanvasPlanningSource/PlanningProjectCopy的身份、根、相对路径与SHA验证，严禁缩略缓存/外链回退。新捕获只从指定CanvasId保存文件载入，不枚举所有画布、不拿未保存传入对象冒充提交。只接受当前project和canvas自有来源或当前策划自有来源。
- 原无hash外链在捕获时读源冻结hash，再渲染并在最终原子提交前重验。所有可见来源读lease跨File.Move持有，来源替换/缺失拒绝；隐藏来源不打开文件，离线不阻断。

### 资源与取消
新快照：最长边1400，最多1,960,000输出像素、1000层，工作估算output32B/px + current layer32B/px <=256MiB；单层超限明确拒绝，不悄降导出或读缓存。单个STA worker串行，渲染含队列20秒、捕获事务30秒；逐行/笔/外链hash取消。WIC原生解码不可强制抢占，取消/过期结果不得发布，不称强制实时中断。
最终发布实测16000×4000空画板→1400×350：float输出7,840,000bytes，不分配全板1,024,000,000bytes float；36.55ms，WS前244432896/后253657088，取消样本退场26.11ms。是进程快照而非native allocator精确峰值或P95，完整原始数值RESOURCE_MEASUREMENT。

### 固定版本与原子保存
- 新PlanningCanvasRevision.RenderVersion=2，SamplingContract=CanvasPremultipliedBilinear.ArtboardCenters.v2，记录实际尺寸，校验PNG头/尺寸/画板比例；未知RenderVersion拒绝。缺字段缺省1，旧PNG原字节保留，不暗迁重算。
- `CaptureCanvasDocumentAsync`绑定项目/策划DocumentId/EditorGeneration/操作代次/当前store，前后Flush已有编辑，再由PlanningProjectStore.AppendCanvasRevisionAsync在原项目gate内追加。生成成功不先UpdateDocument、不先写未提交快照draft；完成磁盘atomic replace后才发布到当前VM。保存失败旧版本/文件不变，不留半版本恢复草稿；编辑同时发生则将已提交版本合入待存文档，防后续Flush丢图。
- 新请求取消旧请求，Load/总览/导入根/控件卸载或离开时取消。晚到成功、晚到错误、跨项目及相同项目策划身份已变都不写新目标。
- 用户仍明确“插入/更新画布版本”；改变画布不会自动重算旧快照。必要UI只有状态、禁用重复插入、取消生成、实际尺寸与独立透明衬底，不增加导航/整轮灯台美化。
- `.ptplan v1`源码与打包范围未改：仅策划元数据内旧有快照字段，工作区/原图不新增打包。实际包断言planning.json/shots.json两项、快照可读，排除依赖仍在，不扩完整暗室版本联动。

## 像素门禁与数值结果
已有r53画布合同只覆盖原导出，Fabric/Konva仅文档/手势参考，未当alpha等价证明。本轮补固定Pillow12.3.0@bb1d8e8a与FFmpeg9.0.2@946fcce0，具体函数、完整commit、license/source/tool SHA和外部执行见NODE_COMPARISON/REFERENCE_PINS/NUMERICAL_RESULTS。外部GPL工具/源码不入产品/本Git；不把进程隔离当许可豁免。
- 1024固定RGBA（含alpha0/1/2/16/64/128/192/254/255）：对Pillow与FFmpeg可见RGBA最大1码、alpha最大0，预设门槛1，不放宽。
- 混合420×300：透明PNG、半透明图层、隐藏离线图、软笔、文字、色板、crop/rotate/flip、越界、自有图。同尺寸快照与现有导出**逐字节像素一致**。同机旧产品DLL与本轮导出像素hash、PNG容器hash均相同，分别BASELINE_EXPORT/FINAL_EXPORT。
- 独立float64像素中心oracle：420×300最大1码，仅3分量差；140×100缩小预览最大0码。预览不是“先全板渲染再resize”，不声称与该不同采样路线一致。
- 方向1–8、crop/flip/rotation/monochrome同导出精确；嵌sRGB PNG8与现有WIC路径同像素/alpha/实际ICC。RGB48+嵌sRGB TIFF在旧WIC合同下NotSupported，快照和导出同拒绝，未改解码/位深数学，不宣称通用16位/ICC已修。
- 屏幕WPF文字/抗锯齿不等于导出float逐位同构；公共内容几何已校验，alpha重心文字差0.331px，画笔/色板0。导出与策划快照的强精确门禁仍0码。
- PV4/PV5/PV6引擎源码未动，相关无操作精度/版本化Core和额外PV6四项通过；不是全历史真实RAW/色彩科学验收。CalibrationEligible=false。

## 发布绑定验证（不是全量测试）
产品7项目、20共享DLL逐hash绑定最终发布后执行：
- WPF受影响42 PASS；独立进程重开2 PASS（策划PNG在源离线状态不重读、画布原导出重开）；完整App真实入口1 PASS；`.ptplan v1`范围支持测试1 PASS。
- Core受影响52：50 PASS、1 FAIL、1 NOT_RUN。FAIL是旧Recovery…ReparseTargets创建符号链接缺系统权限，同一旧基线DLL也复现相同失败；未改权限或删除断言。NOT_RUN为NativeLcmsBradford外部独立参考矩阵未提供，未编造输入。另受限50项重跑全通过仅说明该子集，不抹原52结果。
- PV6额外4 PASS；去重总计**100 PASS / 1环境权限FAIL / 1 NOT_RUN（102方法）**。新问题相关WPF/保存闭环均通过，整个回归状态仍PARTIAL。旧公司/家庭数目不累加。
- 首轮失败保存：baseline真实问题、3次工作夹具归属/未初存/锁异常类型修正、宿主缺库/WindowState最大化、WIC16不支持等日志在development，没将测试通过当实机。
- 发布主DLL SHA256 `c4cd41c9a43bd18c758ed5b55e0091633142befd03686bff951299498c4b12ad`；产品源码、测试支持和测试DLL、Core绑定分开见PUBLISHED_BINDING/CORE_BINDING。

## 设计与截图
最终发布绑定完整App内，真实现有插入按钮→等待渲染/落盘→固定版本→改画布→手动更新→Flush→加载，最后保留两版。按真实client DIP1366×768、1920×1080分别捕获：策划第一版、自由画布选中图（左素材/中画板/右属性+图层）、策划重开两版，共6张生产控件截图，均逐张打开对照原稿。早期最大化导致图不是请求尺寸已修测试并校验ActualWidth，不拿错误截图冒充1366。
界面深灰是透明衬底，不烘焙；自由画布显示板外对象用于编辑，策划按板裁切。旧层列表文案/空状态等并非本轮全面美术签收，未另加模块。所需画板内容和版本入口已检，真实鼠标/设备DPI/ACM/HDR **NOT_RUN**。ComputerUse/Browser/WebView/CDP全程未用。

## 家庭完整应用交付
- 启动CMD：`N:/pixart/Guangjing-R55-PlanningCanvas-20261030/Guangjing-R55-PlanningCanvas-Final-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd`
- ZIP：`N:/pixart/Guangjing-R55-PlanningCanvas-20261030/Guangjing-R55-PlanningCanvas-Final-Diagnostic-win-x64.zip`
- 294文件，77,253,256bytes；SHA256 `101e54a674490aad47d14e11fe910afbb8e45b1cb7c9acb9905a580d9c52a327`；逐ZIP entry流读取SHA匹配发布目录。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false、win-x64自包含，全部模块/策划15/教程退役保留，不Setup。包旁每次新建隔离UserData-PlanningCanvas，无私照/用户项目/研究引擎，外部参考工具不打包。
- 实际EXE及实际CMD启动STARTUP_OK读回69bbf22935f3471013ad6bb3728f95b8b562acfc，均WM_CLOSE正常退出0；是OS进程烟测，不是鼠标/DPI验收。

## 遗留与未验证
1. 系统链接特权测试和独立LCMS参考矩阵门禁未关闭；真实指针/DPI/HDR/ACM、全部模块全量验收仍未运行。
2. WIC特定RGB48+ICC源不支持、非常大单图层预算拒绝保持明确，不扩RAW/宽域引擎。
3. 现有全尺寸导出自身64MP float预算不是本轮优化目标；新策划预览已独立有界。WPF内容抗锯齿不作照片色准证明。
4. `.ptplan v1`不含画布工作区/源图，旧自有副本绝对根跨机重定位不扩张。灯台/研究引擎继续冻结/隔离。

远端推送及最终文档HEAD见REMOTE_READBACK与最终git/ls-remote输出；不以文档HEAD替代产品SHA。
