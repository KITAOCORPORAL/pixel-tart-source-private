# 追加时区勘误（原始证据不重写）

用户指定本批业务验收当地日期／时区为 **2026-10-15 Asia/Shanghai**。客户端会话另提供 Europe/Berlin；业务标签和客户端时钟分别记录，不互相替代。

旧证据误写 Europe/Berlin 的位置：
- 01_START.json 第3行 timezone：运行上下文标签；
- 02_RAW_CONCLUSION.md 第21行：RAW结论的当地日期说明；
- 03_NATIVE_CONCLUSION.md 第7行：实机未执行记录的时区说明；
- BUILD_PROVENANCE.json 第36行 timeZone：构建上下文标签；
- DELIVERY.md 第3行：交付报告日期说明。

完整原句见01_TIMEZONE_FIELDS.json，路径均在旧 validation-closure-20261015 证据目录。上述字段影响报告叙述／上下文标签及其清单哈希，不影响输入文件、ICC／RAW逐点数值、Release身份或测试计数。旧文件、旧manifest、原始 +08 时间戳字节均不改；本文件仅追加勘误。
