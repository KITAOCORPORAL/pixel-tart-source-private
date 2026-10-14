# 阶段1：受控图片路径

2026-10-14 Asia/Shanghai。fetch核验origin/feature/kitao-planning-tray-images-20261014 = 13b0efe689c0114f6f76b70c59a1059cabeddbef。新工作树N:/pixart/kitao-planning-portable-images-home-20261014；分支fix/kitao-planning-portable-images-home-20261014。

读取原DELIVERY/MIGRATION_CONTRACT/OPEN_ITEMS及图片/保存/恢复/历史/DOCX链路。公司D盘Release仅历史参考，未拿来本机运行。家里6个旧工作树已清点，旧源码树3个未跟踪审计/研究目录不动；其他原树干净。不重置、不合并、不删除其他树。

新CopyPath只写images/{DocumentId:N}/{DocumentImageId:N}.png；StorageRoot是JsonIgnore运行时信息，不污染持久元数据。读取逐张校验路径身份、禁止.或..段/非规范路径、越文档根及reparse/symlink、SHA256；缺失或哈希不符异常包含图片Title及文档/图片ID，不凭同名搜索。

兼容旧绝对路径：必须本身符合images/文档ID/图片ID结构；原路径存在先验证并使用，不因原副本篡改而回退。原路径不存在且新根准确身份位置可验证才迁移为相对元数据。旧有效绝对文件在其他根时，保存前复制完整副本至当前身份目录，校验后采用相对路径；不删原副本，不覆盖不同哈希目标。Store读取只改内存不改JSON，草稿/正式保存成功才落迁移。

RichLayout运行时绑定、Store主文件/草稿读取写入、undo/redo快照重新绑定、图片创建/选图清理及DOCX采用路径均接入。仍不是跨文件ACID；完整未引用副本保留。保守清单扫正式/草稿/DOCX恢复/旧迁移备份和当前内存undo/redo ID；未读完记录AUDIT_INCOMPLETE_KEEP，任何项DeletionAllowed=false，无自动删除接口。

专项Core3通过；既有托盘WPF6通过。初次DOCX原测试发现更换DocumentImageId却仍用新副本文件名，违反新路径合同；修复为复用哈希验证一致的原图片完整定位元数据，保留旧失败TRX。原严格门禁未放宽。
