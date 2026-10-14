# 迁移样本结构

legacy-before保留原RTF身份标记/图片ID/副本哈希/位置/图注等，CopyPath为$OLD_PLANNING_ROOT/images/{DocumentId}/{ImageId}.png；占位符仅用于交付脱敏说明，不是可加载真实路径。
legacy-after为实际新根读/主文件锁失败/草稿恢复/成功写入后的JSON，CopyPath已相对；DocumentId/ImageId/来源不因迁移改写。
relative-after为真正整体复制并撤走原根后，同图多次和表内图的相对定位摘要。运行时StorageRoot不序列化。

原始合成样本和嵌图DOCX留artifacts/portable-evidence；未包含客户照片。原测试结果和身份摘要证明迁移，不从报告里的公司D盘寻找副本。脱敏只改测试临时根前缀，不改任何ID/哈希/图片顺序/位置/图注。
