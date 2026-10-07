# 本机私有证据索引（脱敏）

本文件只描述证据类别和仓库相对定位，不包含盘符、用户名、私人绝对路径、原始照片文件名、客户资料或账户信息。`USER_VISUAL_REVIEW=NOT_APPROVED`。

## 证据范围

- 本轮运行对象：同一 `color-studio-rebuild-2026-10-05-r1` Release；身份见 [BUILD_IDENTITY.json](BUILD_IDENTITY.json)。
- 公开证据：本目录 `evidence/` 下 87 张完整窗口 PNG；已逐张复核，均标记 `reviewedPublicOutput=true`。
- 原始截图：343 张完整窗口 JPG，保留在本机的 `LOCAL_REVIEW_ROOT/private-captures/`，没有复制到公开目录。
- 运行会话：7 个会话记录，保留在本机私有会话根；公开 `SESSION_IDENTITIES.json` 仅保留匿名会话、DPI、窗口和构建身份。
- 输入：CC0 合成测试图片的哈希见 [INPUT_IDENTITIES.json](INPUT_IDENTITIES.json)；授权私有 RAW 只用匿名代号 `R01`，原件和映射不入 Git。
- 导出和工作文件：UI 实际导出、冻结工作文件、重开文件、比较 JSON 留在本机私有运行根；公开目录只保留脱敏的 `EXPORT_COMPARISON.json` 摘要。
- 验证文件：本机共有 6 个验证 JSON，包含导出、持久化、过滤和隐私审查结果；公开目录不复制私人输入或原始日志。

## 相对定位约定

- `LOCAL_REVIEW_ROOT`：本轮运行证据本机根，仅作匿名代号。
- `LOCAL_REVIEW_ROOT/private-captures/`：343 张原始窗口截图；包含真实操作上下文，LOCAL_ONLY。
- `LOCAL_REVIEW_ROOT/sessions/`：7 个会话及动作记录，LOCAL_ONLY。
- `LOCAL_REVIEW_ROOT/exports/`：UI 导出文件，LOCAL_ONLY；未把验证器生成文件当作 UI 产物。
- `LOCAL_REVIEW_ROOT/workfiles/`：冻结工作文件及重启复开副本，LOCAL_ONLY。
- `LOCAL_REVIEW_ROOT/verification/`：6 个脱敏验证摘要，部分摘要已复制为本目录 JSON。

## 公开证据与私有证据关系

公开 PNG 只用于证明窗口布局、交互状态和可见结果；它们不包含原始照片文件和导出目录。私有证据用于保留完整操作顺序、原始截图、导出像素和工作文件，不能被公开文档中的“PASS”替代。没有公开图的步骤仍按 [RUNTIME_CHECKLIST.md](RUNTIME_CHECKLIST.md) 的 PARTIAL / NOT_RUN 记录。

## 已知问题证据定位

- CU-01：公开图 112、114、123；完整原始序列留在 `private-captures/`。
- CU-02：公开图 105–107；展开窗口连续操作原图留在 `private-captures/`。
- CU-03：公开图 100–101；同步弹层前后及动作记录留在私有会话根。
- CU-04：公开图 114–115；语言切换完整动作记录留在私有会话根。
- CU-05：公开图 299–304；非法输入和 Escape 的连续截图留在 `private-captures/`。
- CU-06：公开图 063、065–067；影调区间滚动和高亮完整原图留在 `private-captures/`。

## 隐私检查

- 未将私有 RAW、客户信息、账户信息、完整工作文件或原始动作日志提交到公开目录。
- 未在本索引或公开 Markdown 中写入机器用户名、盘符或私人素材文件名。
- 公开截图中的本机导出目录区域已遮盖；图片未裁切或缩放。
- 若后续修复需要重放私有输入，必须建立新的本机证据索引并重新审阅公开副本。
