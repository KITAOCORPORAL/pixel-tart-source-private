# Git 基线与同步记录

## 基线

- 执行日期：2026-10-11（Asia/Shanghai）
- Git 根目录：`N:/pixart/pixel-tart-source-private`
- 远端：`https://github.com/KITAOCORPORAL/pixel-tart-source-private.git`
- 原工作树分支：`integration/pixel-tart-developer-preview`
- 原工作树本地 HEAD：`a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc`
- 获取后的远端集成分支 HEAD：`6a27e166af5c77f8455f44089aa9de5d38e86495`
- 原工作树比远端落后 5 个提交，并含两个未跟踪审计目录。它们均被保留，没有清理、覆盖、暂存或提交。

## 安全开发方式

从远端集成分支 HEAD 创建独立 worktree：

- 工作树：`N:/pixart/pixel-tart-integrated-workflow-color-docs`
- 分支：`feature/pixel-tart-integrated-workflow-color-docs`
- 起点：`6a27e166af5c77f8455f44089aa9de5d38e86495`

未执行 reset、rebase、force push 或对原工作树的合并。本批不触碰客户照片、生产 SQLite 数据库和已有 Studio/策划文档。

## 已读取的 P0-A 交付

已读取并与源码复核：`GIT_BASELINE.md`、`IDENTITY_RELATIONSHIP_MAP.md`、`CROSS_MODULE_GAP_MATRIX.md`、`MINIMAL_BRIDGE_DESIGN.md`、`DATABASE_COMPATIBILITY_PLAN.md`、`EDIT_VERSION_INTERFACE.md`、`P0A_IMPLEMENTATION_TASKS.md`、`TEST_FIXTURE_PLAN.md`、`DELIVERY.md`。

这些文件作为设计输入使用；其中 PROPOSED 项没有被当成既有实现。

## 状态

| 项目 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE | PUSH |
|---|---|---|---|---|---|
| 安全基线与独立分支 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见 `GIT_PUSH_VERIFICATION.md` |
