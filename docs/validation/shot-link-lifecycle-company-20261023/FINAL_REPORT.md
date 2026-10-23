# 镜头外链收据生命周期公司修复

2026-10-23 Europe/Berlin。产品代码和限定自动回归已完成；原生镜头副本闭环仍BLOCKED，整体PARTIAL，不能发行、无安装包。

## 基线和产品
fetch后独立ls-remote确认home/planning-shot-external-reconnect-20261022为b12f95a8b407af507edbe384d9ac6af6c52493b9，已读该分支FINAL_REPORT.md、DESKTOP_STATUS.json、PlanningShotExternalLink.cs和相关保存/包代码。家用原生PASS仅“继续外链”模式，不当作本轮副本实机PASS。

新独立工作树E:/Kitao-Worktrees/planning-shot-link-lifecycle-company-20261023，公司分支company/planning-shot-link-lifecycle-20261023。47旧工作树HEAD/status、423脏文件hash、旧诊断ZIP保全。未reset/clean/stash/rebase旧树、未合main、未强推。

产品阶段bbfaad859c1a318d684aafacb9c2bc28b23a2eb9：公共镜头门禁与旧失配修复；最终产品 **3cd7543050722b2449a75ec7115e70aa29dcff6b**：修复回归发现的无来源变化镜头保存顺序。产品与证据分开提交。

## 生命周期修复
ProjectShotStore公共UpdateAsync门禁覆盖SaveAsync、SaveBatchAsync、SaveCatalogAsync、RemoveAsync和重排序；VM构造/切换根绑定PlanningProjectStore，同一shots根的store实例共享协调注册。编辑前有效、编辑后删除镜头/移除引用/改路径或库身份的批准，从对应ShotIds撤回；仍使用相同ReferenceId和旧来源的其他镜头保留批准。最后一位使用者消失时撤回收据，**不删除任何原文件或项目自有副本**。预存失配不静默清理，交给用户检查。

shots gate→planning gate，批准元数据可回滚写入；若镜头文件替换失败，恢复原策划字节。无来源身份变化的名称/时间/状态编辑不经过planning读取，保持旧shot-first草稿恢复顺序。VM保存取已协调的收据，避免pending旧快照将撤回批准重新写回。直接移除参考即时刷新批准。

共享ID允许有效收据只授权原ShotIds，不把另一个改源/改库镜头自动授权；包验证不再因该共享ID的其它来源存在就否定仍有效的收据，但每个收据接收者仍严格校验。预览对来源冲突仍保守拒绝。

## 可见旧项目修复
策划更多菜单“外链检查、失配修复与手动重连”，列镜头ID/引用ID/原路径/失配原因/受影响副本；逐项移除失效批准、重新定位继续外链或定位并导入副本。用户选文件后显示类型/大小/SHA-256并确认，重新校验当前镜头集合和旧收据。

修复前备份原planning字节到独立.before-shot-link-repair；取消不写、确认后来源变化或保存失败保留旧收据、原文件，删除仅本次新复制文件。旧副本从不删除。仍有有效共享接收者时拒绝用另一来源覆盖它，要求独立引用ID；库身份/冲突来源不定位，不扫描私人目录、不猜同名。

## 最终发布组件回归
正确源码标签最终包VERIFIED_*：

|范围|程序集发现|选择/结果|通过|失败|未执行|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|Planning/ProjectShot Core|1758|63/63|63|0|0|0/0/0|
|Planning/ProjectShot WPF|1736|91/91|91|0|0|0/0/0|

新Core4/WPF2已包含，不重复相加；29最终发布DLL逐hash绑定。实际adapter(testId,FQN,displayName)多重集合计划=结果，原始TRX/发现/runsettings/stdout/stderr留RAW_TESTS.zip。当前公司符号链接测试实际通过，未改系统策略；家用分支原3项权限失败保持历史，不改成通过、不继承成绩。

初NEW_CORE3/4失败是新测试错误地同时填库身份和外链，Normalize正确拒绝；改成合法外链→库来源输入，未放宽产品。首次发布WPF90/91失败是产品回归：规划锁阻断名称编辑旧跨文件恢复；旧断言不改，最终产品提交修复后10专项及91最终全部通过。

上次SEALED脚本错用旧SourceRevisionId，实际是新源码但标识混乱，保留原记录不算最终成绩；本次VERIFIED重新从3cd7543050722b2449a75ec7115e70aa29dcff6b发布并完整重跑63/91，正确命令和hash独立封存。驱动一次多夹具argv元组错误未执行WPF，也保留，不记产品测试失败。

## 镜头副本自动闭环
生产LocateExternalAsync选择“导入项目副本”，签名/类型/大小/SHA校验→保存→新VM重开PNG预览→包导出→异地隔离根导入读取同hash副本。Document.References保持空，ShotExternalLinks保留原镜头/引用/原路径。移除镜头参考及下一次保存不复活旧批准，副本仍在。

最终合成项目58f9ce32-c68b-4d96-9a29-e7be3deb32a2，shot9a634089-ed09-4139-a04f-f198b31cdda4，reference57dd6ad2-0d77-43df-a4c4-3e10067b2bfa，PNG SHA256663a7d1c0cb9333f210b635ba072777d11e3152f6bbac6f4bd8b81bb5e30e0b7。owned包SHA2569a058597807933877daf901f1b37b9eb258346dbe863c0cd7f412c88386eb65b。独立ZIP/hash读回确认异地导入后ReconnectedFile为空、自有副本hash匹配。包内可携带历史收据记录，但导入强制清掉本机批准，不成为另一机器授权。

## 恢复Computer Use后的真实范围
用户误触Escape后明确恢复。最终VERIFIED EXE PID20744，隔离根native-owned-copy/runtime，7实际加载模块hash与包一致。已观察目标控件树；真实点击退出教程仍coordinate input geometry is unavailable；重新获取窗口并激活后专用截图仍FrameArrived timed out。未猜坐标、未绕过教程设置、未用调用模拟成人工。

因此真实“副本导入→保存退出→新进程重开预览” **NOT_RUN/BLOCKED**，原生包导出/异地导入也未完成。前轮受阻记录和恢复尝试都保留。自动VM重建不是进程重启；家用外链模式实机旧成绩不代替本轮副本实机结果。

## 诊断交付和边界
完整Release win-x64 self-contained，WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，290app文件、264运行资产缺失0，统一隔离启动器。当前正确产品3cd7543050722b2449a75ec7115e70aa29dcff6b；不会拿旧包当最终包。样例含shot-missing、shot-owned v1包和合成PNG，不含DB/UserData/私人素材。

本轮未做安装/Word/WPS/物理DPI/全软件发行验收。跨两个JSON的突然断电不是硬件原子事务，普通失败回滚已测；第三方未绑定store/直接改JSON产生的历史失配需用户可见修复，不能静默认领。共享冲突预览保守拒绝，原有PNG安全呈现限制保留。不能发行。

## 最终ZIP

325文件、76208179字节，SHA256 a844a4de011276e07b07dc749c621e0d2eb7e8a0d8f5e7a8eff7db1e83c8e197，每条目读回hash一致。ZIP内报告是打包时快照，外部报告后附自身身份，避免循环hash。不含DB/UserData/私人照片。

## 正常推送读回

最终产品3cd7543050722b2449a75ec7115e70aa29dcff6b与独立证据f6a03f50cfc3b27469b96884d35e54a724e3179d已正常推送公司分支。独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD，工作树干净；后续仅封存读回，不改变发布组件。
