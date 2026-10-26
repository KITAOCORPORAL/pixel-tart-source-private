# 光镜全软件文案与布局精简

2026-10-26 America/New_York。**已交付实际产品改动和完整主程序；整体PARTIAL，不能发行、无安装包。**

## 源码与范围
- 家用远端独立核验home/planning-owned-canvas-20261025=a2c3fd00783b2091ecc7dc3a86e9df52bb7a71ac，公司dad3dd4c到该HEAD落后0/前进4；直接继承8886226d画布自有来源修复，没有从旧分支重做。
- 新独立分支company/whole-app-ui-copy-cleanup-20261026；50旧树HEAD/branch/status、423脏文件hash、旧数据根和ZIP保持。Git本地独有历史分支明确保留，未混入本包；无reset/clean/stash/rebase/main merge/force。
- **产品代码SHA b9cb1e25e811903b117aef3afeb203aadf7d0790**。测试合同修订SHA 0505216f8aacbcc67ea10f7584a8469767afe923：仅旧逐字教程测试/HANDOFF更新，src与产品SHA零diff。最终EXE用productCodeSha源码标签，src实际一致，不把证据HEAD作产品SHA。
- 开放与已关闭issue均通过GitHub API核对为空（无新依赖、无陌生交互实现）；已读既有交接、指定5份家用状态/历史Scenario11和相关代码/历史提交。仓库无统一根HANDOFF，已创建索引式HANDOFF.MD连接旧资料，不复制历史“最终状态”。

## 产品修改摘要
1. 策划侧栏只常驻“导入项目包”，Auto行顺序为标题/导入/搜索/筛选/列表，删除162px硬避让；独立根导入/打开进入更多菜单，原AutomationId随动作保留。取消仅IsPackageTaskRunning期间显示，取消/拒绝/失败finally回到idle。列表#短ID可见稳定区分同名，完整ID详情/提示；原包清单和外链/副本/不可逆确认不删。
2. 工作台/归片分片/日历/联机/在线选片/收支/工具箱/发布/压缩/拼图/整理/素材库实际可达页面删重复教程与实现解释。Color Studio只缩短固定中文版标签和只读提示，保留现有布局，不改RAW/ICC/仿色数学/PV4/5/6，研究默认关闭边界不变。帮助保留真正教程/反馈隐私；输入诊断整个section默认Collapsed，仅原INPUT_ROUTING_DIAGNOSTICS+session gate显示。
3. 设置去重复说明并收紧530px高度；策划重复大标题仅在预览文档保留，空subtitle不生成空块，空配色/影调不留空占位；空态顶部留白收紧。资料/压缩弹窗保留格式/安全限制及命令。
4. 策划副本错误正常显示“项目副本不可用，请检查文件”或“已保存但无法预览”，完整异常在日志和展开详情；附件原路径/hash入文件详情。画布SourceErrors保留完整技术原因，画面仅短状态，更多菜单可看详情。底层禁止回退/哈希/身份等未放宽。
5. 画布先测量，再做80ms布局事件合并、取消过期owned decode、跨画布/来源key立即清图。布局/缩放不每次重复校验解码已加载同来源，显式LoadPreviewsAsync/加载/重开/放大/快照仍按家用安全reader核对；192MiB/128已有预算保留，不另建大型缓存，不改decoder。

18行逐模块入口/删改/保留原因/验证见MODULE_CHECKLIST.md。未可达FTP/旧批量重命名/删废片模板未冒称清理完成，Preview/未实现门禁未放开。

## 本轮针对性最终结果
|范围|总发现|选择/结果|通过|失败|条件未执行|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|Core策划/画布/导航/工具相关|1760|216/216|216|0|0|0/0/0|
|WPF策划/画布/导航/布局/命令|1753|200/200|197|0|3|0/0/0|
|合成缺副本错误态补验|独立精确方法|1/1|1|0|0|0/0/0|

最终发布29产品DLL绑定及读回一致；补验使用同发布组件。WPF新增行为测试校验普通主导入、idle无取消、次要动作不常驻、Auto行无重叠、860/1100/1280宽及125/150%逻辑缩放；20路由可达，24张路由离屏图+普通策划项目图；参考/画布错误态图片已目视检查。代表planning/settings/tether/toolbox/finance前后图在BEFORE_AFTER_FINAL.png。所有图是IN_PROCESS_WPF_RENDER合成，**不是桌面截图或物理DPI证据**。窄窗侧栏按原响应式进入drawer，不算裁切失败。

公司现有符号链接权限下3个家用原失败夹具实际在本轮Core跑通，未改系统策略；旧家用失败不覆盖/不刷绿。家用原4条件未执行按历史保留，本轮选择条件实际3NOT_RUN：授权PhotoGrade照片manifest未提供（不扫描私人照片），两大图opt-in未开启（按要求不重跑公司大图性能）；家用peer/DPI门禁不额外开启。

首轮Core213PASS/3FAIL、WPF193PASS/1FAIL/3NOT_RUN保留，4失败是旧静态文案逐字镜像。只将过期教程substring改成Add/Export/Start/空状态/任务绑定合同；保留批量压缩CreateNew/AutoNumber/noDelete和真实CollageExport3项源字节不变/不覆盖测试。没有为了文案删除安全断言。候选布局1项测试误要求隐藏搜索框宽度>100，改成验证响应式零列或可见几何，原失败保留。画布测量初次反射类型namespace错误未执行有效测量，日志保留。

## 画布实测（不是公司大图性能）
同机合成12自有JPEG对象、200个Editor.Move/SetZoom事件、持有解码门禁：
- 原ownedDecodeCount=248，线程池采样峰值14，事件派发6.6745ms。
- 最终ownedDecodeCount=36，线程池采样峰值4，事件派发14.3743ms。
- 36包含12初始、12显式重载和12坏副本复验；布局burst不再增加每事件owned decode，取消过期工作。event派发未更快，不夸称拖动耗时改善；测量证明排队/解码压力下降，非实鼠标fps。
- generic preview provider调用始终0；修改副本后显式reload清掉所有图并保留详细原因，源身份/hash/禁止回退不变。Source key变化立即clear，旧迟到外链不能覆盖自有来源，旧owned-canvas5项原行为通过。

历史家用Scenario11仅引用HISTORICAL_HOME_SCENARIO11.json：source4f57ca56，10000次1292.9921ms，Passed。本轮未重测，不作当前版性能。

## 原生范围
实际可用工具为本机@oai/sky经node_repl，文档已读；未调用家用不存在mcp__cua_repljs，未控制Vivaldi。新完整产品EXE PID15732在native/runtime启动、控件树可读；一次真实退出教程点击仍coordinate input geometry is unavailable，即停止，无重复空截图。

原生当前根导入、普通列表JPEG/shot/放大、镜头导入副本/编辑保存、发送画布调布局保存、正常UI退出/新PID重开、删除合成外链/cache后重开、快速A/B、原生DOCX/异地包导入全部NOT_RUN。自动A/B/副本原件/cache删除重开回归不是原生新进程。最终二次test-only rebuild主要EXE/WPF/Core/模块hash与原生尝试逐字节一致（NATIVE_FINAL_EQUIVALENCE.json）；不冒称本轮操作完成。

.ptplan v1不含画布workspace，继承家用同机CanvasPlanningSource绝对PlanningRoot，不称画布跨机迁移PASS。

## 完整交付
Guangjing-UI-Cleanup-Final-Diagnostic-win-x64，Release win-x64 self-contained，WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false；290app文件、264声明运行资产缺失0。统一Start-Guangjing-Diagnostic.bat仅进程隔离UserData，附合成A/B v1包、JPEG/PNG及汇总，不含私人照片/DB/UserData。不是Setup，不发行安装包。

下一步：正常可交互公司桌面按MANUAL_RETEST.md完成原生A/B→镜头副本→画布→退出重开链；继续删文案不能破坏功能/安全边界。画布审计/缓存总体GC、WPS/物理DPI及全部发行门禁未关闭。产品、证据、最终分支HEAD及独立push读回分开列。

## ZIP最终读回

325文件、76353237字节，SHA256 fa2be614f7fd910eb597831d8851e39baf6cb0449a79d726c8367fd8b48a5552，全部条目读回一致。ZIP内报告为打包时快照，外部报告后附自身身份避免循环hash。产品/测试修订身份分开见FINAL_IDENTITY.json。

## 推送读回

产品b9cb1e25e811903b117aef3afeb203aadf7d0790、测试合同修订0505216f8aacbcc67ea10f7584a8469767afe923与独立证据ff03edc48f2172af068e5b344c33e99318747e0c已正常推送。独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD且干净。后续仅封存读回，产品src与最终发布组件不变。
