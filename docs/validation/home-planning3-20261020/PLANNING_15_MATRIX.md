# 策划15分区 × 业务闭环（限定范围）

项目 `5c35c6bc-3aa7-4ad6-9a4f-5d60c5193db4`；文档 `0888c455-e25d-5f7e-6403-a2bfd4b9fe76`；镜头 `ecdcaceb-14e3-41ba-ad9e-b2bd3d165ba6`。

所有PASS仅为本轮自动数据/控件合同；真实鼠标统一NOT_RUN。PARTIAL不因界面入口存在变PASS。

|分区|创建|编辑|关联|保存重开|导出|搬迁恢复|
|---|---|---|---|---|---|---|
|项目概览|PASS · CreatePlanningCommand新建|PASS · VM标题/人员/目标/关键词，不是实鼠标|PASS · 同ProjectId/DocumentId|PASS · 同项目/文档身份回读|PARTIAL · 富文本分支不自动汇总概览|PASS · owned planning/shot根搬迁，原DB路径保持|
|文字|PASS · 生产ViewModel/服务创建|PASS · 当前WPF RichTextBox改正文并保存|PASS · 正文自有图片关联DocumentId/ShotId|PASS · 同项目/文档身份回读|PASS scoped · 持久RTF+伴随布局、DOCX、图片式PDF|PASS · owned planning/shot根搬迁，原DB路径保持|
|镜头清单与分镜|PASS · NewShotCommand|PASS · 绑定DataGrid行事务名称/动作/分钟|PASS · ReferenceId与资源ShotIds；非任意引用图谱|PASS · 同项目/文档身份回读|PASS scoped · DOCX稳定ShotId/名称/场景/动作表|PASS · owned planning/shot根搬迁，原DB路径保持|
|参考图|PASS · 生产ViewModel/服务创建|PASS · 分类/情绪板分组；当前分区分类回归|PASS · 稳定ReferenceId连接镜头，正文另生成自有副本|PASS · 同项目/文档身份回读|PARTIAL · 正文已插入副本可导出；普通外链不自动全导出|PARTIAL · 外部原件绝对引用保持；不自动打包迁移|
|情绪板|PASS · 已有参考加入情绪板|PASS · 分组；未测自由排版|PASS · 与参考图共享ReferenceId|PASS · 同项目/文档身份回读|PARTIAL · 未自动导出完整情绪板版面|PARTIAL · metadata可重开；原件外链不便携|
|灯位图|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|服化道与道具|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|设备|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|人员|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|场地|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|档期|PASS · 真实ShootBookingService隔离SQLite|PARTIAL · 关联回写ProjectId；本批未做档期各字段编辑|PASS · booking.ProjectId与planning.BookingId双向|PASS · 同项目/文档身份回读|PARTIAL · 富文本导出不保证档期字段|PARTIAL · planning保存BookingId，DB保留原隔离位置；非DB跨机搬迁|
|当天时间表|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS · 稳定ResourceId→ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|预算|PASS · 当前WPF新增资源modal|PASS · 当前WPF行选择/检查器编辑modal|PASS scoped · 项目真实SQLite支出只读；资源ShotIds|PASS · 同项目/文档身份回读|PARTIAL · 文档导出未包含七类资源表|PASS · owned planning/shot根搬迁，原DB路径保持|
|文件|PASS · AddAttachmentsAsync关联合成文件|PARTIAL · 只验证关联，不声明内嵌编辑器/重命名|PASS · AttachmentId/原位置路径|PASS · 同项目/文档身份回读|NOT_RUN/不支持 · 附件内容非导出正文|PARTIAL · 原位置绝对路径，外部文件没移动|
|文档内容|PASS · DocumentCustomBody|PASS · VM修改、保存失败草稿、新VM恢复|PARTIAL · 与同文档RTF同存；非任意内联SourceIds|PASS · 同项目/文档身份回读|PARTIAL · 富文本路径不自动把CustomBody拼接入DOCX/PDF|PASS · owned planning/shot根搬迁，原DB路径保持|

完整ID、测试原名及限制见CSV。总判断PARTIAL：可以保存/恢复结构化策划资料，不能称导出成一个涵盖全部15区的完整便携摄影提案。
