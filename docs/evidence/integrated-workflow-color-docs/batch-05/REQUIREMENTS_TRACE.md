# 第五批需求追踪

日期：2026-10-11（Asia/Shanghai）。START_HEAD=c2f6dcf6dce84561a4e39f8b720f0ab4b10a4267。源码=84d7baaf696ddd82932071f175665ffa2f98d053。状态只评价本行范围。

| 工作项 | 实际实现 | CODE | TEST | CLI_PROCESS | RELEASE_STARTUP | DESKTOP_RUNTIME | USER_VISUAL |
|---|---|---|---|---|---|---|---|
| 旧标注后台保护 | 无读取快照仅初始化未修改空值；用户清空后 revision 仍受保护 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| CAS 撤销/重做 | 写入与成功项日志同事务，版本校验，连续撤销/重做与过期拒绝 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| UI 标注历史 | 素材库旧撤销 UX、Studio/联机标注撤销入口；权威刷新 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 版本冲突 UI | 本地/仓储/身份/原因；Reload/KeepLocal/Cancel；处理前后再次 CAS | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| 参数字段合并 | 未开放自动字段合并；整份选择或本地新版本，不平均/拼栈 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 跨库复制 | 明确目标库、唯一 hash 验证、确认后再验；选中版本复制、来源保留、幂等 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 导出恢复 | 受管理 encoded.bin、owner/task/pending、hash、状态、单执行者；第四批兼容 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| 取消清理 | 仅当前任务/hash 匹配临时产物可删；成片不删；编码中孤立文件保留 | PASS | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN |
| 联合文档历史 | RTF+语义/页面+光标快照，同一按钮动作一步，恢复后规范化 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 精确输入 | 列宽 5–250mm、各边距 0–80mm；Enter/失焦、Esc、非法值反馈 | PASS | PASS | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 字体/分页输出 | 固定中文英文表格分页文档恢复；缺失字体回退有记录；真实打印未跑 | PASS | PARTIAL | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |

本批可执行数据/命令测试通过；桌面和用户视觉未执行。字体替代跨机器、真实打印、全部视觉组合不升级为 PASS。USER_ACCEPTANCE=NOT_APPROVED。完整 Word 和专业 RAW 是独立后续范围，不用于掩盖本批限制。
