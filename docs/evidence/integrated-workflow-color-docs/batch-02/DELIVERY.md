# 第二批交付

## Git 与范围

- 日期：2026-10-11（Europe/Berlin）。
- 实际工作树：N:/pixart/pixel-tart-integrated-workflow-color-docs。
- 分支：feature/pixel-tart-integrated-workflow-color-docs。
- START_HEAD/获取的起始远端：de6faa156ff4e93995be43bdd0d6da527ec8b198。
- 源码 HEAD：b8ecf52c14e9fb014fd678e864449806dac9b068。
- 原集成工作树仍为 a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc，两个未跟踪审计目录保留。
- 未发现工作树内额外 AGENTS.md；遵守用户提供的全局禁止 Computer Use/Browser/CDP 规则。

## 源码提交

- 08f7102：并发领取、恢复代次、冲突保护。
- b0a8b06：Watch Folder 生产交接、标注、Studio 身份入口、策划包与画布版本。
- c7d1024：色彩版本黄金结果与独立 DOCX 实验。
- b8ecf52：拒绝把已编辑路径重绑定到不同素材/hash。

## 已完成的可执行开发

生产 Watch Folder 的稳定处理阶段接入第一批身份桥；持久操作记录与 OS 锁控制重复和恢复，失败有详情与重试。RAW/JPEG 独立素材身份。捕获上下文落盘，项目/预约/Shot 关系按冻结值写入。素材库、策划引用、自由画布和 Studio 共用解析器。素材库到 Studio 有实际入口，联机不再只交 BitmapSource。

摄影师标注由素材库权威读取，客户字段单独保留，冲突有明确选择。Studio v1 会话增加可选库身份/hash，载入、重开与导出拒绝变更内容；旧 ProcessingVersion 保留。

策划正文增加版本化包，复用原子保存和恢复草稿。情绪板可插入/更新画布版本，内嵌 PNG 与对象树，旧版本保留。DOCX/分页只做独立小范围试验。v3 仅为实验候选，未切换正式渲染路径。

## Release

本机 EXE：N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch02/release-final/win-x64/KitaoPhotoSelector.exe。

EXE SHA256：C2E65B392AD4E77658DADCE0893150A8EC5F0D8FC6A9A52E7EE302A4F5FEC0F2。

DLL ProductVersion：2.3.0-batch02+b8ecf52c14e9fb014fd678e864449806dac9b068。

构建 PASS；36 个发布文件身份见 RELEASE_IDENTITY.json。无 Installer，二进制和原始测试日志未提交。

## 真实剩余项

1. 工作台沿用现有项目入口，尚无所有旧项目路径的统一迁移/重定位向导。
2. 标注跨进程并发版本戳、完整失败注入覆盖仍不足；本批冲突处理不等于所有并发编辑已验证。
3. Studio 多 EditVersion、导出产物身份清单及客户选片溯源尚未完成。
4. 画布快照支持常用对象，但与自由画布全部绘制效果的像素一致性尚未完整验证；超大文档资源包拆分未完成。
5. 完整 Word、任意 DOCX 往返、产品分页编辑/打印预览、批注修订尚未完成。
6. 相机输入特征化、显示器 ICC/显示变换与专业 RAW 新版本未上线。
7. Release 实机、实际 DPI 与用户视觉验收未执行。

因此本批整体 CODE=PARTIAL、TEST=PARTIAL（已执行回归通过，仍有未覆盖项）、RELEASE_RUNTIME=NOT_RUN、USER_ACCEPTANCE=NOT_APPROVED，状态 NOT_READY_FOR_USER_RETEST。没有把本批基础实现升级为整个软件通过。

详见 REQUIREMENTS_TRACE.md、TEST_AND_RELEASE_MATRIX.md、MANUAL_ACCEPTANCE.md 和 FILES_AND_TYPES.md。推送核验单独记录，最终证据提交 SHA 以交付回复为准。
