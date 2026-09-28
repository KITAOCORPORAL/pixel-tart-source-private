# Sync Conflict Policy

普通备注可以按 Last Write Wins；booking date、start/end、price、deposit、payment state、client identity 等高风险字段由 `SyncConflictPolicy` 生成显式 `SyncConflict`，不得静默覆盖。Tombstone 保留并参与同步，时钟只用于排序，revision 才是应用门槛。
