# 15区汇总DOCX图像呈现交付

2026-10-21，Europe/Berlin。**诊断版，不能发行**。项目自有图像呈现在下述合成范围验证通过；实体桌面、Word/WPS和跨机完整项目便携未执行。

## 起点与旧成果保留

fetch及独立ls-remote核实上游company/planning-summary-docx-r2-20261021仍为4de1c269ed41bdcb153cabdeb85c748ccc4a6a45，产品d1a77be4450c5a2cbb3c09b47580b9841280a14f已核对。旧诊断ZIP的SHA256为bdcfd09937b482b408e1b263264c537550ce3d0dcc8b9606765807f2ce33a381，复验一致。

新独立工作树：E:/Kitao-Worktrees/planning-summary-images-20261021；分支：company/planning-summary-images-20261021。所有旧工作树HEAD、未提交和未跟踪状态及旧包保持不变，见PRESERVATION.json。未合main、强推、reset、stash、clean或rebase；私人照片、RAW、数据库、凭据和发布EXE/DLL不进入Git。家用Scenario11原执行说明未改，没有把公司性能数字写成家用新结果。

## 产品源码与图像权威

最终产品源码：**439e9064a6296726a5b4b30bbb56a1c08630a34a**。阶段产品提交：18f667ca为安全分区图像呈现；25556ffc补齐无正文位置元数据；439e9064修复真实发现的公共DOCX分页语义。

- PlanningDocumentImage是项目自有副本，沿用DocumentId、DocumentImageId、当前StorageRoot、规范images/DocumentId/ImageId.png、CopyHash、Placement、Order、ShotId与可选Source身份。本批仅嵌入这类被验证的副本，不新建第二套项目状态。
- ProjectShotReference/PlanningDocumentReference是外链或素材库非拥有引用。名称、位置、ID、状态保持可读；本机存在不等于自动读取、嵌入或完整跨机便携。
- 同一库/素材身份如已有项目自有历史副本，只呈现通过CopyHash验证的历史副本，明确注明未读取库当前原件；不把副本冒称库当前版本。
- PlanningLight.ReferencePath只有精确指向当前项目规范自有副本时才能关联该副本，不按标题、文件名、预览缓存猜图。CanvasRevision仍原元数据范围，不扩成可编辑画布版面导出。

入口保持：策划中心打开项目并完成保存 → 更多策划操作 → 导出15区汇总DOCX → 选择全新文件名。明确选择汇总导出时才读取自有图片，沿用FlushAsync门禁及只读导出快照。

## 分区、身份、顺序和图文布局

分区顺序仍为15区。自有图片按保存的Placement映射：封面→项目概览；光标/文字→正文标记原位置；指定镜头→镜头区；参考图→参考区；情绪板→情绪板；灯位图/灯光图→灯位；服化道/服化道与道具→对应区；未知位置→文档内容并列出原位置。光标图片缺少持久RTF插入位置时，明确保留到文档内容区，不静默略过或猜正文坐标。

同分区稳定按Order排序，并按DocumentImageId去重。原正文刻意重复位置和跨分区重复均保持同一图片ID，不改原引用身份。每图包括标题、中文图注、来源、文档/图片ID和副本Hash；库来源另列LibraryId/AssetId/原素材Hash及“项目自有历史副本、未读取库当前原件”。

汇总专用读取器验证相同文档ID、当前项目规范路径、无重解析点、文件33B–64MiB、SHA256、PNG IHDR、最长边16384/总像素4000万、单帧解码。读句柄按FileShare.Read拒绝普通并发写/删，hash与解码使用同一有界字节。缺失、变化、权限/占用不可读、跨文档、不支持或超限均在对应呈现位置写标题、图片ID及错误原因，不读取另一外链来回退。旧绝对路径若不是当前项目规范副本不被本批悄然搬迁。

图片只在导出投影中等比缩小，不超过保存尺寸、内容宽度或520DIP、内容高度42%或360DIP。表内图片按可用单元格宽度进一步收缩；原图片字节、项目宽高和持久RTF均不改。

已有DOCX被拒绝覆盖；先写独有stage，最终File.Move overwrite=false，即使输出路径竞态也不能覆盖旧DOCX。失败清理仅此stage，不改源图/策划数据/旧导出。

## 本批真实发现与修复

1. 稳定Source匹配与Placement混合时，同区图片顺序可能错乱或重复：统一在分区内按Order/图片ID呈现，原正文重复保留。
2. 有自有图片元数据却没有RTF插入位置时原投影会略过：改为文档内容区保留图片并明确说明。
3. 超大保存显示尺寸和表内图片可能挤占正文：添加本导出路径的尺寸上限及等比约束。
4. 汇总底层原子写出仍可能覆盖同名旧DOCX：最终提交改为不覆盖。
5. 逐页目视发现图片标题停在页尾、图片到下一页。公共PlanningDocxAdapter此前不输出/读取KeepWithNext；先新增KeepNext合同测试复现真实FAIL，再最小增加w:keepNext输出和导入（含与标题/分页属性共存的OpenXML顺序校验）。普通段落不自动强制同页。最终图题已与图片保持在一起。

因第5项改变公共文档组件，扩展了BatchThreeDocument、原摄影DOCX/表格/图片/页眉页脚/分页/原子替换/保存重开等全部相关策划回归。PlanningProposalPdf、PlanningPaginator、PlanningDocumentImages、PlanningDocxAdapter.Pages和原摄影DOCX ViewModel实现零diff；原格式语义不替换，汇总仍不是15区双向导入格式。

## 最终发布组件测试

|范围|程序集发现|选择/结果|通过|失败|跳过|漏项/额外|
|---|---:|---:|---:|---:|---:|---:|
|受影响Core|1723|27/27|27|0|0|0/0|
|扩展策划/文档WPF|1726|76/76|76|0|0|0/0|

其中新增图像投影Core2项、图像导出WPF5项、公共KeepNext合同1项均已包含，不重复相加。以上是本批**受影响范围**，不是完整Core/WPF全软件发行门禁，不继承旧成绩。最终发布组件与测试宿主29项DLL逐hash验证；见FINAL_BINDINGS.json与BUILD_IDENTITY.json。原始adapter发现、执行计划、TRX、stdout/stderr和每次命令完整保留RAW_TESTS.zip，按(testId,FQN,displayName)核对。

合成策划包含3张可用自有图、1张缺图、同一正文图片重复两次、竖幅图、中文图注、同一库引用跨参考/情绪板/灯位、可读外链与长正文。实际DOCX有9个drawing/9个媒体部件：正文4图、参考区2图、情绪板2图、灯位1图；跨区重复图保持相同DocumentImageId。15标题顺序正确；缺图在正文和灯位各说明一次；外链原件没有自动媒体嵌入。保存后新VM重开，与原VM及再次导出逐文本/图片ID次序一致；原planning JSON和可用副本字节不变。

另一个安全拒绝样例明确列出变化Hash、非PNG、超大、跨文档四种原因，媒体部件0；独占占用不可读、表内比例/尺寸、无RTF元数据及拒绝覆盖旧DOCX另有断言。OpenXML validator无错误，实际OOXML检查见SAMPLE_INSPECTION.json。

最终样例产品回读/共享分页管线为14页。每页有非空像素断言，已目视联系图及图题分页关键页，无空白页、图片拉伸或可见重叠；此仅是**文档渲染验证，不是实体桌面或Word/WPS通过**。独立render_docx.py尝试因本机无LibreOffice soffice.exe失败，日志已保留；没有借产品回读称独立应用兼容。

## 原始失败不抹除

- METADATA_EDGE_WPF保留8通过/2失败：旧证据目录重复，File.Copy拒绝覆盖已有样例。未删除旧样例或改产品保护；两项证据变量使用新目录后10/10通过，最终冻结包又完整跑27Core/76WPF。
- KEEP_NEXT_REPRO保留1项真实失败：图片标题缺w:keepNext。修复后合同及图像/旧汇总11项通过，最终扩展回归76项通过。

失败归因、代码修复和后续范围分别见RETAINED_FAILURES.md；没有放宽断言或挑最快结果。

## 新完整诊断包及未执行项

Planning15Images-KeepNext-Diagnostic-win-x64是完整Windows x64自包含主程序，290应用文件、264声明运行依赖缺失0。Start-Guangjing-Diagnostic.cmd启动独立UserData，包内含可打开的合成DOCX与人工清单；不含UserData、数据库、私人照片/RAW/凭据，ZIP不是Setup。构建命令、EXE/WPF/Core和完整文件清单另列；最终ZIP逐文件读回身份见ZIP_IDENTITY.json。

实体桌面：NOT_RUN；Word/WPS实体与跨机项目便携：NOT_RUN。外链/库原件/附件不自动搬迁，图像快照不等于项目完整便携，完整情绪板/灯位可编辑画布版面仍原范围。安装升级卸载、真机/外部服务等既有门禁未闭合。

发行门禁仍BLOCKED，整体PARTIAL；仅本批图像汇总合成验证范围PASS。产品源码与证据提交分开，正常推送独立分支后独立ls-remote读回；不合main、不强推。

## 正常推送读回

产品源码439e9064a6296726a5b4b30bbb56a1c08630a34a与独立验证证据提交62dc1b47d083778a8f7220edcef558945b11ce74已正常推送。独立ls-remote确认LOCAL_HEAD=REMOTE_HEAD，读回时工作树干净；后续仅封存此读回与最终ZIP身份，不改变产品或测试组件。
