# 公司整合交付 · 2026-11-16

客户端时区 Europe/Helsinki。**PARTIAL：最终源码完整诊断包及服务已准备；原公司与合成 JPEG 闭环有真实历史回执；最终源码全段桌面复验、iPhone 双向修改仍未完成。**

## 源码、公司未提交成果与包

独立整合树 `E:/Kitao-Worktrees/company-functional-20261116`，分支 `integration/company-functional-20261116`，接续家中 `0a45ee0e7971c18c6bb4b7e287bced86c448263b`。本轮 fetch 后家中远端未前移。

最终功能源码 **e8180221937fa4abb578a16faacebe99a5d4ac1b**。App、选片服务、档期服务、浏览器原生桥接的 EXE/DLL 均为 `2.3.0+e8180221…`；共 10 份组件/别名绑定逐一核对哈希。浏览器扩展与该树逐字节比较。最终 ZIP、SHA-256、清单和验证回执见 `PACKAGE_DELIVERY.json`、`PACKAGE_VALIDATION.json`。远端交付提交独立记录在 `REMOTE_READBACK.json`；其 src/tests 必须与功能源码完全相同。

原公司树 HEAD `4aa17e64` 的未提交成果与逐字节备份保留；没有 reset、stash、clean 或覆盖。隐藏胶片预览的三个源码文件和对应测试经差异核对，以 `e8180221` 整合；剩余回放工具与探索材料仍保留原树，没有机械并入产品。见 `HIDDEN_PREVIEW_INTEGRATION.json` 和 `SOURCE_AUDIT.json`。

家中报告冻结于 be2f9c70，旧待办落后于后续提交。a9d1bb96 已加入来源策略修复；edd6d25e 对应 17a95b5a，稳定 patch-id 本轮重查完全相同；e28074f1 对应 14eda5c7，prepared 栅格缓存转移、清空和失败回滚均保留，差异只增加家中 WorkflowProjectId 的保存、重开和回滚。未重复应用补丁。公司另外包含 a9932b73 的 sRGB 快照恢复通知守卫、49fbc2f6 的现有索引编辑/发送选片可见入口，以及 e8180221 的隐藏预览生命周期修复；不改像素公式。

公司没有家中原压缩包，不能证明家中原 ZIP 字节；本次公司最终包通过组件源码版本和清单确认包含上述修复。家中项目身份、客户结果与来源字段保留。

## 实际流程与来源重扫

**完整普通 EXE 流程回执的源码为 49fbc2f6；不能把它改标成 e8180221。** 该批次原始记录完整保存在 `history-49fbc2f6/`，主回执 `SOURCE_RESCAN_PROOF.json`、`ACTUAL_WORKFLOW.json` 继续保留原 source。

原 workflow-runtime 的项目 c5bc4616-66eb-43eb-8fb6-70fbcc7f0e0b、预约 b9c03c62-6484-44f5-a1a0-9c9fa472cb26/revision2、客户锁定结果 v1/r4 保持。旧版本已把来源改成 edited-jpeg，之前通过正常原生选择器明确恢复到原授权 source-jpegs，再实际收回、保存、独立重开并重扫。来源仍 1 个，JPEG2/RAW0，中间目录未加入；免费版第二来源正常拒绝。未重写旧客户确认或修改数据库制造通过。

家中运行数据不可用，独立公司 batch-01 使用两张自有合成 JPEG。项目 e9fdf6c1-4950-4010-a939-a30af961216d，相册 5960a0d8-c9c8-4a1a-81ac-93564599795b。实际普通 EXE/标准文件选择器和桌面浏览器完成索引→编辑曝光+0.5→项目工作文件→编辑中间 PNG→代理2/2并发布→浏览器逐项选择并确认 v1/r6→PC收回同一项目→保存/正常关闭/独立重开→重扫→正常导出两张 JPEG→再独立重开。完整匹配2，冲突/未找到0，项目仍1、来源仍1、免费限制不变。输出均1200×800、8位、ICC3144字节；侧车身份和哈希核对一致，原图未改。

最终 e8180221 已有普通窗口批次收回/重扫回执 `FINAL_BATCH_RETURN_RESCAN.json`，本轮读取最终正常窗口：批次完整匹配2、已索引2；原窗口加载原项目。另以只读 SQLite、工作区文件和实际文件哈希核对原项目、预约/客户结果、来源数量与两张导出文件保持。证据见 `FINAL_SOURCE_VALIDATION.json`、`FINAL_ORIGINAL_CHECKPOINT.json`、`FINAL_BATCH_CHECKPOINT.json`、`FINAL_OUTPUT_HASH_RECHECK.json` 和两份 AX 文本。

**最终 e8180221 的完整“重新编辑/发布/确认/导出/独立重开”序列未在本轮重跑完成。** Computer Use 首次返回 `coordinate input geometry is unavailable`，两次截图捕获超时；备用通道明确报告物理 Escape 已停止桌面控制，当时停止全部界面输入。用户随后明确继续，使用open-computer-use备用原生MCP通道恢复了语义动作，实际重扫JPEG2/RAW0并通过当前项目入口重开两张原始JPEG；见FINAL_NATIVE_CONTINUATION.json。后续发送客户选片动作调用没有返回，独立观察没有新prepared目录；仅结束自建MCP客户端，正常App和数据保留，全段仍未完成。没有把只读状态检查、旧导出哈希或单元测试当成新一轮完整原生验收。

## 最终检查、启动与手机准备

最终源码 Release/x64 定向回归 **39/39**：来源/工作文件身份5，栅格缓存、prepared事务、sRGB和隐藏胶片预览34。最终 TRX 单列，原49源码33项回执保存在历史目录。所有测试都不替代手机或40MP普通入口验收。

公司路径配置分开原 workflow-runtime、batch-01 与手机 runtime。三个现有服务本轮健康检查通过；最终包启动入口分别在 Windows PowerShell 与 pwsh 实际执行，均复用现有批次窗口及服务，无第二写入者。最终 source-bound App 的2GiB/50ms守卫继续运行；阶段峰值见 `FINAL_RESOURCE_SUMMARY.json`，含等待/对话框/自动化，不是纯操作延迟。不注入强制GC。

手机服务 PID53528、普通日历 App PID2820 均来自 e8180221；原预约 a08ae657-84ce-43e5-948c-a5f39aae32e9/revision4、当前设备保持，带凭据GET200、匿名401。凭据有效至 **2026-11-18 06:17 Europe/Helsinki**。本轮未修改预约或再次轮换凭据。

Tailscale 本轮仍 **NeedsLogin**；`tailscale up --timeout=5s` 已签发当前本人登录链接，匿名打开HTTP200。链接仅在聊天和包外 `TAILSCALE_LOGIN_PRIVATE.txt`，不入Git/包。服务、原预约与接续工具均已齐备。本人登录后使用包中 `Continue-CompanyPhone.cmd` 或工具 `Continue-CompanyMobile.py --apply`，核对原 tailnet、公司 Self DNS、专用可信HTTPS和原设备后才交付手机访问链接。接续脚本与时区观察服务依赖一并收录，手机令牌/链接仍在原私有运行根。当前没有真实 iPhone 双向修改证据。

## 仍需完成

1. 桌面控制恢复后，在最终 e8180221 完成全段普通 EXE 流程复验；旧49真实闭环和新e818定向复验保持各自标签。
2. 本人完成 Tailscale 登录后，继续 iPhone→PC→iPhone 修改及两端重开；此刻仍未建立公司可信HTTPS。
3. 40MP TIFF：原 a993普通窗口导入触发2GiB的 STOP/null-exit 回执继续保留。e818包含隐藏预览修复；另一62191dff R2普通窗口验收仍在进行，不能借用它提升本包为通过。守卫不放宽，不追加RAW解码。
4. 旧0a窗口 CanvasOwnerClosing 重入现象保留；未保存内容已另存，独立公司批次正常关闭不等于通用关闭边界已修复。

包排除 SQLite、源图/像素缓存、owner key、token、本人登录链接与客户私有URL；不生成安装器。相机SDK/硬件科学门禁、RAW后继授权及高光研究维持原边界。完整产品不能发行。
