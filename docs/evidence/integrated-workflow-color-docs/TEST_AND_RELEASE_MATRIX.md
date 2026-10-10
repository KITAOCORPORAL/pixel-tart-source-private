# 测试与 Release 矩阵

## 自动测试

| 套件 | 结果 | 说明 |
|---|---:|---|
| 身份桥聚焦 | 102 通过 / 0 失败 | 五态解析、导入身份、迁移、回滚、幂等和冲突 |
| 色彩基础聚焦 | 43 通过 / 0 失败 | 矩阵、白点适配、往返、非裁切及既有处理回归 |
| 策划编辑 Core 聚焦 | 7 通过 / 0 失败 | 范围编辑、空选择、编号、撤销友好操作 |
| 策划编辑 WPF 过滤 | 0 失败 / 1 跳过 | 工程构建通过；无匹配可执行 UI 自动化 |
| 完整 Core | 1591 通过 / 0 失败 / 6 跳过 | 总计 1597 |
| 完整 WPF | 1509 通过 / 0 失败 / 13 跳过 | 总计 1522；仅既有分析器警告 |

## Release 构建

- 命令：`dotnet publish src/RAWSelectionAssistant/RAWSelectionAssistant.csproj -c Release -r win-x64 --self-contained false`
- 结果：PASS
- 构建源码 HEAD：`6c59f4a4bf2399e535c74ee8247d90bb35e56fe3`
- 本机输出：`artifacts/integrated-workflow-color-docs/release/win-x64/KitaoPhotoSelector.exe`
- EXE SHA256：`6476D024DA74E2754A88C39304214BD8E8F428BEFD841247B06AC8E31E2C373F`
- 主 DLL SHA256：`933AFDAA36174C75A62CF012D3831FB70C1CA859172635B0F2E367E1398CE62D`
- DLL ProductVersion：`2.3.0`

构建物和 TRX 位于忽略目录，没有提交 Git。

## 状态总表

| 交付 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE | PUSH |
|---|---|---|---|---|---|
| A1 稳定解析 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| A2 导入身份 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| A3 加法迁移 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| 宽色域数学基础 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| 专业 RAW 新管线 | PROPOSED | NOT_RUN | NOT_RUN | NOT_APPROVED | NOT_APPLICABLE |
| 策划结构化文本基础 | PASS | PASS | NOT_RUN | NOT_APPROVED | 见推送记录 |
| 完整 Word 功能 | PARTIAL | PARTIAL | NOT_RUN | NOT_APPROVED | 见推送记录 |

总状态：`NOT_READY_FOR_USER_RETEST`。原因是 Release 实机未运行、专业 RAW 新管线和完整文档编辑器仍未实现，用户验收未批准。
