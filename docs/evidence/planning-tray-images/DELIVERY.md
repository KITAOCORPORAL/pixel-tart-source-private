# 灵感托盘图片插入策划文档：审阅交付

日期2026-10-14。**REVIEW_BUILD_WITH_OPEN_GATES**；不是正式发布或Windows/WPS验收批准。

## 身份
- 指定基线origin/feature/kitao-planning-table-content-20261014：270b8fcb438760cd1a216fb4b63fffa4e0fb7475，fetch核验一致。
- 独立工作树：D:/AI AGENT/kitao-planning-tray-images-20261014。
- 分支：feature/kitao-planning-tray-images-20261014。
- 最终源码：6224754c25cb1fabcb1411b7f09315fe04e9afc3。其后仅文档证据，不复用旧表格版身份。
- 分阶段提交d236b7b（副本/数据合同）、cc1c676（选图/两模式）、d729051（DOCX/恢复）、6224754（旧选图器作废/多选交付验证）。

## 实现
1. 策划中心“从灵感托盘插入”：当前托盘+本项目收藏，搜索、多选、预览、取消。共享真实托盘服务路径，不改托盘内容。
2. 完整LibraryId/AssetId/ContentHash传给素材解析；Available才允许，Offline/Changed/Ambiguous/Missing停止对应项。读完源文件再次验SHA，防解析后文件改变。当前只解析明确打开的所属素材库，其他库Offline。
3. 基础版封面/参考图/情绪板/指定镜头生成真实正文图片段落及图注/分类标题，来源与位置不只存旁边参考图库。进阶按当前光标插入，表格单元格保持原位置；每次独立DocumentImageId，同图多处可用。
4. 图像独立PNG副本，源哈希/副本哈希/文档ID/位置/图注/尺寸/顺序均持久化。新图片不以Base64载荷塞进JSON，RTF仅放身份标记。恢复布局时读副本验证哈希。
5. 两模式图片管理可改标题、说明、宽度、正文图片顺序；基础插入可撤销/重做，进阶图片进入正文历史。旧视图/切项目/关闭或重开选择器回调作废，取消不写副本。
6. DOCX真实ImagePart嵌入；本地路径不写入图片描述。导回同文档且ID+图像哈希匹配才保持来源，否则是“文档内图片”。图注正文/顺序/表格位置按原生文档结构导入。
7. 来源重新定位为明确按钮+确认，不自动替换文档副本。托盘清空或素材库离线后已有文档副本继续显示/导出。

## 自动验证
6项新增强制测试覆盖：同图多次、基本→进阶、保存重开、托盘清空/源移除、错误哈希/全部解析拒绝状态、主文档锁/旧字节保留/重试、表内光标插入/撤销重做、真实素材解析、项目收藏筛选、取消、图片尺寸图注、异步换项目、目录写失败、实际选择器多选按钮、另一无库文档DOCX导入。证据为合成素材，不是客户照片。

|轮次|通过|实际失败|未执行|原strict状态|
|---|---:|---:|---:|---|
|最终源码完整Core|1682|0|7|FAILED|
|最终源码完整WPF，1622项|1599|0|23|FAILED|
|实际发布WPF策划专项|27|0|0|PASSED|
|实际发布Core完整|1683|0|6|FAILED|
|实际Release身份单例|1|0|0|PASSED|

发现/结果逐项一致，无超时/漏跑/额外结果。所有新增测试均required_automatic。预发布30个NotExecuted中Release身份在发布后实际通过，29个已分类条件/人工缺口继续OPEN。严格结果没有改成绿；POSTPUBLISH_GATE按原逐名分类得REVIEW_BUILD_WITH_OPEN_GATES。

## 新Release
完整目录：`D:/AI AGENT/kitao-planning-tray-images-20261014/artifacts/tray/final-release/win-x64`

EXE：`D:/AI AGENT/kitao-planning-tray-images-20261014/artifacts/tray/final-release/win-x64/KitaoPhotoSelector.exe`

产品版本：`2.3.0-tray-review+6224754c25cb1fabcb1411b7f09315fe04e9afc3`

EXE SHA256：`7b5553e91180a60eeed0bfe91b9a85328602959e5b141ac7be7cd9af9728fe41`

win-x64自包含Release配置，非单文件；必须保留完整目录。全部发布文件hash见RELEASE_FILES，7产品组件与隔离测试host逐项一致见RELEASE_IDENTITY。产品EXE未自动启动，未创建安装器。旧Release均保留，二进制不提交。

## 必须保留的边界
- 本地工程CopyPath当前绝对路径，工程迁移/重定位尚不自动修复；本地备份需保留JSON和images目录。跨无素材库的电脑交付用嵌图DOCX，不只拷贝JSON。
- 副本写入与文档提交不是跨文件ACID；突然崩溃可能留下完整未引用PNG，GC未实现。恢复/undo需要的副本不得自行删除。
- 基础版放置为正文分类段落和ShotId标签，并非完整镜头版式设计器；表内跨单元格排序需进阶编辑。浮动图/裁剪/任意Word排版不在范围。
- 外部DOCX/WPS若去除描述或重编码，不保证原库身份；安全降为文档内图片。已自动验证图像内容与位置，不声称WPS真实往返保真。
- Windows实操、物理DPI、WPS实际打开编辑导回、真实打印：**NOT_RUN**。OpenXML/ImagePart和离屏图不能代替WPS验收。
- Color Studio色彩数学零diff；原r5差异、复杂表格/分节/完整Word缺口继续保留。

## 证据
MIGRATION_CONTRACT、ACCEPTANCE、OPEN_ITEMS、IMPLEMENTATION、VISUAL_REVIEW、FILES_AND_TYPES、SOURCE_AUDIT、TEST_CLASSIFICATION、TEST_SUMMARY、TEST_RUNS、严格SUMMARY/DISCOVERY、UNEXECUTED、TEST_EVIDENCE.zip、RELEASE_IDENTITY/FILES、offscreen/、synthetic/tray-document.docx。

原始历史测试保留，设计参考图原件/私人照片/数据库/发布二进制不入Git。正常推送，不强推、不合并main；最终HEAD在回复中核对。
