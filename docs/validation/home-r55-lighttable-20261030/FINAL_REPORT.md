# 家庭R5.5灯箱收尾与完整包

2026-10-30，家庭Windows现场时区 Asia/Shanghai（本机 Get-Date +08:00）。总体 **PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE**。

## 当前身份与同步
- 家庭新分支 `home/r55-lighttable-20261030`；独立树 `N:/pixart/guangjing-r55-lighttable-20261030`。
- fetch核实公司 `company/lighttable-lifecycle-20261030@b1443ed4263dee08e31f3602425833522c481d00`，原公司产品 `e0ff14ab57316fe47d112743f56add7d6ba26e0e`。
- 家庭旧 `7a6b98e9d48e3f21f91cd69449939b26531e5804` 是公司祖先，公司领先13、无分叉；从公司HEAD建新树，不覆盖旧树，不reset/clean/stash/rebase，不合main/研究分支。
- 本轮**产品源码 `1ec06eb5edcb0343d8587304e0536b554c3002d1`**；测试支持 `5ceb1c55b19f8bc2f5c40239596e1d3bec25eff5`（只tests）；文档HEAD以最后推送读回为准，不冒充产品编译SHA。
- 主旧树3个未跟踪资料目录保留。研究树在工作期间出现自身dirty（见WORKTREE_SNAPSHOT），没有读取/整合/覆盖该研究成果。未发现可续的产品dirty视觉优化。

## 本批实际实现（限定范围，主干冻结）
1. 照片图像、照片外短版本名、图像右上白圈状态点、深青2DIP加外暗圈选框、独立上下文条分层；非选中无整卡细边，拖动隐藏选框/上下文条。图像阴影不再施加到文字/状态层，不再全卡0.98透明混合。保留直接Image仿射映射、代理细节上限与只读快照。
2. 22DIP窄条显式`▸/展/开`，独立模板防全局Button模板裁切；1366/1920/800/1366恢复测试。右栏胶片28DIP选择框，强度/颗粒数值输入、复位和可访问名称；真实任务期间参数及复位禁用、取消可用，任务状态实际显示；完成不伪造结果。镜片只加边缘阴影，幻灯只收紧控制条并加标题；未扩物理光学/胶片数学。
3. 每项更新不再内嵌全体选框遍历；帧按身份字典查找；移除Refresh临时重复订阅；固定列表按钮按variant复用、内容/顺序不变不重建；信息控件常驻只改文字。新增失败才通知Failures，成功才通知Frames；保留通知之间的取消/代际门禁。
4. 布局手势使用record语义比较，已验证冻结配方引用保持；只在私有geometry-only路径验证几何并更新`_layout`，不为每个mouse move完整序列化配方及Normalize全Layout。新固定/新结果、外部Layout、保存读取和真正渲染仍完整校验配方/hash、版本。未知版本拒绝、非法几何拒绝、一次手势一次撤销保持。
5. 截图发现失败提示与按钮重叠、胶片选项行高不足，已追加修复（产品1ec06eb5）；以最新Refresh包交付，2174da07中间包只留本地历史。

公司已通过的完成一次消费、普通选中不中止渐进加载、稳定Z、多选比较、暂停/迟到拒绝、源身份/缓存/预算、显影投影与重拍均继续使用。未重做暗室、未新建图库、未加载Spektra/SHARP/EDR二进制。

## 家庭同机同输入，不与公司1650比较
输入沿既有 `N:/pixart/Guangjing-LightTable-20261029/NATURAL_MANIFEST.json`，100份不同自然RAW。清单SHA256 `06532622f84b163951407056b5c4033e6b45ff0c2c28307a21c74551c78e6903`。逐源hash保存本机；未扫描其他目录、未用公司自建JPEG替代RAW。
先家庭编译公司基线并保全DLL，再用同一测量宿主绑定公司基线7项目与最终发布7项目，20个共同DLL逐hash相符。主表使用**正确隔离环境变量**复跑基线baseline-isolated，与最终1ec06eb5发布绑定final比较。初始未正确设置隔离名的第一轮数据/工作日志仍只留本机，未据此主张最终提升；曾使用KITAO_USER_DATA_ROOT并非AppDataPaths覆盖，后续全部改为PIXEL_TART_ISOLATED_RUNTIME和ROOT。首次测量因此曾写入默认用户目录的viewing-user-preferences.json（未在事前保全旧值，不能声称从未触碰日常偏好，也不能编造恢复）；后续正确隔离重跑，不删除或再改该文件。最终交付CMD已验证真正隔离。

|指标|公司基线10|最终10|公司基线100|最终100|
|---|---:|---:|---:|---:|
|成功/失败|10/0|10/0|94/6|94/6|
|初始布局 ms|237.43|334.54|22.97|32.13|
|VM首图 ms|741.22|869.97|566.46|560.52|
|首实际控件Render ms|769.20|889.87|609.95|582.93|
|全完成含控件 s|6.817|7.286|64.805|65.060|
|warm refresh+layout ms|46.12|14.47|366.34|133.72|
|末次实际Render ms|38.23|34.86|56.91|50.14|
|15ms采样WS bytes|523812864|517976064|869371904|885383168|
|采样Private bytes|437035008|451772416|852213760|865419264|
|采样managed bytes|36706128|31911040|43854872|44875424|
|可见实现卡/视觉控件数|10/158|10/189|30/258|30/349|
|取消命令 ms|59.71|14.60|1.85|1.73|
|等待退场 ms|59.71|14.60|400.24|560.25|
|冷Frames/Failures/Status通知|11/10/10|11/0/10|101/100/100|95/6/100|

4次摆放+布局两目标：10基线9.66/9.36ms→2.95/0.46ms，100基线19.15/10.65ms→1.87/1.02ms。不是物理鼠标FPS。每组单次新进程测量，不是P95；OS文件缓存未清，内存为采样峰值而非精确native allocator峰值。首屏10、冷全完成、内存及取消退场有回退，**不称全面提速**，不挑中间75ms当最终值。100主干保留原串行有界native解码，不可抢占native阶段，未放宽内存/X-Trans/LibRaw门禁。
公司基线与最终另外同机核验缓存，数值一致：100：94/6；缓存21,947,816 bytes；cold hash100/decode100，warm hash100/decode106（只重试6不支持源）；peakActive=1，释放后0。另次缓存核验不是额外P95计时样本。

## 旧7失败源逐项实测
旧家庭失败清单映射7个明确授权路径，全部存在、原source hash匹配；详细source hash和native stage/code在BASELINE/FINAL_SEVEN_SOURCES。
- Leica RAW_LEICA_M240.DNG：公司基线已恢复，640×428、orientation1，最终同像素hash；不是本轮修复。
- Sigma DP1/SD10/SD14/SD9_SRGB/DP1S与Polaroid X530六份X3F：两边均`OpenFile / -2 / FileUnsupported`，没有假称全部恢复、没有换数学。

## 像素与行为
- 同条件固定合成输入3份：公司基线与最终代理BGRA32及ProcessForExport像素hash均完全一致，源文件hash一致；比较像素内容，不靠容器/元数据文件hash。灯板/布局效果不进输出。
- 自然100成功94：92份代理hash一致；两份Fuji DSCF0347/DSCF0370不一致。原公司基线和最终各连续3次直接native解码也有不同（见FUJI_REPEATABILITY），因此仅证明旧问题仍可复现，不能证明所有自然RAW像素逐位一致或把变化说成元数据。未改解码代码，问题保持OPEN。
- 家庭真实两份输入自动走固定快照、旋转90°/缩放/已知坐标放大镜反算、比较、暂停、撤销重做、保存及独立进程重开。原Missing/Changed/Cancel/Generation/Selected回归保留；RAW胶片入口仍按真实不可用拒绝，真实生成结果用可分发原生8位合成输入验证，不伪造RAW成功。

## 发布绑定验证
- 43项主受影响（包含自然测量、7源、真实交互、Fuji重复性、已有工作流/显影）PASS；独立进程重开4 PASS；Core原32 PASS；完整宿主1 PASS；教程静态/控件2 PASS；教程首次/升级/重启同一方法3场景PASS；自然缓存1 PASS。
- 去重 **84个测试方法全部PASS**，86次场景执行；额外支持重跑39和截图重跑6不相加。不是全量suite，旧公司68不累加。本地首次环境未齐5失败已更正后重跑；旧Core32缺optin一次跳过已补optin32/32。
- 完整宿主原截图测试部分任务分支条件性跳过，最终只产22张宿主图，不称26张；另真实生产控件任务4张与镜片/幻灯4张补齐。尝试加强旧宿主fixture的两个失败和一次CLR退场崩溃仅测试探索，已还原该测试源，不作为产品通过证据（final-host2/3/4.log留档）。真实任务另由明确可用合成源成功执行、任务/数值断言及截图通过。
- 当前发布主DLL SHA256 `0ed41e35da01d8f25584bd84e51d8f50de2f48e8b326c31c72fed9204a07ced5`；全部项目/测试DLL身份见PUBLISHED_BINDING，后续test-only新增不改发布产品，独立文档HEAD分开。
- EXE及实际CMD的OS启动STARTUP_OK读到产品1ec06eb5edcb0343d8587304e0536b554c3002d1，正常WM_CLOSE退出0；不是鼠标点击验收。

## 设计对照与限制
权威 `docs/design/r55/R55-final.zip` SHA256 aafe6fe6567bf33c609d76eda3f46e0d18e68f8a8af2de42defdd049454a014c：全局、第10章/01/15/16灯箱，第9章节点，其余R5.3。
已逐张打开22张完整宿主+8张生产控件合成截图（screenshots），1366/1920：中性板、开灯、信息、胶片、比较、详情、镜片、即时草稿、已显影摘要、简易/专业保持中央图→节点图→胶片条；任务/投影控件另图。自然10/100截图只留本机并已打开，不进Git/ZIP。
本批分层/窄条/右栏数值/轻量镜边与幻灯条完成；**不是逐像素Grok全部美术签收**。卡尺寸/自动排布仍公司210×150和五列，不冒充原稿130×88七列已适配；共享比较/详情头部旧返回图标与文本贴近仍有美术余项，不扩本轮。完整折射散射、透光、EDR、原图ROI、Spektra、跨崩溃任务仍后续。
物理鼠标、真实设备DPI、多屏ACM/HDR、全产品全量验收 **NOT_RUN**。ComputerUse/Browser/WebView/CDP全程未用。

## 家庭最终完整包
- CMD：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64/Start-Guangjing-Diagnostic.cmd`
- ZIP：`N:/pixart/Guangjing-R55-Home-20261030/Guangjing-R55-Home-Refresh-Diagnostic-win-x64.zip`
- 294文件，77,241,892 bytes，SHA256 `608024acc3fdfc46393d36ecf977ac88541cb6b1af4407adca1224506267876e`；逐ZIP entry读回SHA256匹配本机发布目录。
- WholeAppDiagnosticBuild=true / PhotographerTrialBuild=false，win-x64自包含，完整7项目/全部应用模块与策划15保留，旧教程停用；包旁每次新建独立UserData-HomeR55，不读取日常库。没有Setup、私照/项目库、Spektra/SHARP/EDR附属二进制；公司E盘包不作为家庭交付。
- 研究分支成果保持隔离；本批结束后主干冻结，不继续堆特效/泛审计。

远端交付读回见REMOTE_READBACK；最终文档HEAD由git rev-parse/ls-remote给出，不等于上述产品SHA。
