# 追加时区勘误（不覆盖历史）

按用户本批明确要求，业务验收基线的当地日期/时区记为2026-10-15 Asia/Shanghai。客户端本轮会话另报Asia/Shanghai；二者分别保留，不据此改写主机日志或推算并改写已封存时间。既有把Asia/Shanghai写成此业务验收当地时区的字段/叙述逐文件逐行列于01_TIMEZONE_FIELDS.json。

影响范围仅报告叙述、01_START.timezone、BUILD_PROVENANCE.timeZone及它们的清单hash；不影响原ICC/RAW输入、逐点数值、产品版本、测试计数、Release文件。旧文件和原始+08日志一个字节也不改。此勘误为追加材料，旧manifest继续验证旧文件而非被“修复”重算。
