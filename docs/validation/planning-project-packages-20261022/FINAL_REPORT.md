# 策划项目包导出导入与限定恢复

2026-10-22 Europe/Helsinki。**本批限定功能自动闭环PASS；实体桌面/Word/WPS NOT_RUN，诊断版不能发行；不宣称完整跨机便携。**

## 基线与产品提交

fetch后feature/planning-project-copies-20261022远端最新3dec55b2da6e87579c48721624cd1e816c492d16，与上批产品3710317f8c2681141cb7d0e959e78ab9f9bb6037的src/tests无差异。新独立工作树E:/Kitao-Worktrees/planning-project-packages-20261022，分支feature/planning-project-packages-20261022。

**产品提交9040e320d871f05c1013da9ed45bb04d48257616**：13个产品/新增测试文件，376增14删；证据另提交。45旧工作树、423未提交文件和旧诊断包已hash保全，不修改原项目/原照片，不merge main、强推、reset/stash/clean。

## 用户入口与产品合同

- 选中项目→更多策划操作→导出策划项目包；界面逐项列未包含依赖并确认。
- 策划列表可见“导入策划项目包”“打开已导入项目”“取消项目包任务”；更多菜单也有导入/取消及“检查本项目中断暂存”入口。
- .ptplan v1包含当前项目planning.json、shots.json、已验证ProjectCopy参考/普通附件、既有文档自有PNG。manifest记录版本、项目/文档ID、名称、每文件长度/SHA-256、未包含依赖。
- 完整校验ZIP条目、重复/穿越/设备/符号链接、64MiB/单文件、256MiB/总量、2000文件、metadata限制、hash、身份/格式和声明依赖，再在新stage建立独立项目数据库和stores，最后Directory.Move发布项目目录。
- 相同项目ID目标已存在一律拒绝（不覆盖、不自动重映射ID）；同名不同ID各自独立目录。取消/普通异常清理仅本次stage，原项目包与旧项目不变。导出已有文件也拒绝覆盖。
- 导入后只策划workspace切到所选独立根，旧账本/素材库resolver清空，不把其他模块全局改根；重启使用“打开已导入项目”。未拥有外链不会自动读取预览。新根不伪造账本/档期/库原件；只保留依赖身份。
- 外链/附件/镜头外部引用、库原件、档期关系、捕获素材关系、画布/灵感板/色彩方案及账本等在导出确认、manifest、导入确认/结果逐项列出。Hashes只保证文件一致，不是数字签名。

## .part限定恢复与孤立副本

加载本项目、再次导入项目副本或显式恢复命令，仅检查当前PlanningStore项目/文档/.staging的本功能owner.json。逐项核对schema、项目/文档、Guid.part名称、进程ID+启动时间、超过1小时且原进程不活动；无重解析且独占DeleteOnClose句柄才清理该一个.part。有效策划/恢复草稿副本绝不删除。

完成复制但未提交引用的副本校验hash后**保留孤立副本并写独立审计文件**，不自动认领、不删除；再次用户明确导入可复用。损坏凭据/未过期/活动进程/无凭据旧.part/未知文件保留，报告理由。草稿无法核对则不清理。不会扫描其他项目、旧工作树或原文件。

上批旧.part没有归属凭据，无法安全追溯，故有意不自动删除。该保守边界是本批产品结果的一部分。包导入强杀残留.import-stage不做自动GC，新导入仍使用新stage且残留不暴露为有效项目。没有声称所有恶意内核级路径竞态和断电时序已实机验证。

## 实际合成闭环与本轮回归

最终发布组件重新执行：

|范围|总发现|选中/结果|通过|失败|跳过|漏项/额外/超时|
|---|---:|---:|---:|---:|---:|---:|
|受影响策划/存储Core|1737|42/42|42|0|0|0/0/0|
|受影响策划/文档WPF|1730|85/85|85|0|0|0/0/0|

新Core6、新WPF1已包含，不重复相加。其余包含上批副本8Core/3WPF、保存重开/撤销、原Docx往返/表格/页眉页脚/分页/keepNext/atomic替换等。发布DLL29项与测试宿主绑定hash最后核验，适配器(testId,FQN,displayName)计划=实际；没有重扫122动作/重跑全软件。

合成闭环使用生产CreatePlanningCommand新建项目→选择导入参考PNG与TXT、另一图仅外链→ExportProjectPackageCommand→完整manifest验证→ImportProjectPackageCommand到全新数据根→新VM及可见OpenImportedProjectCommand重开→解析/定位副本→保存→15区汇总。真实JSON/SQLite/WPF布局，不mock存储；用户选择由合成Dialog返回，**不是实鼠标系统文件框**。

项目ce615a0d-d7b4-4360-8bca-1edccb18d81a，文档d1f4541a-dff7-c083-f701-9540889d2189。最终项目包5文件：manifest+planning+shots+2自有文件。PNG hash663a7d1c0cb9333f210b635ba072777d11e3152f6bbac6f4bd8b81bb5e30e0b7；TXT dfe4146b1e82e56619020090eed90c7c44572759740fb66ebe492236ea21b6f8；源文件与旧策划hash在整条链后保持。独立脚本读取包/导入副本再次匹配清单，汇总15标题/1嵌图、普通TXT名称状态可读，未含外链不嵌入。

另验证篡改payload、缺shots、重复entries、../穿越、旧v0/未来v2拒绝、重复导入ID冲突、同名项目、验证/落盘/导出取消、缺副本导出拒绝、恢复过期归属part/有效引用/孤立副本/未知文件/活动与新近进程/损坏凭据/重解析点不删。初WPF0/1因新测试Single匹配两条正确范围提示失败，原TRX保留；改选最终消息后通过，无旧断言放宽。还在实现复核中修复导入根切换同ID旧undo栈、重型校验阻塞取消、排除外链自动预览等产品边界。

## 新完整诊断与未运行

完整Release win-x64 self-contained、WholeAppDiagnosticBuild=true、PhotographerTrialBuild=false，290应用文件/264声明运行依赖缺失0。统一启动器用独立UserData。附当前Synthetic-Planning-Project-v1.ptplan可从产品导入，不再要求手工复制DB。

新EXE PID23708实际启动，7加载模块与包hash一致；原生点击退出教程本轮仍coordinate input geometry is unavailable，刷新后专用截图FrameArrived timed out。人工选择/拖动/系统文件框/外部附件应用/Word/WPS均NOT_RUN，不以自动WPF/OOXML替代。无安装包验收，不是Setup，不能发行。

源/测试冻结后零diff；产品与证据分别提交推送，ZIP逐文件读回及独立ls-remote同HEAD另附。原始发现/TRX/log/first failure在RAW_TESTS.zip，文件清单/类型在PRODUCT.json、APP_FILES.json及ZIP_FILES.json。

## 最终包读回

320文件，75948242字节，SHA256 20d184e4f901b35338cbff322abe26778d0cbfa7412ce1f93d614d6ff88c3c9c，每文件读回一致；不含UserData/数据库/私人输入。合成.ptplan SHA256 4b526f53c06da67a648bdbe6808c832856839bd8f8ea929ab3b99a2454e4b4ed。ZIP内报告是打包前快照；本外部报告后附ZIP自身身份，避免循环hash。

## 推送读回

产品9040e320d871f05c1013da9ed45bb04d48257616与独立证据81a5d1b9a6dae82879f50530950dc93cb4d7e044已正常推送本轮feature分支，独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD，工作树干净。PUSH_READBACK.json保留这次核验；后续仅封存读回，不改变产品或发布组件。
