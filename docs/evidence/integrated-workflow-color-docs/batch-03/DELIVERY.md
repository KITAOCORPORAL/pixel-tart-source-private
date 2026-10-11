# Kitao Studio 光镜 · 第三批交付

## Git 与执行边界

日期：2026-10-11（Europe/Berlin）。实际工作树 N:/pixart/pixel-tart-integrated-workflow-color-docs，分支 feature/pixel-tart-integrated-workflow-color-docs。

START_HEAD 与 fetch 后远端均为 f10126ff7676a37b0ea2d0f4f84761f04657c235，起点工作区干净。原集成工作树仍为 a9f61bab4d6fa048f6ed9412aa4341db7c3e61fc，两个未跟踪审计目录保留。读取第二批文档及机器证据并核对实际生产调用链。未 reset/clean/rebase/强推/自动合并。

源码 SHA：56ae7e2efa968501e2720ae584f1090f08f2ddc1。

- 5b8722b：素材重新定位、跨库映射、标注版本与编辑/导出身份基础。
- 83e1706：生产入口、独立编辑版本、富文本/DOCX/PDF、中文优先多语言资源。
- 56ae7e2：Kitao Studio 光镜显示品牌与粒子启动窗。

## 实际实现

1. 同库按 hash 重新定位，保留 AssetId；明确目标库后建立可追踪映射，Resolver 检查目标库身份和内容。
2. 摄影师标注递增版本/CAS；产品侧期望版本保存、素材侧失败保留草稿；客户字段分离。独立连接并发、ABA、重启及过期提交测试通过。
3. Studio 默认版本迁移、创建/切换、会话持久化；各版本独立参数与编辑历史身份；导出生成原子 .kitao-export.json。
4. 策划正文真实 RichTextBox 编辑、RTF 包、图片/基础表格/列表、分页预览、打印、原 PDF 富文本接线、受限 DOCX 导入导出及损失报告。
5. 候选颜色域契约：相机输入需显式矩阵、RAW WB 防重复、JPG 编码解码、输出裁切规则；旧版本未替换。
6. 显示品牌与抽象三角粒子启动窗；真实初始化状态，主窗口就绪淡出，静态降级和资源解绑。

## Release

EXE：N:/pixart/pixel-tart-integrated-workflow-color-docs/artifacts/batch03/release/win-x64/KitaoPhotoSelector.exe。

EXE SHA256：8908C16856D89E99CF89501BB315750FEE31AF8D02219F3481EFF9D984949E5D。

DLL ProductVersion：2.3.0-batch03+56ae7e2efa968501e2720ae584f1090f08f2ddc1。

39 个发布文件及 hash 见 RELEASE_IDENTITY.json，含 Open XML MIT 许可。只发布本机目录，没有制作 Installer。源码、测试、许可和脱敏报告提交 Git；二进制、原始 TRX、客户照片和生产数据库不提交。

## 测试

Core 全量 1613 通过/0 失败/6 跳过；WPF 全量 1514 通过/0 失败/13 跳过。追加受影响测试及失败修复见 TEST_AND_RELEASE_MATRIX.md，计数不叠加。DOCX 使用 OpenXmlValidator 实际校验，期间发现并修复表格网格、编号与字体属性顺序问题。

## 尚未完全关闭

- 工作台沿现有项目素材库入口复用定位，未新增独立定位面板；跨库源库离线时无法读取其映射。
- 所有历史批量颜色/元数据 UI 尚未统一为用户读取时版本提交；两 OS 进程及生产库故障矩阵没有实测。
- EditVersion 当前存于会话，未建立跨会话版本目录；完整发布配方结构尚未写入导出清单。
- DOCX 只覆盖已支持子集，复杂样式继承、浮动环绕、表格属性、批注修订和任意文档保真未完成。超大富文本性能和完整撤销组合仍需实机验收。
- 专业 RAW 输入特征化、HDR 域迁移、显示器 ICC/高级输出变换未上线；本轮真实 RAW/JPG/色卡对照 NOT_RUN。
- DESIGN.md 未找到，动画未按该文件逐项对照；没有新 Logo 定稿。
- 禁止 Computer Use 的用户 AGENTS 继续生效，本轮未执行 Release 桌面操作/截图/DPI/启动验证。

整体 CODE=PARTIAL、TEST=PARTIAL（已执行回归通过，仍有未覆盖项）、STARTUP=NOT_RUN、RELEASE_RUNTIME=NOT_RUN、USER_ACCEPTANCE=NOT_RUN，USER_VISUAL_REVIEW=NOT_APPROVED。总状态 NOT_READY_FOR_USER_RETEST。没有将第三批升级为整个软件验收通过。

## 文档索引

REQUIREMENTS_TRACE.md、IDENTITY_AND_CONCURRENCY.md、EDIT_VERSION_AND_EXPORT.md、DOCUMENT_EDITOR_RESULTS.md、COLOR_PIPELINE_RESULTS.md、BRAND_AND_STARTUP.md、TEST_AND_RELEASE_MATRIX.md、MANUAL_ACCEPTANCE.md、FILES_AND_TYPES.md。最终证据 SHA 与远端一致性见推送回执和最终回复。
