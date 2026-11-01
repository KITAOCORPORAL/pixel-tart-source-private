## 2026-11-01 家庭当前：大照片画布预览 v3（Europe/Helsinki；优先于下文历史）
- 当前产品源码 **146c0fd254de387ad5ccb9570cff6c54697389d4**；分支 home/r55-canvas-photo-preview-20261101，工作树 N:/pixart/guangjing-r55-canvas-photo-preview-20261101；从40919e48/产品69bbf229接续。测试支持 **5d94691b34f8762532199e991d7b2b44852b1b47**，文档HEAD与远端读回另记。灯台冻结，研究分支不合入，旧树/未提交资料保留。
- 新固定快照RenderVersion3，CanvasJpegNative1024.ArtboardCenters.v3。大baseline 8位JPEG从原图已校验读lease进行WIC native IDCT缩小，源ICC→sRGB后方向；24MP/33MP照片派生输入实际native为1500×1000 / 1752×1168，不能称原图细节或固定1024输出。小图走原v2数学，旧v1/v2保存PNG字节不重算，旧导出不暗改。
- 256MiB是渲染工作估算，不是整应用上限；最多32MiB/8来源任务缓存，每次命中重验来源hash，结束/取消清空。渐进大JPEG、大PNG超预算、RGB48+ICC明确拒绝，不放宽RAW/16位支持。
- 真实授权既有TIFF派生24MP/33MP JPEG＋透明PNG，基线0快照预算拒绝；发布版首次631.7ms，续接复核657.4ms/1快照。完整宿主渲染增量工作集87,908,352B/私有125,042,688B，进程峰值412,651,520B/416,169,984B。八轮新任务cache残留0、PNG hash相同、源句柄可独占重开；强制GC仅寿命诊断，不是预算准入手段。
- 相关自动测试去重 **119PASS/1既有符号链接权限FAIL/1缺LCMS参考NOT_RUN**；新发布绑定60PASS及独立新进程重开/手动更新通过，历史配置/重复夹具失败保留。独立数值有 **ICC max3>1码、4:2:0 FFmpeg max40/mean1.63357/P95=4超预设**，门槛未放宽；不能宣称开源/通用ICC科学门禁全过。Pillow native相同、float64合成/64行边界最大1码；CalibrationEligible=false。
- 完整win-x64自包含诊断包 N:/pixart/Guangjing-R55-CanvasPhoto-20261101/Guangjing-R55-CanvasPhoto-Diagnostic-win-x64.zip；同名目录Start-Guangjing-Diagnostic.cmd。294文件逐项SHA256与ZIP读回通过；全部模块/策划15/教程退役保留，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，无Setup/私照/研究程序。
- 唯一当前报告 docs/validation/home-r55-canvas-photo-20261101/FINAL_REPORT.md，原始TRX、数值失败、绑定、ZIP身份已归档；私人照片/预览/截图只留本地。OFFSCREEN不是鼠标/DPI/ACM/HDR验收；Browser/WebView/ComputerUse/CDP未启用。总体 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**，独立数值门禁及物理验收仍未完成。
- 输入补核：两原TIFF无嵌入ICC，JPEG测试profile/方向为新写入，原有真实ICC+方向源验证NOT_RUN；不以生成元数据冒充原生来源。

## 2026-10-30 家庭当前：策划画布一致性（优先于以下历史）
- 当前产品源码 **69bbf22935f3471013ad6bb3728f95b8b562acfc**；新分支 `home/r55-planning-canvas-20261030`，独立树 `N:/pixart/guangjing-r55-planning-canvas-20261030`。上游灯台eae800e7/产品1ec06eb5冻结，色彩研究未整合；测试支持f720f1a8、文档HEAD另记。
- 旧策划Snapshot独立合成器已替换为CanvasPixelRenderer有界公共入口：Hidden/Opacity/画笔/文字/色板/方向/crop/flip/rotate同合同，固定画板透明裁切。1400边/1.96MP/256MiB估算/1000层，STA串行取消，不为预览分配最大画板float。
- 新快照RenderVersion2；旧缺省1保留PNG不重算。实际落盘原子追加后才发布VM，手动更新保留旧版；项目/策划/操作代次拒绝迟到，缺源/来源变化/保存失败不发布半成品。原.ptplan v1范围、全部模块/策划15/教程退役保持。
- 同尺寸混合快照与旧DLL导出像素和PNG字节相同；缩小采样独立oracle0码；Pillow/FFmpeg两个固定实现alpha最大1码。发布绑定WPF42+独立2+完整宿主1+范围1、Core50+PV6 4通过；去重100PASS/1旧符号链接权限FAIL/1LCMS参考矩阵NOT_RUN，不能称全套PASS或实机。
- 家庭最终完整包：`N:/pixart/Guangjing-R55-PlanningCanvas-20261030/Guangjing-R55-PlanningCanvas-Final-Diagnostic-win-x64.zip`；同名目录Start-Guangjing-Diagnostic.cmd，WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，win-x64自包含，不Setup。
- 唯一当前收据：`docs/validation/home-r55-planning-canvas-20261030/FINAL_REPORT.md`。6张真实1366/1920生产控件离屏图已逐张打开；实际EXE/CMD OS启动与正常退出0通过。真实鼠标/DPI/ACM/HDR、通用16位/ICC未验，WIC既有RGB48+ICC拒绝保持。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。

## 2026-10-30 家庭当前产品（优先于下方历史）
- 当前产品源码 **1ec06eb5edcb0343d8587304e0536b554c3002d1**；家庭分支 `home/r55-lighttable-20261030`，独立树 `N:/pixart/guangjing-r55-lighttable-20261030`。测试支持5ceb1c55（src无差异），文档HEAD另记。
- 公司b1443ed4/产品e0ff14ab为本批上游，家庭旧7a6b98e9为其祖先（公司ahead13/无分叉）；旧树与dirty保全，研究引擎未整合。
- 照片/版本/状态/选框/上下文分层、明确22DIP展开、胶片数值/复位/真实任务、轻量镜边和幻灯条已修；列表/信息控件复用、通知去重、几何操作避免完整配方序列化。渲染数学/RAW/X-Trans/内存门禁不变；灯箱主干冻结。
- 家庭既有100自然RAW同机公司基线→最终94/6不变；warm366→134ms，冷全完成64.805→65.060s，取消退场400→560ms，部分内存上升，不称全面提速。Leica公司已修、六X3F仍OpenFile/-2/FileUnsupported；两Fuji旧native像素重复性OPEN。原片与自然截图只留本地。
- 发布7项目/20共享DLL绑定，84去重方法PASS（86场景执行），非全量/实机。22张完整宿主+8生产控件合成截图逐张查看；OS EXE及实际CMD正常启动退出0。物理鼠标/DPI/ACM/HDR未验，逐像素定稿仍有余项。
- 家庭完整包：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64.zip`；同名目录 `Start-Guangjing-Diagnostic.cmd`。WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，完整模块/策划15与教程退役保留，无Setup/私照/研究二进制。
- 唯一家庭收据：`docs/validation/home-r55-lighttable-20261030/FINAL_REPORT.md`（源码/测试/发布DLL/ZIP/文档HEAD分开）。总体PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。下文公司和旧家庭身份仅历史，不覆盖本节。

# 光镜R5.5当前实施大纲
2026-10-30 Europe/Helsinki。当前产品e0ff14ab57316fe47d112743f56add7d6ba26e0e，company/lighttable-lifecycle-20261030，测试支持dd191d41。来源fefd53f2/产品f7b53078，未回退家庭、未合研究分支/主线。

## 本批完成
完成通知只消费一次；幻灯Loading/Black/Arriving阶段暂停所有权、重复Space/停止/凑近切换；两选/单选比较与冻结来源；实际Uniform映射与等比圆镜；LostCapture/焦点/退出/Esc/异常手势收尾、稳定Z和跨项目history；可见反馈、右栏来源/预检/筛选；已载入卡选择不终止渐进工作。
实际长任务由真实Generate按钮启动、计算后受控边界，右栏开关/取消按钮参与；关闭不取消，显式取消无半成品。满100项不丢旧项。
灯板10–160%有实际显示结果，灯光不进proxy/比较/重算比较/剧照/输出PNG。复杂RAW/胶片域没有扩张，原异步、身份、缓存/内存预算不回退。

## 逐项移植状态与门禁
同一中央宿主/导航/托盘/22侧栏、台面几何/视口/撤销、SDR板材曲线/柔光、代理镜及机械播放阶段/合成音已接；完整玻璃折射散射、真实机械角度/快门美术仍部分。不得再总称PORT_MAP A-D全部完成。用户非商业许可及MIT署名保留，不代表商业发布审查完成。
透光叠底、原图ROI、EDR、色温、Spektra仍后续；Fuji重复性/家庭缺源/超大RAW安全支持OPEN。物理鼠标/设备DPI/ACM/HDR未验。

## 当前证据
最终7程序集绑定：68唯一PASS（Core32/WPF30/独立3/完整宿主1/性能2）；不是旧38加重复次数。完整宿主20张1366/1920截图OFFSCREEN合成输入，已查看并记视觉差异。实际CMD隔离启动/退出0/日志SHA通过。
完整自包含公司包与全部源/绑定/TRX/截图/ZIP/同机性能见../validation/company-r55-interactions-20261030/FINAL_REPORT.md。全模块/策划15保留、旧教程退役、无Setup，诊断PARTIAL不发行。

## 设计保全
原设计包../design/r55/R55-final.zip，相对索引R55_FILE_INDEX.json；aafe6fe6567bf33c609d76eda3f46e0d18e68f8a8af2de42defdd049454a014c。全局规则→第10章+00-16（01/15/16最新视觉）→第9章节点→其余R5.3。设计内20261022分支仅历史参考。指定DOCX字节缺件与Google另hash保持区分。
