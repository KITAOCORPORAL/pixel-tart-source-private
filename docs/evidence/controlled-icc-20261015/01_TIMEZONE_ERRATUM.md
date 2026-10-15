# 追加时区勘误（不覆盖历史）

按用户本批明确要求，业务验收基线当地日期/时区为2026-10-15 Asia/Shanghai。客户端会话另报Europe/Berlin，两种上下文分别记录。旧证据的Europe/Berlin字段位于01_TIMEZONE_FIELDS.json列出的五个文件和具体行；原文件和原始+08时间戳字节均不重写。

影响仅报告叙述、上下文标签及对应清单hash，不影响输入、ICC/RAW逐点数值、Release身份或测试计数。旧manifest不改；本文件是追加勘误。客户端时钟不据业务标签改写。
