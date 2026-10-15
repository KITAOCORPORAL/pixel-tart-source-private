# 追加时区勘误（不覆盖历史）

按用户本批明确要求，业务验收基线的当地日期/时区记为2026-10-15 Asia/Shanghai。客户端会话另报Europe/Berlin，二者分别记录，不修改原始+08日志。既有Europe/Berlin字段逐文件逐行列于01_TIMEZONE_FIELDS.json。

影响仅叙述/上下文标签及对应清单hash，不影响输入、ICC/RAW逐点结果、Release或测试计数。旧文件/manifest不改；此文件为追加勘误。当前用户可见时间仍依客户端时钟，不据业务标签重写主机时间。
