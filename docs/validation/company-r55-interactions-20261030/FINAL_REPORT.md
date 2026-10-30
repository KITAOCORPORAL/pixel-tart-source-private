# 公司光镜 R5.5 交互修复及完整诊断交付
2026-10-30 Europe/Helsinki（原始执行主机TRX/OS收据保留UTC+8时间，不重标）。**本批具体修复集合完成；产品总体 PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。**

## 接续与证据身份
- 起点company/lighttable-lifecycle-20261030@fefd53f2e47f2866ef9d1fc154c87c79ba84c2f2；旧产品f7b5307865d59dc13e424f5643c19a31567cf203。fetch核对0/0，其他研究分支新增仅登记未合并；worktree/ref/dirty见本批清单，原公司/家庭树及其他dirty保全，无reset/clean/stash/强推/合main。
- **本批产品源码 `e0ff14ab57316fe47d112743f56add7d6ba26e0e`**（111faf54主修复、a7463d17反馈、e0ff14ab选择不停止渐进加载）。测试支持dd191d41仅改tests，src与产品源码无差异；最终文档HEAD/远端另记，不混为编译身份。
- 上批远端确实只留设计对照和TEST_SUMMARY，本批补齐原始TRX、最终发布绑定、完整宿主截图、设计档案、OS/CMD、ZIP身份和本报告；不把上批未提交证据冒充本批签收。
- 定稿ZIP原件与相对索引保存在 `docs/design/r55/R55-final.zip`、`R55_FILE_INDEX.json`，SHA256 **aafe6fe6567bf33c609d76eda3f46e0d18e68f8a8af2de42defdd049454a014c**。第10章+01/15/16灯箱、第9章节点、其余R5.3；附档历史代码分支不是回退指令。指定DOCX旧缺件仍登记，不以Google导出另hash假充原件。

## 六组具体修复
|路径|本批行为|实际验证|
|---|---|---|
|生成结果抢选|版本化完成通知只消费一次；补帧只补 pending 新结果，用户改选立即清pending；新任务/加载项目清临时完成状态；满100仍仅存新变体|真实Generate按钮→完整计算→受控提交边界→关/重开右栏→完成；改选后Prepare/迟到frames不抢回；满100回归|
|幻灯所有权|暂停停timer/当前声音，取消旧loading/black/arrival，冻结当前动画的当前变换；黑场保持黑；恢复按最后已呈现索引继续；手动翻页保持暂停；Space repeat不反复切；换放大镜/详情退出旧播放|TaskCompletionSource分别控制Loading/Black/Arriving，不靠长sleep；迟到成功/失败/声音无发布；暂停中手动next/Space repeat/切镜|
|多选比较|两张按项目Z顺序比较所选两张；一张才下一张；0/不足/大于2明确可见反馈；结果来源用完成通知的冻结来源快照，或严格原ID+recipe hash，移出来源卡不借别卡|四图选非邻接0/2、超量拒绝；移出来源后比源/新结果身份准确|
|放大镜|真实Image.RenderSize+Uniform矩形+TransformToAncestor，包含留白/图片比例/卡片旋转缩放/台面与视口；等比例仿射镜内绘制，不UniformToFill裁切；照片外及留白隐藏镜片/不夹边；代理徽标/细节封顶|横400×100/竖100×400/方200×200，旋转37°/卡片1.7×/台面.65×+平移已知像素反算误差<1e-7 DIP；X/Y尺度一致|
|手势层序|LostMouseCapture/失焦/窗口deactivate/退出/Esc/异常统一收尾；提交已发生手势一次历史，Esc/异常回滚；群体置顶置底沿原Z相对序，不依赖HashSet|真实卡片捕获丢失事件、一次撤销、乱序ID输入稳定Z、另一项目加载后无跨项目undo|
|反馈右栏|当前请求错误强制临时可见，过期不覆盖；选择更新标题/摘要/预检/按钮；固定列表筛选记本视图状态，不由每帧重置；选择已载入卡不取消有用渐进工作|真实侧栏按钮开/关/生成/取消；任务继续与无半成品分别验证，筛选1保持；新增progressive selection regression|

关闭右栏不等于取消胶片任务；本批测试不是只关一个无任务面板。任务仍复用原8位不透明JPEG/PNG/BMP后端和任务中心，真实完整源计算后才提交。没有放宽RAW/16位/透明/复杂图准入、未改LibRaw/照片色彩数学或资源预算。单张看图、比较、幻灯仍复用既有管线，没建新图库/比较引擎。

## 显示与光学界限
- 100%以上原Level饱和已改成**仅SDR灯板材质**的R55BoardResponse2：<=100%使用固定PanelWhite曲线，>100%利用剩余编码通道高光余量（最大80%）。10/50/100/120/160%实际颜色分别为#7F796B/#B8B09B/#E3D9BF/#EAE3D0/#F9F7F2（关灯）。不是物理lux/显示nit线性标定，也不把滑块数字当有效效果证据。
- 两份固定源码：Windows `cb8f4c89d845634688627f1c01225cb5d851248f` LightResponse.cs、原版 `8e1868df40e69e6b1a08e207d7ab05cf463532ab` LightResponse.swift，文件hash/实际读取函数/适配差异见OPTICAL_REFERENCE_COMPARISON。151亮度样本的PanelWhite对两个源码公式最大误差2.23e-16/0。Windows源自原版，**不是两个科学独立光学oracle**；Win2D/LightTableEDR未引入。原MIT全文及原作者/Windows移植署名在app/Licenses。
- 保留Windows2s冲光回落（原Swift1.1s，不声称时间逐点相同）、板边WPF轻柔光；**镜片目前是等比例代理采样+圆边，不是折射/散射物理光学**。显示不改照片缓存、比较、剧照或输出。
- 实际执行两次CaptureR54Still、重算比较、ProcessForExport及PNG编码，在灯光/亮度变化前后像素/PNG字节一致；不止验proxy hash，见LAMP_ISOLATION和测试源。

## PORT_MAP 逐项状态
|项目|状态|边界|
|---|---|---|
|中央宿主、底部来源托盘、22窄条、图标胶囊|已接并拍完整宿主|不是逐像素Grok美术签收|
|台面平移缩放/整理/确认平铺/多选/层序/手势撤销|代码与相关回归通过|触控/真实鼠标本机NOT_RUN|
|灯板10–160%/2s冲光/减少动态效果/轻辉光|SDR展示适配通过|非光度标定；开灯叠底透光后续|
|代理放大镜/Space凑近/Tab倍率封顶|真实坐标修复、最长边1600|圆镜边，无完整LightTable折射/镜边光学美术；细节上限明确|
|幻灯黑底/翻页/暂停/停止/机械阶段/合成音|所有权与状态测试通过|WPF平移动画适配，不等于完整ProjectionMotion角度/焦外/shutter重现；音频实际硬件听验NOT_RUN|
|只读显影泳道/四阶段/摘要/高级折叠|继承R5.5并补完整宿主/切版本回归|不增执行节点；七状态不是新光学算法|
|任意角度手柄/完整玻璃光学和动画精修|部分/后续|不伪装已完成|
|透光叠底、原图ROI、色温、EDR、Spektra|后续/禁用|本包无对应研究引擎资源|

## 最终测试与完整宿主
- 精确绑定7项目发布DLL后no-build：**Core32 + WPF工作流30 + 独立进程3 + 完整宿主1 + 同机性能2 = 68项唯一PASS，0FAIL，所选集合0NOT_RUN**。只计唯一测试名，不累加基线38和重复运行。原始TRX/每个hash/完整测试名见TEST_SUMMARY/test-results。
- 完整MainWindow 1366/1920截图：中央灯箱、托盘、折叠栏、关/开灯、信息/胶片栏、非邻接比较、代理详情、放大镜、真实任务运行/成功、只读显影泳道与摘要；已打开检查并对照定稿。曾发现比较/详情透明底透出台面与镜片徽标裁切，已修黑底/徽标内移并最终包重跑。
- 截图全部是**OFFSCREEN / 实际生产控件 / 明确合成输入**。没有ComputerUse/Browser/WebView/CDP，不以这些图宣称真实鼠标/DPI/ACM/HDR。OS启动+正常WM_CLOSE退出0、启动SHA读回通过；实际隔离CMD也启动正确EXE、独立UserData、正常退出0，见CMD_OS_SMOKE（不等同交互）。
- 上批测试中仍引用旧横排stage点击坐标和独立JSON窗，本批改为实际左泳道+右栏摘要断言；没有用公共方法存在当用户入口实现。光学阶段首次测试被系统动画关闭影响，改注入动画能力/可控delay，默认产品仍尊重系统减少动态。

## 公司同机同输入性能（不是新提速宣称）
同一新测量宿主：基线绑定上批f7b53078包，最终绑定本批发布DLL；100个自建JPEG内容/hash相同，另5个既有授权CompanyRAW。**不是家庭100自然RAW**；未访问家庭N盘，未把家庭49.33s复制为基线。

|集合指标|基线R5.5|最终修复|
|---|---:|---:|
|自建100成功/失败|100/0|100/0|
|VM首帧/全部|46.3/323.8ms|53.8/347.3ms|
|冷控件首图含渲染|78.9ms|79.8ms|
|冷控件全完成含渐进呈现|791.1ms|831.6ms|
|warm refresh/实际最后Render|798.6/50.2ms|1043.2/47.0ms|
|100采样WS/managed|193,536,000/20,862,416|187,781,120/22,639,968|
|RAW5成功/不支持/预算拒绝|3/1/1|3/1/1|
|RAW5首帧/全部|4.89/10.49s|5.77/11.67s|
|RAW5采样WS/managed|817,856,512/30,443,088|827,482,112/31,698,520|
|取消请求命令（自建100/RAW5）|17.8/4.8ms|22.4/5.4ms|

本批有刷新/RAW耗时上升，不能宣布全性能提升。100控制视口最终30卡，绘制布局/采样WS与managed分开；Private及private-minus-managed估算在JSON，不称精确native allocator峰值。取消数字不是强制终止不可抢占native解码。Sony OpenFile/FileUnsupported、LMAN1714.RAF原2.89GB估算预算拒绝，成功数与错误分开保留。原Fuji重复性/家庭缺7源/超大RAW仍OPEN，未借UI调整改RAW数学/预算。

## 完整交付
- ZIP：`E:\Kitao-Deliveries\company-r55-interactions-20261030\Guangjing-R55-InteractionFix-Diagnostic-win-x64.zip`
- CMD：同名目录`Start-Guangjing-Diagnostic.cmd`，每次包旁独立UserData；完整自包含win-x64。
- **294文件、77,138,826 bytes；SHA256 `e1904bfd40b77ec32b3cf06bcaa2a58fcce5718bd7cd3efe7b86de405c7a17f4`**，逐文件解压流hash全PASS。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，完整7项目模块和策划15源结构保留，旧教程退役。无Setup、私人照片/用户库/研究引擎；CMD烟测生成的隔离UserData在ZIP封装之后、未进入ZIP。
- 产品源码/发布DLL/测试支持/文档HEAD/ZIP分别登记。HANDOFF与总开发蓝图、R5.5大纲更新，以本报告为当前唯一收据。真实鼠标/设备DPI/多屏ACM/HDR及全产品全量测试NOT_RUN；总体诊断版不发行。
