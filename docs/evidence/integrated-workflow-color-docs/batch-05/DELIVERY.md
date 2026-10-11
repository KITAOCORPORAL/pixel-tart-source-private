# Kitao Studio 光镜 · 第五批交付

## 现场

日期：2026-10-11（Asia/Shanghai）。实际工作树 N:/pixart/pixel-tart-integrated-workflow-color-docs，分支 feature/pixel-tart-integrated-workflow-color-docs。

START_HEAD / fetch 后远端：c2f6dcf6dce84561a4e39f8b720f0ab4b10a4267。起点干净，远端未前进。原集成工作树及两份未跟踪审计目录保留。仓库未发现额外 AGENTS.md，用户全局禁止 Computer Use/Browser/WebView/CDP 继续生效；没有 reset、clean、强推或合并主分支。

## 提交与源码身份

- e9947db：CAS 用户日志、后台旧 API 保护、素材库/联机撤销。
- 5c25601：版本冲突窗口、跨库复制、受管理导出恢复、进程测试和 Studio 标注撤销。
- 84d7baa：正文/语义联合历史、毫米精确输入、命令级组合测试。

源码 SHA：84d7baaf696ddd82932071f175665ffa2f98d053。

## 本批实现

1. CAS 写入和撤销日志同事务，批量仅记录成功项；提交后版本判断撤销/重做，过期拒绝。无快照旧 API 只允许未修改空字段后台初始化，不能覆盖用户值或已清空的字段。
2. 版本冲突真实窗口展示本地/仓储/身份/原因，Reload、KeepLocal、Cancel；先落恢复快照，再次 CAS，跨库仅复制明确选中的版本且保留来源。
3. 导出受管理任务记录编码产物、owner/hash/路径/状态；编码完成、pending、移入、清单阶段均可在进程终止后恢复；并发执行者唯一，原片/外部路径拒绝移动或删除。
4. 文档采用 RTF+语义/页面+光标联合快照历史；按钮和 Ctrl+Z/Y 同一路径，毫米输入具备范围、Enter/失焦、Esc 与非法反馈。

## 验证

Core 全量 1625 通过/0 失败/6 跳过；WPF 全量 1520 通过/0 失败/13 跳过；新增命令专项 38 通过。集合重叠不相加。真实进程验证包括 CAS/ABA、持锁进程终止、产品侧成功素材侧失败、重启重试、过期撤销/目录提交、五个导出终止阶段和并发恢复。失败及修复见 TEST_AND_RELEASE_MATRIX.md 与 TEST_SUMMARY.json。

## Release

EXE：N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch05/release/win-x64/KitaoPhotoSelector.exe。

SHA256：E77FF9F597EA31F057B04B144CC1D75AC2EE7D3D4ADFF2ADFAC5224F28D9209E。

ProductVersion：2.3.0-batch05+84d7baaf696ddd82932071f175665ffa2f98d053。

39 个文件见 RELEASE_IDENTITY.json。构建成功，未启动 Release、未进行桌面操作或视觉批准。没有 Installer；没有提交客户照片、生产数据库、构建产物、原始 TRX 或凭证。

## 仍需明确的限制

- 版本参数字段级合并未开放，用户可整份重新载入或保留为独立新版本；没有自动平均/拼接调整栈。
- 编码中断而尚未产生可信任务记录的文件不会自动续传或清扫；明确返回 Missing 并保留原临时文件。编码完成且 task 持久后可恢复。
- 文档历史最多 100 步/约 128MiB，为本次编辑会话内历史；重开保留文档与恢复快照，不保存整个历史栈。
- 字体回退样本本机页数相同，但跨机器字体、真实打印和所有视觉组合未验证。精确输入/组合撤销有命令级测试，不能代替桌面检查。
- 本轮没有启用真实 RAW/ICC/GPU 或修改正式渲染管线。

本批主要可执行数据/命令范围已实现并通过测试；最终验收仍待人工。CODE/TEST 按 REQUIREMENTS_TRACE.md 分项，不将桌面和视觉缺失升级为 PASS。RELEASE_STARTUP、DESKTOP_RUNTIME、USER_VISUAL 均 NOT_RUN，USER_ACCEPTANCE=NOT_APPROVED。字体/真实输出视觉覆盖仍 PARTIAL。

## 文档索引

REQUIREMENTS_TRACE.md、ANNOTATION_UNDO_RESULTS.md、VERSION_CONFLICT_RESULTS.md、EXPORT_CRASH_RECOVERY.md、DOCUMENT_UNDO_RESULTS.md、TEST_AND_RELEASE_MATRIX.md、MANUAL_ACCEPTANCE.md、FILES_AND_TYPES.md。推送回执见 PUSH_VERIFICATION.md，最终证据 SHA 在交付回复记录。
