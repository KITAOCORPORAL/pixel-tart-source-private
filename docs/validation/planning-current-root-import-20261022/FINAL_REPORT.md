# 当前应用数据根导入交付

2026-10-22 Europe/Helsinki。**限定功能自动PASS；实体桌面/Word/WPS NOT_RUN，整体PARTIAL，诊断版不能发行。**

## 起点、产品与保全

fetch远端feature/planning-project-packages-20261022最新2abcbf70a22c788c71b6421b24372835298478fc；src/tests与指定产品9040e320d871f05c1013da9ed45bb04d48257616一致。新独立worktree E:/Kitao-Worktrees/planning-current-root-import-20261022，分支feature/planning-current-root-import-20261022。46旧树HEAD/status、423未提交文件hash、旧诊断包均未变；无merge main/force/reset/stash/clean。

**产品提交591db7856921502eca252ae9877a73faa5e290c4**：13文件、284增6删。产品与后续证据独立提交，发布EXE/DLL/数据库/私人素材不入Git。

## 正常使用与当前根提交

新可见“导入项目包到当前应用”入口，选择v1包后完整校验，确认页显示项目名称/ID、当前数据库确切路径、策划根、镜头根及排除依赖；不选择另一个长期根。普通项目列表刷新即出现，原名称不变，另行显示完整ProjectId区分同名。像普通项目打开/编辑/保存，原独立根导入/打开保留。

严格新增仓储IPlanningImportRepository独立于Upsert：同ID查询包含已归档，事务中Projects INSERT及PlanningPackageImportReceipts事务收据同时提交，冲突绝不覆写。文件先在当前策划根.package-imports/.import-事务stage准备、校验，逐项无覆盖移动到当前planning/shots/copies，再复验hash，SQLite最后登记。普通取消/磁盘错误只撤回本次已安装hash一致文件，无项目记录半成品。

排除Booking/VisualLinks/CapturedAssets、库Assets和ReferenceLookId不挂接当前无关业务ID；原排除清单保存在文档PackageImportExcludedDependencies扩展，并在确认/结果/汇总显示。不创建账本、库原件或档期业务记录；导入项目禁用自动读取外链预览及账本delegate，其他普通项目保持原行为。

## .import有归属恢复

普通策划列表刷新/启动策划列表、再次导入或更多菜单恢复检查，限定当前planning/.package-imports下.import-*；owner凭据包含版本、事务/项目/文档、确切planning/shots根、逐目标size/hash、Prepared/Installing/FilesInstalled/Committed/RolledBack阶段、PID/启动时间。同ID锁+active锁、活动进程检查，未知/旧无凭据/错误根/未知阶段/变化文件保留报告。

Prepared从未安装，不检查/删除任何最终目标；Installing/FilesInstalled仅处理已从stage移出的本次清单文件且hash一致、数据库无该项目，撤回后RolledBack保留审计；SQLite若本事务receipt已提交，核验有效项目而不回滚。数据库是别的项目事务则全部保留。审计JSON和stage有意保留，不做全盘或其它树清扫。再次导入可安全重试。

复核修复：最初Prepared补偿过宽，改成不删目标并加回归；恢复与新导入原锁域可能交叉，统一同项目ID跨进程锁。列表最初拼ID到名称可能破坏旧名称合同，改独立ID行。首轮边界新测试5/6失败为池化SQLite共享锁阻止直接文件hash，清理合成池后测量，原失败保留，旧断言未放宽。

## 本轮最终发布组件结果

|范围|程序集总发现|选择/实际|通过|失败|跳过|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|受影响Core存储/策划|1743|48/48|48|0|0|0/0/0|
|受影响WPF策划/DOCX|1731|86/86|86|0|0|0/0/0|

29发布DLL绑定逐hash确认；新6Core/1WPF已包含，非重复相加。包括原v1独立根/包校验与副本、RTF/DOCX/图片/表格/keepNext/atomic往返回归。实际adapter多重身份发现=计划=结果，原TRX/stdout/stderr/runsettings完整封存。只受影响回归，不扫描122动作/重跑全软件套件。

合成链：产品当前应用导入命令→普通列表找到→打开编辑→保存→新VM（同数据库和stores，非独立根）重开→解析参考/附件→15区汇总。项目9573266c-0735-4212-ac1f-c9497a3b3063，文档83a487ff-75f5-9f72-0d88-7c77f073c8f8。源PNG/TXT/源策划、已有当前项目hash整链保持。当前命令不弹ChooseFolder，不设置ImportedPackageRoot。此是新VM重建，不冒称真实退出程序重启。

实际v1项目包SHA256 f9747e164947d13822aacdef212f638188b64a620b0f5c9a18b595801955d0f0；汇总15标题/1嵌图、TXT名称状态，hash a683c4aeb52b2bf19b1ba1c2e05b0683fba514331a6a21657910aac02b6eeaac。独立ZIP/OOXML/hash读回见VERIFIED_CURRENT_IMPORT.json。

新增测试含ID重复/已归档、同名不同ID、已有文件无DB拒绝、篡改不写根、取消安装、模拟DB磁盘失败、模拟中断阶段恢复、活动owner/未知/变化文件/错误根保留、源旧项目hash不变。模拟中断通过真实stage/文件与合成owner记录复现，不是OS强杀/断电实测。

## 桌面与发行范围

新完整EXE实际启动PID21028，7加载组件hash与包一致。原生点击退出教程本轮GetCursorPos failed:拒绝访问(0x80070005)；刷新后专用capture FrameArrived timed out。原生鼠标/系统文件框/真实退出重启/WordWPS均NOT_RUN。不得以自动命令、VM重建或OOXML当人工实机PASS。

完整主程序Release win-x64 self-contained，WholeAppDiagnosticBuild=true/PhotographerTrialBuild=false，290应用文件、264运行资产缺失0，统一启动器隔离UserData。不含数据库/私人输入，非Setup，不能发行。v1样例可直接产品导入普通列表。

跨SQLite+文件不是硬件级原子事务；本功能以文件先行和DB收据最后提交及可审计补偿降低半项目风险。恶意同时路径置换、其他旧代码并发同ID写入、所有硬断电顺序未实机穷尽；变化文件保守留待审计。完整跨机便携仍不宣称，排除业务保持未实现。

## 最终包独立读回

完整诊断ZIP 320文件、75549919字节，SHA256 d566827070699f4541a9cc6181de4eb8690680c7c30c158f8a9bdbad0c4554d8，逐文件读回一致；不含UserData/数据库。ZIP内报告是打包时快照，外部报告后附自身身份避免循环hash。

## 推送读回

产品591db7856921502eca252ae9877a73faa5e290c4与独立证据ae582cc79d22e7657207a4c136c08d693d7dec30已正常推送本轮feature分支，独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD且干净。PUSH_READBACK.json保留该次读回；后续只封存文档，不改变产品或发布组件。
