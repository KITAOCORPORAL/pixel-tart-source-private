# 本机策划图片搬迁交付 — BLOCKED_BUILD_ACCEPTANCE

2026-10-14 Europe/Berlin。本批代码已实现并分阶段提交；**最终本机发布组件有必需测试真实失败，审阅构建验收 BLOCKED**。不正式发布、不写全绿、不把旧冻结通过结果替代最终结果。

## 起点及工作树

fetch核验origin/feature/kitao-planning-tray-images-20261014为13b0efe689c0114f6f76b70c59a1059cabeddbef。读取原DELIVERY/MIGRATION_CONTRACT/OPEN_ITEMS及真实代码。新工作树N:/pixart/kitao-planning-portable-images-home-20261014；分支fix/kitao-planning-portable-images-home-20261014。家里原6树保留，旧源码树3个未跟踪审计/研究目录不动，无重置、覆盖、强推或合并main。

## 分阶段提交

|阶段|提交|实际结果|
|---|---|---|
|1 受控路径和清单|ed7bf4d7a6872402d35c53792bebba0a75a8916a|3路径/清单Core + 原6托盘WPF专项通过|
|2 真正搬迁/恢复/表内图注|25866a0|新增3搬迁和6托盘共9项通过；表内图注缺陷修复|
|3 首次冻结/严格门禁|0bbf617|此前冻结本机审阅通过但不是最终源码，证据标historicalOnly|
|3 最终错误提示修复|bbebec8d28628e847ef12d58cce32603c558569e|安全打开和保存状态显示具体图片标题/ID；9专项通过，重新完整回归/发布复验|
|4 实机边界|stage4/NOT_RUN.md|Windows/WPS/物理DPI未执行|

## 功能完成

新CopyPath为images/{DocumentId:N}/{DocumentImageId:N}.png，根目录为JsonIgnore运行时信息。精确身份匹配、禁止.与..、越文档目录/非规范定位、reparse/symlink及SHA256检查。旧绝对路径仅准确身份且hash正确可读；旧文件存在但损坏不回退，旧文件缺失才查新根精确ID目录。成功读取不写JSON，保存前验证/复制完整副本后安全迁移为相对元数据。缺图/篡改由实际打开/保存入口显示具体图片。

真实合成搬迁：同图两次+表内第三图，策划JSON/images整体复制，新Store重开前撤原目录。模式切换、两类undo/redo、图片尺寸/图注/顺序/表格位置、分页、4个真实DOCX ImagePart验证通过。旧绝对文件、主锁原字节不变、草稿重放、缺图和hash篡改测试通过。

清单包括正式、草稿、DOCX恢复、旧迁移备份和VM内存undo/redo引用；未读完保守标记；孤立副本只列出，DeletionAllowed=false，不自动删除。不是跨文件ACID。Grok UI与Color Studio数学零diff，私人资料不动。

## 最终门禁（以FINAL_*为准）

|执行|结果|严格/审阅|
|---|---|---|
|最终源码完整WPF|1602 Passed / 23 NotExecuted，共1625|strict FAILED仅原条件，无真实失败|
|最终源码Core并行运行|1684 Passed / 1 Failed / 7 NotExecuted，共1692|必需范围切换2499.7ms>400ms，BLOCKED记录保留|
|最终源码Core待WPF结束后顺序复验|1685 Passed / 7 NotExecuted，共1692|范围切换Passed，预发布审阅允许构建|
|最终发布WPF精确10类|35 Passed / 0漏跑/timeout/nonzero|搬迁/表格/DOCX/启动偏好全部通过|
|最终发布Core完整（顺序）|1685 Passed / 1 Failed / 6 NotExecuted，共1692|范围切换673.8ms>400ms，最终BLOCKED|
|实际发布Core身份单项|1 Passed|实际程序集位置及内容身份通过|

Final_POSTPUBLISH_GATE为BLOCKED，3个阻断诊断来自同一真实失败/非零退出，29个独立原开放条件继续保留（35个源/发布重复观测）。没有放宽性能阈值、分类或修改无关素材库代码；没有继续重跑挑绿结果。顺序复验也失败，不能确认并行负载是唯一根因。第三阶段构建已生成，但必需验收未通过，不作为已批准审阅包。

## 最终本机目录（诊断/待验收，不自动启动）

N:/pixart/kitao-planning-portable-images-home-20261014/artifacts/home-review-final/win-x64
EXE: KitaoPhotoSelector.exe
ProductVersion: 2.3.0-portable-images-home-review+bbebec8d28628e847ef12d58cce32603c558569e
EXE SHA256: 1ECBE5A3A9E36A6301997E59C467E8BB49B260F08639083B20D4CB851DEABBBE

完整win-x64自包含非单文件，本机从源码构建；公司D盘包未用。RELEASE_IDENTITY明确status=BLOCKED并包含阻断原因。8文件含EXE和7产品DLL，DLL逐项与隔离host相同，完整发布清单在RELEASE_FILES。不提交发布二进制。

## 原始交付证据

RAW_TEST_RESULTS.zip包含全部初次、再次、顺序源码和最终发布运行：发现、TRX、stdout/stderr、runsettings和专项失败；不含数据库、runtime或DLL。RAW_TEST_MANIFEST逐文件hash。samples为合成迁移前后脱敏JSON，原始合成DOCX/PNG留ignored artifacts。历史首次包结果清楚标注historicalOnly。Windows真实启动/素材库临时收集/策划选图保存重开/WPS图注修改导回/物理DPI均NOT_RUN。
