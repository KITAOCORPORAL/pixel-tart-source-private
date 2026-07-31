# 北尾鸲归片助手工程迁移报告

生成时间：2026-07-29（Asia/Shanghai）

## 1. 项目根目录

`D:\AI AGENT\RAWSelectionAssistant`

## 2. 当前分支

不适用。来源目录不是 Git 仓库。

## 3. 当前提交

不适用。没有可验证 Git commit。

## 4. 是否存在未提交修改

无法判断。因为来源不是 Git 仓库，不能把当前文件与任何提交基线比较；未自动初始化、提交或生成伪造补丁。

## 5. 实际版本

`1.2.1`，由 `Directory.Build.props`、Branding、主项目、EXE 元数据和安装脚本共同确认。

## 6. 目标版本

`1.3.0`。免费版/专业版和授权能力仍是目标，不是当前实现。

## 7. 解决方案结构

- `RAWSelectionAssistant.sln`
- `src/RAWSelectionAssistant.Core`：核心模型与服务。
- `src/RAWSelectionAssistant`：WPF WinExe 主程序。
- `tests/RAWSelectionAssistant.Tests`：MSTest 测试项目。
- `installer/RAWSelectionAssistant.iss`：Inno Setup。

## 8. 迁移包目录结构

包含 START_HERE、README、Manifest、SOURCE_STATE、包内校验、source、17 份 docs、4 个 scripts、Git 不适用证据、最新安装包/构建报告以及 2 个安全示例配置。ZIP 顶层只有 `北尾鸲归片助手_工程迁移包`，没有随机临时目录。

## 9. Git Bundle 路径

未生成，不适用。来源不是 Git 仓库；证据位于迁移包 `git/NO_GIT_HISTORY.md`。

## 10. ZIP 路径

`D:\AI AGENT\RAWSelectionAssistant\迁移输出\北尾鸲归片助手_工程迁移包_20260729_1843.zip`

## 11. ZIP 文件大小

48,371,963 字节（约 46.13 MiB）。主要体积来自 1.2.1 正式安装包。

## 12. ZIP SHA-256

`85ACEF0C492C445F0DEB25DF28DC43987F1267411E31771EE70D5000BBBA5428`

ZIP 哈希同时写入 `迁移输出\CHECKSUMS_SHA256.txt`；包内 `CHECKSUMS_SHA256.txt` 校验包内 131 个文件。ZIP 不能把自身最终哈希嵌入自身，否则会改变哈希，因此使用外部同名校验文件。

## 13. 源码文件数量

95 个净化源码/工程文件。

## 14. 文档文件数量

`docs` 下 17 个编号文档；另有 START_HERE、README、SOURCE_STATE、Manifest、构建测试报告和本迁移报告。

## 15. 是否包含安装包

是。`artifacts/latest-installer/北尾鸲归片助手_Setup_1.2.1_x64.exe`，48,632,556 字节，SHA-256：`B1B2426A7859C46A6F66E71C7178085350F3CCD0C7E27379953F0C4D879D67AE`。

来源机 Program Files 中存在版本 1.2.1，已安装 EXE 与当前 publish EXE 哈希一致。安装包和 EXE未签名。

## 16. 是否包含真实照片

否。未包含客户 JPG、RAW、真实输出或 Tutorial 运行时生成数据。`Assets` 仅包含应用图标；教程示例由代码运行时创建。

## 17. 是否发现并清理敏感信息

扫描 89 个可迁移文本文件和敏感文件名；未发现密钥、令牌、私钥、证书、真实激活码、用户邮箱或内部服务器。安装脚本 DLL 导入语法曾被邮箱规则误报，已人工排除。

没有修改原工程。迁移副本排除了含来源机绝对路径且已被新文档替代的三份旧回传报告；提供空值 license example 与无本机路径 settings example。

## 18. 被排除的目录

`.vs`、bin、obj、TestResults、packages、node_modules、原 artifacts 临时构建、LocalAppData 设置/索引/日志/缓存、Tutorial 数据、照片、输出、真实 settings/license、env、用户文件、证书和私钥。原 artifacts 仅单独提取最新正式安装包。

## 19. 构建结果

通过。

- SDK：10.0.302。
- Restore：通过，1.230 秒。
- Release x64 build：通过，1.003 秒。
- 0 警告，0 错误。
- 净化源码临时副本也通过 `Restore-And-Build.ps1`。

## 20. 测试结果

通过：97；失败：0；跳过：0。净化源码临时副本通过 `Run-All-Tests.ps1` 并生成 TRX。

## 21. 当前授权实现状态

未实现。没有 ProductEdition、LicenseStatus、LicensedFeature、License Service/Provider、Feature Gate、Storage、Fingerprint 或激活界面。

## 22. 生产授权是否配置

否。生产授权服务尚未配置；Mock Provider 也不存在。当前不能正式售卖激活码。

## 23. 当前首次教程实现状态

21 步已实现，包含新用户强制、旧用户不锁定、Tutorial 沙盒、中断恢复、真实索引/匹配/复制/报告、路径保护和帮助重播。1.2.1 已修正第 12 步明细定位与备用入口。

## 24. 免费版和专业版实现状态

未实现版本区分。当前所有现有归片能力都在同一 1.2.1 应用中可用；30 编号、1 来源目录等免费限制和专业权益只是 1.3.0 设计。

## 25. 当前已知问题

- 无 Git 历史。
- Windows 11 未实机验证。
- 安装器最低版本实际为 Windows 10 19045。
- 未代码签名。
- 当前是单页工作台，无项目中心/历史。
- MainViewModel 较大，Raw 与 Media 两代服务并存。
- 当前主机阻止桌面自动化鼠标输入；代码、可访问树和测试已验证，但本次未物理重放 21 步点击。

## 26. 迁移到个人电脑后的第一步

解压 ZIP，让新 Codex先完整阅读 `START_HERE_FOR_CODEX.md`、Manifest 与 docs 01-15；随后运行 Verify、Restore-And-Build、Run-All-Tests。确认 97/97 后再制定开发计划，不要先重构。

## 27. 所有新增文件

本任务新增：

- `迁移输出/北尾鸲归片助手_工程迁移包`：132 个文件。
  - 根文件 5 个。
  - source 95 个。
  - docs 17 个。
  - scripts 4 个。
  - git 说明/状态文件 6 个。
  - artifacts 3 个。
  - examples 2 个。
- `迁移输出/北尾鸲归片助手_工程迁移包_20260729_1843.zip`。
- `迁移输出/CHECKSUMS_SHA256.txt`。
- 本报告。

包内所有文件的逐文件完整列表与哈希见包内 `CHECKSUMS_SHA256.txt`。

## 28. 所有修改文件

没有修改原项目源码、测试、README、构建脚本或安装脚本。本任务只新增迁移输出和本报告。

## 29. 所有未包含文件及原因

- 原工程 bin/obj/artifacts 临时产物：可重建且含本机路径；排除。
- 三份旧回传报告：含来源机绝对路径且被本包文档完整替代；排除。
- LocalAppData：真实用户设置、索引、日志、教程进度；排除。
- 照片/RAW/客户 JPG/输出：客户或用户数据；排除。
- Git Bundle：来源不是 Git 仓库；无法生成。
- .editorconfig、packages.lock.json、NuGet.Config：原项目不存在；未伪造。
- 授权配置/真实激活码：当前不存在；仅提供空值设计示例。
- 代码签名证书：当前未签名且证书不得打包。

## 30. 无法确认的事项

- 1.2.0 以前的精确版本号、日期和提交顺序；版本历史中已标“根据现有代码推断”。
- 是否曾存在未提交改动；没有 Git 基线。
- Windows 11 实机行为。
- 未来生产授权平台、价格、离线宽限最终数值和设备指纹策略。
- 项目代码/品牌素材在未来目标电脑之外继续分发的法律权限；本任务只按用户明确指示在本机创建迁移包，未上传或推送。

## 最终结论

工程迁移包已完成，真实可解压、可恢复构建、可运行全部测试，并准确区分了 1.2.1 已实现能力与 1.3.0 设计目标。
