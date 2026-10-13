# P0 / P1a / P1b 交付与逐项状态

基线：origin/feature/kitao-canvas-toolbar-20261013 = 9e95239c51d7ebc551a62804541726934b37437d。
独立工作树：N:/pixart/kitao-planning-foundation-20261013。
分支：feature/kitao-planning-foundation-20261013。
实施前读取基线真实Core模型、Store、VM、View、关闭和导航链路、既有测试及 PLANNING_IMPLEMENTATION_BATCHES.md。未把Grok的旧支持判断当代码事实。未发现本工作树及N盘父级的AGENTS.md，遵守会话限制。

|范围|实际交付|状态/边界|
|---|---|---|
|P0 稳定文档身份|旧无Package文档从命名空间+ProjectId派生固定DocumentId；已有ID保持；只读加载不写盘|代码完成，合成迁移/重开验证|
|P0 无损迁移|保留旧Body/CustomBody/Notes；Notes仅首次迁Action，单独Remarks；扩展字段保留；首次迁移保存原文件字节备份|代码完成；不声称支持未知未来schema|
|P0 安全保存|先写持久恢复草稿，整批镜头校验，镜头批量原子写；两类正式文件成功才清待保存及草稿|代码完成；跨文件是可重放恢复，不是数据库事务|
|P0 失败/取消|校验、IO、取消不清内存草稿；非法分钟原文字也存草稿；切分区/项目/主工作区及关闭前提交编辑并检查保存|代码完成；实机导航NOT_RUN|
|P1a 项目概览|标题、地点、人员和6类Summary字段可编辑与保存|代码完成|
|P1a 文档内容|旧CustomBody原位复用、改名入口；旧正文/RTF和附件关联保留，不搬客户文件|代码完成；不是P3自由块同步|
|P1b 编辑表|11个核心列：镜号、名称、场景、景别、机位镜头、动作、道具、灯光、分钟、状态、备注|代码完成；原参考图/分镜关联仍走已有数据，不新增缩略图列|
|P1b 排序/复制/归档|上下移动/底层顺序重排，复制生成新ShotId与引用ID，归档确认保留记录和源文件|代码完成，保存重开验证|
|P1b 自定义列|稳定ColumnId；增加、隐藏、恢复；按ColumnId存单元格；隐藏不丢值|代码完成；类型为文本，列类型编辑不在本批|
|后续接口|EnsureIdentity、PlanningShotColumn、ShotDraft、StageShot、CapturePendingEditorDraft、SaveBatchAsync|明确接口；完整双模式仍未完成|
|Release|win-x64自包含及实际发布托管组件回归|见TEST_MATRIX/RELEASE_IDENTITY|
|桌面/WPS|未启动桌面或发送输入，未做WPS编辑导回|NOT_RUN，不能提升为通过|

## 仍保留的范围

15摄影分区整体路由中的道具/设备/人员/场地/时间表/预算等专门模块仍属于后续P2，当前不放假可用入口。本批扩充为项目概览、文字、参考图、情绪板、镜头清单、灯光图、服化道、文件、文档内容9个真实入口；不声称整个P1a原表的15分区都完成。
完整Word、WPS往返、双模式复合编辑、DOCX结构回写、P8多进程expectedRevision/冲突副本/三方合并、列拖动/复杂类型均仍待后续批次。现有受限DOCX/PDF不能据此称完整支持。

## 资料与安全

只使用合成文字、两份临时项目文件、隔离临时SQLite数据库和文件锁故障注入；无客户照片/生产数据库/凭据。构建二进制留ignored artifacts，不提交。旧工作树和研究原件保留。没有改Color Studio、PV4/PV5/PV6、胶片或照片导出算法。
