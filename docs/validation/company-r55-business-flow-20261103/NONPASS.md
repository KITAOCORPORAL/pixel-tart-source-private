# 失败、未执行与证据边界

记录：2026-11-03（Asia/Shanghai）。最终所选13项PASS不抹掉以下历史结果。

| 记录 | 结果/原因 | 处理 |
|---|---|---|
| raw/baseline-business-host.trx | 1FAIL：切B仍保留A的2行/旧成功状态；当时oldExpenseVisible=false | 仅修瞬时状态，不声称已观察到可见跨项目旧行 |
| raw/baseline-expense-scope.trx | 3FAIL：ABA旧成功、A迟到失败污染B、空结果语义不明确 | 原始错误不改；最终同方法3PASS |
| raw/baseline-independent-business.trx | 1PASS：原持久关系本已正确 | 不重写项目/策划/日历/财务模型 |
| raw/business-related-regression.trx | 11项中1FAIL：同一路径斜杠形式字面比较差异 | raw/existing-business-canonical.trx以规范化绝对根重跑1PASS，测试未改 |
| raw/release-final-business-host.trx | 接续复跑1FAIL：已有合成资料里的档期冲突 | 原根保留；新final2根+拒绝复用门禁；没有关闭产品冲突检查 |
| raw/release-final2-*.trx | 最终4组共13唯一/13执行PASS | 不将开发复跑累计成覆盖数 |
| raw/publish.log | CS0067/CS0649原警告，publish成功 | 不隐藏、不算全仓无警告 |

NOT_RUN：真实鼠标、有效DPI/缩放、多屏、ACM/HDR、独立Office/WPS实际排版、自然照片与20MP性能/数值专项。未启用ComputerUse、Browser、WebView、CDP；未操作用户WPS，未访问日常账本。权限失败是受控delegate注入，未更改真实数据库ACL。账本摘要是逻辑交易行SHA，不是DB/WAL字节恒等。

旧灯台、DOCX图片、保存失败恢复和JPEG/ICC的既有完成/失败边界沿用原报告，本轮不重复专项。下一项是公司原生鼠标/DPI下A/B业务链确认，不新增云/AI/通知/财务系统。总体仍PARTIAL_DIAGNOSTIC_NOT_FOR_RELEASE。
