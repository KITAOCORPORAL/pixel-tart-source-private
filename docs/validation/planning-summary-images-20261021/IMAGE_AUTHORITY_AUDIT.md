# 本批图像权威与范围

1. PlanningDocumentPackage.RichLayout.Images: 项目自有PNG副本；DocumentId/ImageId、规范images/DocumentId/ImageId.png、CopyHash、StorageRoot、Order、Placement、ShotId、原素材LibraryId/AssetId/ContentHash均有已有合同。仅它们候选嵌入。
2. ProjectShotReference/PlanningDocumentReference: LibraryId/AssetId或ExternalReference非拥有引用；保留可读状态，不因本机存在嵌入。若与自有副本Source的库/素材身份明确相同，可呈现已验证的项目历史副本，明确不代表库当前版本；不会读取库原件。
3. PlanningLight.ReferencePath: 非拥有外链，只有其路径精确指向当前文档自有副本且该副本身份/Hash验证通过才可附图；不按标题/文件名猜测。
4. PlanningCanvasRevision.PreviewPngBase64: 已有快照可作元数据，但本批不扩展为任意可编辑灯位/画布图渲染。没有Source匹配的Placement按原Order映射分区，未知位置保留文档内容区并说明。
5. Body中的自有图片仍保留原标记位置；同时按Placement/稳定Source对应在分区呈现，跨分区重复使用同DocumentImageId，不新建项目状态。外链缺失/变化/权限错误原位说明。


## 最终准入与限制

Read(meta,documentId)要求当前项目StorageRoot、规范路径、相同DocumentId、无链接、33B–64MB、SHA256、PNG IHDR最长边16384/总像素4000万、单帧解码。FileShare.Read阻止普通同时写/删，读取hash和解码使用同一bounded bytes。非当前项目旧绝对副本不自动读取/搬迁；库引用Source只用于选择已有项目副本，说明是历史副本而非库当前原件。已存在DOCX拒绝覆盖，导出stage最终Move不覆盖，竞态也不能改旧DOCX。

映射：封面→概览；光标/文字→正文原位置；指定镜头→镜头区；参考图→参考区；情绪板→情绪板；灯位图/灯光图→灯位；服化道与道具/服化道→对应区；未知位置→文档内容。无RTF位置的光标图片在文档内容明确说明，不静默遗漏。按Order稳定排序，同区按DocumentImageId去重，跨区和正文重复仍同ID。
