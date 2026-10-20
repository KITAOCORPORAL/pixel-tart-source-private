# 家里第一阶段：安全同步与完整诊断构建

记录时间：2026-10-20T19:54:36.685177+08:00（Asia/Shanghai）。**整体PARTIAL；发行BLOCKED；仅诊断版。**

## SHA与工作树

- 独立查询验证远端：`46ba7e29d93660f47f836bd4f60169c894f371ca`，与你提供的SHA一致，无新增提交需追读。
- 产品冻结点：`96e84b14a0cbb040b97cc531efeb31b1f4144367`。新工作树先检出报告分支，再干净detach到冻结点发布，随后返回家用分支；没有修改源码。
- 新工作树：`N:\pixart\guangjing-whole-app-home-20261020`；家用分支：`home/guangjing-whole-app-sync-20261020`。
- 原暗室开发树停在`755b1d80ead4534665373cd54dcd8353a4f1288b`，未挪动；其远端已是`a1e52238616b8d22f09b92595a85e76c7d9c1460`，该差异已从验证分支同步到新树，不在原树pull覆盖。
- 16个旧工作树全部保留。主树211个文件均为原有未跟踪文件，逐文件hash与状态/HEAD复验未变；其他15树干净。旧Release/暗室包未覆盖。
- 产品冻结点与报告HEAD的`src` tree完全相同；报告不是额外产品功能。52个远端ref重新检查与公司记录SHA一致：42为祖先，5可比较但非祖先，5无共同祖先继续BLOCKED。

## 今天确实同步到家的代码

相对家里原暗室HEAD，产品源码有五个提交、16个源文件差异：

1. `d2e52278`：R3视觉控件与CLI护栏（仍待桌面验收）。
2. `4c506701`：原生导入对话框与可重试失败提示。
3. `07c11d75`：面板布局持久化、最新变体选择保留。
4. `f6adf867`：完整MainWindow诊断壳、显式UserData隔离护栏，拒绝单页试用壳混用。
5. `96e84b14`：已有复现的主壳/主题对话框/暗室合同修复；策划阻断保留。

策划15分区、P3、V3等既有代码随冻结快照完整落在新树，但非本轮新增实现。未自动合并photo-engine-followup、导入加固等未集成分支，未动main与色彩数学。

## 家里构建与身份

- Release/win-x64/self-contained；WholeAppDiagnosticBuild=true，PhotographerTrialBuild=false；发布日志无warning/error。
- EXE与关键DLLProductVersion均指向`96e84b14a0cbb040b97cc531efeb31b1f4144367`。
- app共290文件：275 DLL、2 EXE、3 JSON、1 Markdown、9 TXT；deps.json的264运行资产全部存在。
- 家里SDK10.0.401、自包含.NET10.0.12；公司依赖记录.NET10.0.10。不同SDK/编译器/运行时及绝对源码路径影响构建字节：31文件hash相同，258个共同文件hash不同，另一个版本化调试运行库文件替换。没有逐字节一致承诺。
- 公司指定E盘ZIP不存在，未下载/同步；本次为从冻结点本地新建完整包。

| 组件 | 家里SHA-256 |
|---|---|
| EXE | 7C3F7290E3CD1469861479C925826880C6E9FB81E0FB8D4858BED50CC68CF020 |
| WPF | 22E6752A9CD18358968E2276180195A9079FD51345A054C03E3412D8FE471AF1 |
| Core | 2E0C2DA079851B8350D91ADA812818E1A5020D4AF42FF16358AB9F149E4CF963 |

完整清单见HOME_RELEASE_MANIFEST.json、HOME_COMPANY_HASH_COMPARISON.json。原发布组件不替换为任何旧DLL。

## 完整壳启动（不是业务验收）

独立`UserData-Startup-Only`启动PASS，PID 38708，标题为“光镜 · 完整范围诊断预览 · 未完成验证 / 不能发行”，实际加载组件hash与发布匹配，20秒未退出，正常关闭。

只读UI Automation树确认十个主域：工作台/素材库/归片/工作日历/策划/联机/暗室/在线选片/摄影收支/项目历史；工具箱/设置/帮助/授权入口也存在。素材库使用独立AutomationId，因此初始prefix计数9并非缺页面；纠正为10，原记录保留。

首启教程使导航暂禁用，尚未退出教程。只读cursor查询成功，但桌面约5.4秒前有输入，未建立独占空闲条件，因此不激活/不点击、不发送键盘，不尝试绕过；物理鼠标与导航操作NOT_RUN。未出现公司GetCursorPos拒绝，不沿用公司环境错误。无桌面截图，未拿离屏图冒充。

## 测试与门禁

本阶段仅同步/构建/启动；家里完整Core、完整WPF、发布模块专项均NOT_RUN，留第二阶段，不用公司成绩冒充家里结果。
公司基线仍为：策划FAIL，R3测试输出隔离PARTIAL，DPI17失败；P3性能PARTIAL，Fuji NOT_RUN，显示ICC有限研究，PV7/合成parser默认关闭，V3产品接线PARTIAL且非物理校准。WPS/真机/服务/跨屏/安装另列条件阻断。详见TOPIC_DEPENDENCIES.md。

第二阶段尚未开始，没有用修新功能掩盖同步基线。先交本回报，再按用户顺序逐批复现/单独提交。

完整包目录：`N:\pixart\Guangjing-Home-Sync-20261020-193553\Guangjing-FullScope-Diagnostic-home-win-x64`。启动器`Start-Guangjing-Diagnostic.cmd`只启动本地EXE，使用包内UserData；不打开浏览器。新家里ZIP另附ZIP_IDENTITY.json，UserData不入包。
