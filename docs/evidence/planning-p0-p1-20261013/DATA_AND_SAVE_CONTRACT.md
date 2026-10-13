# 数据迁移与保存合同

## 身份与兼容

保留外层Version=1；增加Document.FoundationVersion=1、Shot.PlanningFieldsVersion=1、Catalog.Revision。缺失的新字段默认旧版0，未来大于1明确拒绝。旧ProjectId/ShotId/ReferenceId不变；只有显式复制生成新ShotId/ReferenceId。

无Package的旧文档DocumentId使用SHA256("Kitao.PlanningDocument.v1:" + ProjectId的N格式)前16字节构造Guid，因此重复只读加载不会生成新ID。已有Package严格保留和校验；不覆盖已有文档身份。

旧Notes原文保留，首次读取迁入Action；Remarks是新字段。已经迁移后把Action清空不会再次注入旧Notes。字段值、CustomBody、RTF与未知JsonExtensionData保留。首次正式迁移前，原JSON原字节写入同目录 .foundation-v0.bak，不覆盖已有备份。备份失败则拒绝正式替换。

列ID是Guid；值键为custom:{id:N}。移除列只设Archived，可恢复，数据不清除。非法分钟显示文本保留在draft:EstimatedMinutes，正式保存校验拒绝负值/超范围，不偷偷改成0。

## 两文件可恢复写入

1. View行编辑实现IEditableObject：BeginEdit保存快照，CancelEdit不发布；EndEdit只产生草稿，不假报已保存。
2. Flush入口调用CapturePendingEditorDraft，包含当前DataGrid单元格/行与RichEditor；主窗口关闭和离开Planning也走门禁。
3. 将文档、Summary、全部待保存镜头写入持久 .draft（临时文件+flush+rename），再校验整批镜头及文档身份。
4. SaveBatchAsync在单文件锁内合并稳定ShotId，整批原子替换shots.json；随后替换planning.json。两者成功才删除对应pending项并清草稿。
5. 镜头成功但文档失败时草稿仍存在，重开将新增/归档/排序镜头恢复，按相同ID重放，不再生成复制身份。旧有效正式文件不被坏JSON或部分字节覆盖。
6. 若磁盘连草稿也无法写，保留当前进程内草稿并阻止离开；不承诺进程被强杀后的持久恢复。两个JSON不是跨文件事务，不声称多进程冲突防护已完成。
7. 原RichEditor延迟回调冻结ProjectId+DocumentId，不能把旧编辑器卸载时的正文写入新策划；相同RTF/布局重复flush不重启永久自动保存循环。

## 后续双模式接口

EnsureIdentity(projectId)提供稳定文档接口；ShotDraft(id)读取当前有效草稿；StageShot(typedShot)写入同一保存队列；ShotColumns通过稳定ID提供列定义；CapturePendingEditorDraft用于切换/关闭前提交UI编辑；SaveBatchAsync可按镜头ID批量提交。当前仍不是块SourceIds同步、复合撤销或Word语义树。
