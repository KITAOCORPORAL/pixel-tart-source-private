# Color Studio 本轮交付现场

日期：2026-10-05。**NOT_READY_FOR_USER_RETEST**。本轮已实现、测试和发布；同一 Release 的完整运行验收尚未完成。CODE、TEST、RELEASE_RUNTIME 与用户批准分别记录，不能互相代替。

## Git 与发布身份

- 实际工作树：`N:/pixart/pixel-tart-source-private`。
- 集成分支：`integration/pixel-tart-developer-preview`。
- START_HEAD：`5427406c4ef3c805ef56fbb814251df69365032d`；起点工作区干净。
- 实现提交：`11379a66072ba88cce61dc1d16efd097262680cd`。
- 源文件末尾规范提交：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`；仅删除4处尾部空行。
- r1 SourceHead：`31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`。
- EXE：`N:/pixart/pixel-tart-source-private/artifacts/releases/color-studio-rebuild-2026-10-05-r1/publish/win-x64/KitaoPhotoSelector.exe`。
- DLL ProductVersion：`2.3.0+31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`；Release / x64 / win-x64 / self-contained。
- EXE SHA256：`C9841FB530ABF0E209B8E9BB9B6870C917D54DED985702B2EF41F36EE6DB28CA`。
- 287个发布文件长度和SHA256逐一校验，零缺失、零差异。后续证据提交只含文档与manifest，不改变该EXE对应的生产源码。

## 实际完成与证据入口

| 内容 | 可核对记录 | 限定结论 |
|---|---|---|
| 布局、参数数学、处理链、存储与兼容 | [实施矩阵](IMPLEMENTATION_MATRIX.md)、[参数契约](PARAMETER_CONTRACT.md)、[设计](COLOR_STUDIO_DESIGN_SPEC.md) | 有真实实现；逐项CODE状态不是UX验收 |
| 自动测试 | [TEST_RESULTS.md](TEST_RESULTS.md) | 完整Core1572/0/6；完整WPF1486/0/12早于末轮4项P2，后续P2为50/0/1、Core9/0/0；不合并成一次全套 |
| 同版运行 | [RELEASE_RUNTIME_REPLAY.md](RELEASE_RUNTIME_REPLAY.md)、[证据索引](EVIDENCE_INDEX.json) | r1已启动、重开3图批次；1600×920局部布局与冷暖/曲线/色阶/清晰度有01–05图证；完整重放仍PARTIAL |
| C1调查 | [C1_FEATURE_BEHAVIOR_MATRIX.md](C1_FEATURE_BEHAVIOR_MATRIX.md) | 实际16.6.1观察与12篇官方文档分开；完整调查PARTIAL，不声称私有算法复刻 |
| 输出像素核查 | [QUICK_EXPORT_FORMAT_CONTRACT.md](QUICK_EXPORT_FORMAT_CONTRACT.md) | 工具直接载入r1 DLL，PNG/JPEG/RAW→TIFF自检差0，改变曝光后正确失败；尚无最终UI导出产物，不能据此关闭RT12 |
| 修改范围 | [FILE_CHANGE_LIST.md](FILE_CHANGE_LIST.md) | 54生产文件、22测试文件，另列阶段证据和manifest；原件/二进制/原始日志不入Git |
| 未完成项 | [OPEN_ISSUES.md](OPEN_ISSUES.md) | 完整3D联动、批量、最终UI导出、重启、不同窗口/DPI等仍缺运行证据 |

## 现场保护与继续条件

- 本轮授权的10份ARW/JPEG原件再次核对SHA256，10/10与起点一致。记录在本地 `artifacts/color-studio-rebuild-2026-10-05/input-recheck-final.json`。
- 启动会话PID36892，日志明确 `ProductSourceSha=31d3ac3ae56d6ccd1d9b4c608bf522dbb735d591`。最后非UI查询进程响应正常；该读数不证明后续交互全部通过。
- 用户最新粘贴的Windows全局规则禁止Computer Use，与早先本轮桌面操作授权冲突。已经请求明确本轮例外；得到答复前停止新的桌面输入和截图，不使用其他自动化绕过。
- `05-clarity-37275.jpg` 是已取得截图缓冲的落盘，没有在上述等待期间新增桌面输入。未取得的DPI仍为未知。
- 后续若继续，恢复同一r1并先观察实际窗口；若修改生产源码，需构建新的唯一Release并重放，不混用r1与新构建证据。
- `VisualApproved=false`、`UserVerified=false`、`USER_VISUAL_REVIEW=NOT_APPROVED`。未创建Installer，未推进下一阶段。
