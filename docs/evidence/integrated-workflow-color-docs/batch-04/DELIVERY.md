# Kitao Studio 光镜 · 第四批交付

## 基线与工作区

日期：2026-10-11（Asia/Shanghai）。工作树：N:/pixart/pixel-tart-integrated-workflow-color-docs。分支：feature/pixel-tart-integrated-workflow-color-docs。

START_HEAD 与 fetch 后远端：144de200a563c3648caa70b49373932d0df25f57，远端未前进，起点干净。原集成工作树仍为 a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc，两个未跟踪审计目录未改动。仓库内未发现额外 AGENTS.md，继续遵守用户全局 Computer Use/Browser/WebView/CDP 禁止规则。

## 源码提交

- a587d72：用户标注读取版本、批量逐项结果、撤销版本保护、两 OS 进程测试工具。
- b7565e4：跨会话编辑版本目录、实际入口、结构化配方与成片/清单恢复。
- 4e894e5：语义布局、表格编辑、统一分页/PDF、文档测量与启动摘要视觉。

源码 SHA：4e894e5badb0ef0ac0923c63698d90b7ccebd548。

## 本批可核对结果

- 用户标注主要生产入口提交读取快照；批量结果区分 Success/Conflict/Failed。现代撤销有版本戳，旧无 after-state 的日志拒绝不安全覆盖。
- 两个真实 OS 进程 CAS 竞争、ABA、崩溃锁释放成功；跨进程产品侧成功/素材侧失败、重启重试和过期提交均通过。
- 稳定身份版本仓储接入主应用，跨会话/改名可读取；UI 创建副本、命名、选择、归档与保存。过期目录修订拒绝覆盖。
- 快速导出和实际 Publishing 均生成结构化配方，编码临时文件 hash→pending→成片→成功 sidecar；清单失败显示 Partial，恢复验证 hash 且不重渲染。
- 语义标题/大纲、表格行列/列宽/对齐/边框、页面方向/尺寸/边距/分页已接产品；布局与损失报告随包保存。富文本预览/打印/PDF 共用页面定义。
- 指定 CanvasId/RevisionId 正文对象显式更新；旧包历史保留。500 段+图片保存/分页/撤销已测量并采用 350ms 防抖，显式保存前刷新缓存。
- 启动改为空心三角、确认色板和内密外疏结构；无 Logo 定稿声明。

## 测试与构建

Core 全量：1617 通过、0 失败、6 跳过。WPF 全量：1517 通过、0 失败、13 跳过。后续相关 Core 114 项、原子导出 21 项、WPF 61 项/67 项均通过；集合重叠不相加。进程结果见 PROCESS_RESULTS.json，失败和重测历史见 TEST_SUMMARY.json。

Release x64 publish PASS，39 个发布文件记录哈希。EXE：

N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch04/release/win-x64/KitaoPhotoSelector.exe

SHA256：8C657FAE7CD2832FDB3BE5EEE792006FEC5DF3D011B8306598D9AEFC9521F370。

ProductVersion：2.3.0-batch04+4e894e5badb0ef0ac0923c63698d90b7ccebd548。

未启动 Release、未操作桌面、未截图；RELEASE_STARTUP=NOT_RUN、DESKTOP_RUNTIME=NOT_RUN、USER_VISUAL=NOT_RUN / NOT_APPROVED。未制作 Installer，未提交原片、生产库、二进制或原始日志。

## 本批限定范围结论

限定批次状态 PARTIAL，而不是因整个产品尚未完成而判失败。以下确属本批尚未完全关闭：

1. 无读取快照的历史兼容 API 仍保留后台写入语义，任意外部调用者仍可误用；不能宣称低层接口全面强制 CAS。
2. 版本目录/会话冲突目前安全拒绝并保留文件，缺少专用人工合并界面；跨库新身份与旧身份目录不会自动合并。
3. pending 完成但成片未移入时崩溃，当前拒绝缺失输出恢复，不自动续传临时文件。
4. 页面/语义/表格命令与原生富文本撤销的全部组合、跨字体逐页视觉一致性及极端大文档尚未充分验证；列宽/边距当前为有限档位。
5. 新增直接 CAS 标注未统一接入旧撤销 UX，已刷新权威数据但不能称所有标注撤销交互完成。
6. 桌面、实际 DPI 和用户视觉验收未执行。

完整 Word、专业 RAW、在线客户交付是明确独立后续项，不用于掩盖本批缺口。RAW 候选/v3 未默认启用，历史渲染保持不变。

## 证据索引

REQUIREMENTS_TRACE.md、ANNOTATION_WRITE_PATH_AUDIT.md、EDIT_VERSION_PERSISTENCE.md、EXPORT_RECOVERY_RESULTS.md、DOCUMENT_LAYOUT_RESULTS.md、STARTUP_REFERENCE.md、TEST_AND_RELEASE_MATRIX.md、MANUAL_ACCEPTANCE.md、FILES_AND_TYPES.md。最终远端核验见 PUSH_VERIFICATION.md 与交付回复。
