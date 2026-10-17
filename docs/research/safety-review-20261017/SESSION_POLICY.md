# 会话保存与加载策略

项目文件是自包含编辑版本快照；SaveSession不再自动先写session→写repo→再写session。共享素材版本目录用原“保存版本目录”显式命令，已有CAS冲突/恢复快照不删除。项目可独立重开，不能宣称两类文件全局事务。

已加载/保存路径每workspace保留精确SHA，FileShare.None跨进程锁内再次比较后唯一临时写+atomic move。冲突不写入、无SessionSaved。未加载的已存在目标在公共Save API拒绝；UI可以明确确认替换并捕获确认时SHA，之后仍CAS。源图片路径永远拒绝。

加载解析Normalize、源存在/身份/缩略图、active真实渲染在隔离workspace准备；成功再提交。传入冻结document，准备期间项目hash变化拒绝。提交仍可能源变/取消，因此保留旧活动target、source/matched/尺寸、look/stack/film/engine、layout/filter/session、目标历史字典与undo/redo，并在catch恢复。失败不清旧batch历史，不报SessionRestored。

这不是仅回填Targets。真实测试在SessionCommit抛故障与SessionPrepared之后替换源，参数/undo/redo/保存可用。原尺寸stage准备会额外解码，内存和性能记录不能忽略。
